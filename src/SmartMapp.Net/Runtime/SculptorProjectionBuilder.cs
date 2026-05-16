using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Conventions;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Builds <see cref="Expression{TDelegate}"/> values of the shape
/// <c>origin =&gt; new TTarget { Member1 = ..., Member2 = ..., ... }</c> suitable for
/// <see cref="IQueryable{T}"/> provider translation — in particular EF Core 8+ LINQ-to-SQL.
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Sprint 8 · S8-T06 per spec §8.10. The generated expression contains only
/// operators EF Core can translate: member access, explicit <c>Condition</c> null-checks,
/// <see cref="Expression.Convert(Expression, Type)"/> for numeric and nullable coercion, and
/// <see cref="Expression.MemberInit(NewExpression, MemberBinding[])"/>. No runtime delegate
/// invocations, no <c>MappingScope</c> references, no <c>?.</c> operators — EF cannot
/// translate those.
/// </para>
/// <para>
/// <see cref="PropertyLink"/>s backed by
/// <see cref="PropertyAccessProvider"/> / <see cref="ChainedPropertyAccessProvider"/> are
/// inlined as pure expressions. Any other <see cref="PropertyLink.Provider"/> implementation
/// (expression-based providers, DI-deferred providers, inline transforms, …) is skipped and
/// a <see cref="ProjectionDiagnostic"/> is written to
/// <c>ForgedSculptorConfiguration.ProjectionDiagnostics</c>; the affected target member
/// retains its default value in the projection.
/// </para>
/// <para>
/// For chained (flattened) accessors, null-safety is emitted as an explicit
/// <see cref="Expression.Condition(Expression, Expression, Expression)"/> tree — e.g.
/// <c>origin.Customer == null ? default : origin.Customer.Address.City</c>. This is the shape
/// EF Core's query translator expects; <c>?.</c> is not supported. Deep graph optimisation
/// (pruning redundant null-checks, flattening nested MemberInit) arrives in Sprint 21.
/// </para>
/// </remarks>
internal static class SculptorProjectionBuilder
{
    /// <summary>
    /// Builds (or returns a cached) <see cref="LambdaExpression"/> for the supplied
    /// <paramref name="pair"/>. Results are memoised in
    /// <see cref="ForgedSculptorConfiguration.ProjectionCache"/> — repeated calls return the
    /// exact same <see cref="Expression{TDelegate}"/> instance (spec §S8-T06 Acceptance
    /// bullet 6).
    /// </summary>
    /// <param name="config">The forged sculptor configuration.</param>
    /// <param name="pair">The origin → target pair to project.</param>
    /// <returns>A strongly-typed <see cref="Expression{TDelegate}"/> assignable to <c>Expression&lt;Func&lt;TOrigin, TTarget&gt;&gt;</c>.</returns>
    /// <exception cref="MappingConfigurationException">Thrown when no blueprint is registered for <paramref name="pair"/>.</exception>
    internal static LambdaExpression BuildOrGet(ForgedSculptorConfiguration config, TypePair pair)
    {
        return config.ProjectionCache.GetOrAdd(pair, static (p, cfg) => BuildCore(cfg, p, new HashSet<TypePair>()), config);
    }

    /// <summary>
    /// Builds a <see cref="MemberInitExpression"/> body for <paramref name="pair"/> against the
    /// supplied <paramref name="originExpr"/>. Used both for the top-level projection and for
    /// recursive nested-DTO bindings. <paramref name="visited"/> guards against cyclic
    /// blueprints; the root call starts with an empty set, and each nested call adds the
    /// current pair before recursing.
    /// </summary>
    private static Expression BuildMemberInitBody(
        ForgedSculptorConfiguration config,
        TypePair pair,
        Expression originExpr,
        HashSet<TypePair> visited)
    {
        var blueprint = config.TryGetBlueprint(pair)
            ?? throw new MappingConfigurationException(
                $"Cannot build projection: no blueprint registered for type pair '{pair}'.", pair);

        var bindings = new List<MemberBinding>(blueprint.Links.Count);

        foreach (var link in blueprint.Links)
        {
            if (link.IsSkipped) continue;

            var targetMember = link.TargetMember;
            var targetMemberType = GetMemberType(targetMember);

            var valueExpr = TryBuildValueExpression(config, pair, link, originExpr, targetMemberType, visited);
            if (valueExpr is null)
            {
                config.ProjectionDiagnostics.Add(new ProjectionDiagnostic
                {
                    Pair = pair,
                    TargetMemberName = targetMember.Name,
                    Reason = BuildUnsupportedProviderReason(link),
                });
                continue;
            }

            bindings.Add(Expression.Bind(targetMember, valueExpr));
        }

        var ctor = pair.TargetType.GetConstructor(Type.EmptyTypes);
        if (ctor is null)
        {
            config.ProjectionDiagnostics.Add(new ProjectionDiagnostic
            {
                Pair = pair,
                TargetMemberName = "<ctor>",
                Reason = $"Target '{pair.TargetType.FullName}' has no public parameterless constructor; " +
                         "projection emits default() and cannot be translated to SQL.",
            });
            return Expression.Default(pair.TargetType);
        }

        return Expression.MemberInit(Expression.New(ctor), bindings);
    }

    private static LambdaExpression BuildCore(ForgedSculptorConfiguration config, TypePair pair, HashSet<TypePair> visited)
    {
        var originParam = Expression.Parameter(pair.OriginType, "origin");
        visited.Add(pair);
        var body = BuildMemberInitBody(config, pair, originParam, visited);
        var lambdaType = typeof(Func<,>).MakeGenericType(pair.OriginType, pair.TargetType);
        return Expression.Lambda(lambdaType, body, originParam);
    }

    /// <summary>
    /// Produces a pure expression extracting the origin-side value for the supplied
    /// <paramref name="link"/>. Returns <c>null</c> when the link's provider cannot be
    /// translated to an EF-friendly expression (the caller records a diagnostic and skips
    /// the member).
    /// </summary>
    private static Expression? TryBuildValueExpression(
        ForgedSculptorConfiguration config,
        TypePair parentPair,
        PropertyLink link,
        Expression originExpr,
        Type targetMemberType,
        HashSet<TypePair> visited)
    {
        Expression? sourceExpr = link.Provider switch
        {
            PropertyAccessProvider pap => BuildDirectAccess(originExpr, pap.OriginMember),
            ChainedPropertyAccessProvider cpap => BuildNullSafeChain(originExpr, cpap.Chain, targetMemberType),
            _ => null,
        };

        if (sourceExpr is null) return null;

        // Transformer is explicitly not supported in Sprint 8 projections — it would require
        // emitting the transformer's internals as an expression, which the Sprint 7 surface
        // doesn't expose. Skip the link if a transformer is attached.
        if (link.Transformer is not null) return null;

        // Nested-DTO projection: when source and target differ AND a blueprint is registered
        // for (sourceType, targetMemberType), recurse into BuildMemberInitBody and wrap the
        // nested body in a null-safe guard. Cycle-detection via `visited` prevents infinite
        // recursion when blueprints reference each other circularly (emits a diagnostic and
        // skips the member instead).
        if (sourceExpr.Type != targetMemberType)
        {
            var nestedPair = new TypePair(sourceExpr.Type, targetMemberType);
            if (config.TryGetBlueprint(nestedPair) is not null)
            {
                if (visited.Contains(nestedPair))
                {
                    config.ProjectionDiagnostics.Add(new ProjectionDiagnostic
                    {
                        Pair = parentPair,
                        TargetMemberName = link.TargetMember.Name,
                        Reason = $"Cyclic blueprint reference detected at '{nestedPair}'; projection skips the " +
                                 "member to avoid infinite recursion. Sprint 21 will ship a cycle-breaker.",
                    });
                    return null;
                }
                return BuildNestedProjection(config, sourceExpr, nestedPair, visited);
            }

            // Collection projection: source is IEnumerable<TS>, target is IEnumerable<TD> (or an
            // assignable concrete collection — List<TD>, ICollection<TD>, etc.), and a blueprint
            // is registered for (TS, TD). Emit `source.Select(item => new TD { ... })` which EF
            // Core 8+ translates to a SQL subquery.
            if (TryBuildCollectionProjection(config, sourceExpr, targetMemberType, visited, parentPair, link, out var collectionExpr))
            {
                return collectionExpr;
            }
        }

        return CoerceToTargetType(sourceExpr, targetMemberType);
    }

    /// <summary>
    /// Emits a null-safe <see cref="MemberInitExpression"/> for a nested complex-type member
    /// that has its own registered blueprint. Inlines the body (no <c>Expression.Invoke</c>)
    /// so EF can translate it.
    /// </summary>
    private static Expression BuildNestedProjection(
        ForgedSculptorConfiguration config,
        Expression sourceExpr,
        TypePair nestedPair,
        HashSet<TypePair> visited)
    {
        // Push the current pair onto the visited set for the recursive walk; on return, pop it
        // so sibling nested members don't falsely trigger cycle detection.
        visited.Add(nestedPair);
        try
        {
            var nestedBody = BuildMemberInitBody(config, nestedPair, sourceExpr, visited);

            // Null-safe wrap: source-side reference or Nullable<T> may be null at query time.
            if (IsNullCheckable(sourceExpr.Type))
            {
                var nullCheck = Expression.Equal(sourceExpr, Expression.Constant(null, sourceExpr.Type));
                var defaultValue = Expression.Default(nestedPair.TargetType);
                return Expression.Condition(nullCheck, defaultValue, nestedBody);
            }

            return nestedBody;
        }
        finally
        {
            visited.Remove(nestedPair);
        }
    }

    /// <summary>
    /// Attempts to emit an EF-translatable collection projection. Supports <see cref="IEnumerable{T}"/>
    /// sources and concrete <see cref="List{T}"/> / <see cref="IList{T}"/> / <see cref="ICollection{T}"/> /
    /// <see cref="IReadOnlyList{T}"/> targets. The produced expression is
    /// <c>source.Select(item =&gt; new TDto { ... })</c>; for non-<see cref="IEnumerable{T}"/>
    /// target members, a materialising <c>ToList()</c> call is chained so EF wraps the subquery
    /// in a <c>SELECT ... FROM</c> and returns a concrete collection.
    /// </summary>
    private static bool TryBuildCollectionProjection(
        ForgedSculptorConfiguration config,
        Expression sourceExpr,
        Type targetMemberType,
        HashSet<TypePair> visited,
        TypePair parentPair,
        PropertyLink link,
        out Expression? result)
    {
        result = null;

        var sourceElement = GetEnumerableElementType(sourceExpr.Type);
        var targetElement = GetEnumerableElementType(targetMemberType);
        if (sourceElement is null || targetElement is null) return false;

        var elementPair = new TypePair(sourceElement, targetElement);
        if (config.TryGetBlueprint(elementPair) is null) return false;

        if (visited.Contains(elementPair))
        {
            config.ProjectionDiagnostics.Add(new ProjectionDiagnostic
            {
                Pair = parentPair,
                TargetMemberName = link.TargetMember.Name,
                Reason = $"Cyclic blueprint reference detected at element pair '{elementPair}' within a " +
                         "collection projection; member skipped to avoid infinite recursion.",
            });
            return true; // Caller should skip — we've written the diagnostic; return null result.
        }

        // Build the element lambda: item => new TDto { ... }
        var itemLambda = BuildCore(config, elementPair, visited);

        // Emit Enumerable.Select(source, itemLambda). EF Core understands both Queryable.Select
        // and Enumerable.Select in the expression tree; it translates either into a subquery.
        var selectMethod = typeof(Enumerable).GetMethods()
            .Single(m => m.Name == nameof(Enumerable.Select)
                      && m.GetParameters().Length == 2
                      && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Func<,>))
            .MakeGenericMethod(sourceElement, targetElement);

        var selectCall = Expression.Call(null, selectMethod, sourceExpr, itemLambda);

        // If the target member is assignable from IEnumerable<T> (interface or IEnumerable<T> itself),
        // we can return the lazy IEnumerable<TDto>. For concrete collection types (List<T>,
        // IList<T>, etc.) we need a ToList() call so the materialisation matches EF's expectation.
        if (targetMemberType.IsAssignableFrom(typeof(IEnumerable<>).MakeGenericType(targetElement)))
        {
            result = selectCall;
            return true;
        }

        var toListMethod = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!
            .MakeGenericMethod(targetElement);
        var toList = Expression.Call(null, toListMethod, selectCall);

        if (targetMemberType.IsAssignableFrom(toList.Type))
        {
            result = toList;
            return true;
        }

        // Target is a more exotic collection type we can't assign the List<T> to; bail to
        // diagnostic path.
        return false;
    }

    private static Type? GetEnumerableElementType(Type type)
    {
        if (type == typeof(string)) return null; // don't treat strings as char collections

        if (type.IsArray) return type.GetElementType();

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(IEnumerable<>) || def == typeof(List<>) || def == typeof(IList<>)
                || def == typeof(ICollection<>) || def == typeof(IReadOnlyList<>)
                || def == typeof(IReadOnlyCollection<>))
            {
                return type.GetGenericArguments()[0];
            }
        }

        // Walk interfaces for custom collection types.
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return iface.GetGenericArguments()[0];
            }
        }

        return null;
    }

    private static Expression BuildDirectAccess(Expression originExpr, MemberInfo member)
        => Expression.MakeMemberAccess(originExpr, member);

    /// <summary>
    /// Emits a null-safe nested-member access expression for a chained provider. The chain is
    /// walked left-to-right; every reference-type or <see cref="Nullable{T}"/> intermediate is
    /// guarded by an explicit ternary so EF Core can translate the resulting tree to SQL.
    /// </summary>
    private static Expression BuildNullSafeChain(Expression originExpr, IReadOnlyList<MemberInfo> chain, Type targetMemberType)
    {
        // Walk the chain accumulating access expressions and null-check conditions. For a chain
        // [Customer, Address, City] produced from origin, the final access is
        //   origin.Customer.Address.City
        // Every intermediate value — `origin.Customer` and `origin.Customer.Address` — must be
        // null-checked so the leaf access is guarded. The origin expression itself is not
        // checked (LINQ-to-SQL sources never feed null rows); the leaf access is not checked
        // (it's the terminal). So the null-checkable intermediates are the results of each
        // access EXCEPT the last one.
        Expression current = originExpr;
        var nullChecks = new List<Expression>(chain.Count);
        for (var i = 0; i < chain.Count; i++)
        {
            current = Expression.MakeMemberAccess(current, chain[i]);

            // After the access, if this is an intermediate (not the leaf) and the value is a
            // reference type or Nullable<T>, record a null-check for it. Fences each subsequent
            // deeper access behind an explicit ternary so EF Core can translate the tree.
            if (i < chain.Count - 1 && IsNullCheckable(current.Type))
            {
                nullChecks.Add(Expression.Equal(current, Expression.Constant(null, current.Type)));
            }
        }

        if (nullChecks.Count == 0) return current;

        // Combine: if ANY intermediate is null, return default(leafType); else evaluate the full
        // chain. OR-chained null-checks are still SQL-translatable — EF emits a nested CASE.
        Expression guard = nullChecks[0];
        for (var i = 1; i < nullChecks.Count; i++)
        {
            guard = Expression.OrElse(guard, nullChecks[i]);
        }

        var defaultValue = Expression.Default(current.Type);
        return Expression.Condition(guard, defaultValue, current);
    }

    private static bool IsNullCheckable(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    /// <summary>
    /// Applies the minimum coercion to make <paramref name="expr"/> assignable to
    /// <paramref name="targetMemberType"/>. Only EF-translatable conversions are emitted —
    /// <see cref="Expression.Convert(Expression, Type)"/> for numeric / nullable reshaping,
    /// otherwise the value is left as-is (will surface as a diagnostic via
    /// <see cref="TryBuildValueExpression"/> if truly incompatible).
    /// </summary>
    private static Expression CoerceToTargetType(Expression expr, Type targetMemberType)
    {
        if (expr.Type == targetMemberType) return expr;

        // T → Nullable<T>: implicit conversion.
        var underlyingTarget = Nullable.GetUnderlyingType(targetMemberType);
        if (underlyingTarget is not null && underlyingTarget == expr.Type)
            return Expression.Convert(expr, targetMemberType);

        // Nullable<T> → T: emit `HasValue ? Value : default`.
        var underlyingSource = Nullable.GetUnderlyingType(expr.Type);
        if (underlyingSource is not null && underlyingSource == targetMemberType)
        {
            return Expression.Condition(
                Expression.Property(expr, "HasValue"),
                Expression.Property(expr, "Value"),
                Expression.Default(targetMemberType));
        }

        // Reference-type covariance or identity — no cast needed when assignable.
        if (targetMemberType.IsAssignableFrom(expr.Type)) return expr;

        // Generic numeric / primitive cast path (int → long, int → decimal, …) — EF translates.
        if (IsNumericOrPrimitive(expr.Type) && IsNumericOrPrimitive(targetMemberType))
            return Expression.Convert(expr, targetMemberType);

        // Last resort: emit an explicit Convert. EF may or may not translate; the caller's
        // diagnostic pipeline will capture any runtime translation failure.
        return Expression.Convert(expr, targetMemberType);
    }

    private static bool IsNumericOrPrimitive(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive || t == typeof(decimal);
    }

    private static Type GetMemberType(MemberInfo member) => member switch
    {
        PropertyInfo pi => pi.PropertyType,
        FieldInfo fi => fi.FieldType,
        _ => throw new InvalidOperationException($"Unsupported member kind: {member.MemberType}."),
    };

    private static string BuildUnsupportedProviderReason(PropertyLink link)
    {
        if (link.Transformer is not null)
        {
            return "Property has a custom ITypeTransformer attached; transformers are not EF-translatable " +
                   "in Sprint 8 projections and are deferred to Sprint 21.";
        }

        var providerTypeName = link.Provider.GetType().Name;
        return $"Provider '{providerTypeName}' is not EF-translatable — only PropertyAccessProvider and " +
               "ChainedPropertyAccessProvider produce pure expressions. The target member will use its default value.";
    }
}
