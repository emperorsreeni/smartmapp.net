using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Determines how to construct a target object and builds the corresponding
/// <see cref="Expression"/> tree fragment. Used by <see cref="BlueprintCompiler"/>
/// to generate the <c>new TTarget(...)</c> portion of the mapping delegate.
/// </summary>
internal sealed class TargetConstructionResolver
{
    private readonly TypeModelCache _typeModelCache;

    /// <summary>
    /// Initializes a new instance of <see cref="TargetConstructionResolver"/>.
    /// </summary>
    /// <param name="typeModelCache">The shared type model cache.</param>
    internal TargetConstructionResolver(TypeModelCache typeModelCache)
    {
        _typeModelCache = typeModelCache;
    }

    /// <summary>
    /// Determines the optimal construction strategy for the given target type and blueprint.
    /// </summary>
    /// <param name="targetModel">The target type model.</param>
    /// <param name="blueprint">The mapping blueprint.</param>
    /// <returns>The selected <see cref="ConstructionStrategy"/>.</returns>
    internal ConstructionStrategy ResolveStrategy(TypeModel targetModel, Blueprint blueprint)
    {
        if (blueprint.TargetFactory is not null)
            return ConstructionStrategy.Factory;

        if (targetModel.PrimaryConstructor is not null && targetModel.PrimaryConstructor.ParameterCount > 0)
        {
            if (targetModel.IsRecord)
                return ConstructionStrategy.PrimaryConstructor;

            // Non-record with only one ctor that has params
            if (targetModel.Constructors.Count == 1)
                return ConstructionStrategy.PrimaryConstructor;
        }

        if (targetModel.HasParameterlessConstructor)
            return ConstructionStrategy.Parameterless;

        // Value types always have an implicit parameterless constructor
        if (targetModel.ClrType.IsValueType)
            return ConstructionStrategy.Parameterless;

        return ConstructionStrategy.BestMatchConstructor;
    }

    /// <summary>
    /// Builds an expression tree fragment that constructs a new target instance.
    /// Returns the <see cref="NewExpression"/> (or factory invocation) and the list of
    /// constructor parameter names that were consumed (so the caller can skip those in property assignment).
    /// </summary>
    /// <param name="targetModel">The target type model.</param>
    /// <param name="originModel">The origin type model.</param>
    /// <param name="blueprint">The mapping blueprint.</param>
    /// <param name="originParam">The origin parameter expression (typed).</param>
    /// <param name="scopeParam">The scope parameter expression.</param>
    /// <returns>A tuple of the construction expression and a set of target member names consumed by the constructor.</returns>
    internal (Expression ConstructionExpr, HashSet<string> ConsumedMembers) BuildConstructionExpression(
        TypeModel targetModel,
        TypeModel originModel,
        Blueprint blueprint,
        Expression originParam,
        ParameterExpression scopeParam)
    {
        var strategy = ResolveStrategy(targetModel, blueprint);

        return strategy switch
        {
            ConstructionStrategy.Factory => BuildFactoryExpression(blueprint, originParam),
            ConstructionStrategy.Parameterless => BuildParameterlessExpression(targetModel),
            ConstructionStrategy.PrimaryConstructor => BuildPrimaryCtorExpression(targetModel, originModel, originParam),
            ConstructionStrategy.BestMatchConstructor => BuildBestMatchExpression(targetModel, originModel, originParam),
            _ => throw new MappingCompilationException(
                $"Unknown construction strategy '{strategy}' for type '{targetModel.ClrType.Name}'.")
        };
    }

    /// <summary>
    /// Builds an expression that invokes the user-supplied <see cref="Blueprint.TargetFactory"/>.
    /// </summary>
    private static (Expression, HashSet<string>) BuildFactoryExpression(Blueprint blueprint, Expression originParam)
    {
        // blueprint.TargetFactory is Func<object, object>
        // Expression: (TargetType)factory(origin)
        var factoryConst = Expression.Constant(blueprint.TargetFactory, typeof(Func<object, object>));
        var invokeFactory = Expression.Invoke(factoryConst, Expression.Convert(originParam, typeof(object)));
        var castResult = Expression.Convert(invokeFactory, blueprint.TargetType);
        return (castResult, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds an expression for <c>new TTarget()</c> using the parameterless constructor.
    /// </summary>
    internal static (Expression, HashSet<string>) BuildParameterlessExpression(TypeModel targetModel)
    {
        var ctor = targetModel.Constructors.FirstOrDefault(c => c.ParameterCount == 0);
        if (ctor is not null)
        {
            return (Expression.New(ctor.ConstructorInfo), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        // Value types have an implicit parameterless constructor not returned by GetConstructors
        if (targetModel.ClrType.IsValueType)
        {
            return (Expression.New(targetModel.ClrType), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        throw new MappingCompilationException(
            $"Type '{targetModel.ClrType.Name}' has no public parameterless constructor.");
    }

    /// <summary>
    /// Builds an expression for <c>new TTarget(arg1, arg2, ...)</c> using the primary constructor.
    /// </summary>
    private (Expression, HashSet<string>) BuildPrimaryCtorExpression(
        TypeModel targetModel, TypeModel originModel, Expression originParam)
    {
        var primaryCtor = targetModel.PrimaryConstructor;
        if (primaryCtor is null || primaryCtor.ParameterCount == 0)
        {
            throw new MappingCompilationException(
                $"Type '{targetModel.ClrType.Name}' has no primary constructor with parameters.");
        }

        return BuildCtorExpression(primaryCtor, originModel, originParam, targetModel);
    }

    /// <summary>
    /// Scores all public constructors and selects the best match.
    /// </summary>
    private (Expression, HashSet<string>) BuildBestMatchExpression(
        TypeModel targetModel, TypeModel originModel, Expression originParam)
    {
        ConstructorModel? bestCtor = null;
        var bestScore = -1;
        var bestUnmatched = int.MaxValue;

        foreach (var ctor in targetModel.Constructors)
        {
            if (ctor.ParameterCount == 0) continue;

            var score = 0;
            var unmatched = 0;

            foreach (var param in ctor.Parameters)
            {
                var originMember = param.Name is not null ? originModel.GetMember(param.Name) : null;
                if (originMember is not null && IsTypeCompatible(originMember.MemberType, param.ParameterType))
                {
                    score++;
                }
                else
                {
                    unmatched++;
                }
            }

            if (score > bestScore || (score == bestScore && unmatched < bestUnmatched))
            {
                bestScore = score;
                bestUnmatched = unmatched;
                bestCtor = ctor;
            }
        }

        if (bestCtor is null || bestScore == 0)
        {
            throw new MappingCompilationException(
                $"No viable constructor found for type '{targetModel.ClrType.Name}'. " +
                $"None of the {targetModel.Constructors.Count} public constructor(s) have parameters matching origin type '{originModel.ClrType.Name}'.");
        }

        return BuildCtorExpression(bestCtor, originModel, originParam, targetModel);
    }

    /// <summary>
    /// Builds the <see cref="NewExpression"/> for a specific constructor, matching parameters to origin members.
    /// </summary>
    private (Expression, HashSet<string>) BuildCtorExpression(
        ConstructorModel ctor, TypeModel originModel, Expression originParam, TypeModel targetModel)
    {
        var args = new Expression[ctor.ParameterCount];
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < ctor.ParameterCount; i++)
        {
            var param = ctor.Parameters[i];
            var originMember = param.Name is not null ? originModel.GetMember(param.Name) : null;

            if (originMember is not null && IsTypeCompatible(originMember.MemberType, param.ParameterType))
            {
                var memberAccess = BuildMemberAccess(originParam, originMember);
                args[i] = EnsureType(memberAccess, param.ParameterType);

                // Track the target member that corresponds to this ctor param
                // so PropertyAssignmentBuilder can skip it
                consumed.Add(param.Name!);
            }
            else
            {
                // Unmatched parameter — use default
                args[i] = Expression.Default(param.ParameterType);
            }
        }

        var newExpr = Expression.New(ctor.ConstructorInfo, args);
        return (newExpr, consumed);
    }

    /// <summary>
    /// Builds a member access expression for an origin member (property or field).
    /// </summary>
    internal static Expression BuildMemberAccess(Expression instance, MemberModel member)
    {
        return member.MemberInfo switch
        {
            PropertyInfo pi => Expression.Property(instance, pi),
            FieldInfo fi => Expression.Field(instance, fi),
            _ => throw new MappingCompilationException(
                $"Unsupported member type '{member.MemberInfo.GetType().Name}' for member '{member.Name}'.")
        };
    }

    /// <summary>
    /// Ensures an expression is converted to the target type if needed.
    /// </summary>
    internal static Expression EnsureType(Expression expression, Type targetType)
    {
        if (expression.Type == targetType)
            return expression;

        return Expression.Convert(expression, targetType);
    }

    /// <summary>
    /// Determines whether a value of <paramref name="originType"/> can be assigned or converted
    /// to <paramref name="targetType"/>.
    /// </summary>
    internal static bool IsTypeCompatible(Type originType, Type targetType)
    {
        if (targetType == originType)
            return true;

        if (targetType.IsAssignableFrom(originType))
            return true;

        // Nullable<T> ← T
        var underlyingTarget = Nullable.GetUnderlyingType(targetType);
        if (underlyingTarget is not null && underlyingTarget == originType)
            return true;

        // T ← Nullable<T>
        var underlyingOrigin = Nullable.GetUnderlyingType(originType);
        if (underlyingOrigin is not null && underlyingOrigin == targetType)
            return true;

        // Numeric widening (int → long, float → double, etc.)
        if (IsNumericConversion(originType, targetType))
            return true;

        return false;
    }

    /// <summary>
    /// Checks whether a widening numeric conversion exists between two types.
    /// </summary>
    private static bool IsNumericConversion(Type from, Type to)
    {
        if (!IsNumericType(from) || !IsNumericType(to))
            return false;

        // Allow any numeric-to-numeric conversion (Expression.Convert handles it)
        return true;
    }

    /// <summary>
    /// Determines whether the given type is a primitive numeric type.
    /// </summary>
    private static bool IsNumericType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(byte) || underlying == typeof(sbyte)
            || underlying == typeof(short) || underlying == typeof(ushort)
            || underlying == typeof(int) || underlying == typeof(uint)
            || underlying == typeof(long) || underlying == typeof(ulong)
            || underlying == typeof(float) || underlying == typeof(double)
            || underlying == typeof(decimal);
    }
}
