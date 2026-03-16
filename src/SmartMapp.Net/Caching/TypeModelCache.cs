using System.Collections.Concurrent;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Caching;

/// <summary>
/// Thread-safe, process-lifetime cache of <see cref="TypeModel"/> instances.
/// Used by every component that needs type metadata. Backed by <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class TypeModelCache
{
    /// <summary>
    /// Gets the global default singleton instance.
    /// </summary>
    public static TypeModelCache Default { get; } = new();

    private readonly ConcurrentDictionary<Type, TypeModel> _cache = new();

    /// <summary>
    /// Returns the cached <see cref="TypeModel"/> for the specified type, creating and caching one if absent.
    /// </summary>
    /// <param name="type">The CLR type to get the model for.</param>
    /// <returns>The cached <see cref="TypeModel"/>.</returns>
    public TypeModel GetOrAdd(Type type) => _cache.GetOrAdd(type, static t => new TypeModel(t));

    /// <summary>
    /// Returns the cached <see cref="TypeModel"/> for <typeparamref name="T"/>, creating and caching one if absent.
    /// </summary>
    /// <typeparam name="T">The CLR type to get the model for.</typeparam>
    /// <returns>The cached <see cref="TypeModel"/>.</returns>
    public TypeModel GetOrAdd<T>() => GetOrAdd(typeof(T));

    /// <summary>
    /// Gets the number of types currently cached.
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Clears the entire cache. Intended for testing scenarios only.
    /// </summary>
    public void Clear() => _cache.Clear();
}
