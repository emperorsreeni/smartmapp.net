namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Diagnostic result from <c>ISculptor.Inspect&lt;S,D&gt;()</c>.
/// Contains the blueprint and a trace of how each link was established.
/// Placeholder — will be fleshed out in Sprint 7.
/// </summary>
public sealed record MappingInspection
{
    /// <summary>
    /// Gets the type pair that was inspected.
    /// </summary>
    public TypePair TypePair { get; init; }

    /// <summary>
    /// Gets the resolved blueprint for the type pair, or <c>null</c> if none exists.
    /// </summary>
    public Blueprint? Blueprint { get; init; }

    /// <summary>
    /// Gets the diagnostic trace of how each property link was established.
    /// </summary>
    public IReadOnlyList<string> LinkTrace { get; init; } = Array.Empty<string>();
}
