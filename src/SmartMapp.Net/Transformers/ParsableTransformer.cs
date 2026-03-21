using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that converts <c>string</c> to any type implementing <c>IParsable&lt;T&gt;</c>
/// (.NET 7+) or with a <see cref="TypeConverter"/> (netstandard2.1 fallback).
/// Covers <c>string</c> → <c>int</c>, <c>long</c>, <c>decimal</c>, <c>double</c>,
/// <c>float</c>, <c>short</c>, <c>byte</c>, <c>DateOnly</c>, <c>TimeOnly</c>, and any
/// user type implementing <c>IParsable&lt;T&gt;</c>.
/// </summary>
public sealed class ParsableTransformer : ITypeTransformer
{
    private readonly ConcurrentDictionary<Type, Func<string, IFormatProvider, object>?> _delegateCache = new();

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
    {
        if (originType != typeof(string))
            return false;

        return GetOrCacheParseDelegate(targetType) is not null;
    }

    /// <summary>
    /// Transforms a <c>string</c> value to the specified target type.
    /// </summary>
    /// <param name="origin">The string value to parse.</param>
    /// <param name="targetType">The target type to parse into.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="TransformationException">Thrown when parsing fails.</exception>
    public object Transform(object? origin, Type targetType, MappingScope scope)
    {
        if (origin is null)
        {
            if (targetType.IsValueType)
                return Activator.CreateInstance(targetType)!;
            throw new TransformationException(
                $"Cannot parse null string to '{targetType.Name}'.",
                null, typeof(string), targetType);
        }

        var str = (string)origin;
        var parseDelegate = GetOrCacheParseDelegate(targetType);
        if (parseDelegate is null)
        {
            throw new TransformationException(
                $"No parse strategy found for type '{targetType.Name}'.",
                str, typeof(string), targetType);
        }

        try
        {
            return parseDelegate(str, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is not TransformationException)
        {
            throw new TransformationException(
                $"Cannot parse '{str}' as {targetType.Name}.",
                str, typeof(string), targetType, ex);
        }
    }

    private Func<string, IFormatProvider, object>? GetOrCacheParseDelegate(Type targetType)
    {
        return _delegateCache.GetOrAdd(targetType, static type => BuildParseDelegate(type));
    }

    private static Func<string, IFormatProvider, object>? BuildParseDelegate(Type targetType)
    {
#if NET7_0_OR_GREATER
        // Check for IParsable<T> via reflection
        var parsableInterface = targetType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IParsable<>));

        if (parsableInterface is not null)
        {
            var tryParseMethod = targetType.GetMethod("TryParse",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                null,
                [typeof(string), typeof(IFormatProvider), targetType.MakeByRefType()],
                null);

            if (tryParseMethod is not null)
            {
                return (str, provider) =>
                {
                    var args = new object?[] { str, provider, null };
                    var success = (bool)tryParseMethod.Invoke(null, args)!;
                    if (!success)
                        throw new FormatException($"TryParse failed for '{str}' → {targetType.Name}.");
                    return args[2]!;
                };
            }
        }
#endif

        // Fallback: known primitive types via Convert.ChangeType
        if (targetType == typeof(int)) return (s, p) => int.Parse(s, NumberStyles.Integer, p);
        if (targetType == typeof(long)) return (s, p) => long.Parse(s, NumberStyles.Integer, p);
        if (targetType == typeof(short)) return (s, p) => short.Parse(s, NumberStyles.Integer, p);
        if (targetType == typeof(byte)) return (s, p) => byte.Parse(s, NumberStyles.Integer, p);
        if (targetType == typeof(decimal)) return (s, p) => decimal.Parse(s, NumberStyles.Number, p);
        if (targetType == typeof(double)) return (s, p) => double.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, p);
        if (targetType == typeof(float)) return (s, p) => float.Parse(s, NumberStyles.Float | NumberStyles.AllowThousands, p);
        if (targetType == typeof(bool)) return (s, _) => bool.Parse(s);

        // Fallback: TypeDescriptor converter
        var converter = TypeDescriptor.GetConverter(targetType);
        if (converter.CanConvertFrom(typeof(string)))
        {
            return (s, p) => converter.ConvertFromString(null, p as CultureInfo ?? CultureInfo.InvariantCulture, s)!;
        }

        return null;
    }
}
