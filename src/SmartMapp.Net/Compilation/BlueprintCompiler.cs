using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Discovery;

namespace SmartMapp.Net.Compilation;

/// <summary>
/// Top-level orchestrator that compiles a <see cref="Blueprint"/> into a
/// <c>Func&lt;object, MappingScope, object&gt;</c> delegate using expression trees.
/// Coordinates construction resolution, property assignment, null-safe navigation,
/// type transformation, circular reference tracking, and depth limits.
/// </summary>
internal sealed class BlueprintCompiler
{
    private readonly TargetConstructionResolver _constructionResolver;
    private readonly TypeModelCache _typeModelCache;
    private readonly MappingDelegateCache _delegateCache;
    private readonly Func<TypePair, Blueprint?>? _blueprintResolver;
    private readonly Func<Type, Type, ITypeTransformer?>? _transformerLookup;
    private readonly Abstractions.InheritanceResolver? _inheritanceResolver;
    private readonly PolymorphicDispatchBuilder? _polymorphicDispatchBuilder;

    /// <summary>
    /// Initializes a new instance of <see cref="BlueprintCompiler"/>.
    /// </summary>
    /// <param name="typeModelCache">The shared type model cache.</param>
    /// <param name="delegateCache">The delegate cache for nested type pair resolution.</param>
    /// <param name="blueprintResolver">
    /// Optional resolver for nested type pair blueprints. If <c>null</c>, auto-discovery via
    /// exact name matching is used for nested types.
    /// </param>
    /// <param name="transformerLookup">
    /// Optional lookup for type transformers by (originType, targetType). Bridges Sprint 3's
    /// <c>TypeTransformerRegistry</c> into expression compilation.
    /// </param>
    /// <param name="inheritanceResolver">
    /// Optional inheritance resolver for polymorphic dispatch. When provided, compiled delegates
    /// are automatically wrapped with type-check dispatch for derived types (Sprint 6).
    /// </param>
    internal BlueprintCompiler(
        TypeModelCache typeModelCache,
        MappingDelegateCache delegateCache,
        Func<TypePair, Blueprint?>? blueprintResolver = null,
        Func<Type, Type, ITypeTransformer?>? transformerLookup = null,
        Abstractions.InheritanceResolver? inheritanceResolver = null)
    {
        _typeModelCache = typeModelCache;
        _delegateCache = delegateCache;
        _blueprintResolver = blueprintResolver;
        _transformerLookup = transformerLookup;
        _inheritanceResolver = inheritanceResolver;
        _constructionResolver = new TargetConstructionResolver(typeModelCache, inheritanceResolver);

        if (inheritanceResolver is not null)
        {
            _polymorphicDispatchBuilder = new PolymorphicDispatchBuilder(inheritanceResolver, delegateCache);
        }
    }

    /// <summary>
    /// Compiles the given <see cref="Blueprint"/> into a mapping delegate.
    /// </summary>
    /// <param name="blueprint">The blueprint describing the mapping.</param>
    /// <returns>A compiled delegate that maps an origin object to a target object.</returns>
    internal Func<object, MappingScope, object> Compile(Blueprint blueprint)
    {
        var lambda = CompileToLambda(blueprint);
        var baseDelegate = lambda.Compile();

        // Wrap with polymorphic dispatch if derived pairs exist (Sprint 6)
        if (_polymorphicDispatchBuilder is not null)
        {
            var dispatchDelegate = _polymorphicDispatchBuilder.BuildDispatchDelegate(
                blueprint.TypePair,
                baseDelegate,
                derivedPair =>
                {
                    var derivedBp = ResolveNestedBlueprint(derivedPair, trackReferences: false);
                    var derivedLambda = CompileToLambda(derivedBp);
                    return derivedLambda.Compile();
                });

            if (dispatchDelegate is not null)
                return dispatchDelegate;
        }

        return baseDelegate;
    }

    /// <summary>
    /// Compiles the given <see cref="Blueprint"/> into an uncompiled lambda expression.
    /// Useful for diagnostics or for <c>SelectAs&lt;T&gt;()</c> IQueryable projection.
    /// </summary>
    /// <param name="blueprint">The blueprint describing the mapping.</param>
    /// <returns>An uncompiled lambda expression.</returns>
    internal Expression<Func<object, MappingScope, object>> CompileToLambda(Blueprint blueprint)
    {
        var originParam = Expression.Parameter(typeof(object), "origin");
        var scopeParam = Expression.Parameter(typeof(MappingScope), "scope");

        var body = BuildMappingBody(blueprint, originParam, scopeParam);

        return Expression.Lambda<Func<object, MappingScope, object>>(body, originParam, scopeParam);
    }

    /// <summary>
    /// Builds the full mapping body expression for the given blueprint.
    /// </summary>
    private Expression BuildMappingBody(
        Blueprint blueprint,
        ParameterExpression originParam,
        ParameterExpression scopeParam)
    {
        var originModel = _typeModelCache.GetOrAdd(blueprint.OriginType);
        var targetModel = _typeModelCache.GetOrAdd(blueprint.TargetType);

        // Cast origin to strongly-typed local
        var typedOrigin = Expression.Variable(blueprint.OriginType, "typedOrigin");
        var castOrigin = Expression.Assign(typedOrigin, Expression.Convert(originParam, blueprint.OriginType));

        // Null check on origin
        var originNullCheck = Expression.Equal(originParam, Expression.Constant(null, typeof(object)));

        // Validate required members
        var strategy = _constructionResolver.ResolveStrategy(targetModel, blueprint);
        var preConsumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        RequiredMemberValidator.Validate(targetModel, blueprint, preConsumed, blueprint.StrictRequiredMembers);

        // Determine construction strategy and check if we need MemberInit for init-only props
        var useSequentialAssignment = blueprint.TrackReferences || strategy == ConstructionStrategy.Factory;

        // Build the construction + assignment body
        Expression mappingBody;
        if (useSequentialAssignment)
        {
            mappingBody = BuildSequentialBody(blueprint, originModel, targetModel, typedOrigin, scopeParam);
        }
        else
        {
            mappingBody = BuildMemberInitBody(blueprint, originModel, targetModel, typedOrigin, scopeParam);
        }

        // Wrap with null check: if origin is null, return default(TargetType)
        var nullResult = Expression.Default(blueprint.TargetType);
        var fullBody = Expression.Condition(
            originNullCheck,
            Expression.Convert(nullResult, typeof(object)),
            Expression.Convert(mappingBody, typeof(object)));

        return Expression.Block(
            new[] { typedOrigin },
            castOrigin,
            fullBody);
    }

    /// <summary>
    /// Builds the mapping body using sequential assignment (required when reference tracking is active
    /// because the target must exist before properties are set).
    /// Pattern: var target = new T(); TrackVisited(origin, target); target.P1 = ...; return target;
    /// </summary>
    private Expression BuildSequentialBody(
        Blueprint blueprint,
        TypeModel originModel,
        TypeModel targetModel,
        Expression typedOrigin,
        ParameterExpression scopeParam)
    {
        var (constructionExpr, consumed) = _constructionResolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, typedOrigin, scopeParam);

        var targetVar = Expression.Variable(blueprint.TargetType, "target");
        var statements = new List<Expression>();

        // Construct target
        statements.Add(Expression.Assign(targetVar, constructionExpr));

        // Track visited for circular reference detection (before property assignment)
        if (blueprint.TrackReferences && !blueprint.TargetType.IsValueType)
        {
            statements.Add(CircularReferenceGuard.BuildTrackExpression(typedOrigin, targetVar, scopeParam));
        }

        // OnMapping hook
        if (blueprint.OnMapping is not null)
        {
            statements.Add(BuildHookCall(blueprint.OnMapping, typedOrigin, targetVar));
        }

        // Property assignments
        var assignments = PropertyAssignmentBuilder.BuildAssignments(
            blueprint.Links, typedOrigin, targetVar, scopeParam, consumed,
            (originExpr, originType, targetType, scope) =>
                BuildNestedMappingExpression(originExpr, originType, targetType, scope, blueprint.TrackReferences));

        statements.AddRange(assignments);

        // OnMapped hook
        if (blueprint.OnMapped is not null)
        {
            statements.Add(BuildHookCall(blueprint.OnMapped, typedOrigin, targetVar));
        }

        // Return target
        statements.Add(targetVar);

        return Expression.Block(new[] { targetVar }, statements);
    }

    /// <summary>
    /// Builds the mapping body using MemberInit where possible.
    /// More efficient for types with init-only properties. Used when reference tracking is disabled.
    /// </summary>
    private Expression BuildMemberInitBody(
        Blueprint blueprint,
        TypeModel originModel,
        TypeModel targetModel,
        Expression typedOrigin,
        ParameterExpression scopeParam)
    {
        var (constructionExpr, consumed) = _constructionResolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, typedOrigin, scopeParam);

        // Check if we have any init-only properties that need MemberInit
        var hasInitOnly = blueprint.Links.Any(l =>
            !l.IsSkipped
            && !consumed.Contains(l.TargetMember.Name)
            && l.TargetMember is PropertyInfo pi
            && pi.SetMethod is not null
            && IsInitOnlySetter(pi.SetMethod));

        // Check if we have non-init properties, conditions, fallbacks, complex types, or collections
        // that require sequential assignment (MemberInit doesn't support nested mapping delegates)
        var hasSequentialNeeds = blueprint.Links.Any(l =>
            !l.IsSkipped
            && !consumed.Contains(l.TargetMember.Name)
            && (l.Condition is not null
                || l.PreCondition is not null
                || l.Fallback is not null
                || IsComplexTargetMember(l)
                || IsCollectionTargetMember(l)));

        if (hasSequentialNeeds || blueprint.OnMapping is not null || blueprint.OnMapped is not null)
        {
            // Fall back to sequential assignment for complex cases
            return BuildSequentialBody(blueprint, originModel, targetModel, typedOrigin, scopeParam);
        }

        if (hasInitOnly && constructionExpr is NewExpression newExpr)
        {
            // Use MemberInit for all writable properties (init-only and regular)
            var bindings = PropertyAssignmentBuilder.BuildMemberBindings(
                blueprint.Links, typedOrigin, scopeParam, consumed, initOnlyOnly: false);

            if (bindings.Count > 0)
            {
                return Expression.MemberInit(newExpr, bindings);
            }
        }

        // Simple case: construct + assign settable properties
        if (constructionExpr is NewExpression simpleNew)
        {
            var allBindings = PropertyAssignmentBuilder.BuildMemberBindings(
                blueprint.Links, typedOrigin, scopeParam, consumed, initOnlyOnly: false);

            if (allBindings.Count > 0)
            {
                return Expression.MemberInit(simpleNew, allBindings);
            }

            return simpleNew;
        }

        // Factory or non-New expression: must use sequential
        return BuildSequentialBody(blueprint, originModel, targetModel, typedOrigin, scopeParam);
    }

    /// <summary>
    /// Builds an expression for mapping a nested complex-type property.
    /// Includes depth check, null check, and circular reference tracking.
    /// If the nested types are collections, dispatches to <see cref="CollectionMapper"/> instead.
    /// </summary>
    private Expression BuildNestedMappingExpression(
        Expression originValueExpr,
        Type originType,
        Type targetType,
        ParameterExpression scopeParam,
        bool trackReferences)
    {
        // If both origin and target are collection types, delegate to CollectionMapper
        // rather than trying to build an object-mapping blueprint for collections.
        if (CollectionCategoryResolver.Resolve(targetType) != CollectionCategory.Unknown
            && CollectionCategoryResolver.Resolve(originType) != CollectionCategory.Unknown)
        {
            return CollectionMapper.BuildCollectionExpression(
                originValueExpr, originType, targetType, scopeParam,
                (expr, oType, tType, scope) => BuildNestedMappingExpression(expr, oType, tType, scope, trackReferences));
        }

        // Build the core mapping call via delegate cache
        var mapCall = BuildDelegateCacheCall(originValueExpr, originType, targetType, scopeParam, trackReferences);

        // Wrap with depth limit
        mapCall = DepthLimitGuard.WrapWithDepthCheck(mapCall, scopeParam, targetType);

        // Wrap with circular reference tracking
        if (trackReferences && !targetType.IsValueType)
        {
            mapCall = CircularReferenceGuard.WrapWithReferenceTracking(
                originValueExpr, targetType, () => mapCall, scopeParam, true);
        }

        // Null check on the origin value (for reference types)
        if (!originType.IsValueType)
        {
            var nullCheck = Expression.Equal(
                originValueExpr,
                Expression.Constant(null, originType));
            var defaultTarget = Expression.Default(targetType);
            mapCall = Expression.Condition(nullCheck, defaultTarget, mapCall);
        }
        else if (Nullable.GetUnderlyingType(originType) is not null)
        {
            var hasValue = Expression.Property(originValueExpr, "HasValue");
            var defaultTarget = Expression.Default(targetType);
            mapCall = Expression.Condition(hasValue, mapCall, defaultTarget);
        }

        return mapCall;
    }

    /// <summary>
    /// Builds an expression that calls the delegate cache to get (or compile) the mapping delegate
    /// for a nested type pair, then invokes it.
    /// </summary>
    private Expression BuildDelegateCacheCall(
        Expression originValueExpr,
        Type originType,
        Type targetType,
        ParameterExpression scopeParam,
        bool trackReferences)
    {
        // We capture the delegate cache and create a nested call:
        // ((Func<object, MappingScope, object>)_delegateCache.GetOrCompile(pair, factory))(origin, scope.CreateChild())

        var nestedPair = new TypePair(originType, targetType);

        // Build: scope.CreateChild()
        var createChild = Expression.Call(scopeParam, typeof(MappingScope).GetMethod(nameof(MappingScope.CreateChild))!);

        // For the expression tree, we emit a call to a captured Func
        // that resolves the delegate and invokes it
        Func<object, MappingScope, object> nestedInvoker = (origin, scope) =>
        {
            var del = _delegateCache.GetOrCompile(nestedPair, tp =>
            {
                var nestedBlueprint = ResolveNestedBlueprint(tp, trackReferences);
                return Compile(nestedBlueprint);
            });
            return del(origin, scope);
        };

        var invokerConst = Expression.Constant(nestedInvoker, typeof(Func<object, MappingScope, object>));

        var invokeExpr = Expression.Invoke(
            invokerConst,
            Expression.Convert(originValueExpr, typeof(object)),
            createChild);

        return Expression.Convert(invokeExpr, targetType);
    }

    /// <summary>
    /// Resolves a blueprint for a nested type pair. Tries the external resolver first,
    /// then falls back to auto-discovery via exact name matching.
    /// </summary>
    private Blueprint ResolveNestedBlueprint(TypePair pair, bool trackReferences)
    {
        if (_blueprintResolver is not null)
        {
            var resolved = _blueprintResolver(pair);
            if (resolved is not null)
                return resolved;
        }

        return BuildAutoDiscoveredBlueprint(pair, trackReferences);
    }

    /// <summary>
    /// Builds a minimal blueprint for a nested type pair by auto-discovering property links
    /// via exact name matching. Used when no explicit blueprint is registered for the pair.
    /// </summary>
    private Blueprint BuildAutoDiscoveredBlueprint(TypePair pair, bool trackReferences)
    {
        var originModel = _typeModelCache.GetOrAdd(pair.OriginType);
        var targetModel = _typeModelCache.GetOrAdd(pair.TargetType);

        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            // Create a simple direct-access provider
            var provider = new DirectMemberProvider(originMember.MemberInfo);

            // Look up transformer from registry if types differ
            ITypeTransformer? transformer = null;
            if (originMember.MemberType != PropertyAssignmentBuilder.GetMemberType(targetMember.MemberInfo)
                && _transformerLookup is not null)
            {
                transformer = _transformerLookup(originMember.MemberType, PropertyAssignmentBuilder.GetMemberType(targetMember.MemberInfo));
            }

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = provider,
                Transformer = transformer,
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
            });
        }

        return new Blueprint
        {
            OriginType = pair.OriginType,
            TargetType = pair.TargetType,
            Links = links,
            TrackReferences = trackReferences,
        };
    }

    /// <summary>
    /// Builds an expression that invokes a hook <c>Action&lt;object, object&gt;</c>.
    /// </summary>
    private static Expression BuildHookCall(Action<object, object> hook, Expression origin, Expression target)
    {
        var hookConst = Expression.Constant(hook, typeof(Action<object, object>));
        return Expression.Invoke(hookConst,
            Expression.Convert(origin, typeof(object)),
            Expression.Convert(target, typeof(object)));
    }

    /// <summary>
    /// Checks whether a setter method is init-only.
    /// </summary>
    private static bool IsInitOnlySetter(MethodInfo setter)
    {
        var returnParam = setter.ReturnParameter;
        if (returnParam is null) return false;
        var modifiers = returnParam.GetRequiredCustomModifiers();
        return modifiers.Any(m => m.Name == "IsExternalInit");
    }

    /// <summary>
    /// Checks whether a property link targets a complex type.
    /// </summary>
    private static bool IsComplexTargetMember(PropertyLink link)
    {
        var memberType = PropertyAssignmentBuilder.GetMemberType(link.TargetMember);
        return ComplexTypeDetector.IsComplexType(memberType);
    }

    /// <summary>
    /// Checks whether a property link targets a collection type that requires
    /// <see cref="CollectionMapper"/> dispatch (not available via MemberInit).
    /// </summary>
    private static bool IsCollectionTargetMember(PropertyLink link)
    {
        var memberType = PropertyAssignmentBuilder.GetMemberType(link.TargetMember);
        return CollectionCategoryResolver.Resolve(memberType) != CollectionCategory.Unknown;
    }
}
