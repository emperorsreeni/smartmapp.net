using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that expands known abbreviations in member names using a configurable alias dictionary.
/// Bidirectional: matches both <c>Addr</c> → <c>Address</c> and <c>Address</c> → <c>Addr</c>.
/// Configured via <c>options.Conventions.EnableAbbreviationExpansion(aliases => ...)</c>.
/// </summary>
public sealed class AbbreviationConvention : IPropertyConvention
{
    private readonly Dictionary<string, string> _aliases;
    private readonly Dictionary<string, string> _reverseAliases;

    /// <inheritdoc />
    public int Priority => 400;

    /// <summary>
    /// Initializes a new <see cref="AbbreviationConvention"/> with optional custom aliases
    /// merged with built-in defaults.
    /// </summary>
    /// <param name="aliases">Custom aliases to merge with defaults. Keys are abbreviations, values are expansions.</param>
    public AbbreviationConvention(IDictionary<string, string>? aliases = null)
    {
        _aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Addr"] = "Address",
            ["Qty"] = "Quantity",
            ["Amt"] = "Amount",
            ["Desc"] = "Description",
            ["Num"] = "Number",
            ["Org"] = "Organization",
            ["Info"] = "Information",
            ["Msg"] = "Message",
            ["Tel"] = "Telephone",
            ["Fax"] = "Facsimile",
        };

        if (aliases is not null)
        {
            foreach (var kvp in aliases)
                _aliases[kvp.Key] = kvp.Value;
        }

        _reverseAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _aliases)
        {
            _reverseAliases.TryAdd(kvp.Value, kvp.Key);
        }
    }

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        var targetName = targetMember.Name;
        var expandedTarget = ExpandSegments(targetName);

        var members = originModel.ReadableMembers;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];

            // Skip exact matches (handled by ExactNameConvention)
            if (string.Equals(member.Name, targetName, StringComparison.OrdinalIgnoreCase))
                continue;

            var expandedOrigin = ExpandSegments(member.Name);

            if (string.Equals(expandedTarget, expandedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                provider = new PropertyAccessProvider(member.MemberInfo);
                return true;
            }

            // Also try: expand target, compare with raw origin
            if (string.Equals(expandedTarget, member.Name, StringComparison.OrdinalIgnoreCase))
            {
                provider = new PropertyAccessProvider(member.MemberInfo);
                return true;
            }

            // Also try: raw target, compare with expanded origin
            if (string.Equals(targetName, expandedOrigin, StringComparison.OrdinalIgnoreCase))
            {
                provider = new PropertyAccessProvider(member.MemberInfo);
                return true;
            }
        }

        provider = null;
        return false;
    }

    private string ExpandSegments(string name)
    {
        var segments = NameNormalizer.Segment(name);
        if (segments.Length == 0)
            return name;

        var expanded = false;
        var result = new string[segments.Length];

        for (var i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];

            // Try forward expansion (abbr → full)
            if (_aliases.TryGetValue(seg, out var full))
            {
                result[i] = full;
                expanded = true;
            }
            // Try reverse expansion (full → abbr)
            else if (_reverseAliases.TryGetValue(seg, out var abbr))
            {
                result[i] = abbr;
                expanded = true;
            }
            else
            {
                result[i] = seg;
            }
        }

        return expanded ? NameNormalizer.ToPascalCase(result) : name;
    }
}
