using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Builds expression tree fragments that invoke <see cref="ITypeTransformer"/> instances
/// when the origin and target property types differ.
/// </summary>
internal static class TransformerExpressionHelper
{
    /// <summary>
    /// Builds an expression that transforms <paramref name="originValueExpr"/> from
    /// <paramref name="originType"/> to <paramref name="targetType"/>, using the supplied
    /// transformer or falling back to Convert expressions for compatible types.
    /// </summary>
    /// <param name="originValueExpr">The expression producing the origin value.</param>
    /// <param name="originType">The CLR type of the origin value.</param>
    /// <param name="targetType">The CLR type expected by the target member.</param>
    /// <param name="scopeParam">The <see cref="MappingScope"/> parameter expression.</param>
    /// <param name="transformer">An explicit transformer, or <c>null</c>.</param>
    /// <returns>An expression that produces a value of <paramref name="targetType"/>.</returns>
    internal static Expression BuildTransformExpression(
        Expression originValueExpr,
        Type originType,
        Type targetType,
        ParameterExpression scopeParam,
        ITypeTransformer? transformer)
    {
        // Same type with an explicit transformer attached — invoke it as a post-processor
        // (e.g., string → string Uppercasing via [TransformWith]).
        if (originType == targetType && transformer is not null)
            return BuildTransformerCall(originValueExpr, originType, targetType, scopeParam, transformer);

        // Same type without a transformer — no conversion needed.
        if (originType == targetType)
            return originValueExpr;

        // Nullable wrapping: T → Nullable<T> (must be before IsAssignableFrom check
        // because Nullable<T>.IsAssignableFrom(T) can return true but the expression
        // types still differ and require an explicit Convert node)
        var underlyingTarget = Nullable.GetUnderlyingType(targetType);
        if (underlyingTarget is not null && underlyingTarget == originType)
            return Expression.Convert(originValueExpr, targetType);

        // Nullable unwrapping: Nullable<T> → T
        // Emit: origin.HasValue ? (T)origin : default(T)
        var underlyingOrigin = Nullable.GetUnderlyingType(originType);
        if (underlyingOrigin is not null && underlyingOrigin == targetType)
        {
            var hasValue = Expression.Property(originValueExpr, "HasValue");
            var value = Expression.Convert(originValueExpr, targetType);
            var defaultVal = Expression.Default(targetType);
            return Expression.Condition(hasValue, value, defaultVal);
        }

        // Directly assignable (e.g., string → object)
        if (targetType.IsAssignableFrom(originType))
            return originValueExpr;

        // Explicit transformer provided on the PropertyLink
        if (transformer is not null)
            return BuildTransformerCall(originValueExpr, originType, targetType, scopeParam, transformer);

        // Numeric conversions and other implicit/explicit conversions
        if (TargetConstructionResolver.IsTypeCompatible(originType, targetType))
            return Expression.Convert(originValueExpr, targetType);

        // Enum → string
        if (originType.IsEnum && targetType == typeof(string))
        {
            var toStringMethod = originType.GetMethod("ToString", Type.EmptyTypes)!;
            return Expression.Call(originValueExpr, toStringMethod);
        }

        // string → Enum
        if (originType == typeof(string) && targetType.IsEnum)
        {
            var parseMethod = typeof(Enum).GetMethod("Parse", new[] { typeof(Type), typeof(string), typeof(bool) })!;
            return Expression.Convert(
                Expression.Call(parseMethod, Expression.Constant(targetType), originValueExpr, Expression.Constant(true)),
                targetType);
        }

        // Last resort: try Expression.Convert (handles user-defined implicit/explicit operators)
        try
        {
            return Expression.Convert(originValueExpr, targetType);
        }
        catch (InvalidOperationException)
        {
            throw new MappingCompilationException(
                $"No conversion available from '{originType.Name}' to '{targetType.Name}'. " +
                $"Register a custom ITypeTransformer<{originType.Name}, {targetType.Name}> or configure the property explicitly.");
        }
    }

    /// <summary>
    /// Builds an expression that calls a specific <see cref="ITypeTransformer"/> instance.
    /// Attempts the strongly-typed <see cref="ITypeTransformer{TOrigin,TTarget}"/> path first,
    /// then falls back to the non-generic <see cref="ITypeTransformer"/> path with boxing.
    /// </summary>
    private static Expression BuildTransformerCall(
        Expression originValueExpr,
        Type originType,
        Type targetType,
        ParameterExpression scopeParam,
        ITypeTransformer transformer)
    {
        var transformerType = transformer.GetType();

        // Try strongly-typed path: ITypeTransformer<TOrigin, TTarget>.Transform(TOrigin, MappingScope)
        var genericInterface = typeof(ITypeTransformer<,>).MakeGenericType(originType, targetType);
        if (genericInterface.IsAssignableFrom(transformerType))
        {
            var transformMethod = genericInterface.GetMethod("Transform", new[] { originType, typeof(MappingScope) })!;
            var transformerConst = Expression.Constant(transformer, transformerType);
            return Expression.Call(transformerConst, transformMethod, originValueExpr, scopeParam);
        }

        // Fallback: non-generic path with boxing
        // Call IValueProvider-style: box origin, call transform, unbox result
        // Find a Transform method on the concrete type that accepts compatible params
        var methods = transformerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "Transform" && m.GetParameters().Length == 2)
            .ToArray();

        foreach (var method in methods)
        {
            var parameters = method.GetParameters();
            if (parameters[0].ParameterType.IsAssignableFrom(originType)
                && parameters[1].ParameterType == typeof(MappingScope))
            {
                var transformerConst = Expression.Constant(transformer, transformerType);
                var originArg = TargetConstructionResolver.EnsureType(originValueExpr, parameters[0].ParameterType);
                var call = Expression.Call(transformerConst, method, originArg, scopeParam);
                return TargetConstructionResolver.EnsureType(call, targetType);
            }
        }

        throw new MappingCompilationException(
            $"Transformer '{transformerType.Name}' does not have a compatible Transform method " +
            $"for converting '{originType.Name}' to '{targetType.Name}'.");
    }
}
