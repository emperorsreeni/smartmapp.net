using System.Reflection;
using System.Text;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Conventions;

namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Structured diagnostic result from <c>ISculptor.Inspect&lt;S,D&gt;()</c>. Wraps a
/// <see cref="SmartMapp.Net.Blueprint"/> with a human-readable per-link trace formatted per spec §12.2.
/// </summary>
public sealed record MappingInspection
{
    /// <summary>
    /// Gets the type pair that was inspected.
    /// </summary>
    public TypePair TypePair { get; init; }

    /// <summary>
    /// Gets the resolved blueprint for the type pair.
    /// </summary>
    public Blueprint? Blueprint { get; init; }

    /// <summary>
    /// Gets the mapping strategy declared on the <see cref="SmartMapp.Net.Blueprint"/>.
    /// </summary>
    public MappingStrategy Strategy { get; init; } = MappingStrategy.ExpressionCompiled;

    /// <summary>
    /// Sprint 9 · S9-T10. The concrete <see cref="MappingStrategy"/> the strategy chain
    /// resolved to for this pair after applying <see cref="Configuration.StrategyOptions"/>.
    /// May differ from <see cref="Strategy"/> when adaptive promotion has fired
    /// (<c>ExpressionCompiled → ILEmit</c>). <c>null</c> when the pair has not yet been
    /// compiled (no <c>Map</c> call observed).
    /// </summary>
    public MappingStrategy? ActiveStrategy { get; init; }

    /// <summary>
    /// Sprint 9 · S9-T10. Current adaptive-promotion lifecycle state for this pair under
    /// <see cref="Configuration.StrategyMode.Adaptive"/>. <c>null</c> when the sculptor is not
    /// in <c>Adaptive</c> mode or the pair has not yet been observed.
    /// </summary>
    public Engine.Promotion.PromotionState? PromotionState { get; init; }

    /// <summary>
    /// Sprint 9 · S9-T10. UTC timestamp of the most recent successful promotion for this pair,
    /// or <c>null</c> if the pair has never been promoted.
    /// </summary>
    public DateTimeOffset? LastPromotedAt { get; init; }

    /// <summary>
    /// Sprint 9 · S9-T10. Wall-time of the most recent IL-Emit compilation during promotion,
    /// in milliseconds. <c>null</c> when no promotion has occurred yet.
    /// </summary>
    public double? PromotionCompileDurationMs { get; init; }

    /// <summary>
    /// Sprint 9 · S9-T10. Last error captured by the promotion worker for this pair, or
    /// <c>null</c> on success / never-attempted.
    /// </summary>
    public Exception? PromotionError { get; init; }

    /// <summary>
    /// Sprint 9 · S9-T10. Current invocation count for this pair. <c>0</c> when adaptive
    /// promotion is not active.
    /// </summary>
    public long InvocationCount { get; init; }

    /// <summary>
    /// Gets the total number of property links (including skipped).
    /// </summary>
    public int LinkCount { get; init; }

    /// <summary>
    /// Gets the per-link trace.
    /// </summary>
    public IReadOnlyList<MappingInspectionLine> Links { get; init; } = Array.Empty<MappingInspectionLine>();

    /// <summary>
    /// Gets the names of target members explicitly marked as skipped.
    /// </summary>
    public IReadOnlyList<string> SkippedMembers { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the legacy string trace — one formatted line per <see cref="MappingInspectionLine"/>.
    /// </summary>
    public IReadOnlyList<string> LinkTrace { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Builds a <see cref="MappingInspection"/> for the supplied blueprint.
    /// </summary>
    /// <param name="blueprint">The blueprint to inspect.</param>
    /// <returns>A populated <see cref="MappingInspection"/>.</returns>
    public static MappingInspection Build(Blueprint blueprint)
        => Build(blueprint, config: null);

    /// <summary>
    /// Sprint 9 · S9-T10 overload. Builds a <see cref="MappingInspection"/> that also reflects
    /// the runtime state captured on <paramref name="config"/>: the concrete
    /// <see cref="ActiveStrategy"/> chosen by the strategy chain, current
    /// <see cref="PromotionState"/>, invocation count, and promotion error / timing if any.
    /// </summary>
    internal static MappingInspection Build(Blueprint blueprint, Runtime.ForgedSculptorConfiguration? config)
    {
        if (blueprint is null) throw new ArgumentNullException(nameof(blueprint));

        var lines = new List<MappingInspectionLine>(blueprint.Links.Count);
        var skipped = new List<string>();

        foreach (var link in blueprint.Links)
        {
            var source = link.LinkedBy.ConventionName;
            var originPath = RenderOriginPath(link);
            var nested = TryResolveNestedMapping(link);

            lines.Add(new MappingInspectionLine
            {
                TargetMember = link.TargetMember,
                OriginPath = originPath,
                Source = source,
                IsSkipped = link.IsSkipped,
                Transformer = link.Transformer,
                NestedOriginType = nested?.Origin,
                NestedTargetType = nested?.Target,
            });

            if (link.IsSkipped)
                skipped.Add(link.TargetMember.Name);
        }

        var trace = new List<string>(lines.Count);
        foreach (var l in lines) trace.Add(l.ToString());

        // Sprint 9 · S9-T10 — overlay the runtime promotion view when a forged config is
        // available. Reads are best-effort and non-allocating; missing data degrades to null.
        MappingStrategy? activeStrategy = null;
        Engine.Promotion.PromotionState? promotionState = null;
        DateTimeOffset? promotedAt = null;
        double? compileDurationMs = null;
        Exception? promotionError = null;
        long invocationCount = 0;

        if (config is not null)
        {
            var pair = blueprint.TypePair;
            if (config.ActiveStrategies.TryGetValue(pair, out var active))
                activeStrategy = active;

            if (config.AdaptivePromotion is { } mgr)
            {
                invocationCount = mgr.Counters.Read(pair);
                if (mgr.TryGetRecord(pair) is { } record)
                {
                    promotionState = record.State;
                    promotedAt = record.PromotedAt;
                    compileDurationMs = record.CompileDurationMs;
                    promotionError = record.LastError;
                }
            }
        }

        return new MappingInspection
        {
            TypePair = blueprint.TypePair,
            Blueprint = blueprint,
            Strategy = blueprint.Strategy,
            ActiveStrategy = activeStrategy,
            PromotionState = promotionState,
            LastPromotedAt = promotedAt,
            PromotionCompileDurationMs = compileDurationMs,
            PromotionError = promotionError,
            InvocationCount = invocationCount,
            LinkCount = lines.Count,
            Links = lines,
            SkippedMembers = skipped,
            LinkTrace = trace,
        };
    }

    private static string RenderOriginPath(PropertyLink link)
    {
        if (!string.IsNullOrEmpty(link.LinkedBy.OriginMemberPath))
            return link.LinkedBy.OriginMemberPath;

        return link.Provider switch
        {
            PropertyAccessProvider pap => pap.MemberPath,
            ChainedPropertyAccessProvider cpap => cpap.MemberPath,
            _ => link.Provider.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    /// When the target member is a complex non-collection reference type, returns the resolved
    /// origin complex type so the renderer can emit <c>(NestedMapping: T -&gt; U)</c>.
    /// </summary>
    private static (Type Origin, Type Target)? TryResolveNestedMapping(PropertyLink link)
    {
        var targetType = link.TargetMember switch
        {
            PropertyInfo pi => pi.PropertyType,
            FieldInfo fi => fi.FieldType,
            _ => null,
        };
        if (targetType is null) return null;
        if (!ComplexTypeDetector.IsComplexType(targetType)) return null;

        // Recover the origin member type from the link's provider
        Type? originType = link.Provider switch
        {
            PropertyAccessProvider pap => MemberType(pap.OriginMember),
            ChainedPropertyAccessProvider cpap when cpap.Chain.Count > 0
                => MemberType(cpap.Chain[cpap.Chain.Count - 1]),
            _ => null,
        };
        if (originType is null) return null;
        if (!ComplexTypeDetector.IsComplexType(originType)) return null;
        if (originType == targetType) return null;

        return (originType, targetType);

        static Type? MemberType(MemberInfo m) => m switch
        {
            PropertyInfo pi => pi.PropertyType,
            FieldInfo fi => fi.FieldType,
            _ => null,
        };
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        var origin = Blueprint?.OriginType.Name ?? TypePair.OriginType.Name;
        var target = Blueprint?.TargetType.Name ?? TypePair.TargetType.Name;

        sb.Append(origin).Append(" -> ").Append(target)
          .Append(" (Strategy: ").Append(Strategy)
          .Append(", ").Append(LinkCount).Append(" links)");

        foreach (var line in Links)
        {
            sb.AppendLine();
            sb.Append(line.ToString());
        }

        return sb.ToString();
    }
}
