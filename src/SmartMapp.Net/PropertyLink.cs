using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net;

/// <summary>
/// An immutable instruction describing how a single target member is populated during mapping.
/// Each <see cref="Blueprint"/> contains an ordered list of <see cref="PropertyLink"/> instances.
/// </summary>
public sealed record PropertyLink
{
    /// <summary>
    /// Gets the target property or field being set.
    /// </summary>
    public required MemberInfo TargetMember { get; init; }

    /// <summary>
    /// Gets the value provider that extracts or computes the value from the origin.
    /// </summary>
    public required IValueProvider Provider { get; init; }

    /// <summary>
    /// Gets the optional type transformer applied after the value is provided.
    /// </summary>
    public ITypeTransformer? Transformer { get; init; }

    /// <summary>
    /// Gets the convention match that describes how this link was established.
    /// Enables the <c>Inspect&lt;S,D&gt;()</c> diagnostic.
    /// </summary>
    public required ConventionMatch LinkedBy { get; init; }

    /// <summary>
    /// Gets a value indicating whether this link is a no-op placeholder
    /// (explicitly skipped via <c>.Skip()</c> or <c>[Unmapped]</c>).
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Gets the fallback value to use when the origin value is <c>null</c>.
    /// Configured via <c>.FallbackTo()</c>.
    /// </summary>
    public object? Fallback { get; init; }

    /// <summary>
    /// Gets the execution order within the blueprint. Links are sorted by this value.
    /// Configured via <c>.SetOrder()</c>. Defaults to 0.
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// Gets the optional condition predicate. The link is applied only if this returns <c>true</c>.
    /// Configured via <c>.When()</c>.
    /// </summary>
    public Func<object, bool>? Condition { get; init; }

    /// <summary>
    /// Gets the optional pre-condition predicate evaluated before the provider runs.
    /// Configured via <c>.OnlyIf()</c>.
    /// </summary>
    public Func<object, bool>? PreCondition { get; init; }
}
