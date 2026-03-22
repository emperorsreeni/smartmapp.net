using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <c>int</c> to <c>bool</c>: <c>0</c> → <c>false</c>, any non-zero → <c>true</c>.
/// </summary>
public sealed class IntToBoolTransformer : ITypeTransformer<int, bool>
{
    /// <inheritdoc />
    public bool Transform(int origin, MappingScope scope) => origin != 0;

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(int) && targetType == typeof(bool);
}
