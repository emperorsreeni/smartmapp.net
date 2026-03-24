using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Collections;

/// <summary>
/// Builds expression tree fragments for mapping between <c>Dictionary&lt;string, object&gt;</c>
/// and typed objects. Supports bidirectional mapping:
/// <list type="bullet">
///   <item><description>Dictionary → Object: reads dictionary entries by property name, assigns to target members.</description></item>
///   <item><description>Object → Dictionary: reads object members, adds entries keyed by member name.</description></item>
/// </list>
/// </summary>
internal static class DictionaryObjectMapper
{
    /// <summary>
    /// Determines whether the given type pair represents a dictionary-to-object or object-to-dictionary mapping.
    /// </summary>
    internal static bool IsDictionaryObjectMapping(Type originType, Type targetType)
    {
        return (IsStringObjectDictionary(originType) && !IsStringObjectDictionary(targetType) && !originType.IsArray)
            || (IsStringObjectDictionary(targetType) && !IsStringObjectDictionary(originType) && !targetType.IsArray);
    }

    /// <summary>
    /// Builds an expression that maps a <c>Dictionary&lt;string, object&gt;</c> to a typed object.
    /// For each writable target member, looks up the dictionary entry by member name (case-insensitive).
    /// </summary>
    /// <param name="sourceExpr">Expression producing the source dictionary.</param>
    /// <param name="targetType">The target object type to construct and populate.</param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter expression.</param>
    /// <returns>An expression that produces the mapped target object.</returns>
    internal static Expression BuildDictionaryToObject(
        Expression sourceExpr,
        Type targetType,
        ParameterExpression scopeParam)
    {
        var dictType = typeof(Dictionary<string, object>);
        var tryGetValueMethod = dictType.GetMethod("TryGetValue")!;
        var resultVar = Expression.Variable(targetType, "target");
        var tempValueVar = Expression.Variable(typeof(object), "dictValue");
        var statements = new List<Expression>();

        // Construct target
        var ctor = targetType.GetConstructor(Type.EmptyTypes);
        if (ctor is null)
            throw new MappingCompilationException(
                $"Cannot map Dictionary<string, object> to '{targetType.Name}': no parameterless constructor found.");

        statements.Add(Expression.Assign(resultVar, Expression.New(ctor)));

        // Cast source to Dictionary<string, object> if needed
        var dictExpr = sourceExpr.Type == dictType
            ? sourceExpr
            : Expression.Convert(sourceExpr, dictType);

        // For each writable member, try to read from dictionary
        var targetMembers = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod is not null && p.SetMethod.IsPublic)
            .Cast<MemberInfo>()
            .Concat(targetType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsInitOnly)
                .Cast<MemberInfo>());

        foreach (var member in targetMembers)
        {
            var memberType = PropertyAssignmentBuilder.GetMemberType(member);
            var memberName = member.Name;

            // dict.TryGetValue("MemberName", out dictValue)
            var tryGet = Expression.Call(dictExpr, tryGetValueMethod,
                Expression.Constant(memberName),
                tempValueVar);

            // Convert dictValue to the member type
            Expression convertedValue;
            if (memberType.IsValueType)
            {
                // Unbox: (MemberType)dictValue — with null check for nullable
                if (Nullable.GetUnderlyingType(memberType) is not null)
                {
                    convertedValue = Expression.Condition(
                        Expression.Equal(tempValueVar, Expression.Constant(null, typeof(object))),
                        Expression.Default(memberType),
                        Expression.Convert(tempValueVar, memberType));
                }
                else
                {
                    convertedValue = Expression.Unbox(tempValueVar, memberType);
                }
            }
            else
            {
                convertedValue = Expression.Convert(tempValueVar, memberType);
            }

            var memberAccess = member switch
            {
                PropertyInfo pi => Expression.Property(resultVar, pi),
                FieldInfo fi => (Expression)Expression.Field(resultVar, fi),
                _ => throw new MappingCompilationException($"Unsupported member type for '{memberName}'.")
            };

            var assignMember = Expression.Assign(memberAccess, convertedValue);

            // Wrap in TryGetValue check
            statements.Add(Expression.IfThen(tryGet, assignMember));
        }

        statements.Add(resultVar);

        return Expression.Block(
            new[] { resultVar, tempValueVar },
            statements);
    }

    /// <summary>
    /// Builds an expression that maps a typed object to a <c>Dictionary&lt;string, object&gt;</c>.
    /// For each readable member, adds an entry keyed by member name.
    /// </summary>
    /// <param name="sourceExpr">Expression producing the source object.</param>
    /// <param name="sourceType">The source object type.</param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter expression.</param>
    /// <returns>An expression that produces the target dictionary.</returns>
    internal static Expression BuildObjectToDictionary(
        Expression sourceExpr,
        Type sourceType,
        ParameterExpression scopeParam)
    {
        var dictType = typeof(Dictionary<string, object>);
        var addMethod = dictType.GetMethod("Add", new[] { typeof(string), typeof(object) })!;
        var resultVar = Expression.Variable(dictType, "dict");

        var statements = new List<Expression>();

        // Count writable members for capacity
        var readableMembers = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod is not null && p.GetMethod.IsPublic)
            .Cast<MemberInfo>()
            .Concat(sourceType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsSpecialName)
                .Cast<MemberInfo>())
            .ToList();

        var ctor = dictType.GetConstructor(new[] { typeof(int), typeof(StringComparer) })!;
        statements.Add(Expression.Assign(resultVar,
            Expression.New(ctor,
                Expression.Constant(readableMembers.Count),
                Expression.Property(null, typeof(StringComparer), nameof(StringComparer.OrdinalIgnoreCase)))));

        foreach (var member in readableMembers)
        {
            var memberName = member.Name;
            var memberAccess = member switch
            {
                PropertyInfo pi => Expression.Property(sourceExpr, pi),
                FieldInfo fi => (Expression)Expression.Field(sourceExpr, fi),
                _ => throw new MappingCompilationException($"Unsupported member type for '{memberName}'.")
            };

            // Box value types
            Expression boxed = memberAccess.Type.IsValueType
                ? Expression.Convert(memberAccess, typeof(object))
                : memberAccess;

            statements.Add(Expression.Call(resultVar, addMethod,
                Expression.Constant(memberName),
                boxed));
        }

        statements.Add(resultVar);

        return Expression.Block(
            new[] { resultVar },
            statements);
    }

    private static bool IsStringObjectDictionary(Type type)
    {
        if (!type.IsGenericType) return false;
        var def = type.GetGenericTypeDefinition();
        if (def != typeof(Dictionary<,>) && def != typeof(IDictionary<,>)) return false;
        var args = type.GetGenericArguments();
        return args[0] == typeof(string) && args[1] == typeof(object);
    }
}
