using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that wraps a value type <c>T</c> into <c>Nullable&lt;T&gt;</c>.
/// Matches when the target is <c>Nullable&lt;T&gt;</c> and the origin is assignable to <c>T</c>.
/// </summary>
public sealed class NullableWrapTransformer : ITypeTransformer
{
    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        return underlying is not null && underlying.IsAssignableFrom(originType);
    }

    /// <summary>
    /// Wraps the origin value into <c>Nullable&lt;T&gt;</c>.
    /// Since boxing <c>T</c> naturally produces a valid <c>Nullable&lt;T&gt;</c> when unboxed,
    /// this is effectively an identity operation at the <c>object</c> level.
    /// </summary>
    /// <param name="origin">The value to wrap.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The wrapped value (same object reference when boxed).</returns>
    public object? Transform(object? origin, MappingScope scope)
    {
        // Boxing a value type T to object and unboxing to Nullable<T> is handled by the CLR.
        // At the object level, this is an identity operation.
        return origin;
    }
}
