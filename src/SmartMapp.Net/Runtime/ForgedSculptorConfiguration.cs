using System.Collections.Concurrent;
using System.Collections.Generic;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
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
        TypeTransformerRegistry transformerRegistry)
    {
        Blueprints = blueprints;
        Options = options;
        TypeModelCache = typeModelCache;
        DelegateCache = delegateCache;
        Compiler = compiler;
        TransformerRegistry = transformerRegistry;

        var map = new Dictionary<TypePair, Blueprint>(blueprints.Count);
        foreach (var bp in blueprints)
        {
            map[bp.TypePair] = bp;
        }
        _blueprintsByPair = map;
    }

    internal IReadOnlyList<Blueprint> Blueprints { get; }
    internal SculptorOptions Options { get; }
    internal TypeModelCache TypeModelCache { get; }
    internal MappingDelegateCache DelegateCache { get; }
    internal BlueprintCompiler Compiler { get; }
    internal TypeTransformerRegistry TransformerRegistry { get; }

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
    /// </summary>
    internal ConcurrentDictionary<TypePair, System.Linq.Expressions.LambdaExpression> ProjectionCache { get; } = new();

    internal Blueprint? TryGetBlueprint(TypePair pair)
    {
        _blueprintsByPair.TryGetValue(pair, out var bp);
        return bp;
    }

    internal IReadOnlyDictionary<TypePair, Blueprint> AsDictionary() => _blueprintsByPair;
}
