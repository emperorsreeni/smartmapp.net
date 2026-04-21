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
    /// Gets the mapping strategy used for this pair.
    /// </summary>
    public MappingStrategy Strategy { get; init; } = MappingStrategy.ExpressionCompiled;

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

        return new MappingInspection
        {
            TypePair = blueprint.TypePair,
            Blueprint = blueprint,
            Strategy = blueprint.Strategy,
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
