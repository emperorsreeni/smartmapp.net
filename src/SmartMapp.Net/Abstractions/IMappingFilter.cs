namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Delegate representing the next step in the mapping filter pipeline.
/// </summary>
/// <param name="context">The mapping context for the current operation.</param>
/// <returns>The mapped target object, or <c>null</c>.</returns>
public delegate Task<object?> MappingDelegate(MappingContext context);

/// <summary>
/// A pipeline component that wraps mapping execution, forming a chain-of-responsibility.
/// Filters can inspect, modify, short-circuit, or augment the mapping process.
/// Synchronous filters can return <see cref="Task.FromResult{TResult}"/>.
/// </summary>
public interface IMappingFilter
{
    /// <summary>
    /// Applies this filter to the mapping pipeline.
    /// Call <paramref name="next"/> to continue the pipeline, or return a value to short-circuit.
    /// </summary>
    /// <param name="context">The mapping context for the current operation.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <returns>The mapped target object, or <c>null</c>.</returns>
    Task<object?> ApplyAsync(MappingContext context, MappingDelegate next);
}
