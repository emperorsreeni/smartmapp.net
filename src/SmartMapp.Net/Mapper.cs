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
    private readonly Func<object, MappingScope, object> _delegate;

    internal Mapper(ForgedSculptorConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        var pair = TypePair.Of<TOrigin, TTarget>();
        if (_config.TryGetBlueprint(pair) is null)
        {
            throw MappingExecutor.BuildUnknownPairException(_config, pair);
        }

        _delegate = MappingExecutor.GetOrCompile(_config, pair);
    }

    /// <inheritdoc />
    public TTarget Map(TOrigin origin)
    {
        if (origin is null) return default!;
        var scope = MappingExecutor.CreateScope(_config);
        return (TTarget)_delegate(origin, scope)!;
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
