using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that detects flattened target member names (e.g., <c>CustomerAddressCity</c>)
/// and decomposes them into a chain of origin members (<c>Customer.Address.City</c>)
/// using recursive greedy prefix matching with backtracking.
/// </summary>
public sealed class FlatteningConvention : IPropertyConvention
{
    private readonly TypeModelCache _cache;
    private const int MaxDepth = 5;

    /// <inheritdoc />
    public int Priority => 300;

    /// <summary>
    /// Initializes a new <see cref="FlatteningConvention"/> with the specified type model cache.
    /// </summary>
    /// <param name="cache">The cache used to resolve nested type models.</param>
    public FlatteningConvention(TypeModelCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        var chain = new List<MemberInfo>();
        if (TryFindChain(targetMember.Name, originModel, chain, 0))
        {
            // Only match if chain has more than one member (single-member is ExactNameConvention's job)
            if (chain.Count > 1)
            {
                var path = string.Join(".", chain.Select(m => m.Name));
                provider = new ChainedPropertyAccessProvider(chain, path);
                return true;
            }
        }

        provider = null;
        return false;
    }

    private bool TryFindChain(string remaining, TypeModel model, List<MemberInfo> chain, int depth)
    {
        if (depth > MaxDepth || remaining.Length == 0)
            return remaining.Length == 0;

        var members = model.ReadableMembers;

        // Try members sorted by name length descending (greedy: longest prefix first)
        // But also support backtracking if a longer prefix fails
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var memberName = member.Name;

            if (remaining.Length < memberName.Length)
                continue;

            if (!remaining.StartsWith(memberName, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = remaining.Substring(memberName.Length);
            chain.Add(member.MemberInfo);

            if (rest.Length == 0)
            {
                // Full match
                return true;
            }

            // Recurse into the member's type
            var nestedModel = _cache.GetOrAdd(member.MemberType);
            if (TryFindChain(rest, nestedModel, chain, depth + 1))
                return true;

            // Backtrack
            chain.RemoveAt(chain.Count - 1);
        }

        return false;
    }
}
