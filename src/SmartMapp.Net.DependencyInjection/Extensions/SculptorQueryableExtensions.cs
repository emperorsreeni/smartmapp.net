// SPDX-License-Identifier: MIT
// <copyright file="SculptorQueryableExtensions.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Linq.Expressions;
using System.Reflection;
using SmartMapp.Net.Extensions;

namespace SmartMapp.Net.DependencyInjection.Extensions;

/// <summary>
/// <see cref="IQueryable"/> extensions that plug a SmartMapp.Net projection into LINQ providers
/// (notably EF Core) so consumers can write <c>dbContext.Orders.SelectAs&lt;OrderDto&gt;(sculptor)</c>
/// and have the mapping translate to a single SQL <c>SELECT</c> — per spec §8.10 and
/// Sprint 8 · S8-T06.
/// </summary>
/// <remarks>
/// <para>
/// All overloads delegate to <see cref="ISculptor.GetProjection{TOrigin, TTarget}"/> and then to
/// <see cref="Queryable.Select{TSource, TResult}(IQueryable{TSource}, Expression{Func{TSource, TResult}})"/>.
/// The resulting <see cref="IQueryable{TTarget}"/> is still deferred — no query runs until the
/// caller materialises with <c>ToList</c> / <c>ToListAsync</c> / <c>First</c>.
/// </para>
/// </remarks>
public static class SculptorQueryableExtensions
{
    // Cached MethodInfo lookup for the strongly-typed SelectAs<TOrigin, TTarget> entry point
    // so the runtime-typed overload can forward without re-scanning the method table. Pick the
    // 2-type-parameter variant specifically — there are three SelectAs overloads on this class.
    private static readonly MethodInfo SelectAsGenericMethod =
        typeof(SculptorQueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(SelectAs)
                      && m.IsGenericMethodDefinition
                      && m.GetGenericArguments().Length == 2
                      && m.GetParameters().Length == 2);

    /// <summary>
    /// Projects an <see cref="IQueryable{TOrigin}"/> into an <see cref="IQueryable{TTarget}"/>
    /// using the supplied <paramref name="sculptor"/>'s pre-built projection expression.
    /// </summary>
    /// <typeparam name="TOrigin">The source entity type (inferred from <paramref name="source"/>).</typeparam>
    /// <typeparam name="TTarget">The destination DTO type.</typeparam>
    /// <param name="source">The source queryable (e.g. <c>DbContext.Orders</c>).</param>
    /// <param name="sculptor">The forged <see cref="ISculptor"/>.</param>
    /// <returns>A deferred <see cref="IQueryable{TTarget}"/> translatable to SQL.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
    public static IQueryable<TTarget> SelectAs<TOrigin, TTarget>(this IQueryable<TOrigin> source, ISculptor sculptor)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));

        var projection = sculptor.GetProjection<TOrigin, TTarget>();
        return source.Select(projection);
    }

    /// <summary>
    /// Target-type-only overload: infers <c>TOrigin</c> from the queryable's element type so
    /// callers can write <c>db.Orders.SelectAs&lt;OrderDto&gt;(sculptor)</c> without restating
    /// the source type.
    /// </summary>
    /// <typeparam name="TTarget">The destination DTO type.</typeparam>
    /// <param name="source">The non-generic source queryable — <see cref="IQueryable.ElementType"/> is inspected for the origin type.</param>
    /// <param name="sculptor">The forged <see cref="ISculptor"/>.</param>
    /// <returns>A deferred <see cref="IQueryable{TTarget}"/> translatable to SQL.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
    public static IQueryable<TTarget> SelectAs<TTarget>(this IQueryable source, ISculptor sculptor)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));

        // Delegate through ISculptor.SelectAs which already handles the runtime-typed generic
        // method construction (and caches the projection on ForgedSculptorConfiguration).
        return sculptor.SelectAs<TTarget>(source);
    }

    /// <summary>
    /// Ambient overload (S8-T07): resolves <see cref="ISculptor"/> from
    /// <see cref="SculptorAmbient.Current"/>, which is installed by
    /// <c>SculptorServiceCollectionExtensions.AddSculptor</c> during the sculptor's DI
    /// registration. Zero-argument form — spec §14.5.
    /// </summary>
    /// <typeparam name="TTarget">The destination DTO type.</typeparam>
    /// <param name="source">The non-generic source queryable.</param>
    /// <returns>A deferred <see cref="IQueryable{TTarget}"/> translatable to SQL.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no ambient <see cref="ISculptor"/> is installed.</exception>
    public static IQueryable<TTarget> SelectAs<TTarget>(this IQueryable source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        var sculptor = SculptorAmbient.Current
            ?? throw new InvalidOperationException(
                "No ambient ISculptor is available. Call services.AddSculptor() to register one " +
                "(which installs the ambient accessor), push a scoped override via SculptorAmbient.Set(sculptor), " +
                "or use the explicit SelectAs<TTarget>(sculptor) overload.");
        return sculptor.SelectAs<TTarget>(source);
    }

    /// <summary>
    /// Fully runtime-typed overload used when both <c>TOrigin</c> and <c>TTarget</c> are only
    /// known as <see cref="Type"/> values (e.g. OData / GraphQL dispatch layers). Returns a
    /// non-generic <see cref="IQueryable"/> whose <see cref="IQueryable.ElementType"/> is
    /// <paramref name="targetType"/>.
    /// </summary>
    /// <param name="source">The source queryable.</param>
    /// <param name="sculptor">The forged <see cref="ISculptor"/>.</param>
    /// <param name="targetType">The destination type; must be a concrete type with a public parameterless constructor.</param>
    /// <returns>A deferred <see cref="IQueryable"/> of <paramref name="targetType"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is <c>null</c>.</exception>
    public static IQueryable SelectAs(this IQueryable source, ISculptor sculptor, Type targetType)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        if (targetType is null) throw new ArgumentNullException(nameof(targetType));

        var originType = source.ElementType;
        var closed = SelectAsGenericMethod.MakeGenericMethod(originType, targetType);

        // Upgrade the non-generic IQueryable to IQueryable<TOrigin> via reflection so the
        // strongly-typed overload above can accept it — Queryable.Cast<T> produces exactly that.
        var castMethod = typeof(Queryable).GetMethod(nameof(Queryable.Cast))!
            .MakeGenericMethod(originType);
        var typedSource = castMethod.Invoke(null, new object[] { source })!;

        return (IQueryable)closed.Invoke(null, new object[] { typedSource, sculptor })!;
    }
}
