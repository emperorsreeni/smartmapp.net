using System.Linq;
using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Conventions;

namespace SmartMapp.Net.Engine.ILEmit;

/// <summary>
/// Capability probe for <see cref="ILEmitMappingCompiler"/>. Inspects a <see cref="Blueprint"/>
/// and returns whether every <see cref="PropertyLink"/> it contains is supported by the Sprint 9
/// emit pipeline. Returning <see cref="UnsupportedReason.None"/> guarantees that
/// <see cref="ILEmitMappingCompiler"/>.<c>Compile</c> can lower the blueprint without falling back to
/// the Expression Compiler.
/// </summary>
/// <remarks>
/// The probe is intentionally conservative: any feature whose IL lowering hasn't been delivered
/// yet (e.g. write-only setters, polymorphic dispatch, type-level transformers) returns a
/// non-<c>None</c> reason so the strategy chain (<c>MappingStrategySelector</c>, S9-T05)
/// transparently falls back to the existing Expression Compiler — preserving identical
/// semantics for the full Sprint 1–8 test suite.
/// </remarks>
internal static class EmitDiagnostics
{
    /// <summary>
    /// Determines whether <paramref name="blueprint"/> can be lowered to a <c>DynamicMethod</c>
    /// by <see cref="ILEmitMappingCompiler"/>. When the result is <c>false</c>,
    /// <paramref name="reason"/> carries the first disqualifying observation discovered.
    /// </summary>
    /// <param name="blueprint">The blueprint to inspect.</param>
    /// <param name="reason">When <c>false</c>, the first reason the blueprint is unsupported.</param>
    /// <returns><c>true</c> iff the blueprint is emit-eligible.</returns>
    internal static bool CanEmit(Blueprint blueprint, out UnsupportedReason reason)
    {
        if (blueprint is null) { reason = UnsupportedReason.UnsupportedLinkShape; return false; }

        // Sprint 9 deliberately defers advanced blueprint features to the Expression Compiler
        // so the IL Emit path stays small, fast, and semantically identical to the Sprint 8 RC
        // for the 80 % case (flat DTOs with conventional links).
        if (blueprint.TrackReferences
            || blueprint.TargetFactory is not null
            || blueprint.OnMapping is not null
            || blueprint.OnMapped is not null
            || blueprint.Condition is not null)
        {
            reason = UnsupportedReason.UsesAdvancedBlueprintFeatures;
            return false;
        }

        if (blueprint.TypeTransformer is not null)
        {
            reason = UnsupportedReason.HasTypeLevelTransformer;
            return false;
        }

        if (blueprint.StrictRequiredMembers)
        {
            reason = UnsupportedReason.StrictRequiredMembers;
            return false;
        }

        var target = blueprint.TargetType;
        if (target.IsAbstract || target.IsInterface)
        {
            reason = UnsupportedReason.AbstractOrInterfaceTarget;
            return false;
        }

        if (target.IsGenericTypeDefinition || target.ContainsGenericParameters
            || blueprint.OriginType.IsGenericTypeDefinition || blueprint.OriginType.ContainsGenericParameters)
        {
            reason = UnsupportedReason.OpenGeneric;
            return false;
        }

        if (!target.IsValueType && target.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            binder: null, types: System.Type.EmptyTypes, modifiers: null) is null)
        {
            reason = UnsupportedReason.MissingParameterlessCtor;
            return false;
        }

        foreach (var link in blueprint.Links)
        {
            if (link.IsSkipped) continue;
            if (!IsLinkEmittable(link, out reason)) return false;
        }

        reason = UnsupportedReason.None;
        return true;
    }

    /// <summary>
    /// Inspects a single <see cref="PropertyLink"/> for emit eligibility. Returns
    /// <c>false</c> with a populated <paramref name="reason"/> on the first
    /// disqualifying observation.
    /// </summary>
    private static bool IsLinkEmittable(PropertyLink link, out UnsupportedReason reason)
    {
        // Fancy-shape links flow through the Expression Compiler so we don't have to re-implement
        // their semantics in IL. Conditions / fallbacks / pre-conditions are valid mapping
        // features — they're simply outside the Sprint 9 emit-fast-path budget.
        if (link.Condition is not null || link.PreCondition is not null || link.Fallback is not null)
        {
            reason = UnsupportedReason.UnsupportedLinkShape;
            return false;
        }

        if (link.TargetMember is not PropertyInfo pi)
        {
            // Field-targeted links are valid but not on the Sprint 9 fast path.
            reason = UnsupportedReason.UnsupportedTargetMember;
            return false;
        }

        var setter = pi.SetMethod;
        if (setter is null)
        {
            reason = UnsupportedReason.UnsupportedTargetMember;
            return false;
        }

        // init-only setters are reachable from emitted IL when skipVisibility is true, but the
        // semantics differ slightly from a regular setter (modreq IsExternalInit) and the
        // Expression Compiler already handles them correctly. Defer for now.
        if (IsInitOnlySetter(setter))
        {
            reason = UnsupportedReason.UnsupportedTargetMember;
            return false;
        }

        if (pi.GetIndexParameters().Length > 0)
        {
            reason = UnsupportedReason.UnsupportedTargetMember;
            return false;
        }

        // Only the direct PropertyAccessProvider is inlinable by the Sprint 9 emitter — its
        // origin getter is a single MemberInfo we can lower to a Callvirt/Call op-code.
        if (link.Provider is not PropertyAccessProvider pap)
        {
            reason = UnsupportedReason.UnsupportedValueProvider;
            return false;
        }

        if (pap.OriginMember is not PropertyInfo originPi)
        {
            reason = UnsupportedReason.UnsupportedValueProvider;
            return false;
        }

        if (originPi.GetMethod is null || originPi.GetIndexParameters().Length > 0)
        {
            reason = UnsupportedReason.UnsupportedValueProvider;
            return false;
        }

        // All transformers in TypeTransformerRegistry are de-facto singletons (the registry
        // stores instances, not factories) so every registered ITypeTransformer is safe to cache
        // in a static IL slot. The ScopedTransformer reason value is retained for symmetry with
        // the spec but is currently unreachable through the registry path.
        _ = link.Transformer;

        // Coercion check: same type, widening numeric, Nullable<T> unwrap, or assignment-
        // compatible reference. Anything else (collection-to-collection, struct-to-class without
        // a transformer, etc.) flows through the Expression Compiler.
        var originType = originPi.PropertyType;
        var targetType = pi.PropertyType;
        if (!Internal.TypeCoercion.IsCoercible(originType, targetType, link.Transformer is not null))
        {
            reason = UnsupportedReason.UnsupportedTypeCoercion;
            return false;
        }

        reason = UnsupportedReason.None;
        return true;
    }

    private static bool IsInitOnlySetter(MethodInfo setter)
    {
        var modifiers = setter.ReturnParameter?.GetRequiredCustomModifiers();
        return modifiers is not null && modifiers.Any(m => m.Name == "IsExternalInit");
    }

}
