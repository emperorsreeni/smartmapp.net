using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="Guid"/> to <c>string</c> using the standard "D" format
/// (<c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>).
/// </summary>
public sealed class GuidToStringTransformer : ITypeTransformer<Guid, string>
{
    /// <inheritdoc />
    public string Transform(Guid origin, MappingScope scope)
        => origin.ToString("D");

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(Guid) && targetType == typeof(string);
}
