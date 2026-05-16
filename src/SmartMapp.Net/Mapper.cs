using SmartMapp.Net.Caching;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net;

/// <summary>
/// Strongly-typed, allocation-free mapper for a single <c>(TOrigin, TTarget)</c> pair.
/// The compiled delegate is resolved at construction and reused on every <see cref="Map(TOrigin)"/>
/// invocation, skipping the dictionary lookup performed by <see cref="ISculptor.Map{TOrigin,TTarget}(TOrigin)"/>.
/// </summary>
/// <typeparam name="TOrigin">The source type.</typeparam>
/// <typeparam name="TTarget">The destination type.</typeparam>
public sealed class Mapper<TOrigin, TTarget> : IMapper<TOrigin, TTarget>
{
    private readonly ForgedSculptorConfiguration _config;

    // Sprint 8 RC fast path: cached delegate when adaptive promotion is OFF (CompiledOnly /
    // EmitFirst / EmitOnly). Zero overhead vs Sprint 8 baseline.
    private readonly Func<object, MappingScope, object>? _delegate;

    // Sprint 9 · S9-T08 slot-aware path: when adaptive promotion is ON we hold the swappable
    // DelegateSlot and re-read it per call so the IL-emitted replacement (published by the
    // promotion worker) becomes visible immediately.
    private readonly DelegateSlot? _slot;
    private readonly TypePair _pair;

    internal Mapper(ForgedSculptorConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        _pair = TypePair.Of<TOrigin, TTarget>();
        if (_config.TryGetBlueprint(_pair) is null)
        {
            throw MappingExecutor.BuildUnknownPairException(_config, _pair);
        }

        if (_config.Options.Strategy.Mode == Configuration.StrategyMode.Adaptive)
        {
            _slot = MappingExecutor.GetSlot(_config, _pair);
            _delegate = null;
        }
        else
        {
            _delegate = MappingExecutor.GetOrCompile(_config, _pair);
            _slot = null;
        }
    }

    /// <inheritdoc />
    public TTarget Map(TOrigin origin)
    {
        if (origin is null) return default!;
        var scope = MappingExecutor.CreateScope(_config);
        var del = _slot is null ? _delegate! : _slot.Current!;
        var result = (TTarget)del(origin, scope)!;
        _config.AdaptivePromotion?.Observe(_pair);
        return result;
    }

    /// <inheritdoc />
    public TTarget Map(TOrigin origin, TTarget existingTarget)
    {
        // Existing-target mapping is delivered in Sprint 14; Sprint 7 performs a fresh map
        // and returns the result. Documented limitation.
        _ = existingTarget;
        return Map(origin);
    }

    /// <inheritdoc />
    public IReadOnlyList<TTarget> MapAll(IEnumerable<TOrigin> origins)
    {
        if (origins is null) throw new ArgumentNullException(nameof(origins));

        var list = origins is ICollection<TOrigin> coll
            ? new List<TTarget>(coll.Count)
            : new List<TTarget>();

        foreach (var origin in origins)
        {
            list.Add(Map(origin));
        }
        return list;
    }
}
