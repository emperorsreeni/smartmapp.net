using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that matches target members to origin members by normalizing names across
/// different casing styles: snake_case, camelCase, PascalCase, SCREAMING_SNAKE, kebab-case.
/// Uses <see cref="NameNormalizer"/> for segmentation and comparison.
/// </summary>
public sealed class CaseConvention : IPropertyConvention
{
    /// <inheritdoc />
    public int Priority => 200;

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        var targetName = targetMember.Name;
        var targetSegments = NameNormalizer.Segment(targetName);

        if (targetSegments.Length == 0)
        {
            provider = null;
            return false;
        }

        var members = originModel.ReadableMembers;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            // Skip exact name matches (handled by ExactNameConvention at higher priority)
            if (string.Equals(member.Name, targetName, StringComparison.OrdinalIgnoreCase))
                continue;

            var originSegments = NameNormalizer.Segment(member.Name);
            if (SegmentsMatch(targetSegments, originSegments))
            {
                provider = new PropertyAccessProvider(member.MemberInfo);
                return true;
            }
        }

        provider = null;
        return false;
    }

    private static bool SegmentsMatch(string[] target, string[] origin)
    {
        if (target.Length != origin.Length) return false;

        for (var i = 0; i < target.Length; i++)
        {
            if (!string.Equals(target[i], origin[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
