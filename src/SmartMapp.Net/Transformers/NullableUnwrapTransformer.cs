using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that unwraps <c>Nullable&lt;T&gt;</c> to <c>T</c>.
/// When the nullable has no value, returns <c>default(T)</c> for value types.
/// </summary>
public sealed class NullableUnwrapTransformer : ITypeTransformer
{
    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(originType);
        return underlying is not null && targetType.IsAssignableFrom(underlying);
    }

    /// <summary>
    /// Unwraps the <c>Nullable&lt;T&gt;</c> value.
    /// Returns the inner value if present, or <c>default(T)</c> if <c>null</c>.
    /// </summary>
    /// <param name="origin">The nullable value (boxed).</param>
    /// <param name="targetType">The target non-nullable type.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The unwrapped value, or <c>default(T)</c> for null.</returns>
    public object Transform(object? origin, Type targetType, MappingScope scope)
    {
        if (origin is null)
        {
            // Return default for value types (0, false, DateTime.MinValue, etc.)
            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType)!;

            return null!;
        }

        // Boxed Nullable<T> is either null or the underlying T value — return as-is
        return origin;
    }
}
