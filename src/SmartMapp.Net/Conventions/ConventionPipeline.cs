using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Orchestrates the execution of property conventions to build a <see cref="PropertyLink"/> list
/// for a given origin/target type pair. Conventions are tried in <see cref="IPropertyConvention.Priority"/>
/// order (ascending); the first convention to match a target member wins.
/// </summary>
public sealed class ConventionPipeline
{
    private readonly List<IPropertyConvention> _conventions;
    private readonly TypeModelCache _cache;
    private readonly StructuralSimilarityScorer _scorer;

    /// <summary>
    /// Initializes a new <see cref="ConventionPipeline"/> with the specified conventions and services.
    /// </summary>
    /// <param name="conventions">The conventions to apply, in any order (sorted internally by priority).
    /// When two conventions share the same <see cref="IPropertyConvention.Priority"/>, their relative
    /// execution order is determined by insertion order (i.e., the order they appear in this enumerable).</param>
    /// <param name="cache">The type model cache for type resolution.</param>
    /// <param name="scorer">The structural similarity scorer for confidence scoring.</param>
    public ConventionPipeline(
        IEnumerable<IPropertyConvention> conventions,
        TypeModelCache cache,
        StructuralSimilarityScorer scorer)
    {
        _conventions = conventions.OrderBy(c => c.Priority).ToList();
        _cache = cache;
        _scorer = scorer;
    }

    /// <summary>
    /// Gets the ordered list of conventions in this pipeline.
    /// </summary>
    public IReadOnlyList<IPropertyConvention> Conventions => _conventions;

    /// <summary>
    /// Gets or sets a value indicating whether strict mode is enabled.
    /// When <c>true</c>, <see cref="BuildLinks(TypeModel, TypeModel)"/> throws <see cref="InvalidOperationException"/>
    /// if any required target member cannot be linked.
    /// </summary>
    public bool StrictMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether unlinked (skipped) members should be
    /// omitted from the returned link list. Default is <c>false</c> (include all members).
    /// </summary>
    public bool IgnoreUnlinked { get; set; }

    /// <summary>
    /// Builds a list of <see cref="PropertyLink"/> instances by matching each writable target member
    /// to an origin value using the convention pipeline.
    /// </summary>
    /// <param name="origin">The origin type model.</param>
    /// <param name="target">The target type model.</param>
    /// <returns>A list of property links, one per writable target member.</returns>
    /// <exception cref="InvalidOperationException">Thrown in strict mode when required members are unlinked.</exception>
    public List<PropertyLink> BuildLinks(TypeModel origin, TypeModel target)
    {
        var targetMembers = target.WritableMembers;
        var links = new List<PropertyLink>(targetMembers.Count);

        for (var i = 0; i < targetMembers.Count; i++)
        {
            var targetMember = targetMembers[i];
            var link = TryLinkMember(targetMember, origin);
            links.Add(link);
        }

        // Strict mode: fail if any required member is unlinked
        if (StrictMode)
        {
            var unlinkedRequired = new List<string>();
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].IsSkipped)
                {
                    var memberName = links[i].TargetMember.Name;
                    // Check if the corresponding MemberModel is required
                    for (var j = 0; j < targetMembers.Count; j++)
                    {
                        if (targetMembers[j].Name == memberName && targetMembers[j].IsRequired)
                        {
                            unlinkedRequired.Add(memberName);
                            break;
                        }
                    }
                }
            }

            if (unlinkedRequired.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Strict mode: the following required target members could not be linked: " +
                    string.Join(", ", unlinkedRequired));
            }
        }

        // Post-processing note: Unflattening coordination (grouping multiple flat origin members
        // targeting the same intermediate object, e.g. CustomerFirstName + CustomerLastName → Customer)
        // is handled within UnflatteningConvention.TryLink itself — it collects ALL matching origin
        // members for a given target prefix in a single call, so no separate post-processing step
        // is required at the pipeline level.

        // Optionally filter out skipped links
        if (IgnoreUnlinked)
        {
            links.RemoveAll(l => l.IsSkipped);
        }

        return links;
    }

    /// <summary>
    /// Builds a list of <see cref="PropertyLink"/> instances for the specified CLR types.
    /// </summary>
    /// <param name="originType">The origin CLR type.</param>
    /// <param name="targetType">The target CLR type.</param>
    /// <returns>A list of property links, one per writable target member.</returns>
    public List<PropertyLink> BuildLinks(Type originType, Type targetType)
    {
        var origin = _cache.GetOrAdd(originType);
        var target = _cache.GetOrAdd(targetType);
        return BuildLinks(origin, target);
    }

    private PropertyLink TryLinkMember(MemberModel targetMember, TypeModel origin)
    {
        for (var i = 0; i < _conventions.Count; i++)
        {
            var convention = _conventions[i];
            if (convention.TryLink(targetMember.MemberInfo, origin, out var provider) && provider is not null)
            {
                // Determine origin member path from the provider
                var originPath = provider switch
                {
                    PropertyAccessProvider pap => pap.MemberPath,
                    ChainedPropertyAccessProvider cpap => cpap.MemberPath,
                    MethodAccessProvider map => map.Method.Name + "()",
                    UnflatteningValueProvider _ => "(unflattened)",
                    _ => ""
                };

                return new PropertyLink
                {
                    TargetMember = targetMember.MemberInfo,
                    Provider = provider,
                    LinkedBy = new ConventionMatch
                    {
                        ConventionName = convention.GetType().Name,
                        OriginMemberPath = originPath,
                        Confidence = GetConfidence(convention),
                    },
                    IsSkipped = false,
                };
            }
        }

        // No convention matched — create a skipped link
        return CreateSkippedLink(targetMember);
    }

    private static PropertyLink CreateSkippedLink(MemberModel targetMember)
    {
        return new PropertyLink
        {
            TargetMember = targetMember.MemberInfo,
            Provider = NullValueProvider.Instance,
            LinkedBy = new ConventionMatch
            {
                ConventionName = "None",
                OriginMemberPath = "",
                Confidence = 0.0,
            },
            IsSkipped = true,
        };
    }

    private static double GetConfidence(IPropertyConvention convention) => convention switch
    {
        ExactNameConvention => 1.0,
        CaseConvention => 0.9,
        MethodToPropertyConvention => 0.95,
        PrefixDroppingConvention => 0.85,
        FlatteningConvention => 1.0,
        UnflatteningConvention => 1.0,
        AbbreviationConvention => 0.8,
        _ => 0.7,
    };

    /// <summary>
    /// Creates a default convention pipeline with all built-in conventions.
    /// </summary>
    /// <param name="cache">The type model cache. Defaults to <see cref="TypeModelCache.Default"/> if null.</param>
    /// <returns>A fully configured <see cref="ConventionPipeline"/>.</returns>
    public static ConventionPipeline CreateDefault(TypeModelCache? cache = null)
    {
        cache ??= TypeModelCache.Default;
        var conventions = new IPropertyConvention[]
        {
            new ExactNameConvention(),
            new CaseConvention(),
            new PrefixDroppingConvention(),
            new MethodToPropertyConvention(),
            new FlatteningConvention(cache),
            new UnflatteningConvention(cache),
            new AbbreviationConvention(),
        };
        return new ConventionPipeline(conventions, cache, new StructuralSimilarityScorer());
    }
}

/// <summary>
/// A no-op value provider used for skipped (unlinked) property links.
/// </summary>
internal sealed class NullValueProvider : IValueProvider
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly NullValueProvider Instance = new();

    private NullValueProvider() { }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => null;

    /// <inheritdoc />
    public override string ToString() => "NullProvider";
}
