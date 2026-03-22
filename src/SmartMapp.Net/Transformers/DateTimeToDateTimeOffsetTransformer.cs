using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="DateTime"/> to <see cref="DateTimeOffset"/> with <see cref="DateTimeKind"/> awareness.
/// <list type="bullet">
/// <item><see cref="DateTimeKind.Utc"/> → offset <see cref="TimeSpan.Zero"/></item>
/// <item><see cref="DateTimeKind.Local"/> → local offset</item>
/// <item><see cref="DateTimeKind.Unspecified"/> → treated as UTC (configurable in Sprint 7)</item>
/// </list>
/// </summary>
public sealed class DateTimeToDateTimeOffsetTransformer : ITypeTransformer<DateTime, DateTimeOffset>
{
    /// <inheritdoc />
    public DateTimeOffset Transform(DateTime origin, MappingScope scope)
    {
        return origin.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(origin, TimeSpan.Zero),
            DateTimeKind.Local => new DateTimeOffset(origin),
            _ => new DateTimeOffset(origin, TimeSpan.Zero), // Unspecified → treat as UTC
        };
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(DateTime) && targetType == typeof(DateTimeOffset);
}
