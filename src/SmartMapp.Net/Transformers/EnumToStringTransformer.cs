using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that converts any <see cref="Enum"/> to <c>string</c>.
/// Supports <c>ToString()</c> (default) or <c>[Description]</c> attribute value when opted in.
/// </summary>
public sealed class EnumToStringTransformer : ITypeTransformer
{
    private readonly EnumTransformerOptions _options;
    private readonly ConcurrentDictionary<Type, Dictionary<object, string>> _descriptionCache = new();

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    /// <param name="options">Enum transformer configuration.</param>
    public EnumToStringTransformer(EnumTransformerOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public EnumToStringTransformer() : this(new EnumTransformerOptions()) { }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType.IsEnum && targetType == typeof(string);

    /// <summary>
    /// Transforms an enum value to its string representation.
    /// </summary>
    /// <param name="origin">The enum value.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The string representation of the enum value.</returns>
    public string Transform(object origin, MappingScope scope)
    {
        if (_options.UseDescriptionAttribute)
        {
            var descriptions = GetDescriptionMap(origin.GetType());
            if (descriptions.TryGetValue(origin, out var desc))
                return desc;
        }

        return origin.ToString()!;
    }

    private Dictionary<object, string> GetDescriptionMap(Type enumType)
    {
        return _descriptionCache.GetOrAdd(enumType, static type =>
        {
            var map = new Dictionary<object, string>();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var desc = field.GetCustomAttribute<DescriptionAttribute>();
                if (desc is not null)
                {
                    var value = field.GetValue(null)!;
                    map[value] = desc.Description;
                }
            }
            return map;
        });
    }
}
