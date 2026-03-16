namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Non-generic marker interface for type transformers.
/// Enables runtime discovery and registry lookups by <c>(Type, Type)</c> pair.
/// </summary>
public interface ITypeTransformer
{
    /// <summary>
    /// Determines whether this transformer can convert from <paramref name="originType"/> to <paramref name="targetType"/>.
    /// </summary>
    /// <param name="originType">The source type.</param>
    /// <param name="targetType">The destination type.</param>
    /// <returns><c>true</c> if this transformer handles the conversion; otherwise <c>false</c>.</returns>
    bool CanTransform(Type originType, Type targetType);
}

/// <summary>
/// Strongly-typed interface for converting a value from <typeparamref name="TOrigin"/> to <typeparamref name="TTarget"/>.
/// Used both for built-in type conversions (e.g., <c>DateTime</c> → <c>DateTimeOffset</c>)
/// and user-defined custom transformers.
/// </summary>
/// <typeparam name="TOrigin">The source value type (contravariant).</typeparam>
/// <typeparam name="TTarget">The destination value type (covariant).</typeparam>
public interface ITypeTransformer<in TOrigin, out TTarget> : ITypeTransformer
{
    /// <summary>
    /// Transforms a value from the origin type to the target type.
    /// </summary>
    /// <param name="origin">The source value to transform.</param>
    /// <param name="scope">The current mapping scope providing context, services, and cancellation.</param>
    /// <returns>The transformed value.</returns>
    TTarget Transform(TOrigin origin, MappingScope scope);
}
