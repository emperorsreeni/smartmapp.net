using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Collections;

/// <summary>
/// Inspects a <see cref="TypeModel"/> and determines the <see cref="CollectionCategory"/>
/// that best describes the collection type. Resolution is cached per <see cref="Type"/>.
/// </summary>
internal static class CollectionCategoryResolver
{
    private static readonly ConcurrentDictionary<Type, CollectionCategory> Cache = new();

    /// <summary>
    /// Resolves the <see cref="CollectionCategory"/> for the given type.
    /// Returns <see cref="CollectionCategory.Unknown"/> for non-collection types.
    /// </summary>
    /// <param name="type">The CLR type to classify.</param>
    /// <returns>The resolved collection category.</returns>
    internal static CollectionCategory Resolve(Type type)
    {
        return Cache.GetOrAdd(type, static t => Evaluate(t));
    }

    private static CollectionCategory Evaluate(Type type)
    {
        // String is NOT a collection despite implementing IEnumerable<char>
        if (type == typeof(string))
            return CollectionCategory.Unknown;

        // Arrays
        if (type.IsArray)
            return CollectionCategory.Array;

        if (!type.IsGenericType && !HasGenericCollectionInterface(type))
            return typeof(IEnumerable).IsAssignableFrom(type) ? CollectionCategory.Enumerable : CollectionCategory.Unknown;

        var typeDef = type.IsGenericType ? type.GetGenericTypeDefinition() : null;

        // Concrete types first (most specific wins)
        if (typeDef is not null)
        {
            // Dictionary family
            if (typeDef == typeof(Dictionary<,>) || typeDef == typeof(ConcurrentDictionary<,>))
                return CollectionCategory.Dictionary;

            // Immutable collections
            if (typeDef == typeof(ImmutableList<>))
                return CollectionCategory.ImmutableList;
            if (typeDef == typeof(ImmutableArray<>))
                return CollectionCategory.ImmutableArray;

            // HashSet
            if (typeDef == typeof(HashSet<>))
                return CollectionCategory.HashSet;

            // ObservableCollection
            if (typeDef == typeof(ObservableCollection<>))
                return CollectionCategory.ObservableCollection;

            // ReadOnlyCollection (concrete)
            if (typeDef == typeof(ReadOnlyCollection<>))
                return CollectionCategory.ReadOnlyCollectionConcrete;

            // List
            if (typeDef == typeof(List<>))
                return CollectionCategory.List;
        }

        // Interface types — check what the type itself is
        if (typeDef is not null)
        {
            if (typeDef == typeof(IDictionary<,>) || typeDef == typeof(IReadOnlyDictionary<,>))
                return CollectionCategory.Dictionary;

            if (typeDef == typeof(ISet<>)
#if NET8_0_OR_GREATER
                || typeDef == typeof(IReadOnlySet<>)
#endif
               )
                return CollectionCategory.HashSet;

            if (typeDef == typeof(IImmutableList<>))
                return CollectionCategory.ImmutableList;

            if (typeDef == typeof(IList<>))
                return CollectionCategory.List;

            if (typeDef == typeof(IReadOnlyList<>))
                return CollectionCategory.ReadOnlyList;

            if (typeDef == typeof(ICollection<>))
                return CollectionCategory.Collection;

            if (typeDef == typeof(IReadOnlyCollection<>))
                return CollectionCategory.ReadOnlyCollection;

            if (typeDef == typeof(IEnumerable<>))
                return CollectionCategory.Enumerable;
        }

        // Fall back to interface inspection for concrete types that implement collection interfaces
        if (ImplementsGenericInterface(type, typeof(IDictionary<,>)))
            return CollectionCategory.Dictionary;

        if (ImplementsGenericInterface(type, typeof(ISet<>)))
            return CollectionCategory.HashSet;

        if (ImplementsGenericInterface(type, typeof(IList<>)))
            return CollectionCategory.List;

        if (ImplementsGenericInterface(type, typeof(ICollection<>)))
            return CollectionCategory.Collection;

        if (ImplementsGenericInterface(type, typeof(IEnumerable<>)))
            return CollectionCategory.Enumerable;

        if (typeof(IEnumerable).IsAssignableFrom(type))
            return CollectionCategory.Enumerable;

        return CollectionCategory.Unknown;
    }

    private static bool HasGenericCollectionInterface(Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType)
            {
                var def = iface.GetGenericTypeDefinition();
                if (def == typeof(IEnumerable<>) || def == typeof(IDictionary<,>))
                    return true;
            }
        }
        return false;
    }

    private static bool ImplementsGenericInterface(Type type, Type genericInterfaceDef)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericInterfaceDef)
                return true;
        }
        return false;
    }
}
