using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <c>byte[]</c> to <c>string</c> using Base64 encoding via <see cref="Convert.ToBase64String(byte[])"/>.
/// </summary>
public sealed class ByteArrayToBase64Transformer : ITypeTransformer<byte[], string>
{
    /// <inheritdoc />
    public string Transform(byte[] origin, MappingScope scope)
    {
        if (origin is null)
            return null!;

        return Convert.ToBase64String(origin);
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(byte[]) && targetType == typeof(string);
}
