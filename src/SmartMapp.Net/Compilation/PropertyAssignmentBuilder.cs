using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds expression tree fragments that assign values to target members (properties and fields)
/// based on <see cref="PropertyLink"/> instructions from a <see cref="Blueprint"/>.
/// </summary>
internal sealed class PropertyAssignmentBuilder
{
    /// <summary>
    /// Builds a list of assignment expressions for each non-skipped <see cref="PropertyLink"/>
    /// whose target member is not in <paramref name="consumedByConstructor"/>.
    /// </summary>
    /// <param name="links">The property links from the blueprint.</param>
    /// <param name="originParam">The typed origin parameter expression.</param>
    /// <param name="targetVar">The target variable expression.</param>
    /// <param name="scopeParam">The scope parameter expression.</param>
    /// <param name="consumedByConstructor">Member names already set via the constructor.</param>
    /// <param name="nestedMapper">
    /// A delegate that, given (originExpr, originType, targetType, scopeParam), returns an expression
    /// that maps the nested object. Used for complex-type properties.
    /// </param>
    /// <returns>A list of expressions representing property assignments.</returns>
    internal static IReadOnlyList<Expression> BuildAssignments(
        IReadOnlyList<PropertyLink> links,
        Expression originParam,
        Expression targetVar,
        ParameterExpression scopeParam,
        HashSet<string> consumedByConstructor,
        Func<Expression, Type, Type, ParameterExpression, Expression>? nestedMapper = null)
    {
        var assignments = new List<Expression>();

        foreach (var link in links.OrderBy(l => l.Order))
        {
            if (link.IsSkipped)
                continue;

            var targetMemberName = link.TargetMember.Name;

            // Skip if already set by constructor
            if (consumedByConstructor.Contains(targetMemberName))
                continue;

            var assignment = BuildSingleAssignment(link, originParam, targetVar, scopeParam, nestedMapper);
            if (assignment is not null)
                assignments.Add(assignment);
        }

        return assignments;
    }

    /// <summary>
    /// Builds a list of <see cref="MemberBinding"/> for use with MemberInit expressions.
    /// Used for init-only properties that must be set during object initialization.
    /// </summary>
    /// <param name="links">The property links from the blueprint.</param>
    /// <param name="originParam">The typed origin parameter expression.</param>
    /// <param name="scopeParam">The scope parameter expression.</param>
    /// <param name="consumedByConstructor">Member names already set via the constructor.</param>
    /// <param name="initOnlyOnly">If <c>true</c>, only returns bindings for init-only members.</param>
    /// <returns>A list of member bindings.</returns>
    internal static IReadOnlyList<MemberBinding> BuildMemberBindings(
        IReadOnlyList<PropertyLink> links,
        Expression originParam,
        ParameterExpression scopeParam,
        HashSet<string> consumedByConstructor,
        bool initOnlyOnly = false)
    {
        var bindings = new List<MemberBinding>();

        foreach (var link in links.OrderBy(l => l.Order))
        {
            if (link.IsSkipped)
                continue;

            if (consumedByConstructor.Contains(link.TargetMember.Name))
                continue;

            // If filtering for init-only, check the member
            if (initOnlyOnly && link.TargetMember is PropertyInfo pi)
            {
                var setter = pi.SetMethod;
                if (setter is null) continue;
                var modifiers = setter.ReturnParameter?.GetRequiredCustomModifiers() ?? Type.EmptyTypes;
                var isInit = modifiers.Any(m => m.Name == "IsExternalInit");
                if (!isInit) continue;
            }

            var valueExpr = BuildValueExpression(link, originParam, scopeParam, null);
            if (valueExpr is null) continue;

            var targetMemberType = GetMemberType(link.TargetMember);
            valueExpr = TargetConstructionResolver.EnsureType(valueExpr, targetMemberType);

            bindings.Add(Expression.Bind(link.TargetMember, valueExpr));
        }

        return bindings;
    }

    /// <summary>
    /// Builds a single assignment expression for a <see cref="PropertyLink"/>.
    /// </summary>
    private static Expression? BuildSingleAssignment(
        PropertyLink link,
        Expression originParam,
        Expression targetVar,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression>? nestedMapper)
    {
        var targetMemberType = GetMemberType(link.TargetMember);
        var valueExpr = BuildValueExpression(link, originParam, scopeParam, nestedMapper);

        if (valueExpr is null)
            return null;

        valueExpr = TargetConstructionResolver.EnsureType(valueExpr, targetMemberType);

        // Build target member access
        var targetAccess = link.TargetMember switch
        {
            PropertyInfo pi => Expression.Property(targetVar, pi),
            FieldInfo fi => Expression.Field(targetVar, fi),
            _ => throw new MappingCompilationException(
                $"Unsupported target member type '{link.TargetMember.GetType().Name}' for '{link.TargetMember.Name}'.")
        };

        Expression assignment = Expression.Assign(targetAccess, valueExpr);

        // Wrap with fallback: if value is null and fallback is set, use fallback
        if (link.Fallback is not null)
        {
            assignment = WrapWithFallback(link, originParam, scopeParam, targetAccess, targetMemberType, assignment, nestedMapper);
        }

        // Wrap with condition
        if (link.Condition is not null)
        {
            var conditionConst = Expression.Constant(link.Condition, typeof(Func<object, bool>));
            var conditionResult = Expression.Invoke(conditionConst, Expression.Convert(originParam, typeof(object)));
            assignment = Expression.IfThen(conditionResult, assignment);
        }

        // Wrap with precondition
        if (link.PreCondition is not null)
        {
            var preConditionConst = Expression.Constant(link.PreCondition, typeof(Func<object, bool>));
            var preConditionResult = Expression.Invoke(preConditionConst, Expression.Convert(originParam, typeof(object)));
            assignment = Expression.IfThen(preConditionResult, assignment);
        }

        return assignment;
    }

    /// <summary>
    /// Builds the value expression for a property link. Handles direct member access,
    /// provider invocation, type transformation, and nested mapping.
    /// </summary>
    private static Expression? BuildValueExpression(
        PropertyLink link,
        Expression originParam,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression>? nestedMapper)
    {
        var targetMemberType = GetMemberType(link.TargetMember);

        // Try to find matching origin member by the convention match path
        var originMemberPath = link.LinkedBy.OriginMemberPath;
        Expression? valueExpr = null;

        if (!string.IsNullOrEmpty(originMemberPath))
        {
            valueExpr = BuildOriginAccessExpression(originParam, originMemberPath, targetMemberType);
        }

        // If we couldn't build an expression from the path, fall back to provider
        if (valueExpr is null)
        {
            valueExpr = BuildProviderCall(link, originParam, scopeParam);
        }

        if (valueExpr is null)
            return null;

        var originType = valueExpr.Type;

        // Check if this is a complex type that needs recursive mapping.
        // This includes same-type complex objects (deep copy) and cross-type mappings.
        if (nestedMapper is not null && ComplexTypeDetector.IsComplexType(targetMemberType)
            && ComplexTypeDetector.IsComplexType(originType))
        {
            return nestedMapper(valueExpr, originType, targetMemberType, scopeParam);
        }

        // Apply transformer if types don't match
        if (originType != targetMemberType)
        {
            valueExpr = TransformerExpressionHelper.BuildTransformExpression(
                valueExpr, originType, targetMemberType, scopeParam, link.Transformer);
        }

        return valueExpr;
    }

    /// <summary>
    /// Builds an expression accessing an origin member by its path (e.g., "Name" or "Customer.Name").
    /// For multi-segment paths, applies null-safe navigation.
    /// </summary>
    private static Expression? BuildOriginAccessExpression(
        Expression originParam, string path, Type targetType)
    {
        var segments = path.Split('.');

        if (segments.Length == 1)
        {
            // Direct property/field access
            var memberName = segments[0];
            return TryBuildDirectAccess(originParam, memberName);
        }

        // Multi-segment path: build access chain with null safety
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
    /// Tries to build a direct member access expression.
    /// </summary>
    private static Expression? TryBuildDirectAccess(Expression instance, string memberName)
    {
        var member = FindMember(instance.Type, memberName);
        if (member is null) return null;

        return member switch
        {
            PropertyInfo pi => Expression.Property(instance, pi),
            FieldInfo fi => Expression.Field(instance, fi),
            _ => null
        };
    }

    /// <summary>
    /// Finds a public instance member by name (case-insensitive).
    /// </summary>
    private static MemberInfo? FindMember(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

        var prop = type.GetProperty(name, flags);
        if (prop is not null) return prop;

        var field = type.GetField(name, flags);
        return field;
    }

    /// <summary>
    /// Builds an expression that invokes the <see cref="IValueProvider"/> on a property link.
    /// </summary>
    private static Expression? BuildProviderCall(
        PropertyLink link, Expression originParam, ParameterExpression scopeParam)
    {
        if (link.Provider is null)
            return null;

        var targetMemberType = GetMemberType(link.TargetMember);

        // Call IValueProvider.Provide(object origin, object target, string targetMemberName, MappingScope scope)
        var providerConst = Expression.Constant(link.Provider, typeof(IValueProvider));
        var provideMethod = typeof(IValueProvider).GetMethod(nameof(IValueProvider.Provide))!;

        var call = Expression.Call(
            providerConst,
            provideMethod,
            Expression.Convert(originParam, typeof(object)),
            Expression.Constant(null, typeof(object)), // target not available during compilation
            Expression.Constant(link.TargetMember.Name),
            scopeParam);

        // Unbox/cast from object? to the target member type
        if (targetMemberType.IsValueType)
        {
            return Expression.Unbox(call, targetMemberType);
        }
        return Expression.Convert(call, targetMemberType);
    }

    /// <summary>
    /// Wraps an assignment with fallback logic: if the origin value is null, use the fallback value.
    /// </summary>
    private static Expression WrapWithFallback(
        PropertyLink link,
        Expression originParam,
        ParameterExpression scopeParam,
        Expression targetAccess,
        Type targetMemberType,
        Expression originalAssignment,
        Func<Expression, Type, Type, ParameterExpression, Expression>? nestedMapper)
    {
        var valueExpr = BuildValueExpression(link with { Fallback = null }, originParam, scopeParam, nestedMapper);
        if (valueExpr is null)
            return originalAssignment;

        var fallbackConst = Expression.Constant(link.Fallback, targetMemberType);

        // Check if the value is null
        Expression nullCheck;
        if (valueExpr.Type.IsValueType && Nullable.GetUnderlyingType(valueExpr.Type) is null)
        {
            // Value types are never null — just use the original assignment
            return originalAssignment;
        }
        else if (Nullable.GetUnderlyingType(valueExpr.Type) is not null)
        {
            nullCheck = Expression.Not(Expression.Property(valueExpr, "HasValue"));
        }
        else
        {
            nullCheck = Expression.Equal(
                Expression.Convert(valueExpr, typeof(object)),
                Expression.Constant(null, typeof(object)));
        }

        var withFallback = Expression.IfThenElse(
            nullCheck,
            Expression.Assign(targetAccess, fallbackConst),
            originalAssignment);

        return withFallback;
    }

    /// <summary>
    /// Gets the type of a member (property type or field type).
    /// </summary>
    internal static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo pi => pi.PropertyType,
            FieldInfo fi => fi.FieldType,
            _ => throw new MappingCompilationException(
                $"Unsupported member type '{member.GetType().Name}' for '{member.Name}'.")
        };
    }
}
