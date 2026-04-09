using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Generates inverse blueprints for bidirectional mappings.
/// When <c>.Bidirectional()</c> is called, the forward <c>(A → B)</c> blueprint's property links
/// are inverted to create a reverse <c>(B → A)</c> blueprint. Only simple direct-member links
/// can be auto-inverted; computed/expression links are skipped with a warning.
/// </summary>
internal sealed class BidirectionalMapper
{
    private readonly List<string> _warnings = new();

    /// <summary>
    /// Gets the warnings generated during inverse blueprint creation.
    /// </summary>
    internal IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Generates inverse blueprints for all bidirectional binding configurations.
    /// Returns only the new inverse blueprints (not the originals).
    /// </summary>
    /// <param name="configs">All binding configurations.</param>
    /// <param name="existingBlueprints">Already-built blueprints (to avoid duplicates).</param>
    /// <returns>A list of newly generated inverse blueprints.</returns>
    internal IReadOnlyList<Blueprint> GenerateInverseBlueprints(
        IReadOnlyList<BindingConfiguration> configs,
        IReadOnlyList<Blueprint> existingBlueprints)
    {
        var existingPairs = new HashSet<TypePair>(existingBlueprints.Select(b => b.TypePair));
        var result = new List<Blueprint>();

        foreach (var config in configs)
        {
            if (!config.IsBidirectional)
                continue;

            var inversePair = new TypePair(config.TypePair.TargetType, config.TypePair.OriginType);

            // Don't overwrite an existing explicit reverse binding
            if (existingPairs.Contains(inversePair))
                continue;

            // Find the forward blueprint
            var forwardBlueprint = existingBlueprints.FirstOrDefault(b => b.TypePair == config.TypePair);
            if (forwardBlueprint is null)
                continue;

            var inverseBlueprint = BuildInverseBlueprint(forwardBlueprint);
            if (inverseBlueprint is not null)
            {
                result.Add(inverseBlueprint);
                existingPairs.Add(inversePair);
            }
        }

        return result;
    }

    /// <summary>
    /// Builds an inverse blueprint by inverting all invertible property links.
    /// </summary>
    private Blueprint? BuildInverseBlueprint(Blueprint forward)
    {
        var inverseLinks = new List<PropertyLink>();

        foreach (var link in forward.Links)
        {
            if (link.IsSkipped)
                continue; // Skipped properties don't appear in inverse

            var inverseLink = TryInvertLink(link, forward);
            if (inverseLink is not null)
            {
                inverseLinks.Add(inverseLink);
            }
        }

        if (inverseLinks.Count == 0)
        {
            _warnings.Add(
                $"Bidirectional mapping {forward.OriginType.Name} <-> {forward.TargetType.Name}: " +
                "no invertible links found. The inverse blueprint has zero links.");
        }

        return new Blueprint
        {
            OriginType = forward.TargetType,
            TargetType = forward.OriginType,
            Links = inverseLinks,
            MaxDepth = forward.MaxDepth,
            TrackReferences = forward.TrackReferences,
        };
    }

    /// <summary>
    /// Attempts to invert a single property link.
    /// Returns <c>null</c> if the link cannot be auto-inverted (e.g., computed expression).
    /// </summary>
    private PropertyLink? TryInvertLink(PropertyLink link, Blueprint forward)
    {
        // Only DirectMemberProvider links can be automatically inverted
        if (link.Provider is DirectMemberProvider directProvider)
        {
            // The forward link reads from origin member and writes to target member.
            // The inverse reads from target member (now origin) and writes to origin member (now target).
            var forwardOriginMember = directProvider.Member;
            var forwardTargetMember = link.TargetMember;

            // Check the inverse target (original origin) has a writable member matching the forward origin member
            // and the inverse origin (original target) has a readable member matching the forward target member
            return new PropertyLink
            {
                TargetMember = forwardOriginMember, // write to original origin member
                Provider = new DirectMemberProvider(forwardTargetMember), // read from original target member
                LinkedBy = ConventionMatch.Explicit($"Bidirectional({forwardTargetMember.Name} -> {forwardOriginMember.Name})"),
                Order = link.Order,
            };
        }

        // ExpressionValueProvider with simple member access can also be inverted
        if (link.Provider is ExpressionValueProvider exprProvider)
        {
            var body = exprProvider.Expression.Body;

            // Unwrap Convert
            if (body is System.Linq.Expressions.UnaryExpression { NodeType: System.Linq.Expressions.ExpressionType.Convert } unary)
                body = unary.Operand;

            if (body is System.Linq.Expressions.MemberExpression memberExpr
                && memberExpr.Expression is System.Linq.Expressions.ParameterExpression)
            {
                // Simple member access: s => s.Property — can be inverted
                return new PropertyLink
                {
                    TargetMember = memberExpr.Member,
                    Provider = new DirectMemberProvider(link.TargetMember),
                    LinkedBy = ConventionMatch.Explicit($"Bidirectional({link.TargetMember.Name} -> {memberExpr.Member.Name})"),
                    Order = link.Order,
                };
            }
        }

        // Cannot auto-invert computed expressions
        _warnings.Add(
            $"Bidirectional mapping {forward.OriginType.Name} <-> {forward.TargetType.Name}: " +
            $"property '{link.TargetMember.Name}' has a computed origin expression and cannot be auto-inverted. " +
            "Provide an explicit reverse configuration.");

        return null;
    }
}
