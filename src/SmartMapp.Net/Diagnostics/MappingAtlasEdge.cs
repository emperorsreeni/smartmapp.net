using System.Text.Json.Serialization;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// A directed edge in the <see cref="MappingAtlas"/> graph — one per registered <see cref="Blueprint"/>.
/// </summary>
public sealed record MappingAtlasEdge
{
    /// <summary>Gets the type pair this edge represents. Excluded from JSON serialization
    /// because <see cref="System.Type"/> is not JSON-serializable; use
    /// <see cref="OriginTypeFullName"/> / <see cref="TargetTypeFullName"/> for the string surface.
    /// Always initialised by <see cref="MappingAtlas.Build"/>.</summary>
    [JsonIgnore]
    public TypePair Pair { get; init; }

    /// <summary>Gets the mapping strategy.</summary>
    public required MappingStrategy Strategy { get; init; }

    /// <summary>Gets the number of property links in the underlying blueprint.</summary>
    public required int LinkCount { get; init; }

    /// <summary>Gets <see cref="Pair"/>'s origin type full name for JSON output.</summary>
    public string OriginTypeFullName => Pair.OriginType.FullName ?? Pair.OriginType.Name;

    /// <summary>Gets <see cref="Pair"/>'s target type full name for JSON output.</summary>
    public string TargetTypeFullName => Pair.TargetType.FullName ?? Pair.TargetType.Name;
}
