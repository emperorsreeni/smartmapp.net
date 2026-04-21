using System.Reflection;

namespace SmartMapp.Net.Discovery;

/// <summary>
/// Immutable snapshot of everything <see cref="AssemblyScanner"/> found in one or more assemblies:
/// mapping blueprints, value providers, type transformers, and attributed type pairs.
/// Safe to cache, safe to share across threads.
/// </summary>
public sealed class AssemblyScanResult
{
    /// <summary>
    /// Gets the assemblies that were scanned.
    /// </summary>
    public required IReadOnlyList<Assembly> ScannedAssemblies { get; init; }

    /// <summary>
    /// Gets the concrete (non-abstract, non-open-generic) <see cref="MappingBlueprint"/> subclasses.
    /// </summary>
    public required IReadOnlyList<Type> BlueprintTypes { get; init; }

    /// <summary>
    /// Gets the concrete implementations of <see cref="Abstractions.IValueProvider{TOrigin,TTarget,TMember}"/>
    /// along with captured closed generic arguments.
    /// </summary>
    public required IReadOnlyList<ScannedClosedGeneric> ValueProviders { get; init; }

    /// <summary>
    /// Gets the concrete implementations of <see cref="Abstractions.ITypeTransformer{TOrigin,TTarget}"/>
    /// along with captured closed generic arguments.
    /// </summary>
    public required IReadOnlyList<ScannedClosedGeneric> TypeTransformers { get; init; }

    /// <summary>
    /// Gets the type pairs discovered via <c>[MappedBy]</c> and <c>[MapsInto]</c> attributes.
    /// </summary>
    public required IReadOnlyList<ScannedTypePair> AttributedPairs { get; init; }

    /// <summary>
    /// An empty result — no assemblies scanned.
    /// </summary>
    public static readonly AssemblyScanResult Empty = new()
    {
        ScannedAssemblies = Array.Empty<Assembly>(),
        BlueprintTypes = Array.Empty<Type>(),
        ValueProviders = Array.Empty<ScannedClosedGeneric>(),
        TypeTransformers = Array.Empty<ScannedClosedGeneric>(),
        AttributedPairs = Array.Empty<ScannedTypePair>(),
    };
}
