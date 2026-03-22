using System.Linq.Expressions;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds expression tree fragments that enforce the maximum recursion depth
/// configured via <see cref="Blueprint.MaxDepth"/>. Before any nested mapping,
/// checks <see cref="MappingScope.IsMaxDepthReached"/> and returns <c>default(T)</c>
/// if the limit is exceeded.
/// </summary>
internal static class DepthLimitGuard
{
    /// <summary>
    /// Wraps a nested mapping expression with a depth check.
    /// If <c>scope.IsMaxDepthReached</c> is <c>true</c>, returns <c>default(targetType)</c>
    /// instead of executing the mapping body.
    /// </summary>
    /// <param name="mappingBody">The expression that performs the actual nested mapping.</param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter expression.</param>
    /// <param name="targetType">The target type (for the default branch).</param>
    /// <returns>An expression with the depth check guard.</returns>
    internal static Expression WrapWithDepthCheck(
        Expression mappingBody,
        ParameterExpression scopeParam,
        Type targetType)
    {
        var isMaxDepthReached = Expression.Property(scopeParam, nameof(MappingScope.IsMaxDepthReached));
        var defaultValue = Expression.Default(targetType);

        var body = TargetConstructionResolver.EnsureType(mappingBody, targetType);

        return Expression.Condition(isMaxDepthReached, defaultValue, body);
    }
}
