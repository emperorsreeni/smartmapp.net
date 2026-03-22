using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds null-safe property/field access expressions, equivalent to the C# <c>?.</c> operator.
/// For a chain like <c>origin.Customer.Address.City</c>, generates nested conditional expressions
/// that check each reference-type intermediate for <c>null</c>.
/// </summary>
internal static class NullSafeAccessBuilder
{
    /// <summary>
    /// Builds a null-safe access expression for a chain of members.
    /// For reference-type intermediates, emits null checks. Value-type intermediates are accessed directly.
    /// </summary>
    /// <param name="root">The root expression (e.g., the typed origin parameter).</param>
    /// <param name="memberChain">The ordered list of members to traverse.</param>
    /// <param name="targetType">The expected result type (for the <c>default</c> branch).</param>
    /// <returns>An expression that safely navigates the member chain, returning <c>default</c> on null.</returns>
    internal static Expression BuildNullSafeAccess(
        Expression root,
        IReadOnlyList<MemberInfo> memberChain,
        Type targetType)
    {
        if (memberChain.Count == 0)
            return Expression.Default(targetType);

        if (memberChain.Count == 1)
        {
            var access = AccessMember(root, memberChain[0]);
            return TargetConstructionResolver.EnsureType(access, targetType);
        }

        // Build the full access chain with null checks on reference-type intermediates.
        // We process from innermost to outermost to build nested ternary expressions.
        //
        // For origin.Customer.Address.City:
        //   customer = origin.Customer
        //   customer != null
        //     ? (address = customer.Address) != null
        //       ? address.City
        //       : default
        //     : default

        // First, build all the access expressions
        var accesses = new Expression[memberChain.Count];
        var current = root;
        for (var i = 0; i < memberChain.Count; i++)
        {
            current = AccessMember(current, memberChain[i]);
            accesses[i] = current;
        }

        // The final access is the value we want
        var result = TargetConstructionResolver.EnsureType(accesses[memberChain.Count - 1], targetType);
        var defaultValue = Expression.Default(targetType);

        // Wrap from inside out: check each intermediate (except the last) for null if it's a reference type
        for (var i = memberChain.Count - 2; i >= 0; i--)
        {
            var intermediateType = GetMemberType(memberChain[i]);
            var underlyingNullable = Nullable.GetUnderlyingType(intermediateType);

            if (underlyingNullable is not null)
            {
                // Nullable<T>: check HasValue
                // We need to rebuild the access chain up to this point
                var intermediateAccess = BuildAccessChain(root, memberChain, i);
                var hasValue = Expression.Property(intermediateAccess, "HasValue");
                result = Expression.Condition(hasValue, result, defaultValue);
            }
            else if (!intermediateType.IsValueType)
            {
                // Reference type: check != null
                var intermediateAccess = BuildAccessChain(root, memberChain, i);
                var nullCheck = Expression.NotEqual(intermediateAccess, Expression.Constant(null, intermediateType));
                result = Expression.Condition(nullCheck, result, defaultValue);
            }
            // Value types (non-nullable): no check needed
        }

        return result;
    }

    /// <summary>
    /// Builds a null-safe access expression for a single member on a root expression.
    /// If the root is a reference type, wraps in a null check.
    /// </summary>
    /// <param name="root">The root expression.</param>
    /// <param name="member">The member to access.</param>
    /// <param name="targetType">The target type for the default branch.</param>
    /// <returns>A null-safe member access expression.</returns>
    internal static Expression BuildNullSafeSingleAccess(Expression root, MemberInfo member, Type targetType)
    {
        var access = AccessMember(root, member);
        var result = TargetConstructionResolver.EnsureType(access, targetType);

        // If root could be null (reference type), wrap in null check
        if (!root.Type.IsValueType)
        {
            var defaultValue = Expression.Default(targetType);
            var nullCheck = Expression.NotEqual(root, Expression.Constant(null, root.Type));
            return Expression.Condition(nullCheck, result, defaultValue);
        }

        return result;
    }

    /// <summary>
    /// Builds a member access chain from root through memberChain[0..index].
    /// </summary>
    private static Expression BuildAccessChain(Expression root, IReadOnlyList<MemberInfo> memberChain, int endIndex)
    {
        var current = root;
        for (var i = 0; i <= endIndex; i++)
        {
            current = AccessMember(current, memberChain[i]);
        }
        return current;
    }

    /// <summary>
    /// Accesses a member (property or field) on the given expression.
    /// </summary>
    private static Expression AccessMember(Expression instance, MemberInfo member)
    {
        return member switch
        {
            PropertyInfo pi => Expression.Property(instance, pi),
            FieldInfo fi => Expression.Field(instance, fi),
            _ => throw new MappingCompilationException(
                $"Unsupported member type '{member.GetType().Name}' for member '{member.Name}'.")
        };
    }

    /// <summary>
    /// Gets the return type of a member (property type or field type).
    /// </summary>
    private static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo pi => pi.PropertyType,
            FieldInfo fi => fi.FieldType,
            _ => throw new MappingCompilationException(
                $"Unsupported member type '{member.GetType().Name}' for member '{member.Name}'.")
        };
    }
}
