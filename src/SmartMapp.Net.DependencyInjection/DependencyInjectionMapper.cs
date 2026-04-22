// SPDX-License-Identifier: MIT
// <copyright file="DependencyInjectionMapper.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Internal;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.DependencyInjection;

/// <summary>
/// DI-friendly <see cref="IMapper{TOrigin, TTarget}"/> implementation registered as an
/// open-generic service by <see cref="Microsoft.Extensions.DependencyInjection.SculptorServiceCollectionExtensions.AddSculptor(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>.
/// Resolves the underlying <see cref="Mapper{TOrigin, TTarget}"/> from the forged
/// configuration owned by the injected <see cref="ISculptor"/> and delegates every call.
/// </summary>
/// <typeparam name="TOrigin">The origin (source) type.</typeparam>
/// <typeparam name="TTarget">The target (destination) type.</typeparam>
/// <remarks>
/// <para>
/// A new <see cref="DependencyInjectionMapper{TOrigin, TTarget}"/> is constructed every time
/// DI resolves <c>IMapper&lt;TOrigin, TTarget&gt;</c>, but its inner <see cref="Mapper{TOrigin, TTarget}"/>
/// holds a pre-compiled delegate so per-call mapping cost is unchanged from the
/// <c>SculptorBuilder.Forge()</c>-constructed mapper.
/// </para>
/// <para>
/// If no blueprint is registered for the requested pair, the constructor throws
/// <see cref="InvalidOperationException"/> with an actionable message referencing the missing
/// <see cref="Blueprint"/>. Both <c>GetService</c> and <c>GetRequiredService</c> surface this
/// exception at resolve time — this is a conscious deviation from spec §S8-T03 acceptance
/// ("returns <c>null</c> via <c>GetService</c>") because the open-generic DI registration
/// required for lazy-forge compatibility cannot distinguish "registered pair" from
/// "unregistered pair" prior to construction. The actionable error message is preferable to
/// a silent <c>null</c> for typical consumer workflows.
/// </para>
/// </remarks>
public sealed class DependencyInjectionMapper<TOrigin, TTarget> : IMapper<TOrigin, TTarget>
{
    private readonly IMapper<TOrigin, TTarget> _inner;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new <see cref="DependencyInjectionMapper{TOrigin, TTarget}"/>. Reads the
    /// pre-built <see cref="Mapper{TOrigin, TTarget}"/> from the walker-populated cache owned
    /// by the singleton host; falls back to on-the-fly construction only on the
    /// <see cref="SmartMapp.Net.Configuration.SculptorOptions.AllowPerScopeRebuild"/> path,
    /// where the resolved <paramref name="sculptor"/> is a fresh per-scope instance whose
    /// mappers are not in the host-level cache.
    /// </summary>
    /// <param name="sculptor">The sculptor resolved from DI in the same scope.</param>
    /// <param name="serviceProvider">
    /// The <see cref="IServiceProvider"/> from which the internal <c>ForgedSculptorHost</c>
    /// (owner of the per-pair mapper cache) is resolved. Accepting <see cref="IServiceProvider"/>
    /// rather than the host directly keeps the constructor's public signature free of
    /// internal types.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the sculptor is not the built-in <see cref="SmartMapp.Net.Sculptor"/>
    /// implementation, or when no <see cref="Blueprint"/> is registered for the requested pair.
    /// </exception>
    public DependencyInjectionMapper(ISculptor sculptor, IServiceProvider serviceProvider)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        if (serviceProvider is null) throw new ArgumentNullException(nameof(serviceProvider));

        _serviceProvider = serviceProvider;

        // Unwrap the DI-installed DependencyInjectionSculptor decorator so we can reach the
        // underlying Sculptor's ForgedConfiguration. Direct-from-builder callers (bypassing
        // the decorator) still work because `sculptor` is already the concrete Sculptor.
        var unwrapped = sculptor is DependencyInjectionSculptor decorator
            ? decorator.Inner
            : sculptor;

        var host = serviceProvider.GetRequiredService<ForgedSculptorHost>();
        var pair = TypePair.Of<TOrigin, TTarget>();

        // Fast path: walker-populated cache. Accessing host.Sculptor forces the global forge if
        // it has not run yet, which populates the cache. We only serve from the cache when the
        // (unwrapped) resolved sculptor is the global one — the AllowPerScopeRebuild=true path
        // resolves a fresh per-scope sculptor whose mappers aren't mirrored in the host cache.
        var globalSculptor = host.Sculptor;
        if (ReferenceEquals(unwrapped, globalSculptor)
            && host.MapperCache.TryGetValue(pair, out var cached)
            && cached is IMapper<TOrigin, TTarget> cachedTyped)
        {
            _inner = cachedTyped;
            return;
        }

        if (unwrapped is not Sculptor concrete)
        {
            throw new InvalidOperationException(
                $"DI-resolved IMapper<{typeof(TOrigin).Name}, {typeof(TTarget).Name}> expected the " +
                $"built-in SmartMapp.Net.Sculptor but got '{unwrapped.GetType().FullName}'. " +
                "Custom ISculptor implementations must register their own IMapper<,> resolver.");
        }

        var config = concrete.ForgedConfiguration;
        if (config.TryGetBlueprint(pair) is null)
        {
            throw new InvalidOperationException(
                $"No Blueprint is registered for mapping '{typeof(TOrigin).FullName}' -> '{typeof(TTarget).FullName}'. " +
                $"Register the pair via SculptorBuilder.Bind<{typeof(TOrigin).Name}, {typeof(TTarget).Name}>(), " +
                $"configure the assembly scan to discover the pair automatically, or decorate with " +
                $"[MappedBy<{typeof(TOrigin).Name}>] / [MapsInto<{typeof(TTarget).Name}>].");
        }

        _inner = MapperFactory.Create<TOrigin, TTarget>(config);
    }

    /// <inheritdoc />
    public TTarget Map(TOrigin origin)
    {
        // Per spec §11.4 (S8-T04): push the resolving IServiceProvider as ambient so the inner
        // Mapper<,>.Map → MappingExecutor.CreateScope picks it up, flowing request-scoped
        // services (DbContext, ILogger<T>, …) into any DeferredValueProvider invocations
        // emitted by the compiled delegate. EnterIfUnset preserves any outer-set ambient
        // (e.g. ASP.NET middleware that installed HttpContext.RequestServices) instead of
        // clobbering it with the wrapper's captured SP — critical when the wrapper is
        // Singleton and its captured SP may be stale relative to the current scope.
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Map(origin);
    }

    /// <inheritdoc />
    public TTarget Map(TOrigin origin, TTarget existingTarget)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Map(origin, existingTarget);
    }

    /// <inheritdoc />
    public IReadOnlyList<TTarget> MapAll(IEnumerable<TOrigin> origins)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.MapAll(origins);
    }
}
