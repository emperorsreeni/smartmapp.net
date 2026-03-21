#if NET6_0_OR_GREATER
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="DateTime"/> to <see cref="TimeOnly"/> by extracting the time component.
/// </summary>
public sealed class DateTimeToTimeOnlyTransformer : ITypeTransformer<DateTime, TimeOnly>
{
    /// <inheritdoc />
    public TimeOnly Transform(DateTime origin, MappingScope scope)
    {
        return TimeOnly.FromDateTime(origin);
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(DateTime) && targetType == typeof(TimeOnly);
}
#endif
