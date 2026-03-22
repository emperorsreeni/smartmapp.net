using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms a Base64-encoded <c>string</c> to <c>byte[]</c> via <see cref="Convert.FromBase64String(string)"/>.
/// </summary>
public sealed class Base64ToByteArrayTransformer : ITypeTransformer<string, byte[]>
{
    /// <inheritdoc />
    public byte[] Transform(string origin, MappingScope scope)
    {
        if (origin is null)
            return null!;

        try
        {
            return Convert.FromBase64String(origin);
        }
        catch (FormatException ex)
        {
            throw new TransformationException(
                $"Invalid Base64 string: '{(origin.Length > 50 ? origin[..50] + "..." : origin)}'.",
                origin, typeof(string), typeof(byte[]), ex);
        }
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(string) && targetType == typeof(byte[]);
}
