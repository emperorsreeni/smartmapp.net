#if NET6_0_OR_GREATER
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="DateOnly"/> to <see cref="DateTime"/> by combining with <see cref="TimeOnly.MinValue"/>.
/// </summary>
public sealed class DateOnlyToDateTimeTransformer : ITypeTransformer<DateOnly, DateTime>
{
    /// <inheritdoc />
    public DateTime Transform(DateOnly origin, MappingScope scope)
    {
        return origin.ToDateTime(TimeOnly.MinValue);
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(DateOnly) && targetType == typeof(DateTime);
}
#endif
