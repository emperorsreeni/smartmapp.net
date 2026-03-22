using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <c>string</c> to <see cref="Guid"/> via <see cref="Guid.TryParse(string, out Guid)"/>.
/// Accepts all standard formats (D, N, B, P).
/// </summary>
public sealed class StringToGuidTransformer : ITypeTransformer<string, Guid>
{
    /// <inheritdoc />
    public Guid Transform(string origin, MappingScope scope)
    {
        if (origin is null)
            throw new TransformationException(
                "Cannot parse null string to Guid.",
                null, typeof(string), typeof(Guid));

        if (Guid.TryParse(origin, out var result))
            return result;

        throw new TransformationException(
            $"Cannot parse '{origin}' as Guid.",
            origin, typeof(string), typeof(Guid));
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(string) && targetType == typeof(Guid);
}
