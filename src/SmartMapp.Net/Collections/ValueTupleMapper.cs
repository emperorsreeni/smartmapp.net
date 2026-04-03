using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Collections;

/// <summary>
/// Builds expression tree fragments for mapping between <c>ValueTuple</c> types and typed objects.
/// Supports bidirectional mapping:
/// <list type="bullet">
///   <item><description>Tuple → Object: maps tuple fields (Item1..ItemN or named elements) to target members.</description></item>
///   <item><description>Object → Tuple: maps object members to tuple fields by name or position.</description></item>
/// </list>
/// </summary>
internal static class ValueTupleMapper
{
    private static readonly HashSet<Type> ValueTupleTypes = new()
    {
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    };

    /// <summary>
    /// Determines whether the given type is a <c>ValueTuple</c>.
    /// </summary>
    internal static bool IsValueTuple(Type type)
    {
        return type.IsValueType
            && type.IsGenericType
            && ValueTupleTypes.Contains(type.GetGenericTypeDefinition());
    }

    /// <summary>
    /// Determines whether the given type pair represents a tuple-to-object or object-to-tuple mapping.
    /// </summary>
    internal static bool IsValueTupleObjectMapping(Type originType, Type targetType)
    {
        return (IsValueTuple(originType) && !IsValueTuple(targetType))
            || (IsValueTuple(targetType) && !IsValueTuple(originType));
    }

    /// <summary>
    /// Builds an expression that maps a <c>ValueTuple</c> to a typed object.
    /// Matches tuple fields to target properties by name (via <see cref="TupleElementNamesAttribute"/>)
    /// or by position (Item1 → first writable member, etc.).
    /// </summary>
    internal static Expression BuildTupleToObject(
        Expression sourceExpr,
        Type targetType,
        ParameterExpression scopeParam)
    {
        var tupleType = sourceExpr.Type;
        var fields = GetTupleFields(tupleType);
        var resultVar = Expression.Variable(targetType, "target");
        var statements = new List<Expression>();

        var ctor = targetType.GetConstructor(Type.EmptyTypes);
        if (ctor is null)
            throw new MappingCompilationException(
                $"Cannot map ValueTuple to '{targetType.Name}': no parameterless constructor found.");

        statements.Add(Expression.Assign(resultVar, Expression.New(ctor)));

        // Get writable target members
        var targetMembers = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod is not null && p.SetMethod.IsPublic)
            .Cast<MemberInfo>()
            .Concat(targetType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly)
                .Cast<MemberInfo>())
            .ToList();

        // Match by position: Item1 → first writable, Item2 → second writable, etc.
        for (var i = 0; i < fields.Count && i < targetMembers.Count; i++)
        {
            var field = fields[i];
            var targetMember = targetMembers[i];
            var targetMemberType = PropertyAssignmentBuilder.GetMemberType(targetMember);

            var fieldAccess = Expression.Field(sourceExpr, field);
            Expression value = fieldAccess;

            if (field.FieldType != targetMemberType)
            {
                value = TransformerExpressionHelper.BuildTransformExpression(
                    value, field.FieldType, targetMemberType, scopeParam, transformer: null);
            }

            var memberAccess = targetMember switch
            {
                PropertyInfo pi => Expression.Property(resultVar, pi),
                FieldInfo fi => (Expression)Expression.Field(resultVar, fi),
                _ => throw new MappingCompilationException($"Unsupported member for '{targetMember.Name}'.")
            };

            statements.Add(Expression.Assign(memberAccess, value));
        }

        statements.Add(resultVar);

        return Expression.Block(
            new[] { resultVar },
            statements);
    }

    /// <summary>
    /// Builds an expression that maps a typed object to a <c>ValueTuple</c>.
    /// Matches object members to tuple fields by position.
    /// </summary>
    internal static Expression BuildObjectToTuple(
        Expression sourceExpr,
        Type tupleType,
        ParameterExpression scopeParam)
    {
        var fields = GetTupleFields(tupleType);
        var fieldTypes = fields.Select(f => f.FieldType).ToArray();

        // Get readable source members
        var sourceMembers = sourceExpr.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod is not null && p.GetMethod.IsPublic)
            .Cast<MemberInfo>()
            .Concat(sourceExpr.Type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsSpecialName)
                .Cast<MemberInfo>())
            .ToList();

        // Build constructor arguments by position
        var ctorArgs = new Expression[fields.Count];
        for (var i = 0; i < fields.Count; i++)
        {
            if (i < sourceMembers.Count)
            {
                var sourceMember = sourceMembers[i];
                var sourceMemberType = PropertyAssignmentBuilder.GetMemberType(sourceMember);
                Expression memberAccess = sourceMember switch
                {
                    PropertyInfo pi => Expression.Property(sourceExpr, pi),
                    FieldInfo fi => Expression.Field(sourceExpr, fi),
                    _ => throw new MappingCompilationException($"Unsupported member for '{sourceMember.Name}'.")
                };

                if (sourceMemberType != fieldTypes[i])
                {
                    memberAccess = TransformerExpressionHelper.BuildTransformExpression(
                        memberAccess, sourceMemberType, fieldTypes[i], scopeParam, transformer: null);
                }

                ctorArgs[i] = memberAccess;
            }
            else
            {
                ctorArgs[i] = Expression.Default(fieldTypes[i]);
            }
        }

        var tupleCtor = tupleType.GetConstructor(fieldTypes)!;
        return Expression.New(tupleCtor, ctorArgs);
    }

    /// <summary>
    /// Gets the public instance fields of a ValueTuple in order (Item1, Item2, ...).
    /// </summary>
    private static IReadOnlyList<FieldInfo> GetTupleFields(Type tupleType)
    {
        return tupleType.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.Name.StartsWith("Item"))
            .OrderBy(f => f.Name)
            .ToList();
    }
}
