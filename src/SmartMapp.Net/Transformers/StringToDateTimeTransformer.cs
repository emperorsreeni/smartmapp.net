using System.Globalization;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <c>string</c> to <see cref="DateTime"/> using <see cref="DateTime.TryParse(string, IFormatProvider, DateTimeStyles, out DateTime)"/>
/// with <see cref="CultureInfo.InvariantCulture"/>.
/// Takes precedence over <see cref="ParsableTransformer"/> via exact-match registration.
/// </summary>
public sealed class StringToDateTimeTransformer : ITypeTransformer<string, DateTime>
{
    /// <inheritdoc />
    public DateTime Transform(string origin, MappingScope scope)
    {
        if (origin is null)
            throw new TransformationException(
                "Cannot parse null string to DateTime.",
                null, typeof(string), typeof(DateTime));

        if (DateTime.TryParse(origin, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
            return result;

        throw new TransformationException(
            $"Cannot parse '{origin}' as DateTime.",
            origin, typeof(string), typeof(DateTime));
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(string) && targetType == typeof(DateTime);
}
