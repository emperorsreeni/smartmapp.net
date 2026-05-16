using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Engine.ILEmit.Internal;

namespace SmartMapp.Net.Engine.ILEmit;

/// <summary>
/// Lowers an emit-eligible <see cref="Blueprint"/> to a <c>DynamicMethod</c>-backed mapping
/// delegate. Sprint 9's hot-path complement to the Sprint 4 Expression Compiler — selected by
/// the strategy chain (S9-T05) when <see cref="EmitDiagnostics.CanEmit"/> approves the blueprint.
/// </summary>
/// <remarks>
/// <para>
/// The compiler is intentionally state-free: every <c>Compile</c> overload is pure and produces a fresh
/// delegate on every call. Caching, promotion, and the lock-free swap with the existing
/// Expression-Compiled delegate are owned by <c>AdaptivePromotionManager</c> (S9-T07).
/// </para>
/// <para>
/// Every entry point is annotated <see cref="RequiresDynamicCodeAttribute"/> because the
/// underlying <c>System.Reflection.Emit</c> infrastructure is unsupported under NativeAOT. The
/// strategy chain refuses to select this compiler under <c>CompiledOnly</c> mode (which is the
/// default and the AOT-safe configuration), so callers that never opt in to <c>EmitFirst</c>
/// /<c>Adaptive</c>/<c>EmitOnly</c> pay zero AOT-warning cost.
/// </para>
/// </remarks>
[RequiresDynamicCode(
    "SmartMapp.Net IL Emit is part of the dynamic-code subsystem and is unsupported under NativeAOT. " +
    "Use MappingStrategy.SourceGenerated (Sprint 12) or Strategy.Mode = CompiledOnly for AOT-published apps.")]
internal sealed class ILEmitMappingCompiler
{
    /// <summary>
    /// Singleton instance reused by the strategy chain. The class is stateless so a single
    /// shared instance avoids unnecessary allocation on the strategy hot path.
    /// </summary>
    internal static ILEmitMappingCompiler Instance { get; } = new();

    private ILEmitMappingCompiler() { }

    /// <summary>
    /// Lowers <paramref name="blueprint"/> to a <c>Func&lt;object, MappingScope, object&gt;</c>
    /// backed by a <c>DynamicMethod</c>. Returns <c>null</c> when the blueprint is not
    /// emit-eligible (S9-T01 capability probe via <see cref="EmitDiagnostics.CanEmit"/>) so the
    /// strategy chain can transparently fall back to the Expression Compiler.
    /// </summary>
    /// <param name="blueprint">The blueprint to lower.</param>
    /// <param name="reason">When the result is <c>null</c>, the first reason the blueprint was
    /// rejected. When the result is non-<c>null</c>, <see cref="UnsupportedReason.None"/>.</param>
    /// <returns>The compiled delegate or <c>null</c> on fall-back.</returns>
    internal Func<object, MappingScope, object>? Compile(Blueprint blueprint, out UnsupportedReason reason)
        => Compile(blueprint, nestedResolver: null, transformerLookup: null, out reason);

    /// <summary>
    /// Lowers <paramref name="blueprint"/> with optional cross-pair resolvers for nested
    /// complex-type links and registered transformers. The strategy chain (S9-T05) passes
    /// resolvers that delegate to <see cref="Caching.MappingDelegateCache"/> and
    /// <see cref="Transformers.TypeTransformerRegistry"/> respectively so emitted IL can call
    /// into the rest of the forged configuration without performing reflection at run time.
    /// </summary>
    internal Func<object, MappingScope, object>? Compile(
        Blueprint blueprint,
        Func<TypePair, Func<object, MappingScope, object>?>? nestedResolver,
        Func<Type, Type, ITypeTransformer?>? transformerLookup,
        out UnsupportedReason reason)
    {
        if (blueprint is null) throw new ArgumentNullException(nameof(blueprint));

        if (!EmitDiagnostics.CanEmit(blueprint, out reason))
            return null;

        var origin = blueprint.OriginType;
        var target = blueprint.TargetType;
        var slots = new DelegateSlotTable();

        // Signature: object Invoke(EmittedClosure closure, object origin, MappingScope scope)
        // The closure is bound as the delegate target via DynamicMethod.CreateDelegate(..., closure)
        // so the public-facing Func<object, MappingScope, object> signature matches the
        // existing Sprint 4 ExpressionCompiler output (spec naming alias documented in
        // sprint9-progress.md).
        var dyn = new DynamicMethod(
            name: $"ILEmit_Map_{origin.Name}_To_{target.Name}",
            returnType: typeof(object),
            parameterTypes: new[] { typeof(EmittedClosure), typeof(object), typeof(MappingScope) },
            owner: typeof(EmittedMapperHost),
            skipVisibility: true);

        var il = dyn.GetILGenerator();
        var typedOrigin = il.DeclareLocal(origin);
        var typedTarget = il.DeclareLocal(target);

        var ctx = new EmitContext(
            il, slots, origin, target, typedOrigin, typedTarget,
            closureArgIndex: 0, originArgIndex: 1, scopeArgIndex: 2);

        EmitBody(ctx, blueprint, nestedResolver, transformerLookup);

        var closure = slots.Materialise();
        var del = (Func<object, MappingScope, object>)dyn.CreateDelegate(
            typeof(Func<object, MappingScope, object>), closure);

        reason = UnsupportedReason.None;
        return del;
    }

    // ---------------------------------------------------------------------------------------
    // IL body emission. Pattern matches the spec §9.2 sample C# code:
    //     if (origin == null) return default(Target);
    //     var typedOrigin = (Origin)origin;
    //     var typedTarget = new Target();
    //     typedTarget.X = typedOrigin.X;
    //     ...
    //     return (object)typedTarget;
    // ---------------------------------------------------------------------------------------

    private static void EmitBody(
        EmitContext ctx,
        Blueprint blueprint,
        Func<TypePair, Func<object, MappingScope, object>?>? nestedResolver,
        Func<Type, Type, ITypeTransformer?>? transformerLookup)
    {
        var il = ctx.IL;

        // Null-check the origin: if (origin == null) return null;
        var afterNullCheck = il.DefineLabel();
        il.Emit(OpCodes.Ldarg, ctx.OriginArgIndex);
        il.Emit(OpCodes.Brtrue_S, afterNullCheck);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Ret);
        il.MarkLabel(afterNullCheck);

        // typedOrigin = (Origin)origin
        il.Emit(OpCodes.Ldarg, ctx.OriginArgIndex);
        il.Emit(ctx.OriginType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, ctx.OriginType);
        il.Emit(OpCodes.Stloc, ctx.TypedOriginLocal);

        // typedTarget = new Target()  (or  initobj for value-type targets)
        if (ctx.TargetType.IsValueType)
        {
            il.Emit(OpCodes.Ldloca, ctx.TypedTargetLocal);
            il.Emit(OpCodes.Initobj, ctx.TargetType);
        }
        else
        {
            var ctor = ctx.TargetType.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null, types: Type.EmptyTypes, modifiers: null)
                ?? throw new InvalidOperationException(
                    $"IL Emit: target type {ctx.TargetType} has no parameterless ctor (CanEmit should have rejected this).");
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Stloc, ctx.TypedTargetLocal);
        }

        // Per-link assignment.
        foreach (var link in blueprint.Links)
        {
            if (link.IsSkipped) continue;
            EmitPropertyAssignment(ctx, link, nestedResolver, transformerLookup);
        }

        // return (object)typedTarget;
        il.Emit(OpCodes.Ldloc, ctx.TypedTargetLocal);
        if (ctx.TargetType.IsValueType)
            il.Emit(OpCodes.Box, ctx.TargetType);
        il.Emit(OpCodes.Ret);
    }

    /// <summary>
    /// Lowers a single <see cref="PropertyLink"/> to the sequence:
    /// <c>typedTarget.X = coerce(transform(typedOrigin.X))</c>.
    /// </summary>
    private static void EmitPropertyAssignment(
        EmitContext ctx,
        PropertyLink link,
        Func<TypePair, Func<object, MappingScope, object>?>? nestedResolver,
        Func<Type, Type, ITypeTransformer?>? transformerLookup)
    {
        var il = ctx.IL;
        var pap = (PropertyAccessProvider)link.Provider;
        var originProp = (PropertyInfo)pap.OriginMember;
        var targetProp = (PropertyInfo)link.TargetMember;
        var setter = targetProp.SetMethod!;

        var originType = originProp.PropertyType;
        var targetType = targetProp.PropertyType;

        // Pre-load the target instance for the eventual setter call (Callvirt/Call takes the
        // target as the first stack slot, so we load it once up-front and let the value
        // computation push the value second).
        if (ctx.TargetType.IsValueType)
            il.Emit(OpCodes.Ldloca, ctx.TypedTargetLocal);
        else
            il.Emit(OpCodes.Ldloc, ctx.TypedTargetLocal);

        // Compute the origin value, pushing it on the stack.
        EmitLoadOriginValue(ctx, link, originProp, originType, targetType,
            nestedResolver, transformerLookup);

        // setter call. Use Call for value-type targets so the address-based receiver is honoured.
        il.Emit(ctx.TargetType.IsValueType ? OpCodes.Call : OpCodes.Callvirt, setter);
    }

    private static void EmitLoadOriginValue(
        EmitContext ctx,
        PropertyLink link,
        PropertyInfo originProp,
        Type originType,
        Type targetType,
        Func<TypePair, Func<object, MappingScope, object>?>? nestedResolver,
        Func<Type, Type, ITypeTransformer?>? transformerLookup)
    {
        var il = ctx.IL;
        var originLocal = ctx.TypedOriginLocal;

        // Load: typedOrigin.X   (Ldloc origin; callvirt get_X)
        if (originProp.DeclaringType!.IsValueType)
            il.Emit(OpCodes.Ldloca, originLocal);
        else
            il.Emit(OpCodes.Ldloc, originLocal);
        il.EmitCallSmart(originProp.GetMethod!);

        // Path 1 — registered transformer (T04). Emit lookup-bound slot invocation.
        if (link.Transformer is not null && transformerLookup is not null)
        {
            var resolved = transformerLookup(originType, targetType) ?? link.Transformer;
            EmitTransformerCall(ctx, resolved, originType, targetType);
            return;
        }
        if (link.Transformer is not null)
        {
            EmitTransformerCall(ctx, link.Transformer, originType, targetType);
            return;
        }

        // Path 2 — nested complex-type mapping (T03). Detect by asking the resolver for a
        // delegate; if it returns non-null, route through it.
        if (nestedResolver is not null && originType != targetType
            && !originType.IsPrimitive && originType != typeof(string)
            && !targetType.IsPrimitive && targetType != typeof(string))
        {
            // Strip Nullable<> wrappers for resolver lookup.
            var originLookup = Nullable.GetUnderlyingType(originType) ?? originType;
            var targetLookup = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var nestedDel = nestedResolver(new TypePair(originLookup, targetLookup));
            if (nestedDel is not null)
            {
                EmitNestedDelegateCall(ctx, nestedDel, originType, targetType);
                return;
            }
        }

        // Path 3 — same type or widening numeric: coerce in place.
        if (originType != targetType)
            EmitCoercion(ctx, originType, targetType);
    }

    private static void EmitTransformerCall(
        EmitContext ctx, ITypeTransformer transformer, Type originType, Type targetType)
    {
        var il = ctx.IL;
        var slot = ctx.Slots.AddTransformer(transformer);

        // Stack: [value]
        // We need to load the closure-held transformer instance, cast to ITypeTransformer<TS,TD>,
        // pass (value, scope), receive return value.
        // Plan: stash value in a local, load transformer, load value, load scope, callvirt.
        var valueLocal = il.DeclareLocal(originType);
        il.Emit(OpCodes.Stloc, valueLocal);

        // closure.Transformers[slot]
        il.Emit(OpCodes.Ldarg, ctx.ClosureArgIndex);
        il.Emit(OpCodes.Call, typeof(EmittedClosure).GetProperty(nameof(EmittedClosure.Transformers))!.GetMethod!);
        EmitLdcI4(il, slot);
        il.Emit(OpCodes.Ldelem_Ref);

        // Strip Nullable wrapper for the transformer generic args.
        var tOrigin = Nullable.GetUnderlyingType(originType) ?? originType;
        var tTarget = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var iface = typeof(ITypeTransformer<,>).MakeGenericType(tOrigin, tTarget);
        il.Emit(OpCodes.Castclass, iface);

        // Load value (unwrap Nullable<T> via GetValueOrDefault if needed).
        if (Nullable.GetUnderlyingType(originType) is not null)
        {
            il.Emit(OpCodes.Ldloca, valueLocal);
            var getOrDefault = originType.GetMethod("GetValueOrDefault", Type.EmptyTypes)!;
            il.Emit(OpCodes.Call, getOrDefault);
        }
        else
        {
            il.Emit(OpCodes.Ldloc, valueLocal);
        }

        // Load scope.
        il.Emit(OpCodes.Ldarg, ctx.ScopeArgIndex);

        // Callvirt ITypeTransformer<TS,TD>.Transform(TS, MappingScope) returns TD.
        var transformMethod = iface.GetMethod("Transform")!;
        il.Emit(OpCodes.Callvirt, transformMethod);

        // Wrap into Nullable<TTarget> if target is Nullable.
        if (Nullable.GetUnderlyingType(targetType) is Type wrapInner && wrapInner == tTarget)
        {
            var ctor = targetType.GetConstructor(new[] { wrapInner })!;
            il.Emit(OpCodes.Newobj, ctor);
        }
    }

    private static void EmitNestedDelegateCall(
        EmitContext ctx,
        Func<object, MappingScope, object> nestedDel,
        Type originType,
        Type targetType)
    {
        var il = ctx.IL;
        var slot = ctx.Slots.AddDelegate(nestedDel);

        // Stack on entry: [value]   (we'll need to box it if value-type origin)
        var valueLocal = il.DeclareLocal(originType);
        il.Emit(OpCodes.Stloc, valueLocal);

        // Reference-type origin null-safety: if (value == null) push default(target); else call.
        var elseLabel = il.DefineLabel();
        var endLabel = il.DefineLabel();

        if (!originType.IsValueType)
        {
            il.Emit(OpCodes.Ldloc, valueLocal);
            il.Emit(OpCodes.Brtrue_S, elseLabel);
            il.EmitDefault(targetType);
            il.Emit(OpCodes.Br_S, endLabel);
            il.MarkLabel(elseLabel);
        }
        else if (Nullable.GetUnderlyingType(originType) is not null)
        {
            il.Emit(OpCodes.Ldloca, valueLocal);
            var hasValue = originType.GetProperty("HasValue")!.GetMethod!;
            il.Emit(OpCodes.Call, hasValue);
            il.Emit(OpCodes.Brtrue_S, elseLabel);
            il.EmitDefault(targetType);
            il.Emit(OpCodes.Br_S, endLabel);
            il.MarkLabel(elseLabel);
        }

        // closure.Delegates[slot](value, scope.CreateChild())
        il.Emit(OpCodes.Ldarg, ctx.ClosureArgIndex);
        il.Emit(OpCodes.Call, typeof(EmittedClosure).GetProperty(nameof(EmittedClosure.Delegates))!.GetMethod!);
        EmitLdcI4(il, slot);
        il.Emit(OpCodes.Ldelem_Ref);

        // value (boxed)
        il.Emit(OpCodes.Ldloc, valueLocal);
        if (originType.IsValueType)
        {
            var boxType = Nullable.GetUnderlyingType(originType) is Type inner
                ? inner
                : originType;
            if (Nullable.GetUnderlyingType(originType) is not null)
            {
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldloca, valueLocal);
                var getOrDefault = originType.GetMethod("GetValueOrDefault", Type.EmptyTypes)!;
                il.Emit(OpCodes.Call, getOrDefault);
                il.Emit(OpCodes.Box, boxType);
            }
            else
            {
                il.Emit(OpCodes.Box, originType);
            }
        }

        // scope.CreateChild()
        il.Emit(OpCodes.Ldarg, ctx.ScopeArgIndex);
        il.Emit(OpCodes.Callvirt, typeof(MappingScope).GetMethod(nameof(MappingScope.CreateChild))!);

        // delegate.Invoke(...)
        var invoke = typeof(Func<object, MappingScope, object>).GetMethod("Invoke")!;
        il.Emit(OpCodes.Callvirt, invoke);

        // Cast / unbox to target type.
        if (targetType.IsValueType)
            il.Emit(OpCodes.Unbox_Any, targetType);
        else
            il.Emit(OpCodes.Castclass, targetType);

        il.MarkLabel(endLabel);
    }

    private static void EmitCoercion(EmitContext ctx, Type originType, Type targetType)
    {
        var il = ctx.IL;

        // Nullable<T> -> T   (value-type unwrap via GetValueOrDefault)
        var originUnderlying = Nullable.GetUnderlyingType(originType);
        if (originUnderlying is not null && originUnderlying == targetType)
        {
            // Stack currently holds the Nullable<T> value — re-issue via a temp local so we can
            // call instance method on its address.
            var tmp = il.DeclareLocal(originType);
            il.Emit(OpCodes.Stloc, tmp);
            il.Emit(OpCodes.Ldloca, tmp);
            var getOrDefault = originType.GetMethod("GetValueOrDefault", Type.EmptyTypes)!;
            il.Emit(OpCodes.Call, getOrDefault);
            return;
        }

        // T -> Nullable<T>   (value-type wrap via ctor)
        var targetUnderlying = Nullable.GetUnderlyingType(targetType);
        if (targetUnderlying is not null && targetUnderlying == originType)
        {
            var ctor = targetType.GetConstructor(new[] { originType })!;
            il.Emit(OpCodes.Newobj, ctor);
            return;
        }

        // Reference covariance — nothing to do, IL is naturally polymorphic.
        if (!originType.IsValueType && !targetType.IsValueType && targetType.IsAssignableFrom(originType))
            return;

        // Widening numeric.
        TypeCoercion.EmitWideningConversion(il, originType, targetType);
    }

    private static void EmitLdcI4(ILGenerator il, int value)
    {
        switch (value)
        {
            case 0: il.Emit(OpCodes.Ldc_I4_0); return;
            case 1: il.Emit(OpCodes.Ldc_I4_1); return;
            case 2: il.Emit(OpCodes.Ldc_I4_2); return;
            case 3: il.Emit(OpCodes.Ldc_I4_3); return;
            case 4: il.Emit(OpCodes.Ldc_I4_4); return;
            case 5: il.Emit(OpCodes.Ldc_I4_5); return;
            case 6: il.Emit(OpCodes.Ldc_I4_6); return;
            case 7: il.Emit(OpCodes.Ldc_I4_7); return;
            case 8: il.Emit(OpCodes.Ldc_I4_8); return;
            default:
                if (value >= -128 && value <= 127) il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
                else il.Emit(OpCodes.Ldc_I4, value);
                return;
        }
    }
}
