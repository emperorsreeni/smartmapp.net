using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="Uri"/> to <c>string</c> via <see cref="Uri.OriginalString"/>
/// to preserve the original input format.
/// </summary>
public sealed class UriToStringTransformer : ITypeTransformer<Uri, string>
{
    /// <inheritdoc />
    public string Transform(Uri origin, MappingScope scope)
    {
        if (origin is null)
            return null!;

        return origin.OriginalString;
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(Uri) && targetType == typeof(string);
}
