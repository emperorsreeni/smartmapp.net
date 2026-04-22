using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net;

/// <summary>
/// Primary <see cref="ISculptor"/> implementation. Holds the immutable
/// <see cref="ForgedSculptorConfiguration"/> produced by <see cref="SculptorBuilder.Forge"/>
/// and dispatches every API call to the appropriate Sprint 4/5/6 compilation artefact.
/// Thread-safe after construction.
/// </summary>
public sealed class Sculptor : ISculptor, ISculptorConfiguration
{
    private readonly ForgedSculptorConfiguration _config;

    internal Sculptor(ForgedSculptorConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Gets the read-only configuration view of this sculptor.
    /// </summary>
    public ISculptorConfiguration Configuration => this;

    /// <summary>
    /// Gets the internal <see cref="ForgedSculptorConfiguration"/> backing this sculptor.
    /// Exposed to first-party consumers (tests, benchmarks,
    /// <c>SmartMapp.Net.DependencyInjection</c>) so they can construct
    /// <see cref="Mapper{TOrigin,TTarget}"/> instances directly via
    /// <see cref="MapperFactory.Create{TOrigin,TTarget}"/> without a double dispatch through
    /// <see cref="ISculptor"/>.
    /// </summary>
    internal ForgedSculptorConfiguration ForgedConfiguration => _config;

    // ---------------- ISculptor ----------------

    /// <inheritdoc />
    public TTarget Map<TOrigin, TTarget>(TOrigin origin)
    {
        if (origin is null) return default!;
        var del = CachedDelegate<TOrigin, TTarget>.GetOrResolve(_config);
        var scope = MappingExecutor.CreateScope(_config);
        return (TTarget)del(origin!, scope)!;
    }

    /// <inheritdoc />
    public TTarget Map<TOrigin, TTarget>(TOrigin origin, TTarget existingTarget)
    {
        _ = existingTarget; // Existing-target path delivered in Sprint 14.
        return Map<TOrigin, TTarget>(origin);
    }

    /// <inheritdoc />
    public object Map(object origin, Type originType, Type targetType)
    {
        if (origin is null) throw new ArgumentNullException(nameof(origin));
        var pair = new TypePair(originType, targetType);
        var del = MappingExecutor.GetOrCompile(_config, pair);
        var scope = MappingExecutor.CreateScope(_config);
        return del(origin, scope);
    }

    /// <inheritdoc />
    public object Map(object origin, object target, Type originType, Type targetType)
    {
        _ = target;
        return Map(origin, originType, targetType);
    }

    /// <inheritdoc />
    public IReadOnlyList<TTarget> MapAll<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        if (origins is null) throw new ArgumentNullException(nameof(origins));

        var del = CachedDelegate<TOrigin, TTarget>.GetOrResolve(_config);
        var scope = MappingExecutor.CreateScope(_config);

        var list = origins is ICollection<TOrigin> coll
            ? new List<TTarget>(coll.Count)
            : new List<TTarget>();

        foreach (var origin in origins)
        {
            if (origin is null) { list.Add(default!); continue; }
            list.Add((TTarget)del(origin!, scope)!);
        }
        return list;
    }

    /// <inheritdoc />
    public TTarget[] MapToArray<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        var list = MapAll<TOrigin, TTarget>(origins);
        var array = new TTarget[list.Count];
        for (var i = 0; i < list.Count; i++) array[i] = list[i];
        return array;
    }

    /// <inheritdoc />
    public IEnumerable<TTarget> MapLazy<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        if (origins is null) throw new ArgumentNullException(nameof(origins));
        return MapLazyIterator<TOrigin, TTarget>(origins);
    }

    private IEnumerable<TTarget> MapLazyIterator<TOrigin, TTarget>(IEnumerable<TOrigin> origins)
    {
        var del = CachedDelegate<TOrigin, TTarget>.GetOrResolve(_config);
        var scope = MappingExecutor.CreateScope(_config);
        foreach (var origin in origins)
        {
            if (origin is null) { yield return default!; continue; }
            yield return (TTarget)del(origin!, scope)!;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TTarget> MapStream<TOrigin, TTarget>(
        IAsyncEnumerable<TOrigin> origins,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (origins is null) throw new ArgumentNullException(nameof(origins));

        var del = CachedDelegate<TOrigin, TTarget>.GetOrResolve(_config);
        var scope = MappingExecutor.CreateScope(_config);

        await foreach (var origin in origins.WithCancellation(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            if (origin is null) { yield return default!; continue; }
            yield return (TTarget)del(origin!, scope)!;
        }
    }

    /// <inheritdoc />
    public TTarget Compose<TTarget>(params object[] origins)
    {
        if (origins is null) throw new ArgumentNullException(nameof(origins));
        if (origins.Length == 0)
            throw new ArgumentException("Compose requires at least one origin.", nameof(origins));

        // Single-origin Compose reduces to plain Map (spec §S8-T08 Acceptance bullet 6) when no
        // explicit composition blueprint is registered for the target. If the caller DID declare
        // a composition for this target, we honour the declaration (single-origin flows through
        // CompositionDispatcher with one slot filled).
        if (origins.Length == 1 && _config.TryGetCompositionBlueprint(typeof(TTarget)) is null)
        {
            var single = origins[0];
            if (single is null) throw new ArgumentNullException(nameof(origins), "Compose cannot accept a single null origin.");
            return (TTarget)Map(single, single.GetType(), typeof(TTarget));
        }

        // Sprint 8 · S8-T08: runtime dispatch across 1–N origins via CompositionDispatcher. It
        // matches caller instances to declared origin slots, executes each origin's partial
        // blueprint, and merges the contributed members into a single target.
        return CompositionDispatcher.Dispatch<TTarget>(_config, origins);
    }

    /// <inheritdoc />
    public IQueryable<TTarget> SelectAs<TTarget>(IQueryable source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        var originType = source.ElementType;
        var projection = GetProjectionExpression(originType, typeof(TTarget));

        var queryable = typeof(Queryable);
        var selectMethod = queryable.GetMethods()
            .First(m => m.Name == nameof(Queryable.Select)
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType.GenericTypeArguments[0].GenericTypeArguments.Length == 2)
            .MakeGenericMethod(originType, typeof(TTarget));

        return (IQueryable<TTarget>)selectMethod.Invoke(null, new object[] { source, projection })!;
    }

    /// <inheritdoc />
    public Expression<Func<TOrigin, TTarget>> GetProjection<TOrigin, TTarget>()
    {
        var expr = GetProjectionExpression(typeof(TOrigin), typeof(TTarget));
        return (Expression<Func<TOrigin, TTarget>>)expr;
    }

    private LambdaExpression GetProjectionExpression(Type originType, Type targetType)
    {
        // S8-T06: delegate to SculptorProjectionBuilder which emits pure MemberInit expressions
        // translatable by IQueryable providers (EF Core 8+). ProjectionCache in
        // ForgedSculptorConfiguration memoises the result — the same Expression instance is
        // returned on every subsequent request for the pair.
        var pair = new TypePair(originType, targetType);
        return SculptorProjectionBuilder.BuildOrGet(_config, pair);
    }

    /// <inheritdoc />
    public MappingInspection Inspect<TOrigin, TTarget>()
    {
        var pair = TypePair.Of<TOrigin, TTarget>();
        return _config.InspectionCache.GetOrAdd(pair, p =>
        {
            var bp = _config.TryGetBlueprint(p)
                ?? throw new MappingConfigurationException(
                    $"Cannot inspect: no blueprint registered for type pair '{p}'.", p);
            return MappingInspection.Build(bp);
        });
    }

    /// <inheritdoc />
    public MappingAtlas GetMappingAtlas()
    {
        var cached = _config.CachedAtlas;
        if (cached is not null) return cached;

        var atlas = MappingAtlas.Build(_config.Blueprints);
        _config.CachedAtlas = atlas;
        return atlas;
    }

    /// <summary>
    /// Resolves an <see cref="IMapper{TOrigin,TTarget}"/> for the specified pair.
    /// Useful outside DI contexts.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>A strongly-typed mapper.</returns>
    public IMapper<TOrigin, TTarget> GetMapper<TOrigin, TTarget>()
        => MapperFactory.Create<TOrigin, TTarget>(_config);

    // ---------------- ISculptorConfiguration ----------------

    IReadOnlyList<Blueprint> ISculptorConfiguration.GetAllBlueprints() => _config.Blueprints;

    Blueprint? ISculptorConfiguration.GetBlueprint<TOrigin, TTarget>()
        => _config.TryGetBlueprint(TypePair.Of<TOrigin, TTarget>());

    void ISculptorConfiguration.Validate()
    {
        var result = ((ISculptorConfiguration)this).ValidateConfiguration();
        if (!result.IsValid)
            throw new BlueprintValidationException(result.Inner);
    }

    bool ISculptorConfiguration.HasBinding<TOrigin, TTarget>()
        => _config.TryGetBlueprint(TypePair.Of<TOrigin, TTarget>()) is not null;

    Blueprint? ISculptorConfiguration.GetBlueprint(Type originType, Type targetType)
        => _config.TryGetBlueprint(new TypePair(originType, targetType));

    bool ISculptorConfiguration.HasBinding(Type originType, Type targetType)
        => _config.TryGetBlueprint(new TypePair(originType, targetType)) is not null;

    IReadOnlyDictionary<TypePair, Blueprint> ISculptorConfiguration.GetAllBlueprintsByPair()
        => _config.AsDictionary();

    ValidationResult ISculptorConfiguration.ValidateConfiguration()
    {
        var cached = _config.CachedValidation;
        if (cached is not null) return cached;

        var validator = new BlueprintValidator(_config.TypeModelCache);
        var inner = validator.Validate(
            _config.Blueprints,
            configs: null,
            strictMode: _config.Options.StrictMode);

        var wrapped = new ValidationResult(inner);
        _config.CachedValidation = wrapped;
        return wrapped;
    }

    /// <summary>
    /// Generic-static cache holding the compiled delegate for a specific <c>(TOrigin, TTarget)</c>
    /// closure, keyed by the forged configuration instance. Avoids the dictionary lookup on the
    /// hot path and guarantees zero allocation for the identity lookup.
    /// </summary>
    private static class CachedDelegate<TOrigin, TTarget>
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<
            ForgedSculptorConfiguration, StrongBox> Table = new();

        internal static Func<object, MappingScope, object> GetOrResolve(ForgedSculptorConfiguration config)
        {
            var box = Table.GetValue(config, c => new StrongBox
            {
                Delegate = MappingExecutor.GetOrCompile(c, TypePair.Of<TOrigin, TTarget>()),
            });
            return box.Delegate;
        }

        private sealed class StrongBox
        {
            internal required Func<object, MappingScope, object> Delegate { get; init; }
        }
    }
}
