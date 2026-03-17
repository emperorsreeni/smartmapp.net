using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Type convention that auto-pairs types by matching base names with configurable suffixes.
/// For example, <c>Order</c> pairs with <c>OrderDto</c>, <c>OrderViewModel</c>, etc.
/// Uses <see cref="StructuralSimilarityScorer"/> to verify candidate pairs exceed a minimum score.
/// </summary>
public sealed class NameSuffixTypeConvention : ITypeConvention
{
    private readonly List<(string OriginSuffix, string TargetSuffix)> _suffixPairs;
    private readonly StructuralSimilarityScorer _scorer;
    private readonly TypeModelCache _cache;
    private readonly double _minScore;

    /// <summary>
    /// Initializes a new <see cref="NameSuffixTypeConvention"/> with default suffix pairs and optional customization.
    /// </summary>
    /// <param name="scorer">The similarity scorer for verifying candidate pairs.</param>
    /// <param name="cache">The type model cache.</param>
    /// <param name="minScore">Minimum structural similarity score to accept a pair. Default: 0.7.</param>
    /// <param name="customSuffixPairs">Additional suffix pairs to merge with defaults.</param>
    public NameSuffixTypeConvention(
        StructuralSimilarityScorer scorer,
        TypeModelCache cache,
        double minScore = 0.7,
        IEnumerable<(string, string)>? customSuffixPairs = null)
    {
        _scorer = scorer;
        _cache = cache;
        _minScore = minScore;
        _suffixPairs = new List<(string, string)>
        {
            ("", "Dto"),
            ("", "ViewModel"),
            ("", "Vm"),
            ("", "Model"),
            ("", "Response"),
            ("", "Request"),
            ("", "Command"),
            ("Entity", "Dto"),
            ("Entity", "ViewModel"),
        };

        if (customSuffixPairs is not null)
            _suffixPairs.AddRange(customSuffixPairs);
    }

    /// <inheritdoc />
    public bool TryBind(Type originType, Type targetType, out Blueprint? blueprint)
    {
        blueprint = null;

        var originName = GetBaseTypeName(originType);
        var targetName = GetBaseTypeName(targetType);

        for (var i = 0; i < _suffixPairs.Count; i++)
        {
            var (originSuffix, targetSuffix) = _suffixPairs[i];

            // Strip origin suffix to get base name
            string baseName;
            if (originSuffix.Length == 0)
            {
                baseName = originName;
            }
            else if (originName.EndsWith(originSuffix, StringComparison.Ordinal))
            {
                baseName = originName.Substring(0, originName.Length - originSuffix.Length);
            }
            else
            {
                continue;
            }

            // Check if target name matches baseName + targetSuffix
            var expectedTarget = baseName + targetSuffix;
            if (!string.Equals(targetName, expectedTarget, StringComparison.Ordinal))
                continue;

            // Verify structural similarity
            var originModel = _cache.GetOrAdd(originType);
            var targetModel = _cache.GetOrAdd(targetType);
            var score = _scorer.Score(originModel, targetModel);

            if (score >= _minScore)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a custom suffix pair to this convention.
    /// </summary>
    /// <param name="originSuffix">The suffix to strip from origin type names.</param>
    /// <param name="targetSuffix">The expected suffix on target type names.</param>
    public void AddSuffixPair(string originSuffix, string targetSuffix)
    {
        _suffixPairs.Add((originSuffix, targetSuffix));
    }

    /// <summary>
    /// Gets the default suffix pairs.
    /// </summary>
    public IReadOnlyList<(string OriginSuffix, string TargetSuffix)> SuffixPairs => _suffixPairs;

    private static string GetBaseTypeName(Type type)
    {
        var name = type.Name;

        // Strip generic arity suffix (e.g., "Order`1" → "Order")
        var arityIndex = name.IndexOf('`');
        if (arityIndex >= 0)
            name = name.Substring(0, arityIndex);

        return name;
    }
}
