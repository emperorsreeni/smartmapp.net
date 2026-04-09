using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Resolves inheritance hierarchies for polymorphic mapping dispatch.
/// Pre-computes derived type pair lookups during the build phase and provides
/// O(1) amortized runtime dispatch for polymorphic mapping.
/// Thread-safe after construction — all internal structures are read-only.
/// </summary>
internal sealed class InheritanceResolver
{
    private readonly TypeHierarchyMap _hierarchyMap;
    private readonly DerivedTypePairLookup _derivedPairLookup;
    private readonly Dictionary<TypePair, List<TypePair>> _explicitDerivedPairs = new();
    private readonly Dictionary<TypePair, DiscriminatorConfig> _discriminators = new();
    private readonly Dictionary<TypePair, Type> _materializeTypes = new();
    private readonly Dictionary<TypePair, TypePair> _inheritFromPairs = new();

    /// <summary>
    /// Initializes a new <see cref="InheritanceResolver"/> with the given known types.
    /// </summary>
    /// <param name="knownTypes">All types registered in the system (from blueprints).</param>
    internal InheritanceResolver(IEnumerable<Type> knownTypes)
    {
        _hierarchyMap = new TypeHierarchyMap(knownTypes);
        _derivedPairLookup = new DerivedTypePairLookup();
    }

    /// <summary>
    /// Initializes a new <see cref="InheritanceResolver"/> with no known types.
    /// Used for testing or when polymorphism is not needed.
    /// </summary>
    internal InheritanceResolver()
    {
        _hierarchyMap = new TypeHierarchyMap(Array.Empty<Type>());
        _derivedPairLookup = new DerivedTypePairLookup();
    }

    /// <summary>
    /// Registers an explicit derived pair for a base pair via <c>ExtendWith&lt;TDerived&gt;()</c>.
    /// Explicit pairs take priority over auto-discovered pairs.
    /// </summary>
    internal void RegisterExplicitDerivedPair(TypePair basePair, TypePair derivedPair)
    {
        if (!_explicitDerivedPairs.TryGetValue(basePair, out var list))
        {
            list = new List<TypePair>();
            _explicitDerivedPairs[basePair] = list;
        }

        if (!list.Contains(derivedPair))
            list.Add(derivedPair);
    }

    /// <summary>
    /// Registers a discriminator configuration for a base pair via <c>DiscriminateBy()</c>.
    /// </summary>
    internal void RegisterDiscriminator(TypePair basePair, DiscriminatorConfig config)
    {
        _discriminators[basePair] = config;
    }

    /// <summary>
    /// Registers a materialization type for an interface/abstract target via <c>Materialize&lt;T&gt;()</c>.
    /// </summary>
    internal void RegisterMaterializeType(TypePair basePair, Type concreteType)
    {
        _materializeTypes[basePair] = concreteType;
    }

    /// <summary>
    /// Registers a blueprint inheritance relationship via <c>InheritFrom&lt;TBase&gt;()</c>.
    /// </summary>
    internal void RegisterInheritFrom(TypePair derivedPair, TypePair basePair)
    {
        _inheritFromPairs[derivedPair] = basePair;
    }

    /// <summary>
    /// Gets the base pair that the given derived pair inherits from, if any.
    /// </summary>
    internal TypePair? GetInheritFromPair(TypePair derivedPair)
    {
        return _inheritFromPairs.TryGetValue(derivedPair, out var basePair) ? basePair : null;
    }

    /// <summary>
    /// Gets the materialization type for an interface/abstract target, if configured.
    /// </summary>
    internal Type? GetMaterializeType(TypePair pair)
    {
        return _materializeTypes.TryGetValue(pair, out var type) ? type : null;
    }

    /// <summary>
    /// Gets the discriminator config for a base pair, if configured.
    /// </summary>
    internal DiscriminatorConfig? GetDiscriminator(TypePair basePair)
    {
        return _discriminators.TryGetValue(basePair, out var config) ? config : null;
    }

    /// <summary>
    /// Builds the derived pair lookup from registered blueprints.
    /// Must be called after all explicit pairs and blueprints are registered, before mapping begins.
    /// </summary>
    /// <param name="registeredPairs">All type pairs that have registered blueprints.</param>
    internal void BuildDerivedPairLookup(IEnumerable<TypePair> registeredPairs)
    {
        var allPairs = registeredPairs.ToList();

        foreach (var basePair in allPairs)
        {
            var derivedPairs = new List<TypePair>();

            // 1. Explicit pairs take priority
            if (_explicitDerivedPairs.TryGetValue(basePair, out var explicitPairs))
            {
                derivedPairs.AddRange(explicitPairs);
            }

            // 2. Auto-discover derived pairs from hierarchy
            var derivedOrigins = _hierarchyMap.GetDerivedTypes(basePair.OriginType);
            var derivedTargets = _hierarchyMap.GetDerivedTypes(basePair.TargetType);

            foreach (var derivedOrigin in derivedOrigins)
            {
                // Try to find a matching target by name convention
                var matchingTarget = FindMatchingDerivedTarget(
                    derivedOrigin, basePair.TargetType, derivedTargets);

                if (matchingTarget is not null)
                {
                    var derivedPair = new TypePair(derivedOrigin, matchingTarget);

                    // Don't add duplicates or self-references
                    if (derivedPair != basePair && !derivedPairs.Contains(derivedPair))
                        derivedPairs.Add(derivedPair);
                }
            }

            if (derivedPairs.Count > 0)
            {
                // Sort by origin specificity (most derived first)
                derivedPairs.Sort((a, b) =>
                    GetInheritanceDepth(b.OriginType) - GetInheritanceDepth(a.OriginType));

                _derivedPairLookup.Register(basePair, derivedPairs);
            }
        }
    }

    /// <summary>
    /// Gets all known derived <see cref="TypePair"/> mappings for the given base pair.
    /// Includes both auto-discovered and explicitly registered pairs.
    /// </summary>
    internal IReadOnlyList<TypePair> GetDerivedPairs(TypePair basePair)
    {
        return _derivedPairLookup.GetDerivedPairs(basePair);
    }

    /// <summary>
    /// Returns <c>true</c> if the given base pair has any derived mappings.
    /// </summary>
    internal bool HasDerivedPairs(TypePair basePair) => _derivedPairLookup.HasDerivedPairs(basePair);

    /// <summary>
    /// Resolves the best derived <see cref="TypePair"/> for a given base pair and runtime origin type.
    /// Walks the origin hierarchy upward until a registered derived pair is found.
    /// Returns <c>null</c> if no derived pair matches.
    /// </summary>
    /// <param name="basePair">The declared base type pair.</param>
    /// <param name="runtimeOriginType">The actual runtime type of the origin object.</param>
    /// <returns>The best matching derived pair, or <c>null</c> if none found.</returns>
    internal TypePair? ResolveBestPair(TypePair basePair, Type runtimeOriginType)
    {
        if (runtimeOriginType == basePair.OriginType)
            return null; // No polymorphism needed

        var derivedPairs = GetDerivedPairs(basePair);
        if (derivedPairs.Count == 0)
            return null;

        // Walk from most specific to least specific
        foreach (var pair in derivedPairs)
        {
            if (pair.OriginType.IsAssignableFrom(runtimeOriginType))
                return pair;
        }

        return null;
    }

    /// <summary>
    /// Finds a matching derived target type for a derived origin type using name-suffix convention.
    /// E.g., given derived origin <c>Circle</c> and base target <c>ShapeDto</c>,
    /// looks for <c>CircleDto</c> among the derived targets.
    /// </summary>
    private static Type? FindMatchingDerivedTarget(
        Type derivedOrigin,
        Type baseTarget,
        IReadOnlyList<Type> derivedTargets)
    {
        // Extract suffix from base target name (e.g., "Dto" from "ShapeDto", "ViewModel" from "ShapeViewModel")
        var baseOriginName = baseTarget.Name;
        var suffixes = ExtractCommonSuffixes(baseTarget.Name);

        foreach (var suffix in suffixes)
        {
            var expectedTargetName = derivedOrigin.Name + suffix;
            foreach (var candidate in derivedTargets)
            {
                if (string.Equals(candidate.Name, expectedTargetName, StringComparison.Ordinal))
                    return candidate;
            }
        }

        // Fallback: exact name match (e.g., Circle → Circle in same hierarchy)
        foreach (var candidate in derivedTargets)
        {
            if (string.Equals(candidate.Name, derivedOrigin.Name, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Extracts common DTO/ViewModel suffixes from a type name.
    /// </summary>
    private static IReadOnlyList<string> ExtractCommonSuffixes(string typeName)
    {
        var suffixes = new List<string>();
        var knownSuffixes = new[] { "Dto", "DTO", "ViewModel", "Vm", "Model", "Response", "Request", "Command", "Query" };

        foreach (var suffix in knownSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.Ordinal) && typeName.Length > suffix.Length)
            {
                suffixes.Add(suffix);
            }
        }

        // If no known suffix found, try to extract by comparing with a generic pattern
        if (suffixes.Count == 0)
        {
            // Use the full name as a potential suffix-less match
            suffixes.Add("");
        }

        return suffixes;
    }

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
