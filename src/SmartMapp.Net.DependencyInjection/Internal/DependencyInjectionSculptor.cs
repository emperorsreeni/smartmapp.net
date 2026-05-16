// SPDX-License-Identifier: MIT
// <copyright file="DependencyInjectionSculptor.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Linq.Expressions;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// <see cref="ISculptor"/> decorator registered by the DI container. Forwards every call to
/// the inner sculptor while pushing the ambient <see cref="IServiceProvider"/> via
/// <see cref="ServiceProviderAmbientAccessor"/> so deferred value-provider and transformer
/// resolution in the mapping pipeline sees the request-scoped services (e.g. <c>DbContext</c>,
/// <c>ILogger&lt;T&gt;</c>) — per spec §11.4 / S8-T04.
/// </summary>
/// <remarks>
/// <para>
/// The ambient slot is cleared in a <c>finally</c> on every method exit so no request scope
/// leaks across awaits. For async streams (<see cref="MapStream{TOrigin, TTarget}"/>) and
/// lazy enumerables (<see cref="MapLazy{TOrigin, TTarget}"/>) the ambient slot is captured
/// up front and released only when the enumeration completes or the consumer disposes.
/// </para>
/// <para>
/// This wrapper intentionally adds <b>zero</b> work on the hot path when no providers are
/// used: pushing the ambient <see cref="IServiceProvider"/> is a single <c>AsyncLocal</c>
/// swap, and <see cref="SmartMapp.Net.Abstractions.IValueProvider"/> and
/// <see cref="SmartMapp.Net.Abstractions.ITypeTransformer"/> activations only query the
/// ambient slot when a deferred value-provider placeholder is actually executed by the
/// compiled delegate.
/// </para>
/// </remarks>
internal sealed class DependencyInjectionSculptor : ISculptor, ISculptorConfiguration
{
    private readonly ISculptor _inner;
    private readonly ISculptorConfiguration _innerConfig;
    private readonly IServiceProvider _serviceProvider;

    internal DependencyInjectionSculptor(ISculptor inner, IServiceProvider serviceProvider)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        // The built-in Sculptor implements both ISculptor and ISculptorConfiguration, so user
        // code that casts an ISculptor to ISculptorConfiguration continues to work through
        // the decorator — important for Sprint 7 tests that bypass GetRequiredService<ISculptorConfiguration>.
        _innerConfig = inner as ISculptorConfiguration
            ?? throw new InvalidOperationException(
                $"The underlying sculptor '{inner.GetType().FullName}' does not implement ISculptorConfiguration. " +
                "DependencyInjectionSculptor requires the built-in SmartMapp.Net.Sculptor.");
    }

    /// <summary>
    /// Exposes the underlying sculptor. Used by <c>DependencyInjectionMapper&lt;,&gt;</c> so the
    /// mapper can unwrap to the concrete <see cref="Sculptor"/> for
    /// <c>ForgedConfiguration</c> access.
    /// </summary>
    internal ISculptor Inner => _inner;

    /// <inheritdoc />
    public TTarget Map<TOrigin, TTarget>(TOrigin origin)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Map<TOrigin, TTarget>(origin);
    }

    /// <inheritdoc />
    public TTarget Map<TOrigin, TTarget>(TOrigin origin, TTarget existingTarget)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Map(origin, existingTarget);
    }

    /// <inheritdoc />
    public object Map(object origin, Type originType, Type targetType)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Map(origin, originType, targetType);
    }

    /// <inheritdoc />
    public object Map(object origin, object target, Type originType, Type targetType)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Map(origin, target, originType, targetType);
    }

    /// <inheritdoc />
    public IReadOnlyList<TTarget> MapAll<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.MapAll<TOrigin, TTarget>(origins);
    }

    /// <inheritdoc />
    public TTarget[] MapToArray<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.MapToArray<TOrigin, TTarget>(origins);
    }

    /// <inheritdoc />
    public IEnumerable<TTarget> MapLazy<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        // Deferred enumerable: the caller may pull items long after this method returns,
        // possibly on a different async context. Install ambient SP per-pull so the scope
        // flows into each MoveNext().
        return MapLazyCore<TOrigin, TTarget>(origins);
    }

    private IEnumerable<TTarget> MapLazyCore<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        foreach (var item in _inner.MapLazy<TOrigin, TTarget>(origins))
        {
            using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
            yield return item;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TTarget> MapStream<TOrigin, TTarget>(
        IAsyncEnumerable<TOrigin> origins,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in _inner.MapStream<TOrigin, TTarget>(origins, ct).ConfigureAwait(false))
        {
            using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
            yield return item;
        }
    }

    /// <inheritdoc />
    public TTarget Compose<TTarget>(params object[] origins)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.Compose<TTarget>(origins);
    }

    /// <inheritdoc />
    public IQueryable<TTarget> SelectAs<TTarget>(IQueryable source)
    {
        using var _ = ServiceProviderAmbientAccessor.EnterIfUnset(_serviceProvider);
        return _inner.SelectAs<TTarget>(source);
    }

    /// <inheritdoc />
    public Expression<Func<TOrigin, TTarget>> GetProjection<TOrigin, TTarget>()
        => _inner.GetProjection<TOrigin, TTarget>();

    /// <inheritdoc />
    public MappingInspection Inspect<TOrigin, TTarget>()
        => _inner.Inspect<TOrigin, TTarget>();

    /// <inheritdoc />
    public MappingAtlas GetMappingAtlas()
        => _inner.GetMappingAtlas();

    // ---------------- ISculptorConfiguration (pass-through) ----------------
    // The underlying Sculptor implements both interfaces; the decorator preserves that dual
    // behaviour so callers that cast an ISculptor to ISculptorConfiguration (a Sprint 7
    // pattern) continue to work through the DI wrapper.

    /// <inheritdoc />
    IReadOnlyList<Blueprint> ISculptorConfiguration.GetAllBlueprints()
        => _innerConfig.GetAllBlueprints();

    /// <inheritdoc />
    IReadOnlyDictionary<TypePair, Blueprint> ISculptorConfiguration.GetAllBlueprintsByPair()
        => _innerConfig.GetAllBlueprintsByPair();

    /// <inheritdoc />
    Blueprint? ISculptorConfiguration.GetBlueprint<TOrigin, TTarget>()
        where TTarget : default
        => _innerConfig.GetBlueprint<TOrigin, TTarget>();

    /// <inheritdoc />
    Blueprint? ISculptorConfiguration.GetBlueprint(Type originType, Type targetType)
        => _innerConfig.GetBlueprint(originType, targetType);

    /// <inheritdoc />
    void ISculptorConfiguration.Validate() => _innerConfig.Validate();

    /// <inheritdoc />
    ValidationResult ISculptorConfiguration.ValidateConfiguration()
        => _innerConfig.ValidateConfiguration();

    /// <inheritdoc />
    bool ISculptorConfiguration.HasBinding<TOrigin, TTarget>()
        => _innerConfig.HasBinding<TOrigin, TTarget>();

    /// <inheritdoc />
    bool ISculptorConfiguration.HasBinding(Type originType, Type targetType)
        => _innerConfig.HasBinding(originType, targetType);
}
