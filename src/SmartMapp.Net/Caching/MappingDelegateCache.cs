using System.Collections.Concurrent;

namespace SmartMapp.Net.Caching;

/// <summary>
/// Thread-safe cache of compiled mapping delegates, indexed by <see cref="TypePair"/>.
/// Each delegate is compiled once (lazily) and stored for the lifetime of the application.
/// </summary>
public sealed class MappingDelegateCache
{
    // Sprint 9 · S9-T08: storage migrated from Lazy<Func<>> to DelegateSlot so the adaptive
    // promotion manager can atomically swap an IL-emitted delegate into the slot via
    // Interlocked.CompareExchange without invalidating the existing cache lookup contract.
    private readonly ConcurrentDictionary<TypePair, DelegateSlot> _cache = new();

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
        var slot = _cache.GetOrAdd(pair, static _ => new DelegateSlot());
        return slot.GetOrCompile(() => compileFactory(pair));
    }

    /// <summary>
    /// Returns the swappable <see cref="DelegateSlot"/> for <paramref name="pair"/>, compiling
    /// it via <paramref name="compileFactory"/> when the slot is empty. Callers that need to
    /// observe adaptive-promotion swaps (Sprint 9 · S9-T08) should keep a reference to the
    /// slot and read <see cref="DelegateSlot.Current"/> per invocation.
    /// </summary>
    public DelegateSlot GetSlot(TypePair pair, Func<TypePair, Func<object, MappingScope, object>> compileFactory)
    {
        var slot = _cache.GetOrAdd(pair, static _ => new DelegateSlot());
        _ = slot.GetOrCompile(() => compileFactory(pair));
        return slot;
    }

    /// <summary>
    /// Attempts to retrieve a previously compiled delegate without triggering compilation.
    /// </summary>
    /// <param name="pair">The type pair to look up.</param>
    /// <param name="del">The cached delegate, if found and already compiled.</param>
    /// <returns><c>true</c> if a compiled delegate was found; otherwise <c>false</c>.</returns>
    public bool TryGet(TypePair pair, out Func<object, MappingScope, object>? del)
    {
        if (_cache.TryGetValue(pair, out var slot) && slot.Current is { } current)
        {
            del = current;
            return true;
        }

        del = null;
        return false;
    }

    /// <summary>
    /// Sprint 9 · S9-T08 atomic swap. Replaces the cached delegate for <paramref name="pair"/>
    /// with <paramref name="newDelegate"/> via <see cref="Interlocked.CompareExchange{T}(ref T, T, T)"/>.
    /// Returns <c>true</c> on successful publication; <c>false</c> when no slot existed yet
    /// (the IL Emit promotion path always primes the slot via <see cref="GetOrCompile"/> first).
    /// </summary>
    public bool TrySwap(TypePair pair, Func<object, MappingScope, object> newDelegate)
    {
        if (!_cache.TryGetValue(pair, out var slot)) return false;
        return slot.TrySwap(newDelegate);
    }

    /// <summary>
    /// Returns all type pairs that have compiled delegates in the cache.
    /// Useful for diagnostics.
    /// </summary>
    /// <returns>A collection of cached type pairs.</returns>
    public IReadOnlyCollection<TypePair> GetCachedPairs()
    {
        return _cache.Where(kv => kv.Value.Current is not null).Select(kv => kv.Key).ToArray();
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
