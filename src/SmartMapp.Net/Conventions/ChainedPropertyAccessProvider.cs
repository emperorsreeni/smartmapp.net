using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Conventions;

/// <summary>
/// A value provider that navigates a chain of members (e.g., <c>Customer.Address.City</c>)
/// with null-safe traversal. Uses a compiled expression delegate for performance.
/// </summary>
public sealed class ChainedPropertyAccessProvider : IValueProvider
{
    private readonly Func<object, object?> _accessor;

    /// <summary>
    /// Gets the ordered chain of members from root to leaf.
    /// </summary>
    public IReadOnlyList<MemberInfo> Chain { get; }

    /// <summary>
    /// Gets the dotted member path (e.g., <c>"Customer.Address.City"</c>).
    /// </summary>
    public string MemberPath { get; }

    /// <summary>
    /// Initializes a new <see cref="ChainedPropertyAccessProvider"/> for the specified member chain.
    /// </summary>
    /// <param name="chain">The ordered member chain from root to leaf.</param>
    /// <param name="memberPath">The dotted path string.</param>
    public ChainedPropertyAccessProvider(IReadOnlyList<MemberInfo> chain, string memberPath)
    {
        Chain = chain;
        MemberPath = memberPath;
        _accessor = BuildChainedAccessor(chain);
    }

    /// <inheritdoc />
    public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
    {
        return _accessor(origin);
    }

    /// <inheritdoc />
    public override string ToString() => $"ChainedAccess({MemberPath})";

    private static Func<object, object?> BuildChainedAccessor(IReadOnlyList<MemberInfo> chain)
    {
        // Build individual compiled accessors for each link in the chain,
        // then compose them with null-safe navigation at runtime.
        var accessors = new Func<object, object?>[chain.Count];
        for (var i = 0; i < chain.Count; i++)
        {
            var member = chain[i];
            var declaringType = member.DeclaringType ?? throw new InvalidOperationException(
                $"Member '{member.Name}' has no declaring type.");

            var param = Expression.Parameter(typeof(object), "obj");
            var cast = Expression.Convert(param, declaringType);
            var access = Expression.MakeMemberAccess(cast, member);
            var boxed = Expression.Convert(access, typeof(object));
            accessors[i] = Expression.Lambda<Func<object, object?>>(boxed, param).Compile();
        }

        // Compose with null-safe navigation
        return origin =>
        {
            object? current = origin;
            for (var i = 0; i < accessors.Length; i++)
            {
                if (current is null) return null;
                current = accessors[i](current);
            }
            return current;
        };
    }
}
