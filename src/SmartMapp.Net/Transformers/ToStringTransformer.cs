using System.Globalization;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Universal fallback transformer that converts any type to <c>string</c> via <c>ToString()</c>.
/// Respects <see cref="IFormattable"/> for culture-aware formatting using <see cref="CultureInfo.InvariantCulture"/>.
/// <para>
/// Lowest priority open transformer — only used when no more specific transformer
/// (e.g., <see cref="GuidToStringTransformer"/>) is registered.
/// </para>
/// </summary>
public sealed class ToStringTransformer : ITypeTransformer
{
    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
    {
        return targetType == typeof(string);
    }

    /// <summary>
    /// Transforms the origin value to <c>string</c>.
    /// Uses <see cref="IFormattable.ToString(string?, IFormatProvider?)"/> when available;
    /// otherwise falls back to <see cref="object.ToString"/>.
    /// </summary>
    /// <param name="origin">The value to convert.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The string representation, or <c>null</c> if origin is <c>null</c>.</returns>
    public string? Transform(object? origin, MappingScope scope)
    {
        if (origin is null)
            return null;

        if (origin is IFormattable formattable)
            return formattable.ToString(null, CultureInfo.InvariantCulture);

        return origin.ToString();
    }
}
