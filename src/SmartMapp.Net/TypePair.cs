namespace SmartMapp.Net;

/// <summary>
/// Uniquely identifies an (OriginType, TargetType) combination.
/// Used as the universal key across all caches (blueprint, delegate, convention link).
/// </summary>
public readonly record struct TypePair(Type OriginType, Type TargetType)
{
    /// <summary>
    /// Creates a <see cref="TypePair"/> from generic type arguments.
    /// </summary>
    /// <typeparam name="TOrigin">The origin type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <returns>A new <see cref="TypePair"/> instance.</returns>
    public static TypePair Of<TOrigin, TTarget>() => new(typeof(TOrigin), typeof(TTarget));

    /// <inheritdoc />
    public override string ToString() => $"{OriginType.Name} -> {TargetType.Name}";
}
