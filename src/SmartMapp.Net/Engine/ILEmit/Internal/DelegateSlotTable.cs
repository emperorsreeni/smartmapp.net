using System.Collections.Generic;

namespace SmartMapp.Net.Engine.ILEmit.Internal;

/// <summary>
/// Holds the static slots that each <c>DynamicMethod</c> emitted by
/// <see cref="ILEmitMappingCompiler"/> uses to reach nested mapping delegates and type
/// transformers without performing reflection on the hot path. A single
/// <see cref="DelegateSlotTable"/> is associated with each emit operation and the resulting
/// <c>DynamicMethod</c> closes over a private array via the
/// <see cref="EmittedClosure"/> wrapper.
/// </summary>
/// <remarks>
/// Sprint 9 deliberately keeps the slot table simple: emitted IL loads the closure array via the
/// captured <c>MappingScope</c> parameter and indexes into it with a constant. This avoids the
/// extra <c>DelegateSlotTable</c>-as-static-field plumbing while still buying us the
/// reflection-free dispatch the spec calls for in §9.2.
/// </remarks>
internal sealed class EmittedClosure
{
    /// <summary>
    /// Nested-mapping delegates referenced from the emitted IL, indexed in the order they were
    /// appended via <see cref="DelegateSlotTable.AddDelegate"/>.
    /// </summary>
    internal Func<object, MappingScope, object>?[] Delegates { get; }

    /// <summary>
    /// Type-transformer instances referenced from the emitted IL, indexed in the order they
    /// were appended via <see cref="DelegateSlotTable.AddTransformer"/>.
    /// </summary>
    internal SmartMapp.Net.Abstractions.ITypeTransformer?[] Transformers { get; }

    internal EmittedClosure(
        Func<object, MappingScope, object>?[] delegates,
        SmartMapp.Net.Abstractions.ITypeTransformer?[] transformers)
    {
        Delegates = delegates;
        Transformers = transformers;
    }
}

/// <summary>
/// Build-time accumulator for nested-delegate / transformer references discovered while
/// lowering a <see cref="Blueprint"/> to IL. After emission, <see cref="Materialise"/> produces
/// the read-only <see cref="EmittedClosure"/> the <c>DynamicMethod</c> closes over.
/// </summary>
internal sealed class DelegateSlotTable
{
    private readonly List<Func<object, MappingScope, object>?> _delegates = new();
    private readonly List<SmartMapp.Net.Abstractions.ITypeTransformer?> _transformers = new();

    internal int AddDelegate(Func<object, MappingScope, object>? del)
    {
        var idx = _delegates.Count;
        _delegates.Add(del);
        return idx;
    }

    internal int AddTransformer(SmartMapp.Net.Abstractions.ITypeTransformer transformer)
    {
        var idx = _transformers.Count;
        _transformers.Add(transformer);
        return idx;
    }

    /// <summary>Replaces an existing slot (used during two-phase recursive-pair patching).</summary>
    internal void PatchDelegate(int slot, Func<object, MappingScope, object> del)
        => _delegates[slot] = del;

    internal int DelegateCount => _delegates.Count;
    internal int TransformerCount => _transformers.Count;

    internal EmittedClosure Materialise()
        => new(_delegates.ToArray(), _transformers.ToArray());
}
