using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Transforms <c>bool</c> to <c>int</c>: <c>true</c> → <c>1</c>, <c>false</c> → <c>0</c>.
/// Common in database interop scenarios.
/// </summary>
public sealed class BoolToIntTransformer : ITypeTransformer<bool, int>
{
    /// <inheritdoc />
    public int Transform(bool origin, MappingScope scope) => origin ? 1 : 0;

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(bool) && targetType == typeof(int);
}
