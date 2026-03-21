using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <c>string</c> to <see cref="Uri"/> via <see cref="Uri.TryCreate(string, UriKind, out Uri)"/>
/// with <see cref="UriKind.RelativeOrAbsolute"/>.
/// </summary>
public sealed class StringToUriTransformer : ITypeTransformer<string, Uri>
{
    /// <inheritdoc />
    public Uri Transform(string origin, MappingScope scope)
    {
        if (origin is null)
            return null!;

        if (string.IsNullOrWhiteSpace(origin))
            throw new TransformationException(
                "Cannot parse empty or whitespace string to Uri.",
                origin, typeof(string), typeof(Uri));

        if (Uri.TryCreate(origin, UriKind.RelativeOrAbsolute, out var result))
            return result;

        throw new TransformationException(
            $"Cannot parse '{origin}' as Uri.",
            origin, typeof(string), typeof(Uri));
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(string) && targetType == typeof(Uri);
}
