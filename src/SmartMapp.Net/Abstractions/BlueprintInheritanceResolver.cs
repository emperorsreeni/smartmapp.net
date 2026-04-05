namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Resolves blueprint inheritance by merging property links from base blueprints into derived blueprints.
/// Handles multi-level inheritance chains and detects circular inheritance.
/// </summary>
internal sealed class BlueprintInheritanceResolver
{
    private readonly InheritanceResolver _inheritanceResolver;
    private readonly Dictionary<TypePair, Blueprint> _blueprintLookup;

    /// <summary>
    /// Initializes a new <see cref="BlueprintInheritanceResolver"/>.
    /// </summary>
    internal BlueprintInheritanceResolver(
        InheritanceResolver inheritanceResolver,
        IEnumerable<Blueprint> blueprints)
    {
        _inheritanceResolver = inheritanceResolver;
        _blueprintLookup = new Dictionary<TypePair, Blueprint>();
        foreach (var bp in blueprints)
        {
            _blueprintLookup[bp.TypePair] = bp;
        }
    }

    /// <summary>
    /// Resolves all blueprint inheritance relationships, producing a new set of blueprints
    /// with inherited links merged in. Base blueprints are not modified.
    /// </summary>
    /// <returns>A new list of blueprints with inheritance resolved.</returns>
    internal IReadOnlyList<Blueprint> ResolveAll()
    {
        var result = new List<Blueprint>(_blueprintLookup.Count);

        foreach (var (pair, blueprint) in _blueprintLookup)
        {
            var basePair = _inheritanceResolver.GetInheritFromPair(pair);
            if (basePair.HasValue)
            {
                var merged = ResolveInheritance(pair, new HashSet<TypePair>());
                result.Add(merged);
            }
            else
            {
                result.Add(blueprint);
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves inheritance for a single blueprint, recursively resolving base blueprints.
    /// </summary>
    private Blueprint ResolveInheritance(TypePair derivedPair, HashSet<TypePair> visited)
    {
        if (!visited.Add(derivedPair))
        {
            throw new Diagnostics.BlueprintValidationException(
                $"Circular blueprint inheritance detected: {string.Join(" -> ", visited.Select(p => p.ToString()))} -> {derivedPair}");
        }

        if (!_blueprintLookup.TryGetValue(derivedPair, out var derivedBlueprint))
        {
            throw new Diagnostics.BlueprintValidationException(
                $"Cannot inherit from unregistered blueprint: {derivedPair}");
        }

        var basePair = _inheritanceResolver.GetInheritFromPair(derivedPair);
        if (!basePair.HasValue)
            return derivedBlueprint;

        // Recursively resolve the base blueprint (it may also inherit)
        var baseBlueprint = ResolveInheritance(basePair.Value, visited);

        return MergeLinks(baseBlueprint, derivedBlueprint);
    }

    /// <summary>
    /// Merges property links from a base blueprint into a derived blueprint.
    /// Derived links override base links for the same target member.
    /// </summary>
    internal static Blueprint MergeLinks(Blueprint baseBlueprint, Blueprint derivedBlueprint)
    {
        // Start with all base links
        var mergedLinks = new List<PropertyLink>(baseBlueprint.Links.Count + derivedBlueprint.Links.Count);
        var derivedTargetMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Collect all target member names from the derived blueprint
        foreach (var link in derivedBlueprint.Links)
        {
            derivedTargetMembers.Add(link.TargetMember.Name);
        }

        // Add base links that are NOT overridden by derived links
        foreach (var baseLink in baseBlueprint.Links)
        {
            if (!derivedTargetMembers.Contains(baseLink.TargetMember.Name))
            {
                // Deep copy via record `with` expression to avoid shared references
                mergedLinks.Add(baseLink with { });
            }
        }

        // Add all derived links (they take priority)
        mergedLinks.AddRange(derivedBlueprint.Links);

        // Sort by order
        mergedLinks.Sort((a, b) => a.Order.CompareTo(b.Order));

        return derivedBlueprint with
        {
            Links = mergedLinks,
        };
    }
}
