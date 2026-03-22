using System.Collections.Concurrent;

namespace SmartMapp.Net.Caching;

/// <summary>
/// Thread-safe cache of compiled mapping delegates, indexed by <see cref="TypePair"/>.
/// Each delegate is compiled once (lazily) and stored for the lifetime of the application.
/// </summary>
public sealed class MappingDelegateCache
{
    private readonly ConcurrentDictionary<TypePair, Lazy<Func<object, MappingScope, object>>> _cache = new();

    /// <summary>
    /// Gets the compiled delegate for the given type pair, compiling it via the factory if not yet cached.
    /// Thread-safe — the factory is guaranteed to execute at most once per type pair.
    /// </summary>
    /// <param name="pair">The origin/target type pair.</param>
    /// <param name="compileFactory">A factory that compiles a new delegate for the type pair.</param>
    /// <returns>The cached (or newly compiled) mapping delegate.</returns>
    public Func<object, MappingScope, object> GetOrCompile(
        TypePair pair,
        Func<TypePair, Func<object, MappingScope, object>> compileFactory)
    {
        var lazy = _cache.GetOrAdd(pair, tp => new Lazy<Func<object, MappingScope, object>>(
            () => compileFactory(tp),
            LazyThreadSafetyMode.ExecutionAndPublication));
        return lazy.Value;
    }

    /// <summary>
    /// Attempts to retrieve a previously compiled delegate without triggering compilation.
    /// </summary>
    /// <param name="pair">The type pair to look up.</param>
    /// <param name="del">The cached delegate, if found and already compiled.</param>
    /// <returns><c>true</c> if a compiled delegate was found; otherwise <c>false</c>.</returns>
    public bool TryGet(TypePair pair, out Func<object, MappingScope, object>? del)
    {
        if (_cache.TryGetValue(pair, out var lazy) && lazy.IsValueCreated)
        {
            del = lazy.Value;
            return true;
        }

        del = null;
        return false;
    }

    /// <summary>
    /// Returns all type pairs that have compiled delegates in the cache.
    /// Useful for diagnostics.
    /// </summary>
    /// <returns>A collection of cached type pairs.</returns>
    public IReadOnlyCollection<TypePair> GetCachedPairs()
    {
        return _cache.Where(kv => kv.Value.IsValueCreated).Select(kv => kv.Key).ToArray();
    }

    /// <summary>
    /// Gets the number of entries currently in the cache (including pending compilations).
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Clears the entire cache. Intended for testing scenarios only.
    /// </summary>
    public void Clear() => _cache.Clear();
}
