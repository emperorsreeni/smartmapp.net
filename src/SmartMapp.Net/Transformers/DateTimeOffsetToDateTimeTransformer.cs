using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="DateTimeOffset"/> to <see cref="DateTime"/> by extracting the UTC date/time.
/// </summary>
public sealed class DateTimeOffsetToDateTimeTransformer : ITypeTransformer<DateTimeOffset, DateTime>
{
    /// <inheritdoc />
    public DateTime Transform(DateTimeOffset origin, MappingScope scope)
    {
        return origin.UtcDateTime;
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(DateTimeOffset) && targetType == typeof(DateTime);
}
