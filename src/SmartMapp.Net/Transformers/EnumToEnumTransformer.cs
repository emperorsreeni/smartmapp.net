using System.Collections.Concurrent;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that converts between two different <see cref="Enum"/> types.
/// Supports by-name (default, case-insensitive) and by-value strategies.
/// Caches conversion mappings per <see cref="TypePair"/> for performance.
/// </summary>
public sealed class EnumToEnumTransformer : ITypeTransformer
{
    private readonly EnumTransformerOptions _options;
    private readonly ConcurrentDictionary<TypePair, Func<object, object>> _cache = new();

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    /// <param name="options">Enum transformer configuration.</param>
    public EnumToEnumTransformer(EnumTransformerOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public EnumToEnumTransformer() : this(new EnumTransformerOptions()) { }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType.IsEnum && targetType.IsEnum && originType != targetType;

    /// <summary>
    /// Transforms an enum value from the origin enum type to the target enum type.
    /// </summary>
    /// <param name="origin">The origin enum value.</param>
    /// <param name="targetType">The target enum type.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The converted enum value.</returns>
    /// <exception cref="TransformationException">Thrown when no matching member is found and no fallback is configured.</exception>
    public object Transform(object origin, Type targetType, MappingScope scope)
    {
        var originType = origin.GetType();
        var pair = new TypePair(originType, targetType);

        var converter = _cache.GetOrAdd(pair, p => BuildConverter(p.OriginType, p.TargetType));
        return converter(origin);
    }

    private Func<object, object> BuildConverter(Type originType, Type targetType)
    {
        if (_options.Strategy == EnumMappingStrategy.ByValue)
            return BuildByValueConverter(originType, targetType);

        return BuildByNameConverter(originType, targetType);
    }

    private Func<object, object> BuildByNameConverter(Type originType, Type targetType)
    {
        // Pre-compute name → target value mapping
        var targetValues = new Dictionary<string, object>(
            _options.CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var name in Enum.GetNames(targetType))
        {
            targetValues[name] = Enum.Parse(targetType, name);
        }

        return origin =>
        {
            var name = origin.ToString()!;

            // Handle [Flags] enums: split by comma
            if (name.Contains(','))
            {
                return ConvertFlagsByName(name, originType, targetType, targetValues);
            }

            if (targetValues.TryGetValue(name, out var result))
                return result;

            if (_options.TryGetFallback(targetType, out var fallback))
                return fallback!;

            throw new TransformationException(
                $"No matching enum member '{name}' in {targetType.Name}.",
                origin, originType, targetType);
        };
    }

    private object ConvertFlagsByName(string name, Type originType, Type targetType,
        Dictionary<string, object> targetValues)
    {
        var parts = name.Split(',');
        var underlyingType = Enum.GetUnderlyingType(targetType);
        long combined = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (targetValues.TryGetValue(part, out var value))
            {
                combined |= Convert.ToInt64(value);
            }
            else if (_options.TryGetFallback(targetType, out _))
            {
                // Skip unmatched flags when fallback is configured
            }
            else
            {
                throw new TransformationException(
                    $"No matching enum member '{part}' in {targetType.Name}.",
                    name, originType, targetType);
            }
        }

        return Enum.ToObject(targetType, combined);
    }

    private Func<object, object> BuildByValueConverter(Type originType, Type targetType)
    {
        return origin =>
        {
            try
            {
                var numericValue = Convert.ChangeType(origin, Enum.GetUnderlyingType(targetType));
                var result = Enum.ToObject(targetType, numericValue);

                // Verify the value is defined (unless it's a Flags enum)
                if (!targetType.IsDefined(typeof(FlagsAttribute), false) &&
                    !Enum.IsDefined(targetType, result))
                {
                    if (_options.TryGetFallback(targetType, out var fallback))
                        return fallback!;

                    throw new TransformationException(
                        $"Value '{origin}' ({Convert.ToInt64(origin)}) is not defined in {targetType.Name}.",
                        origin, originType, targetType);
                }

                return result;
            }
            catch (Exception ex) when (ex is not TransformationException)
            {
                throw new TransformationException(
                    $"Cannot convert enum value '{origin}' from {originType.Name} to {targetType.Name}.",
                    origin, originType, targetType, ex);
            }
        };
    }
}
