using System.Reflection.Emit;

namespace SmartMapp.Net.Engine.ILEmit.Internal;

/// <summary>
/// Per-blueprint state shared by every emitter (flat-property, null-safe, nested-mapping,
/// transformer) participating in a single <see cref="ILEmitMappingCompiler"/> compile call.
/// Kept as a class (not a <c>ref struct</c>) because <see cref="DelegateSlotTable"/> reference
/// has to outlive emission so the materialised closure can be passed to
/// <c>DynamicMethod.CreateDelegate</c>.
/// </summary>
internal sealed class EmitContext
{
    internal EmitContext(
        ILGenerator il,
        DelegateSlotTable slots,
        Type originType,
        Type targetType,
        LocalBuilder typedOriginLocal,
        LocalBuilder typedTargetLocal,
        int closureArgIndex,
        int originArgIndex,
        int scopeArgIndex)
    {
        IL = il;
        Slots = slots;
        OriginType = originType;
        TargetType = targetType;
        TypedOriginLocal = typedOriginLocal;
        TypedTargetLocal = typedTargetLocal;
        ClosureArgIndex = closureArgIndex;
        OriginArgIndex = originArgIndex;
        ScopeArgIndex = scopeArgIndex;
    }

    internal ILGenerator IL { get; }
    internal DelegateSlotTable Slots { get; }
    internal Type OriginType { get; }
    internal Type TargetType { get; }

    /// <summary>Strongly-typed origin local: <c>(OriginType)arg-origin</c>.</summary>
    internal LocalBuilder TypedOriginLocal { get; }

    /// <summary>Strongly-typed target local: <c>new TargetType()</c>.</summary>
    internal LocalBuilder TypedTargetLocal { get; }

    /// <summary>Method-arg index for the captured <see cref="EmittedClosure"/>.</summary>
    internal int ClosureArgIndex { get; }

    /// <summary>Method-arg index for the boxed origin parameter.</summary>
    internal int OriginArgIndex { get; }

    /// <summary>Method-arg index for the <see cref="MappingScope"/> parameter.</summary>
    internal int ScopeArgIndex { get; }
}
