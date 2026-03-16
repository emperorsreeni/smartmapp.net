using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SmartMapp.Net;

/// <summary>
/// Per-mapping context object created for each top-level <c>Map()</c> call.
/// Passed down through nested mappings and into providers/transformers.
/// <para>
/// <b>Not thread-safe</b> — each thread/task must receive its own scope instance.
/// In parallel collection mapping, each parallel task gets its own scope.
/// </para>
/// </summary>
public sealed class MappingScope
{
    /// <summary>
    /// Gets the current mapping depth. Incremented on each nested map call.
    /// </summary>
    public int CurrentDepth { get; private set; }

    /// <summary>
    /// Gets or sets the maximum allowed recursion depth. Defaults to <see cref="int.MaxValue"/>.
    /// Set from <c>Blueprint.MaxDepth</c> (configured via <c>.DepthLimit()</c>).
    /// </summary>
    public int MaxDepth { get; init; } = int.MaxValue;

    /// <summary>
    /// Gets or sets the optional DI service provider for resolving value providers and other services.
    /// </summary>
    public IServiceProvider? ServiceProvider { get; init; }

    /// <summary>
    /// Gets or sets the cancellation token propagated to async operations.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the arbitrary state bag for passing data between filters and providers.
    /// Similar to <c>HttpContext.Items</c>.
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets a value indicating whether the maximum recursion depth has been reached.
    /// </summary>
    public bool IsMaxDepthReached => CurrentDepth >= MaxDepth;

    private Dictionary<object, object>? _visited;

    /// <summary>
    /// Creates a child scope with incremented depth, sharing the visited map and service provider.
    /// </summary>
    /// <returns>A new <see cref="MappingScope"/> with <see cref="CurrentDepth"/> incremented by 1.</returns>
    public MappingScope CreateChild()
    {
        var child = new MappingScope
        {
            CurrentDepth = CurrentDepth + 1,
            MaxDepth = MaxDepth,
            ServiceProvider = ServiceProvider,
            CancellationToken = CancellationToken,
        };
        child._visited = _visited;
        return child;
    }

    /// <summary>
    /// Checks the identity map for a previously visited origin object.
    /// </summary>
    /// <param name="origin">The origin object to look up.</param>
    /// <param name="target">The previously mapped target, if found.</param>
    /// <returns><c>true</c> if the origin was previously tracked; otherwise <c>false</c>.</returns>
    public bool TryGetVisited(object origin, [NotNullWhen(true)] out object? target)
    {
        if (_visited is not null)
            return _visited.TryGetValue(origin, out target);
        target = null;
        return false;
    }

    /// <summary>
    /// Adds an origin-target pair to the identity map for circular reference tracking.
    /// Uses reference equality (not structural equality) for comparisons.
    /// </summary>
    /// <param name="origin">The origin object.</param>
    /// <param name="target">The mapped target object.</param>
    public void TrackVisited(object origin, object target)
    {
#if NET8_0_OR_GREATER
        _visited ??= new Dictionary<object, object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
#else
        _visited ??= new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
#endif
        _visited[origin] = target;
    }

    /// <summary>
    /// Resolves a service from the <see cref="ServiceProvider"/>.
    /// Throws <see cref="InvalidOperationException"/> if the service is not registered or no provider is set.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service cannot be resolved.</exception>
    public T GetService<T>() where T : class
    {
        if (ServiceProvider is null)
            throw new InvalidOperationException(
                $"Cannot resolve service '{typeof(T).Name}': no ServiceProvider is configured on this MappingScope.");

        var service = ServiceProvider.GetService(typeof(T)) as T;
        return service ?? throw new InvalidOperationException(
            $"Cannot resolve service '{typeof(T).Name}' from the configured ServiceProvider.");
    }

    /// <summary>
    /// Attempts to resolve a service from the <see cref="ServiceProvider"/>.
    /// Returns <c>null</c> if the service is not registered or no provider is set.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance, or <c>null</c>.</returns>
    public T? TryGetService<T>() where T : class
    {
        return ServiceProvider?.GetService(typeof(T)) as T;
    }

    /// <summary>
    /// Resets all mutable state for object pool reuse.
    /// Clears depth, visited objects, and items.
    /// </summary>
    public void Reset()
    {
        CurrentDepth = 0;
        _visited?.Clear();
        Items.Clear();
    }
}
