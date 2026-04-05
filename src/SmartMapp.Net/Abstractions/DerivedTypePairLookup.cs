namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Pre-computed lookup that maps a base <see cref="TypePair"/> to all known derived <see cref="TypePair"/>
/// mappings. Used by <see cref="InheritanceResolver"/> for polymorphic dispatch.
/// </summary>
internal sealed class DerivedTypePairLookup
{
    private readonly Dictionary<TypePair, IReadOnlyList<TypePair>> _derivedPairs;

    /// <summary>
    /// Initializes a new empty <see cref="DerivedTypePairLookup"/>.
    /// </summary>
    internal DerivedTypePairLookup()
    {
        _derivedPairs = new Dictionary<TypePair, IReadOnlyList<TypePair>>();
    }

    /// <summary>
    /// Initializes a new <see cref="DerivedTypePairLookup"/> with the given entries.
    /// </summary>
    internal DerivedTypePairLookup(Dictionary<TypePair, IReadOnlyList<TypePair>> entries)
    {
        _derivedPairs = entries;
    }

    /// <summary>
    /// Gets all known derived <see cref="TypePair"/> mappings for the given base pair.
    /// Returns empty if no derived pairs exist. Sorted by origin type specificity (most derived first).
    /// </summary>
    /// <param name="basePair">The base type pair.</param>
    /// <returns>Derived type pairs sorted by specificity.</returns>
    internal IReadOnlyList<TypePair> GetDerivedPairs(TypePair basePair)
    {
        return _derivedPairs.TryGetValue(basePair, out var pairs) ? pairs : Array.Empty<TypePair>();
    }

    /// <summary>
    /// Returns <c>true</c> if the given base pair has any derived mappings.
    /// </summary>
    internal bool HasDerivedPairs(TypePair basePair) => _derivedPairs.ContainsKey(basePair);

    /// <summary>
    /// Adds a set of derived pairs for a base pair. Used during build phase.
    /// </summary>
    internal void Register(TypePair basePair, IReadOnlyList<TypePair> derivedPairs)
    {
        _derivedPairs[basePair] = derivedPairs;
    }

    /// <summary>
    /// Gets all registered base pairs.
    /// </summary>
    internal IReadOnlyCollection<TypePair> GetAllBasePairs() => _derivedPairs.Keys;
}
