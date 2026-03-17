using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Highest-priority convention: matches target member name to origin member name
/// using case-insensitive comparison. Prefers exact-case matches over case-insensitive,
/// and properties over fields when both match.
/// </summary>
public sealed class ExactNameConvention : IPropertyConvention
{
    /// <inheritdoc />
    public int Priority => 100;

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        var targetName = targetMember.Name;
        MemberModel? exactCase = null;
        MemberModel? caseInsensitive = null;

        var members = originModel.ReadableMembers;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            if (string.Equals(member.Name, targetName, StringComparison.Ordinal))
            {
                // Prefer properties over fields for exact-case match
                if (exactCase is null || (!member.IsField && exactCase.IsField))
                    exactCase = member;
            }
            else if (string.Equals(member.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                // Prefer properties over fields for case-insensitive match
                if (caseInsensitive is null || (!member.IsField && caseInsensitive.IsField))
                    caseInsensitive = member;
            }
        }

        var match = exactCase ?? caseInsensitive;
        if (match is not null)
        {
            provider = new PropertyAccessProvider(match.MemberInfo);
            return true;
        }

        provider = null;
        return false;
    }
}
