using System.Linq.Expressions;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds expression tree fragments that check <see cref="MappingScope.TryGetVisited"/>
/// before mapping a nested object and call <see cref="MappingScope.TrackVisited"/>
/// after construction. Prevents infinite recursion on circular object graphs.
/// </summary>
internal static class CircularReferenceGuard
{
    /// <summary>
    /// Wraps a nested mapping expression with circular reference tracking.
    /// If <paramref name="trackReferences"/> is <c>false</c> or the target is a value type,
    /// the original expression is returned unchanged.
    /// </summary>
    /// <param name="originExpr">Expression producing the nested origin object (reference type).</param>
    /// <param name="targetType">The target type of the nested mapping.</param>
    /// <param name="buildMappingBody">
    /// A factory that receives a <c>targetVariable</c> expression and returns the expression block
    /// that constructs + populates the target. The guard will insert <c>TrackVisited</c> after
    /// construction but the factory is responsible for the actual mapping body.
    /// </param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter.</param>
    /// <param name="trackReferences">Whether reference tracking is enabled for this blueprint.</param>
    /// <returns>An expression that implements the reference-tracking pattern.</returns>
    internal static Expression WrapWithReferenceTracking(
        Expression originExpr,
        Type targetType,
        Func<Expression> buildMappingBody,
        ParameterExpression scopeParam,
        bool trackReferences)
    {
        // No tracking needed for value types or when disabled
        if (!trackReferences || targetType.IsValueType)
            return buildMappingBody();

        // Pattern:
        //   object __cached;
        //   if (scope.TryGetVisited(origin, out __cached))
        //       return (TTarget)__cached;
        //   else
        //       <mapping body>  // which must call TrackVisited after construction

        var cachedVar = Expression.Variable(typeof(object), "__cached");

        var tryGetVisited = typeof(MappingScope).GetMethod(
            nameof(MappingScope.TryGetVisited),
            new[] { typeof(object), typeof(object).MakeByRefType() })!;

        var callTryGet = Expression.Call(
            scopeParam,
            tryGetVisited,
            Expression.Convert(originExpr, typeof(object)),
            cachedVar);

        var returnCached = Expression.Convert(cachedVar, targetType);
        var mappingBody = buildMappingBody();

        var ifThenElse = Expression.Condition(
            callTryGet,
            returnCached,
            Expression.Convert(mappingBody, targetType));

        return Expression.Block(
            new[] { cachedVar },
            ifThenElse);
    }

    /// <summary>
    /// Builds an expression that calls <see cref="MappingScope.TrackVisited"/> for the given
    /// origin/target pair. Should be inserted immediately after target construction.
    /// </summary>
    /// <param name="originExpr">The origin object expression.</param>
    /// <param name="targetExpr">The target object expression (just constructed).</param>
    /// <param name="scopeParam">The scope parameter.</param>
    /// <returns>An expression that calls <c>scope.TrackVisited(origin, target)</c>.</returns>
    internal static Expression BuildTrackExpression(
        Expression originExpr,
        Expression targetExpr,
        ParameterExpression scopeParam)
    {
        var trackVisited = typeof(MappingScope).GetMethod(
            nameof(MappingScope.TrackVisited),
            new[] { typeof(object), typeof(object) })!;

        return Expression.Call(
            scopeParam,
            trackVisited,
            Expression.Convert(originExpr, typeof(object)),
            Expression.Convert(targetExpr, typeof(object)));
    }
}
