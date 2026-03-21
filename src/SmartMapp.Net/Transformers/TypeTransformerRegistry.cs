using System.Collections.Concurrent;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Central registry for <see cref="ITypeTransformer"/> instances.
/// Stores and looks up transformers by <c>(Type, Type)</c> pair with fallback strategies:
/// exact match → open transformer <c>CanTransform</c> scan.
/// <para>
/// Registration happens at startup; lookups happen at mapping time.
/// Thread-safe for concurrent reads after registration is complete.
/// </para>
/// </summary>
public sealed class TypeTransformerRegistry
{
    private readonly ConcurrentDictionary<TypePair, ITypeTransformer> _transformers = new();
    private readonly List<ITypeTransformer> _openTransformers = [];
    private readonly ConcurrentDictionary<TypePair, ITypeTransformer?> _scanCache = new();

    /// <summary>
    /// Registers a strongly-typed transformer for an exact <c>(TOrigin, TTarget)</c> pair.
    /// </summary>
    /// <typeparam name="TOrigin">The origin type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="transformer">The transformer instance.</param>
    public void Register<TOrigin, TTarget>(ITypeTransformer<TOrigin, TTarget> transformer)
    {
        var pair = TypePair.Of<TOrigin, TTarget>();
        _transformers[pair] = transformer;
    }

    /// <summary>
    /// Registers a transformer for an explicit <c>(originType, targetType)</c> pair.
    /// </summary>
    /// <param name="originType">The origin type.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="transformer">The transformer instance.</param>
    public void Register(Type originType, Type targetType, ITypeTransformer transformer)
    {
        var pair = new TypePair(originType, targetType);
        _transformers[pair] = transformer;
    }

    /// <summary>
    /// Registers an open transformer that dynamically matches type pairs via <see cref="ITypeTransformer.CanTransform"/>.
    /// Open transformers are scanned in registration order when no exact match is found.
    /// </summary>
    /// <param name="transformer">The open transformer instance.</param>
    public void RegisterOpen(ITypeTransformer transformer)
    {
        _openTransformers.Add(transformer);
        _scanCache.Clear();
    }

    /// <summary>
    /// Removes all open transformers and clears the scan cache.
    /// Used to support idempotent re-registration (e.g., <see cref="TypeTransformerRegistryDefaults.RegisterDefaults"/>).
    /// </summary>
    public void ClearOpen()
    {
        _openTransformers.Clear();
        _scanCache.Clear();
    }

    /// <summary>
    /// Looks up the best transformer for the given type pair.
    /// Strategy: exact dictionary lookup first, then open transformer <c>CanTransform</c> scan.
    /// Scan results are cached to avoid repeated reflection.
    /// </summary>
    /// <param name="originType">The origin type.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns>The matching transformer, or <c>null</c> if none found.</returns>
    public ITypeTransformer? GetTransformer(Type originType, Type targetType)
    {
        var pair = new TypePair(originType, targetType);

        // 1. Exact match
        if (_transformers.TryGetValue(pair, out var transformer))
            return transformer;

        // 2. Cached scan result
        if (_scanCache.TryGetValue(pair, out var cached))
            return cached;

        // 3. CanTransform scan over open transformers
        ITypeTransformer? found = null;
        for (int i = 0; i < _openTransformers.Count; i++)
        {
            if (_openTransformers[i].CanTransform(originType, targetType))
            {
                found = _openTransformers[i];
                break;
            }
        }

        // Cache the result (including null for "no match found")
        _scanCache[pair] = found;
        return found;
    }

    /// <summary>
    /// Checks whether a transformer exists for the given type pair without retrieving it.
    /// </summary>
    /// <param name="originType">The origin type.</param>
    /// <param name="targetType">The target type.</param>
    /// <returns><c>true</c> if a transformer is available; otherwise <c>false</c>.</returns>
    public bool HasTransformer(Type originType, Type targetType)
    {
        return GetTransformer(originType, targetType) is not null;
    }

    /// <summary>
    /// Returns all explicitly registered type pairs (excludes open transformer matches).
    /// Useful for diagnostics and the <c>MappingAtlas</c>.
    /// </summary>
    /// <returns>A read-only list of registered type pairs.</returns>
    public IReadOnlyList<TypePair> GetRegisteredPairs()
    {
        return _transformers.Keys.ToArray();
    }

    /// <summary>
    /// Returns the count of explicitly registered transformers.
    /// </summary>
    public int Count => _transformers.Count;

    /// <summary>
    /// Returns the count of open transformers registered for dynamic matching.
    /// </summary>
    public int OpenCount => _openTransformers.Count;

    /// <summary>
    /// Clears all registered transformers and cached scan results. Intended for testing.
    /// </summary>
    public void Clear()
    {
        _transformers.Clear();
        _openTransformers.Clear();
        _scanCache.Clear();
    }
}
