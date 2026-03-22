using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Open transformer that detects and uses <c>implicit</c> and <c>explicit</c> conversion operators
/// (<c>op_Implicit</c>, <c>op_Explicit</c>) defined on either the origin or target type.
/// Prefers <c>implicit</c> over <c>explicit</c> operators.
/// <para>
/// Operator methods are discovered via reflection, compiled to delegates, and cached per type pair.
/// A <c>null</c> cache entry means "scanned, no operator found."
/// </para>
/// </summary>
public sealed class ImplicitExplicitOperatorTransformer : ITypeTransformer
{
    private readonly ConcurrentDictionary<TypePair, Func<object, object>?> _cache = new();
    private readonly bool _allowExplicit;

    /// <summary>
    /// Initializes a new instance that detects both implicit and explicit operators.
    /// </summary>
    /// <param name="allowExplicit">
    /// If <c>true</c> (default), both <c>implicit</c> and <c>explicit</c> operators are used.
    /// If <c>false</c>, only <c>implicit</c> operators are used.
    /// </param>
    public ImplicitExplicitOperatorTransformer(bool allowExplicit = true)
    {
        _allowExplicit = allowExplicit;
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
    {
        return GetCachedDelegate(originType, targetType) is not null;
    }

    /// <summary>
    /// Transforms the origin value using the detected conversion operator.
    /// </summary>
    /// <param name="origin">The origin value.</param>
    /// <param name="originType">The origin type.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The converted value.</returns>
    public object Transform(object? origin, Type originType, Type targetType, MappingScope scope)
    {
        if (origin is null)
            throw new TransformationException(
                $"Cannot apply operator conversion on null from {originType.Name} to {targetType.Name}.",
                null, originType, targetType);

        var func = GetCachedDelegate(originType, targetType)
            ?? throw new TransformationException(
                $"No implicit/explicit operator found for {originType.Name} → {targetType.Name}.",
                origin, originType, targetType);

        return func(origin);
    }

    private Func<object, object>? GetCachedDelegate(Type originType, Type targetType)
    {
        var pair = new TypePair(originType, targetType);
        return _cache.GetOrAdd(pair, p => FindAndCompileOperator(p.OriginType, p.TargetType));
    }

    private Func<object, object>? FindAndCompileOperator(Type originType, Type targetType)
    {
        // 1. Prefer implicit operators
        var method = FindOperator(originType, targetType, "op_Implicit");

        // 2. Fallback to explicit if allowed
        if (method is null && _allowExplicit)
            method = FindOperator(originType, targetType, "op_Explicit");

        if (method is null)
            return null;

        return CompileOperator(method, originType);
    }

    private static MethodInfo? FindOperator(Type originType, Type targetType, string operatorName)
    {
        // Search on origin type
        var method = FindOperatorOnType(originType, originType, targetType, operatorName);
        if (method is not null)
            return method;

        // Search on target type
        return FindOperatorOnType(targetType, originType, targetType, operatorName);
    }

    private static MethodInfo? FindOperatorOnType(
        Type declaringType, Type originType, Type targetType, string operatorName)
    {
        var methods = declaringType.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        for (int i = 0; i < methods.Length; i++)
        {
            var m = methods[i];
            if (m.Name != operatorName || !m.IsSpecialName)
                continue;

            var parameters = m.GetParameters();
            if (parameters.Length != 1)
                continue;

            if (parameters[0].ParameterType.IsAssignableFrom(originType) &&
                targetType.IsAssignableFrom(m.ReturnType))
            {
                return m;
            }
        }

        return null;
    }

    private static Func<object, object> CompileOperator(MethodInfo operatorMethod, Type originType)
    {
        // Build: (object input) => (object)OperatorMethod((OriginType)input)
        var param = Expression.Parameter(typeof(object), "input");
        var converted = Expression.Convert(param, operatorMethod.GetParameters()[0].ParameterType);
        var call = Expression.Call(null, operatorMethod, converted);
        var boxed = Expression.Convert(call, typeof(object));

        return Expression.Lambda<Func<object, object>>(boxed, param).Compile();
    }
}
