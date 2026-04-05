using System.Collections.Concurrent;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Pre-computed, immutable map of type inheritance hierarchies.
/// Built once during the forge/build phase and shared read-only across all mapping operations.
/// </summary>
internal sealed class TypeHierarchyMap
{
    private readonly Dictionary<Type, IReadOnlyList<Type>> _derivedTypes;

    /// <summary>
    /// Initializes a new <see cref="TypeHierarchyMap"/> by scanning the given types
    /// and building a base-to-derived lookup.
    /// </summary>
    /// <param name="knownTypes">All types registered in the system (origin + target types from blueprints).</param>
    internal TypeHierarchyMap(IEnumerable<Type> knownTypes)
    {
        _derivedTypes = BuildDerivedMap(knownTypes);
    }

    /// <summary>
    /// Gets all known derived types for the given base type, sorted by specificity (most derived first).
    /// Returns empty if no derived types are registered.
    /// </summary>
    /// <param name="baseType">The base type to look up.</param>
    /// <returns>An ordered list of derived types, most specific first.</returns>
    internal IReadOnlyList<Type> GetDerivedTypes(Type baseType)
    {
        return _derivedTypes.TryGetValue(baseType, out var derived) ? derived : Array.Empty<Type>();
    }

    /// <summary>
    /// Returns <c>true</c> if the given type has any known derived types.
    /// </summary>
    internal bool HasDerivedTypes(Type baseType) => _derivedTypes.ContainsKey(baseType);

    /// <summary>
    /// Builds the base-to-derived lookup from known types.
    /// For each type, walks the inheritance chain and records it as a derived type of each ancestor.
    /// </summary>
    private static Dictionary<Type, IReadOnlyList<Type>> BuildDerivedMap(IEnumerable<Type> knownTypes)
    {
        var map = new Dictionary<Type, List<Type>>();

        foreach (var type in knownTypes)
        {
            if (type.IsInterface || type == typeof(object)) continue;

            // Walk the class hierarchy
            var baseType = type.BaseType;
            while (baseType is not null && baseType != typeof(object))
            {
                if (!map.TryGetValue(baseType, out var list))
                {
                    list = new List<Type>();
                    map[baseType] = list;
                }

                if (!list.Contains(type))
                    list.Add(type);

                baseType = baseType.BaseType;
            }

            // Also register for interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (!map.TryGetValue(iface, out var list))
                {
                    list = new List<Type>();
                    map[iface] = list;
                }

                if (!list.Contains(type))
                    list.Add(type);
            }
        }

        // Sort each list by depth descending (most derived first)
        var result = new Dictionary<Type, IReadOnlyList<Type>>(map.Count);
        foreach (var (baseType, derivedList) in map)
        {
            derivedList.Sort((a, b) => GetInheritanceDepth(b) - GetInheritanceDepth(a));
            result[baseType] = derivedList;
        }

        return result;
    }

    /// <summary>
    /// Returns the inheritance depth of a type (distance from <see cref="object"/>).
    /// </summary>
    private static int GetInheritanceDepth(Type type)
    {
        var depth = 0;
        var current = type;
        while (current is not null && current != typeof(object))
        {
            depth++;
            current = current.BaseType;
        }
        return depth;
    }
}
