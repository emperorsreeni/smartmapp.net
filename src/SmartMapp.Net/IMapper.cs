namespace SmartMapp.Net;

/// <summary>
/// Strongly-typed mapping interface for a specific <c>(TOrigin, TTarget)</c> pair.
/// Resolved from DI as <c>IMapper&lt;Order, OrderDto&gt;</c>.
/// </summary>
/// <typeparam name="TOrigin">The source type.</typeparam>
/// <typeparam name="TTarget">The destination type.</typeparam>
public interface IMapper<in TOrigin, TTarget>
{
    /// <summary>
    /// Maps an origin object into a new instance of <typeparamref name="TTarget"/>.
    /// </summary>
    /// <param name="origin">The source object.</param>
    /// <returns>A new mapped target instance.</returns>
    TTarget Map(TOrigin origin);

    /// <summary>
    /// Maps an origin object onto an existing target instance.
    /// </summary>
    /// <param name="origin">The source object.</param>
    /// <param name="existingTarget">The existing target to populate.</param>
    /// <returns>The populated target instance.</returns>
    TTarget Map(TOrigin origin, TTarget existingTarget);

    /// <summary>
    /// Maps a collection of origins into a materialized list of targets.
    /// </summary>
    /// <param name="origins">The source collection.</param>
    /// <returns>A list of mapped target instances.</returns>
    IReadOnlyList<TTarget> MapAll(IEnumerable<TOrigin> origins);
}
