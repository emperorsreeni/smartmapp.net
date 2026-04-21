namespace SmartMapp.Net.Discovery;

/// <summary>
/// A <see cref="TypePair"/> discovered by the <see cref="AssemblyScanner"/> via an attribute,
/// carrying the attribute that introduced the pair for diagnostic attribution.
/// </summary>
public sealed record ScannedTypePair
{
    /// <summary>
    /// Gets the origin type.
    /// </summary>
    public required Type OriginType { get; init; }

    /// <summary>
    /// Gets the target type.
    /// </summary>
    public required Type TargetType { get; init; }

    /// <summary>
    /// Gets the attribute that declared the pair.
    /// </summary>
    public required AttributeSource Source { get; init; }

    /// <summary>
    /// Gets the computed <see cref="SmartMapp.Net.TypePair"/>.
    /// </summary>
    public TypePair Pair => new(OriginType, TargetType);
}
