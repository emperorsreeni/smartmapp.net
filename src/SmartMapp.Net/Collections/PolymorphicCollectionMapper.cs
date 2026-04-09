using System.Linq.Expressions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Collections;

/// <summary>
/// Extends <see cref="CollectionMapper"/> with polymorphic element mapping support.
/// When a collection's element type has known derived mappings, each element is dispatched
/// at runtime to the most specific mapping delegate based on its actual type.
/// </summary>
internal sealed class PolymorphicCollectionMapper
{
    private readonly InheritanceResolver _inheritanceResolver;
    private readonly MappingDelegateCache _delegateCache;

    /// <summary>
    /// Initializes a new <see cref="PolymorphicCollectionMapper"/>.
    /// </summary>
    internal PolymorphicCollectionMapper(
        InheritanceResolver inheritanceResolver,
        MappingDelegateCache delegateCache)
    {
        _inheritanceResolver = inheritanceResolver;
        _delegateCache = delegateCache;
    }

    /// <summary>
    /// Determines whether a collection element type requires polymorphic dispatch.
    /// Returns <c>true</c> if the element type has known derived mappings.
    /// </summary>
    /// <param name="baseElementPair">The base element type pair.</param>
    /// <returns><c>true</c> if polymorphic dispatch is needed.</returns>
    internal bool RequiresPolymorphicDispatch(TypePair baseElementPair)
    {
        return _inheritanceResolver.HasDerivedPairs(baseElementPair);
    }

    /// <summary>
    /// Builds a polymorphic element mapping delegate that dispatches each element
    /// to the correct derived mapper based on its runtime type.
    /// </summary>
    /// <param name="baseElementPair">The base element type pair (e.g., Shape → ShapeDto).</param>
    /// <param name="baseElementDelegate">The compiled delegate for the base element type pair.</param>
    /// <param name="compileDerived">Factory to compile a derived type pair's delegate on demand.</param>
    /// <returns>A delegate that maps a single element with polymorphic dispatch.</returns>
    internal Func<object, MappingScope, object> BuildPolymorphicElementMapper(
        TypePair baseElementPair,
        Func<object, MappingScope, object> baseElementDelegate,
        Func<TypePair, Func<object, MappingScope, object>> compileDerived)
    {
        var derivedPairs = _inheritanceResolver.GetDerivedPairs(baseElementPair);

        if (derivedPairs.Count == 0)
            return baseElementDelegate;

        // Pre-compile all derived delegates
        var derivedDelegates = new List<(Type originType, Func<object, MappingScope, object> del)>();
        foreach (var pair in derivedPairs)
        {
            var del = _delegateCache.GetOrCompile(pair, compileDerived);
            derivedDelegates.Add((pair.OriginType, del));
        }

        // Return a polymorphic dispatch delegate
        return (element, scope) =>
        {
            if (element is null)
                return null!;

            var runtimeType = element.GetType();

            // Check derived types (most specific first)
            foreach (var (originType, del) in derivedDelegates)
            {
                if (originType.IsAssignableFrom(runtimeType))
                    return del(element, scope);
            }

            // Fallback to base
            return baseElementDelegate(element, scope);
        };
    }

    /// <summary>
    /// Maps a collection of heterogeneous elements using polymorphic dispatch.
    /// Each element is dispatched to its most specific mapping delegate.
    /// </summary>
    /// <typeparam name="TBaseTarget">The base target element type.</typeparam>
    /// <param name="elements">The source elements to map.</param>
    /// <param name="elementMapper">The polymorphic element mapper delegate.</param>
    /// <param name="scope">The mapping scope.</param>
    /// <returns>A list of mapped elements, each as its most specific derived type.</returns>
    internal static List<TBaseTarget> MapPolymorphicCollection<TBaseTarget>(
        System.Collections.IEnumerable elements,
        Func<object, MappingScope, object> elementMapper,
        MappingScope scope)
    {
        var result = new List<TBaseTarget>();

        foreach (var element in elements)
        {
            if (element is null)
            {
                result.Add(default!);
                continue;
            }

            var mapped = elementMapper(element, scope);
            result.Add((TBaseTarget)mapped);
        }

        return result;
    }
}
