using System.Collections.Concurrent;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that converts <c>string</c> to any <see cref="Enum"/> type.
/// Uses <c>Enum.TryParse</c> with configurable case sensitivity and fallback values.
/// </summary>
public sealed class StringToEnumTransformer : ITypeTransformer
{
    private readonly EnumTransformerOptions _options;

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    /// <param name="options">Enum transformer configuration.</param>
    public StringToEnumTransformer(EnumTransformerOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public StringToEnumTransformer() : this(new EnumTransformerOptions()) { }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(string) && targetType.IsEnum;

    /// <summary>
    /// Transforms a string value to the specified enum type.
    /// </summary>
    /// <param name="origin">The string value to parse.</param>
    /// <param name="targetType">The target enum type.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The parsed enum value.</returns>
    /// <exception cref="TransformationException">Thrown when parsing fails and no fallback is configured.</exception>
    public object Transform(object? origin, Type targetType, MappingScope scope)
    {
        if (origin is null || origin is string s && string.IsNullOrWhiteSpace(s))
        {
            if (_options.TryGetFallback(targetType, out var fallback))
                return fallback!;

            throw new TransformationException(
                $"Cannot parse null/empty string to enum '{targetType.Name}'.",
                origin, typeof(string), targetType);
        }

        var str = (string)origin;

        try
        {
            return Enum.Parse(targetType, str, _options.CaseInsensitive);
        }
        catch (ArgumentException)
        {
            if (_options.TryGetFallback(targetType, out var fallback))
                return fallback!;

            throw new TransformationException(
                $"Cannot parse '{str}' as {targetType.Name}. No matching enum member found.",
                str, typeof(string), targetType);
        }
    }
}
