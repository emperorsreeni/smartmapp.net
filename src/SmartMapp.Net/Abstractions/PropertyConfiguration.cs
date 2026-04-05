using System.Linq.Expressions;
using System.Reflection;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Internal mutable accumulator for a single target property's configuration.
/// Built up by <see cref="IPropertyRule{TOrigin,TTarget,TMember}"/> calls,
/// then converted to a <see cref="PropertyLink"/> during blueprint construction.
/// </summary>
internal sealed class PropertyConfiguration
{
    /// <summary>
    /// Gets or sets the target member being configured.
    /// </summary>
    internal MemberInfo TargetMember { get; set; } = null!;

    /// <summary>
    /// Gets or sets the origin expression (from <c>.From(s => s.X)</c>).
    /// </summary>
    internal LambdaExpression? OriginExpression { get; set; }

    /// <summary>
    /// Gets or sets the DI-resolved provider type (from <c>.From&lt;TProvider&gt;()</c>).
    /// </summary>
    internal Type? ProviderType { get; set; }

    /// <summary>
    /// Gets or sets whether this property is explicitly skipped.
    /// </summary>
    internal bool IsSkipped { get; set; }

    /// <summary>
    /// Gets or sets the fallback value for null origins.
    /// </summary>
    internal object? FallbackValue { get; set; }

    /// <summary>
    /// Gets or sets whether a fallback value has been explicitly set (to distinguish null fallback from no fallback).
    /// </summary>
    internal bool HasFallback { get; set; }

    /// <summary>
    /// Gets or sets the condition expression (from <c>.When()</c>).
    /// </summary>
    internal LambdaExpression? Condition { get; set; }

    /// <summary>
    /// Gets or sets the pre-condition expression (from <c>.OnlyIf()</c>).
    /// </summary>
    internal LambdaExpression? PreCondition { get; set; }

    /// <summary>
    /// Gets or sets the transformer type (from <c>.TransformWith&lt;T&gt;()</c>).
    /// </summary>
    internal Type? TransformerType { get; set; }

    /// <summary>
    /// Gets or sets the inline transform expression (from <c>.TransformWith(v => ...)</c>).
    /// </summary>
    internal LambdaExpression? InlineTransform { get; set; }

    /// <summary>
    /// Gets or sets the explicit execution order.
    /// </summary>
    internal int Order { get; set; }

    /// <summary>
    /// Gets or sets the post-processing expression (from <c>.PostProcess()</c>).
    /// </summary>
    internal LambdaExpression? PostProcess { get; set; }
}
