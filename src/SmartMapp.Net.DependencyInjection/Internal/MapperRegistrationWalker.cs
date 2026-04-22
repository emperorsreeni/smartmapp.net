// SPDX-License-Identifier: MIT
// <copyright file="MapperRegistrationWalker.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Reflection;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Diagnostic helper that enumerates every <see cref="Blueprint"/> in a forged sculptor
/// configuration and produces the closed-generic <see cref="IMapper{TOrigin, TTarget}"/>
/// types that DI will resolve for each pair. Spec §S8-T03 Outputs call this the "walker".
/// </summary>
/// <remarks>
/// <para>
/// The DI package registers <c>IMapper&lt;,&gt;</c> as an open-generic service mapped to
/// <see cref="DependencyInjectionMapper{TOrigin, TTarget}"/>, so no per-pair descriptor is
/// strictly required for resolution — the open-generic registration covers every pair
/// automatically.
/// </para>
/// <para>
/// The walker nevertheless exists so that:
/// <list type="bullet">
///   <item><description>Tests can assert exactly which pairs are resolvable after a forge.</description></item>
///   <item><description>Future sprints (e.g. Sprint 16 keyed services, Sprint 18 insights endpoint) can enumerate registered pairs without touching the open-generic plumbing.</description></item>
///   <item><description>A <see cref="TryResolve"/> helper short-circuits the closed-generic construction costs on repeated probes via a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/> cache of <see cref="MethodInfo"/>s.</description></item>
/// </list>
/// </para>
/// </remarks>
internal static class MapperRegistrationWalker
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<TypePair, Type> PairTypeCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<TypePair, MethodInfo> CreateMethodCache = new();
    private static readonly MethodInfo CreateMethod =
        typeof(MapperFactory).GetMethod(
            nameof(MapperFactory.Create),
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
        ?? throw new InvalidOperationException("MapperFactory.Create<TOrigin, TTarget> not found.");

    /// <summary>
    /// Populates the supplied mapper cache with one <see cref="Mapper{TOrigin, TTarget}"/> per
    /// <see cref="Blueprint"/> in the forged configuration. Invoked exactly once per host by
    /// <see cref="ForgedSculptorHost.ForgeOnce"/> immediately after
    /// <see cref="SculptorBuilder.Forge"/> completes — fulfils spec §S8-T03 Constraints:
    /// "Registration runs inside the Singleton factory for the sculptor, exactly once".
    /// </summary>
    /// <param name="sculptor">The freshly forged sculptor.</param>
    /// <param name="cache">The <see cref="ForgedSculptorHost.MapperCache"/> to populate.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="sculptor"/> is not the built-in <see cref="Sculptor"/>.</exception>
    internal static void PopulateCache(
        ISculptor sculptor,
        System.Collections.Concurrent.ConcurrentDictionary<TypePair, object> cache)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        if (cache is null) throw new ArgumentNullException(nameof(cache));
        if (sculptor is not Sculptor concrete)
        {
            throw new InvalidOperationException(
                $"MapperRegistrationWalker requires the built-in SmartMapp.Net.Sculptor; got '{sculptor.GetType().FullName}'.");
        }

        var config = concrete.ForgedConfiguration;
        foreach (var blueprint in config.Blueprints)
        {
            var pair = blueprint.TypePair;
            // GetOrAdd so the method is idempotent — callers that re-run PopulateCache on the
            // same host (e.g. unit tests explicitly invoking the walker) see stable values.
            cache.GetOrAdd(pair, static (p, cfg) =>
            {
                var generic = CreateMethodCache.GetOrAdd(p, static pp => CreateMethod.MakeGenericMethod(pp.OriginType, pp.TargetType));
                return generic.Invoke(null, new object[] { cfg })!;
            }, config);
        }
    }

    /// <summary>
    /// Enumerates every blueprint in the forged configuration and yields the closed-generic
    /// <see cref="IMapper{TOrigin, TTarget}"/> type that will be resolvable from DI for that pair.
    /// </summary>
    /// <param name="sculptor">The forged sculptor — must be the built-in <see cref="Sculptor"/> implementation.</param>
    /// <returns>A lazily-enumerated sequence of (pair, resolvable <c>IMapper&lt;,&gt;</c> closed-generic type) tuples.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sculptor"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="sculptor"/> is not the built-in <see cref="Sculptor"/>.</exception>
    internal static IEnumerable<(TypePair Pair, Type MapperServiceType)> EnumerateResolvablePairs(ISculptor sculptor)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        if (sculptor is not Sculptor concrete)
        {
            throw new InvalidOperationException(
                $"MapperRegistrationWalker requires the built-in SmartMapp.Net.Sculptor; got '{sculptor.GetType().FullName}'.");
        }

        foreach (var blueprint in concrete.ForgedConfiguration.Blueprints)
        {
            var pair = blueprint.TypePair;
            yield return (pair, GetOrMakeClosedMapperType(pair));
        }
    }

    /// <summary>
    /// Attempts to build a closed-generic <see cref="Mapper{TOrigin, TTarget}"/> for the specified pair
    /// via reflection over <see cref="MapperFactory.Create{TOrigin, TTarget}"/>. Used by tests to
    /// verify the walker-produced types are actually constructible.
    /// </summary>
    /// <param name="sculptor">The forged sculptor whose configuration backs the mapper.</param>
    /// <param name="pair">The type pair to resolve.</param>
    /// <param name="mapper">The resolved mapper instance, or <c>null</c> when the pair is not registered.</param>
    /// <returns><c>true</c> when a mapper was produced; <c>false</c> when the pair is unregistered.</returns>
    internal static bool TryResolve(ISculptor sculptor, TypePair pair, out object? mapper)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        if (sculptor is not Sculptor concrete)
        {
            throw new InvalidOperationException(
                $"MapperRegistrationWalker requires the built-in SmartMapp.Net.Sculptor; got '{sculptor.GetType().FullName}'.");
        }

        var config = concrete.ForgedConfiguration;
        if (config.TryGetBlueprint(pair) is null)
        {
            mapper = null;
            return false;
        }

        var closed = CreateMethod.MakeGenericMethod(pair.OriginType, pair.TargetType);
        mapper = closed.Invoke(null, new object[] { config });
        return mapper is not null;
    }

    /// <summary>
    /// Returns the closed-generic <see cref="IMapper{TOrigin, TTarget}"/> type for the supplied
    /// pair. Cached to avoid repeated <see cref="Type.MakeGenericType(Type[])"/> cost.
    /// </summary>
    internal static Type GetOrMakeClosedMapperType(TypePair pair)
        => PairTypeCache.GetOrAdd(pair, static p =>
            typeof(IMapper<,>).MakeGenericType(p.OriginType, p.TargetType));
}
