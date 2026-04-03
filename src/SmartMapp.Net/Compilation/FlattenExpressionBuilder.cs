using System.Linq.Expressions;
using System.Reflection;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds expression tree fragments for flattening (deep read) and unflattening (deep write)
/// of property chains. Integrates with <see cref="PropertyAssignmentBuilder"/> and
/// <see cref="NullSafeAccessBuilder"/> to generate compiled delegates that handle
/// multi-level property traversal without reflection at runtime.
/// </summary>
internal static class FlattenExpressionBuilder
{
    /// <summary>
    /// Builds a flattening (deep read) expression for a dot-separated origin member path.
    /// Applies null-safe navigation at each intermediate level.
    /// <para>
    /// Example: for path <c>"Customer.Address.City"</c>, generates:
    /// <code>origin.Customer?.Address?.City ?? default</code>
    /// </para>
    /// </summary>
    /// <param name="originParam">The typed origin parameter expression.</param>
    /// <param name="memberPath">The dot-separated origin member path (e.g., <c>"Customer.Address.City"</c>).</param>
    /// <param name="targetType">The target property type (for the default branch on null).</param>
    /// <returns>An expression producing the flattened value, or <c>null</c> if the path cannot be resolved.</returns>
    internal static Expression? BuildFlattenRead(Expression originParam, string memberPath, Type targetType)
    {
        var segments = memberPath.Split('.');
        if (segments.Length <= 1)
            return null; // Single-segment paths are handled by direct access

        var memberChain = new List<MemberInfo>();
        var currentType = originParam.Type;

        foreach (var segment in segments)
        {
            var member = FindMember(currentType, segment);
            if (member is null) return null;
            memberChain.Add(member);
            currentType = GetMemberType(member);
        }

        return NullSafeAccessBuilder.BuildNullSafeAccess(originParam, memberChain, targetType);
    }

    /// <summary>
    /// Builds an unflattening (deep write) expression that constructs intermediate objects
    /// as needed and assigns the value to the leaf property.
    /// <para>
    /// Example: for target path <c>"Customer.Address.City"</c> with value <c>originValue</c>, generates:
    /// <code>
    /// target.Customer ??= new Customer();
    /// target.Customer.Address ??= new Address();
    /// target.Customer.Address.City = originValue;
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="targetVar">The target variable expression.</param>
    /// <param name="memberChain">The ordered list of target members forming the write path.</param>
    /// <param name="valueExpr">The value expression to assign to the leaf member.</param>
    /// <returns>A block expression performing the deep write with intermediate construction.</returns>
    internal static Expression BuildUnflattenWrite(
        Expression targetVar,
        IReadOnlyList<MemberInfo> memberChain,
        Expression valueExpr)
    {
        if (memberChain.Count == 0)
            throw new MappingCompilationException("Unflatten member chain must have at least one member.");

        if (memberChain.Count == 1)
        {
            // Simple single-member assignment
            var access = AccessMember(targetVar, memberChain[0]);
            return Expression.Assign(access, TargetConstructionResolver.EnsureType(valueExpr, access.Type));
        }

        var statements = new List<Expression>();
        var currentExpr = targetVar;

        // For each intermediate member (all except the last), ensure the object exists
        for (var i = 0; i < memberChain.Count - 1; i++)
        {
            var member = memberChain[i];
            var memberType = GetMemberType(member);
            var memberAccess = AccessMember(currentExpr, member);

            if (!memberType.IsValueType)
            {
                // Null-coalescing construction: member ??= new MemberType()
                var ctor = memberType.GetConstructor(Type.EmptyTypes);
                if (ctor is not null)
                {
                    var newExpr = Expression.New(ctor);
                    var nullCheck = Expression.Equal(memberAccess, Expression.Constant(null, memberType));
                    var assignNew = Expression.Assign(memberAccess, newExpr);
                    statements.Add(Expression.IfThen(nullCheck, assignNew));
                }
            }

            currentExpr = memberAccess;
        }

        // Assign the leaf value
        var leafMember = memberChain[^1];
        var leafAccess = AccessMember(currentExpr, leafMember);
        statements.Add(Expression.Assign(leafAccess, TargetConstructionResolver.EnsureType(valueExpr, leafAccess.Type)));

        return Expression.Block(statements);
    }

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

    private static MemberInfo? FindMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
        var prop = type.GetProperty(name, flags);
        if (prop is not null) return prop;
        return type.GetField(name, flags);
    }

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
