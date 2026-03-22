using System.Collections;
using System.Collections.Concurrent;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Determines whether a type is a "complex" type that requires recursive mapping
/// (as opposed to a primitive, string, or other simple type that can be assigned directly or transformed).
/// Results are cached per type for performance.
/// </summary>
internal static class ComplexTypeDetector
{
    private static readonly ConcurrentDictionary<Type, bool> Cache = new();

    private static readonly HashSet<Type> SimpleTypes = new()
    {
        typeof(string),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid),
        typeof(Uri),
        typeof(byte[]),
    };

    /// <summary>
    /// Returns <c>true</c> if the given type is a complex type that requires recursive mapping.
    /// Returns <c>false</c> for primitives, enums, strings, common value types, and collections
    /// (collections are handled separately in Sprint 5).
    /// Results are cached per type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns><c>true</c> if the type needs recursive mapping; otherwise <c>false</c>.</returns>
    internal static bool IsComplexType(Type type)
    {
        return Cache.GetOrAdd(type, Evaluate);
    }

    /// <summary>
    /// Evaluates whether a type is complex without caching. Used internally by the cache factory.
    /// </summary>
    private static bool Evaluate(Type type)
    {
        // Nullable<T>: check the underlying type
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return Evaluate(underlying);

        // Primitives, enums, and known simple types
        if (type.IsPrimitive)
            return false;

        if (type.IsEnum)
            return false;

        if (SimpleTypes.Contains(type))
            return false;

        // Collections (IEnumerable but not string) — handled in Sprint 5
        if (type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type))
            return false;

        // Interfaces and abstract types — can't construct, not complex for our purposes
        if (type.IsInterface || type.IsAbstract)
            return false;

        // Everything else is a complex type (classes, structs, records)
        return type.IsClass || (type.IsValueType && !type.IsPrimitive && !type.IsEnum);
    }
}
