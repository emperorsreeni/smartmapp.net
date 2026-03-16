using System.Linq.Expressions;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net;

/// <summary>
/// The primary entry point and runtime engine for all mapping operations.
/// Thread-safe after construction — can be called concurrently from multiple threads.
/// </summary>
public interface ISculptor
{
    /// <summary>
    /// Maps an origin object into a new instance of <typeparamref name="TTarget"/>.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origin">The source object.</param>
    /// <returns>A new instance of <typeparamref name="TTarget"/> populated from the origin.</returns>
    TTarget Map<TOrigin, TTarget>(TOrigin origin);

    /// <summary>
    /// Maps an origin object onto an existing target instance.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origin">The source object.</param>
    /// <param name="existingTarget">The existing target object to populate.</param>
    /// <returns>The populated target object.</returns>
    TTarget Map<TOrigin, TTarget>(TOrigin origin, TTarget existingTarget);

    /// <summary>
    /// Maps an origin object into a target type specified at runtime.
    /// </summary>
    /// <param name="origin">The source object.</param>
    /// <param name="originType">The runtime source type.</param>
    /// <param name="targetType">The runtime destination type.</param>
    /// <returns>A new instance of the target type populated from the origin.</returns>
    object Map(object origin, Type originType, Type targetType);

    /// <summary>
    /// Maps an origin object onto an existing target, with types specified at runtime.
    /// </summary>
    /// <param name="origin">The source object.</param>
    /// <param name="target">The existing target object.</param>
    /// <param name="originType">The runtime source type.</param>
    /// <param name="targetType">The runtime destination type.</param>
    /// <returns>The populated target object.</returns>
    object Map(object origin, object target, Type originType, Type targetType);

    /// <summary>
    /// Maps a collection of origins into a materialized list of targets.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origins">The source collection.</param>
    /// <returns>A list of mapped target instances.</returns>
    IReadOnlyList<TTarget> MapAll<TOrigin, TTarget>(IEnumerable<TOrigin> origins);

    /// <summary>
    /// Maps a collection of origins into an array of targets.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origins">The source collection.</param>
    /// <returns>An array of mapped target instances.</returns>
    TTarget[] MapToArray<TOrigin, TTarget>(IEnumerable<TOrigin> origins);

    /// <summary>
    /// Lazily maps a sequence of origins, yielding targets one at a time.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origins">The source sequence.</param>
    /// <returns>A deferred enumerable of mapped targets.</returns>
    IEnumerable<TTarget> MapLazy<TOrigin, TTarget>(IEnumerable<TOrigin> origins);

    /// <summary>
    /// Asynchronously maps a stream of origins, yielding targets one at a time.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origins">The async source stream.</param>
    /// <param name="ct">A cancellation token.</param>
    /// <returns>An async enumerable of mapped targets.</returns>
    IAsyncEnumerable<TTarget> MapStream<TOrigin, TTarget>(
        IAsyncEnumerable<TOrigin> origins, CancellationToken ct = default);

    /// <summary>
    /// Composes multiple origin objects into a single target instance.
    /// </summary>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="origins">The source objects to compose from.</param>
    /// <returns>A new target instance composed from the origins.</returns>
    TTarget Compose<TTarget>(params object[] origins);

    /// <summary>
    /// Projects an <see cref="IQueryable"/> source into <typeparamref name="TTarget"/> for database query translation.
    /// </summary>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <param name="source">The queryable source.</param>
    /// <returns>A projected queryable.</returns>
    IQueryable<TTarget> SelectAs<TTarget>(IQueryable source);

    /// <summary>
    /// Gets the projection expression for translating queries (e.g., EF Core SQL generation).
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>An expression tree representing the mapping.</returns>
    Expression<Func<TOrigin, TTarget>> GetProjection<TOrigin, TTarget>();

    /// <summary>
    /// Inspects the mapping configuration for a type pair, returning diagnostic information.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>A <see cref="MappingInspection"/> with blueprint and link trace details.</returns>
    MappingInspection Inspect<TOrigin, TTarget>();

    /// <summary>
    /// Gets the full mapping atlas — a graph of all registered blueprints and their relationships.
    /// </summary>
    /// <returns>A <see cref="MappingAtlas"/> containing all blueprints.</returns>
    MappingAtlas GetMappingAtlas();
}
