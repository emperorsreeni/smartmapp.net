namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// A graph of all registered blueprints and their relationships.
/// Used by the diagnostics endpoint and <c>ISculptor.GetMappingAtlas()</c>.
/// Placeholder — will be fleshed out in Sprint 7.
/// </summary>
public sealed record MappingAtlas
{
    /// <summary>
    /// Gets all registered blueprints.
    /// </summary>
    public IReadOnlyList<Blueprint> Blueprints { get; init; } = Array.Empty<Blueprint>();

    /// <summary>
    /// Converts the atlas to DOT graph format for visualization.
    /// </summary>
    /// <returns>A string in DOT format.</returns>
    public string ToDotFormat() => string.Empty;
}
