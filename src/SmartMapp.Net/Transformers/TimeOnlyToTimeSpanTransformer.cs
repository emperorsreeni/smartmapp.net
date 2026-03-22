#if NET6_0_OR_GREATER
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <see cref="TimeOnly"/> to <see cref="TimeSpan"/> via <see cref="TimeOnly.ToTimeSpan"/>.
/// </summary>
public sealed class TimeOnlyToTimeSpanTransformer : ITypeTransformer<TimeOnly, TimeSpan>
{
    /// <inheritdoc />
    public TimeSpan Transform(TimeOnly origin, MappingScope scope)
    {
        return origin.ToTimeSpan();
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(TimeOnly) && targetType == typeof(TimeSpan);
}
#endif
