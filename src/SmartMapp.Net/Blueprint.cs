using System.Diagnostics;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net;

/// <summary>
/// An immutable, pre-computed instruction set describing how every target member is populated
/// for a specific <c>(OriginType, TargetType)</c> pair. Stored in the blueprint cache for the
/// lifetime of the application.
/// </summary>
[DebuggerDisplay("{DebugView}")]
public sealed record Blueprint
{
    /// <summary>
    /// Gets the origin (source) type.
    /// </summary>
    public required Type OriginType { get; init; }

    /// <summary>
    /// Gets the target (destination) type.
    /// </summary>
    public required Type TargetType { get; init; }

    /// <summary>
    /// Gets the <see cref="TypePair"/> computed from <see cref="OriginType"/> and <see cref="TargetType"/>.
    /// </summary>
    public TypePair TypePair => new(OriginType, TargetType);

    /// <summary>
    /// Gets the ordered list of property links that define how each target member is set.
    /// Sorted by <see cref="PropertyLink.Order"/>.
    /// </summary>
    public IReadOnlyList<PropertyLink> Links { get; init; } = Array.Empty<PropertyLink>();

    /// <summary>
    /// Gets the code generation strategy used for this mapping.
    /// </summary>
    public MappingStrategy Strategy { get; init; } = MappingStrategy.ExpressionCompiled;

    /// <summary>
    /// Gets a value indicating whether this mapping is eligible for parallel execution
    /// when processing collections.
    /// </summary>
    public bool IsParallelEligible { get; init; }

    /// <summary>
    /// Gets the mapping filters applied to this blueprint's pipeline.
    /// </summary>
    public IReadOnlyList<IMappingFilter> Filters { get; init; } = Array.Empty<IMappingFilter>();

    /// <summary>
    /// Gets the maximum recursion depth. Defaults to <see cref="int.MaxValue"/>.
    /// Configured via <c>.DepthLimit()</c>.
    /// </summary>
    public int MaxDepth { get; init; } = int.MaxValue;

    /// <summary>
    /// Gets a value indicating whether circular reference tracking is enabled.
    /// Configured via <c>.TrackReferences()</c>.
    /// </summary>
    public bool TrackReferences { get; init; }

    /// <summary>
    /// Gets the optional factory function for constructing target instances.
    /// Configured via <c>.BuildWith()</c>.
    /// </summary>
    public Func<object, object>? TargetFactory { get; init; }

    /// <summary>
    /// Gets the optional pre-map hook invoked before mapping begins.
    /// Parameters are (origin, target).
    /// </summary>
    public Action<object, object>? OnMapping { get; init; }

    /// <summary>
    /// Gets the optional post-map hook invoked after mapping completes.
    /// Parameters are (origin, target).
    /// </summary>
    public Action<object, object>? OnMapped { get; init; }

    /// <summary>
    /// Gets a value indicating whether missing <c>required</c> members should cause a compilation error.
    /// When <c>true</c> (strict), throws <see cref="Compilation.MappingCompilationException"/> if required members are unmapped.
    /// When <c>false</c> (default), missing required members are silently ignored.
    /// Only effective on .NET 7+.
    /// </summary>
    public bool StrictRequiredMembers { get; init; }

    /// <summary>
    /// Creates an empty blueprint for the given type pair. Useful for test scaffolding.
    /// </summary>
    /// <param name="pair">The type pair.</param>
    /// <returns>A new empty <see cref="Blueprint"/>.</returns>
    public static Blueprint Empty(TypePair pair) => new()
    {
        OriginType = pair.OriginType,
        TargetType = pair.TargetType,
    };

    private string DebugView =>
        $"{OriginType.Name} -> {TargetType.Name} [{Strategy}] ({Links.Count} links)";
}
