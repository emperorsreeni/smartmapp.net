namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Contextual information for a mapping operation, passed through the filter pipeline.
/// Similar in concept to <c>HttpContext</c> in ASP.NET Core.
/// </summary>
public sealed record MappingContext
{
    /// <summary>
    /// Gets the origin (source) type being mapped.
    /// </summary>
    public required Type OriginType { get; init; }

    /// <summary>
    /// Gets the target (destination) type being mapped into.
    /// </summary>
    public required Type TargetType { get; init; }

    /// <summary>
    /// Gets the origin object instance being mapped.
    /// </summary>
    public required object Origin { get; init; }

    /// <summary>
    /// Gets the target object instance being populated, or <c>null</c> if a new instance will be created.
    /// </summary>
    public object? Target { get; init; }

    /// <summary>
    /// Gets the current <see cref="MappingScope"/> providing depth tracking, visited objects, and services.
    /// </summary>
    public required MappingScope Scope { get; init; }

    /// <summary>
    /// Gets a general-purpose state bag for passing data between filters in the pipeline.
    /// Similar to <c>HttpContext.Items</c>.
    /// </summary>
    public IDictionary<string, object?> Items { get; init; } = new Dictionary<string, object?>();
}
