using System.Collections.Concurrent;
using System.Collections.Generic;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Composition;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Immutable snapshot of a fully forged sculptor's configuration — consumed by the
/// <see cref="Sculptor"/> runtime facade and exposed via <see cref="ISculptorConfiguration"/>.
/// Thread-safe after construction.
/// </summary>
internal sealed class ForgedSculptorConfiguration
{
    private readonly Dictionary<TypePair, Blueprint> _blueprintsByPair;

    internal ForgedSculptorConfiguration(
        IReadOnlyList<Blueprint> blueprints,
        SculptorOptions options,
        TypeModelCache typeModelCache,
        MappingDelegateCache delegateCache,
        BlueprintCompiler compiler,
        TypeTransformerRegistry transformerRegistry,
        IReadOnlyList<CompositionBlueprint>? compositionBlueprints = null)
    {
        Blueprints = blueprints;
        Options = options;
        TypeModelCache = typeModelCache;
        DelegateCache = delegateCache;
        Compiler = compiler;
        TransformerRegistry = transformerRegistry;
        CompositionBlueprints = compositionBlueprints ?? Array.Empty<CompositionBlueprint>();

        var map = new Dictionary<TypePair, Blueprint>(blueprints.Count);
        foreach (var bp in blueprints)
        {
            map[bp.TypePair] = bp;
        }
        _blueprintsByPair = map;

        var compositionMap = new Dictionary<Type, CompositionBlueprint>(CompositionBlueprints.Count);
        foreach (var cb in CompositionBlueprints)
        {
            compositionMap[cb.TargetType] = cb;
        }
        _compositionsByTarget = compositionMap;
    }

    internal IReadOnlyList<Blueprint> Blueprints { get; }
    internal SculptorOptions Options { get; }
    internal TypeModelCache TypeModelCache { get; }
    internal MappingDelegateCache DelegateCache { get; }
    internal BlueprintCompiler Compiler { get; }
    internal TypeTransformerRegistry TransformerRegistry { get; }

    /// <summary>
    /// Composition blueprints registered via <c>Compose&lt;T&gt;().FromOrigin&lt;O&gt;()</c>.
    /// Consumed by <see cref="CompositionDispatcher"/> at runtime when
    /// <c>ISculptor.Compose&lt;T&gt;(params object[])</c> is invoked with multiple origins.
    /// </summary>
    internal IReadOnlyList<CompositionBlueprint> CompositionBlueprints { get; }

    private readonly Dictionary<Type, CompositionBlueprint> _compositionsByTarget;

    internal CompositionBlueprint? TryGetCompositionBlueprint(Type targetType)
    {
        _compositionsByTarget.TryGetValue(targetType, out var cb);
        return cb;
    }

    /// <summary>
    /// Per-origin compiled-delegate cache for composition partials. Keyed by
    /// <c>(TargetType, OriginType)</c> so composition delegates stay isolated from the
    /// main <see cref="DelegateCache"/> (which is keyed by <see cref="TypePair"/> and may
    /// already hold a delegate for a regular <c>Bind&lt;Origin, Target&gt;()</c> with a
    /// different shape).
    /// </summary>
    internal ConcurrentDictionary<(Type Target, Type Origin), Func<object, MappingScope, object>> CompositionOriginDelegates { get; } = new();

    /// <summary>
    /// Slot-assignment cache for composition dispatch — key is a string signature built from the
    /// caller's target type + per-origin runtime types, value is an <c>int[]</c> whose index
    /// <c>i</c> gives the declared origin slot index for caller origin <c>i</c> (<c>-1</c>
    /// means the caller's origin didn't match any declared slot and should be skipped).
    /// Populated by <see cref="CompositionDispatcher.Dispatch{TTarget}"/> on first call for a
    /// given caller signature so subsequent calls avoid the O(caller × declared) type-match
    /// scan and ambiguity check — spec §S8-T08 Technical Considerations bullet 1.
    /// </summary>
    internal ConcurrentDictionary<string, int[]> CompositionSlotAssignments { get; } = new();

    /// <summary>
    /// Cached <see cref="ValidationResult"/> — computed lazily once per configuration.
    /// </summary>
    internal ValidationResult? CachedValidation { get; set; }

    /// <summary>
    /// Cached inspections keyed by type pair — built lazily.
    /// </summary>
    internal ConcurrentDictionary<TypePair, MappingInspection> InspectionCache { get; } = new();

    /// <summary>
    /// Cached mapping atlas (built on first request).
    /// </summary>
    internal MappingAtlas? CachedAtlas { get; set; }

    /// <summary>
    /// Cached projection expressions keyed by type pair — built lazily on first request.
    /// Backed by a dedicated <see cref="ProjectionExpressionCache"/> (spec §S8-T06 Outputs
    /// bullet 3) so the storage and memoisation semantics are isolated from the rest of the
    /// configuration surface.
    /// </summary>
    internal ProjectionExpressionCache ProjectionCache { get; } = new();

    /// <summary>
    /// Accumulated diagnostics emitted by <see cref="SculptorProjectionBuilder"/> when a
    /// <see cref="PropertyLink"/> cannot be translated to an EF-friendly expression. Sprint 16
    /// insights endpoint will surface these (spec §S8-T06 Technical Considerations).
    /// </summary>
    internal System.Collections.Concurrent.ConcurrentBag<ProjectionDiagnostic> ProjectionDiagnostics { get; } = new();

    internal Blueprint? TryGetBlueprint(TypePair pair)
    {
        _blueprintsByPair.TryGetValue(pair, out var bp);
        return bp;
    }

    internal IReadOnlyDictionary<TypePair, Blueprint> AsDictionary() => _blueprintsByPair;
}
