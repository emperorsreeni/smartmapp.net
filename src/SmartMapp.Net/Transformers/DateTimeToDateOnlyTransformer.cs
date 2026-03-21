#if NET6_0_OR_GREATER
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="DateTime"/> to <see cref="DateOnly"/> by extracting the date component.
/// </summary>
public sealed class DateTimeToDateOnlyTransformer : ITypeTransformer<DateTime, DateOnly>
{
    /// <inheritdoc />
    public DateOnly Transform(DateTime origin, MappingScope scope)
    {
        return DateOnly.FromDateTime(origin);
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(DateTime) && targetType == typeof(DateOnly);
}
#endif
