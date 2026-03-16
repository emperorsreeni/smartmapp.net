namespace SmartMapp.Net;

/// <summary>
/// Captures how a <see cref="PropertyLink"/> was linked — enabling the <c>Inspect&lt;S,D&gt;()</c> diagnostic.
/// Records the convention name, confidence level, and the origin member path used.
/// </summary>
public sealed record ConventionMatch
{
    /// <summary>
    /// Gets the name of the convention that produced this match (e.g., "ExactName", "Flattening").
    /// </summary>
    public required string ConventionName { get; init; }

    /// <summary>
    /// Gets the origin member path that was matched (e.g., "Customer.Address.City").
    /// </summary>
    public required string OriginMemberPath { get; init; }

    /// <summary>
    /// Gets the confidence level of the match, from 0.0 (no confidence) to 1.0 (exact match).
    /// Used by <c>StructuralSimilarityScorer</c> for fuzzy matching.
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Gets a value indicating whether this match was explicitly configured by the user
    /// (as opposed to auto-discovered by a convention).
    /// </summary>
    public bool IsExplicit { get; init; }

    /// <summary>
    /// Creates a match representing an exact property name match.
    /// </summary>
    /// <param name="path">The origin member path.</param>
    /// <returns>A new <see cref="ConventionMatch"/> with <c>ConventionName = "ExactName"</c>.</returns>
    public static ConventionMatch ExactName(string path)
        => new() { ConventionName = "ExactName", OriginMemberPath = path, Confidence = 1.0 };

    /// <summary>
    /// Creates a match representing a flattened property path match.
    /// </summary>
    /// <param name="path">The origin member path (e.g., "Customer.Address.City").</param>
    /// <returns>A new <see cref="ConventionMatch"/> with <c>ConventionName = "Flattening"</c>.</returns>
    public static ConventionMatch Flattened(string path)
        => new() { ConventionName = "Flattening", OriginMemberPath = path, Confidence = 1.0 };

    /// <summary>
    /// Creates a match representing an explicit user binding.
    /// </summary>
    /// <param name="path">The origin member path.</param>
    /// <returns>A new <see cref="ConventionMatch"/> marked as explicit.</returns>
    public static ConventionMatch Explicit(string path)
        => new() { ConventionName = "ExplicitBinding", OriginMemberPath = path, IsExplicit = true, Confidence = 1.0 };

    /// <summary>
    /// Creates a match representing a custom value provider.
    /// </summary>
    /// <param name="providerType">The type of the custom value provider.</param>
    /// <returns>A new <see cref="ConventionMatch"/> marked as explicit.</returns>
    public static ConventionMatch CustomProvider(Type providerType)
        => new() { ConventionName = $"CustomProvider:{providerType.Name}", OriginMemberPath = "", IsExplicit = true, Confidence = 1.0 };

    /// <inheritdoc />
    public override string ToString() => IsExplicit
        ? $"{ConventionName}"
        : $"{ConventionName} ({OriginMemberPath}, {Confidence:P0})";
}
