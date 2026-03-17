using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Computes a similarity score (0.0–1.0) between two types based on how many of the target's
/// writable members can be linked to origin readable members. Used by type conventions for
/// candidate pair discovery and by the convention pipeline for confidence scoring.
/// </summary>
public sealed class StructuralSimilarityScorer
{
    /// <summary>
    /// Computes a simple similarity score between origin and target types.
    /// </summary>
    /// <param name="origin">The origin type model.</param>
    /// <param name="target">The target type model.</param>
    /// <returns>A score from 0.0 (no match) to 1.0 (all target members matched).</returns>
    public double Score(TypeModel origin, TypeModel target) => ScoreDetailed(origin, target).Score;

    /// <summary>
    /// Computes a detailed similarity result including matched and unmatched member lists.
    /// </summary>
    /// <param name="origin">The origin type model.</param>
    /// <param name="target">The target type model.</param>
    /// <returns>A <see cref="StructuralSimilarityResult"/> with score and member details.</returns>
    public StructuralSimilarityResult ScoreDetailed(TypeModel origin, TypeModel target)
    {
        var targetMembers = target.WritableMembers;
        if (targetMembers.Count == 0)
        {
            return new StructuralSimilarityResult
            {
                Score = 0.0,
                MatchedMembers = Array.Empty<(MemberModel, MemberModel)>(),
                UnmatchedTargetMembers = Array.Empty<MemberModel>(),
                UnmatchedOriginMembers = origin.ReadableMembers,
            };
        }

        // Build a lookup of origin readable members by normalized name for O(1) matching
        var originByName = new Dictionary<string, MemberModel>(StringComparer.OrdinalIgnoreCase);
        var originReadable = origin.ReadableMembers;
        for (var i = 0; i < originReadable.Count; i++)
        {
            originByName.TryAdd(originReadable[i].Name, originReadable[i]);
        }

        // Also add normalized (PascalCase) names for cross-case matching
        var originByNormalized = new Dictionary<string, MemberModel>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < originReadable.Count; i++)
        {
            var segments = NameNormalizer.Segment(originReadable[i].Name);
            if (segments.Length > 0)
            {
                var normalized = NameNormalizer.ToPascalCase(segments);
                originByNormalized.TryAdd(normalized, originReadable[i]);
            }
        }

        var matched = new List<(MemberModel Origin, MemberModel Target)>();
        var unmatched = new List<MemberModel>();
        var matchedOriginNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        double totalWeight = 0.0;

        for (var i = 0; i < targetMembers.Count; i++)
        {
            var targetMember = targetMembers[i];

            // Try exact name match (case-insensitive) — weight 1.0
            if (originByName.TryGetValue(targetMember.Name, out var exactMatch))
            {
                var weight = 1.0;
                // Type compatibility bonus
                if (targetMember.MemberType.IsAssignableFrom(exactMatch.MemberType))
                    weight += 0.1;

                totalWeight += weight;
                matched.Add((exactMatch, targetMember));
                matchedOriginNames.Add(exactMatch.Name);
                continue;
            }

            // Try case-normalized match — weight 0.9
            var targetNormalized = NameNormalizer.ToPascalCase(NameNormalizer.Segment(targetMember.Name));
            if (originByNormalized.TryGetValue(targetNormalized, out var normalizedMatch)
                && !matchedOriginNames.Contains(normalizedMatch.Name))
            {
                var weight = 0.9;
                if (targetMember.MemberType.IsAssignableFrom(normalizedMatch.MemberType))
                    weight += 0.1;

                totalWeight += weight;
                matched.Add((normalizedMatch, targetMember));
                matchedOriginNames.Add(normalizedMatch.Name);
                continue;
            }

            unmatched.Add(targetMember);
        }

        // Collect unmatched origin members
        var unmatchedOrigin = new List<MemberModel>();
        for (var i = 0; i < originReadable.Count; i++)
        {
            if (!matchedOriginNames.Contains(originReadable[i].Name))
                unmatchedOrigin.Add(originReadable[i]);
        }

        // Score: normalize by target member count, cap at 1.0
        var score = Math.Min(totalWeight / targetMembers.Count, 1.0);

        return new StructuralSimilarityResult
        {
            Score = score,
            MatchedMembers = matched,
            UnmatchedTargetMembers = unmatched,
            UnmatchedOriginMembers = unmatchedOrigin,
        };
    }
}

/// <summary>
/// Detailed result of a structural similarity comparison between two types.
/// </summary>
public sealed record StructuralSimilarityResult
{
    /// <summary>
    /// Gets the similarity score from 0.0 (no match) to 1.0 (all target members matched).
    /// </summary>
    public required double Score { get; init; }

    /// <summary>
    /// Gets the list of successfully matched (origin, target) member pairs.
    /// </summary>
    public required IReadOnlyList<(MemberModel Origin, MemberModel Target)> MatchedMembers { get; init; }

    /// <summary>
    /// Gets the list of target writable members that could not be matched.
    /// </summary>
    public required IReadOnlyList<MemberModel> UnmatchedTargetMembers { get; init; }

    /// <summary>
    /// Gets the list of origin readable members that were not consumed.
    /// </summary>
    public required IReadOnlyList<MemberModel> UnmatchedOriginMembers { get; init; }
}
