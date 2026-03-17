using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// Convention that reverses flattening: given an origin member like <c>CustomerAddressCity</c>,
/// it decomposes the name into a target member chain (<c>Customer.Address.City</c>) and
/// produces a provider that reads the flat origin value and writes through the target chain.
/// </summary>
public sealed class UnflatteningConvention : IPropertyConvention
{
    private readonly TypeModelCache _cache;
    private const int MaxDepth = 5;

    /// <inheritdoc />
    public int Priority => 350;

    /// <summary>
    /// Initializes a new <see cref="UnflatteningConvention"/> with the specified type model cache.
    /// </summary>
    /// <param name="cache">The cache used to resolve nested type models.</param>
    public UnflatteningConvention(TypeModelCache cache)
    {
        _cache = cache;
    }

    /// <inheritdoc />
    public bool TryLink(MemberInfo targetMember, TypeModel originModel, out IValueProvider? provider)
    {
        // For unflattening, we look at origin readable members whose names
        // could be decomposed into a chain of target writable members.
        // However, since TryLink is called per *target* member, we actually check
        // if any origin member name starts with the target member name and forms
        // a valid unflattening pattern.
        //
        // Actually, unflattening works differently: the *target* has nested structure
        // and the *origin* has flat names. So for target member "Customer" (a complex type),
        // we look at origin members like "CustomerName", "CustomerAddressCity" etc.
        //
        // But this convention is called per target member. If the target member is
        // a complex type, we can't directly link it to a single origin member.
        //
        // The proper approach: for each target member, check if any origin member's name
        // starts with the target member's name followed by a property of the target member's type.
        // If so, this target member needs to be populated via unflattening.
        //
        // For Sprint 2, we implement the simple case: the target member itself is a
        // leaf property, and the origin has a compound name that ends with this member's
        // chain. This is the inverse of flattening.
        //
        // Example: target has "Customer" (type Customer with "Name"), origin has "CustomerName".
        // When pipeline processes target member "Customer", we detect that origin has
        // "CustomerName", "CustomerAddressCity" etc. and create an UnflatteningValueProvider.

        var targetName = targetMember.Name;
        var targetType = GetMemberType(targetMember);

        // Only unflatten into complex (non-primitive, non-string, non-collection) types
        if (IsSimpleType(targetType))
        {
            provider = null;
            return false;
        }

        var targetTypeModel = _cache.GetOrAdd(targetType);
        if (targetTypeModel.WritableMembers.Count == 0)
        {
            provider = null;
            return false;
        }

        // Check if any origin readable member starts with target member name
        // and the remainder matches a member of the target type (recursively)
        var matchedOriginMembers = new List<(MemberInfo OriginMember, List<MemberInfo> TargetChain)>();

        var originMembers = originModel.ReadableMembers;
        for (var i = 0; i < originMembers.Count; i++)
        {
            var originMember = originMembers[i];
            var originName = originMember.Name;

            if (originName.Length <= targetName.Length)
                continue;

            if (!originName.StartsWith(targetName, StringComparison.OrdinalIgnoreCase))
                continue;

            var remainder = originName.Substring(targetName.Length);

            // Try to resolve the remainder into a chain within the target member's type
            var chain = new List<MemberInfo>();
            if (TryFindWritableChain(remainder, targetTypeModel, chain, 0))
            {
                matchedOriginMembers.Add((originMember.MemberInfo, chain));
            }
        }

        if (matchedOriginMembers.Count > 0)
        {
            provider = new UnflatteningValueProvider(targetType, matchedOriginMembers);
            return true;
        }

        provider = null;
        return false;
    }

    private bool TryFindWritableChain(string remaining, TypeModel model, List<MemberInfo> chain, int depth)
    {
        if (depth > MaxDepth || remaining.Length == 0)
            return remaining.Length == 0;

        var members = model.WritableMembers;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (remaining.Length < member.Name.Length)
                continue;

            if (!remaining.StartsWith(member.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            var rest = remaining.Substring(member.Name.Length);
            chain.Add(member.MemberInfo);

            if (rest.Length == 0)
                return true;

            // Recurse into the member's type
            var memberType = member.MemberType;
            if (IsSimpleType(memberType))
            {
                chain.RemoveAt(chain.Count - 1);
                continue;
            }

            var nestedModel = _cache.GetOrAdd(memberType);
            if (TryFindWritableChain(rest, nestedModel, chain, depth + 1))
                return true;

            chain.RemoveAt(chain.Count - 1);
        }

        return false;
    }

    private static bool IsSimpleType(Type type) =>
        type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
        type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(Guid) ||
        type == typeof(TimeSpan) || type.IsEnum || Nullable.GetUnderlyingType(type) is not null;

    private static Type GetMemberType(MemberInfo member) => member switch
    {
        PropertyInfo p => p.PropertyType,
        FieldInfo f => f.FieldType,
        _ => typeof(object)
    };
}

/// <summary>
/// A value provider for unflattening: reads multiple flat origin members and assembles
/// them into a nested target object by creating intermediate objects as needed.
/// </summary>
public sealed class UnflatteningValueProvider : IValueProvider
{
    private readonly Type _targetType;
    private readonly IReadOnlyList<(MemberInfo OriginMember, List<MemberInfo> TargetChain)> _mappings;

    /// <summary>
    /// Initializes a new <see cref="UnflatteningValueProvider"/>.
    /// </summary>
    /// <param name="targetType">The type of the target object to construct.</param>
    /// <param name="mappings">The origin-to-target-chain mappings.</param>
    public UnflatteningValueProvider(
        Type targetType,
        IReadOnlyList<(MemberInfo OriginMember, List<MemberInfo> TargetChain)> mappings)
    {
        _targetType = targetType;
        _mappings = mappings;
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        // Create or reuse the target object
        object? result;
        try
        {
            result = Activator.CreateInstance(_targetType);
        }
        catch
        {
            return null;
        }

        if (result is null)
            return null;

        foreach (var (originMember, targetChain) in _mappings)
        {
            // Read the value from origin
            var value = ReadMember(origin, originMember);

            // Write the value through the target chain
            SetChainedValue(result, targetChain, value);
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => $"Unflatten({_targetType.Name}, {_mappings.Count} mappings)";

    private static object? ReadMember(object obj, MemberInfo member) => member switch
    {
        PropertyInfo p => p.GetValue(obj),
        FieldInfo f => f.GetValue(obj),
        _ => null
    };

    private static void SetMember(object obj, MemberInfo member, object? value)
    {
        switch (member)
        {
            case PropertyInfo p when p.CanWrite:
                p.SetValue(obj, value);
                break;
            case FieldInfo f when !f.IsInitOnly:
                f.SetValue(obj, value);
                break;
        }
    }

    private static void SetChainedValue(object root, List<MemberInfo> chain, object? value)
    {
        var current = root;
        for (var i = 0; i < chain.Count - 1; i++)
        {
            var member = chain[i];
            var next = ReadMember(current, member);

            if (next is null)
            {
                // Create intermediate object
                var memberType = member switch
                {
                    PropertyInfo p => p.PropertyType,
                    FieldInfo f => f.FieldType,
                    _ => typeof(object)
                };

                try
                {
                    next = Activator.CreateInstance(memberType);
                }
                catch
                {
                    return;
                }

                if (next is null) return;
                SetMember(current, member, next);
            }

            current = next;
        }

        // Set the leaf value
        if (chain.Count > 0)
        {
            SetMember(current, chain[^1], value);
        }
    }
}
