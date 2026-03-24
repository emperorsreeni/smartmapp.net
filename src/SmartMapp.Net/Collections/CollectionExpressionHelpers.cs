using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace SmartMapp.Net.Collections;

/// <summary>
/// Reusable expression tree fragments for collection mapping: for-loops,
/// element mapping invocation, null checks on source collections, and
/// enumeration patterns.
/// </summary>
internal static class CollectionExpressionHelpers
{
    /// <summary>
    /// Builds a for-loop expression that iterates over a countable collection using an index variable.
    /// <code>
    /// for (int i = 0; i &lt; count; i++) { body(i) }
    /// </code>
    /// </summary>
    /// <param name="countExpr">Expression producing the loop upper bound (exclusive).</param>
    /// <param name="bodyFactory">A function receiving the index variable and returning the loop body.</param>
    /// <returns>A block expression containing the loop.</returns>
    internal static Expression BuildForLoop(Expression countExpr, Func<ParameterExpression, Expression> bodyFactory)
    {
        var indexVar = Expression.Variable(typeof(int), "i");
        var breakLabel = Expression.Label("breakLoop");

        var loop = Expression.Block(
            new[] { indexVar },
            Expression.Assign(indexVar, Expression.Constant(0)),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.LessThan(indexVar, countExpr),
                    Expression.Block(
                        bodyFactory(indexVar),
                        Expression.PostIncrementAssign(indexVar)),
                    Expression.Break(breakLabel)),
                breakLabel));

        return loop;
    }

    /// <summary>
    /// Builds a foreach-style expression using <c>GetEnumerator()</c> / <c>MoveNext()</c> / <c>Current</c>.
    /// <code>
    /// var enumerator = source.GetEnumerator();
    /// while (enumerator.MoveNext()) { body(enumerator.Current) }
    /// </code>
    /// </summary>
    /// <param name="sourceExpr">The source collection expression.</param>
    /// <param name="elementType">The element type.</param>
    /// <param name="bodyFactory">A function receiving the current-element expression and returning the loop body.</param>
    /// <returns>A block expression containing the enumeration loop.</returns>
    internal static Expression BuildForEachLoop(Expression sourceExpr, Type elementType, Func<Expression, Expression> bodyFactory)
    {
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(elementType);
        var enumeratorType = typeof(IEnumerator<>).MakeGenericType(elementType);

        var getEnumeratorMethod = enumerableType.GetMethod("GetEnumerator")!;
        var moveNextMethod = typeof(IEnumerator).GetMethod("MoveNext")!;
        var currentProp = enumeratorType.GetProperty("Current")!;

        var enumeratorVar = Expression.Variable(enumeratorType, "enumerator");
        var breakLabel = Expression.Label("breakForeach");

        var castSource = Expression.Convert(sourceExpr, enumerableType);
        var getEnumerator = Expression.Assign(enumeratorVar, Expression.Call(castSource, getEnumeratorMethod));

        var currentExpr = Expression.Property(enumeratorVar, currentProp);
        var body = bodyFactory(currentExpr);

        var loop = Expression.Loop(
            Expression.IfThenElse(
                Expression.Call(enumeratorVar, moveNextMethod),
                body,
                Expression.Break(breakLabel)),
            breakLabel);

        return Expression.Block(
            new[] { enumeratorVar },
            getEnumerator,
            loop);
    }

    /// <summary>
    /// Wraps an expression with a null check on the source collection.
    /// Returns <c>null</c> (or default) when the source is null.
    /// </summary>
    /// <param name="sourceExpr">The source collection expression.</param>
    /// <param name="mappingBody">The body to execute when source is non-null.</param>
    /// <param name="targetType">The target collection type (for the null branch).</param>
    /// <returns>A conditional expression with null guard.</returns>
    internal static Expression WrapWithNullCheck(Expression sourceExpr, Expression mappingBody, Type targetType)
    {
        if (sourceExpr.Type.IsValueType)
            return mappingBody;

        var nullCheck = Expression.Equal(sourceExpr, Expression.Constant(null, sourceExpr.Type));
        var nullResult = Expression.Default(targetType);
        return Expression.Condition(nullCheck, nullResult, mappingBody);
    }

    /// <summary>
    /// Builds an expression that invokes the element mapping delegate for a single element.
    /// Calls <c>elementMapper(element, scope)</c> and casts the result to <paramref name="targetElementType"/>.
    /// </summary>
    /// <param name="elementExpr">The source element expression.</param>
    /// <param name="originElementType">The source element CLR type.</param>
    /// <param name="targetElementType">The target element CLR type.</param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter.</param>
    /// <param name="elementMapper">
    /// A delegate that, given (originExpr, originType, targetType, scopeParam), returns
    /// an expression that maps a single element.
    /// </param>
    /// <returns>An expression producing the mapped element of <paramref name="targetElementType"/>.</returns>
    internal static Expression BuildElementMappingCall(
        Expression elementExpr,
        Type originElementType,
        Type targetElementType,
        ParameterExpression scopeParam,
        Func<Expression, Type, Type, ParameterExpression, Expression> elementMapper)
    {
        return elementMapper(elementExpr, originElementType, targetElementType, scopeParam);
    }

    /// <summary>
    /// Gets the <c>Count</c> property expression from a collection, or <c>Length</c> for arrays.
    /// </summary>
    /// <param name="sourceExpr">The source collection expression.</param>
    /// <returns>An expression producing the count/length as <see cref="int"/>.</returns>
    internal static Expression GetCountExpression(Expression sourceExpr)
    {
        var type = sourceExpr.Type;

        if (type.IsArray)
            return Expression.ArrayLength(sourceExpr);

        // Try Count property (List<T>, HashSet<T>, Dictionary<K,V>, etc.)
        var countProp = type.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
        if (countProp is not null)
            return Expression.Property(sourceExpr, countProp);

        // Try ICollection<T>.Count
        var elementType = GetGenericElementType(type);
        if (elementType is not null)
        {
            var iCollType = typeof(ICollection<>).MakeGenericType(elementType);
            if (iCollType.IsAssignableFrom(type))
            {
                countProp = iCollType.GetProperty("Count")!;
                return Expression.Property(Expression.Convert(sourceExpr, iCollType), countProp);
            }

            var iReadOnlyCollType = typeof(IReadOnlyCollection<>).MakeGenericType(elementType);
            if (iReadOnlyCollType.IsAssignableFrom(type))
            {
                countProp = iReadOnlyCollType.GetProperty("Count")!;
                return Expression.Property(Expression.Convert(sourceExpr, iReadOnlyCollType), countProp);
            }
        }

        // Fallback: Enumerable.Count() — should not typically be hit
        throw new Compilation.MappingCompilationException(
            $"Cannot determine count for collection type '{type.Name}'. No Count or Length property found.");
    }

    /// <summary>
    /// Gets the generic element type T from IEnumerable&lt;T&gt; implemented by the given type.
    /// </summary>
    internal static Type? GetGenericElementType(Type type)
    {
        if (type.IsArray)
            return type.GetElementType();

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    /// Gets the key and value types from a dictionary type.
    /// </summary>
    internal static (Type keyType, Type valueType)? GetDictionaryTypes(Type type)
    {
        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
            {
                var args = type.GetGenericArguments();
                return (args[0], args[1]);
            }
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                var args = iface.GetGenericArguments();
                return (args[0], args[1]);
            }
        }

        return null;
    }
}
