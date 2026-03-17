using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that strips configurable prefixes from origin member names and suffixes from
/// target member names before attempting a match. Handles common patterns like
/// <c>GetName</c> → <c>Name</c>, <c>m_id</c> → <c>Id</c>, <c>NameField</c> → <c>Name</c>.
/// </summary>
public sealed class PrefixDroppingConvention : IPropertyConvention
{
    private readonly IReadOnlyList<string> _originPrefixes;
    private readonly IReadOnlyList<string> _targetSuffixes;

    /// <inheritdoc />
    public int Priority => 250;

    /// <summary>
    /// Initializes a new <see cref="PrefixDroppingConvention"/> with optional custom prefix/suffix lists.
    /// </summary>
    /// <param name="originPrefixes">Origin member prefixes to strip. Defaults to common prefixes.</param>
    /// <param name="targetSuffixes">Target member suffixes to strip. Defaults to common suffixes.</param>
    public PrefixDroppingConvention(
        IReadOnlyList<string>? originPrefixes = null,
        IReadOnlyList<string>? targetSuffixes = null)
    {
        // Sort by length descending so longest prefix is matched first
        _originPrefixes = (originPrefixes ?? new[] { "Get", "get", "Str", "str", "m_", "_", "M_" })
            .OrderByDescending(p => p.Length).ToList();
        _targetSuffixes = (targetSuffixes ?? new[] { "Field", "Property", "Prop" })
            .OrderByDescending(s => s.Length).ToList();
    }

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        var targetName = StripSuffix(targetMember.Name);

        var members = originModel.ReadableMembers;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var strippedOrigin = StripPrefix(member.Name);

            // Only match if stripping actually changed something (avoid duplicating ExactNameConvention)
            if (string.Equals(member.Name, targetMember.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(strippedOrigin, targetName, StringComparison.OrdinalIgnoreCase))
            {
                provider = new PropertyAccessProvider(member.MemberInfo);
                return true;
            }
        }

        provider = null;
        return false;
    }

    private string StripPrefix(string name)
    {
        for (var i = 0; i < _originPrefixes.Count; i++)
        {
            var prefix = _originPrefixes[i];
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal))
                return name.Substring(prefix.Length);
        }
        return name;
    }

    private string StripSuffix(string name)
    {
        for (var i = 0; i < _targetSuffixes.Count; i++)
        {
            var suffix = _targetSuffixes[i];
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                return name.Substring(0, name.Length - suffix.Length);
        }
        return name;
    }
}
