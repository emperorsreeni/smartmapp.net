using System.Linq.Expressions;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Internal mutable accumulator for a single type-pair binding's configuration.
/// Built up by <see cref="IBindingRule{TOrigin,TTarget}"/> calls,
/// then converted to an immutable <see cref="Blueprint"/> during the build phase.
/// </summary>
internal sealed class BindingConfiguration
{
    /// <summary>
    /// Gets the type pair for this binding.
    /// </summary>
    internal TypePair TypePair { get; }

    /// <summary>
    /// Gets the property configurations accumulated via <c>.Property()</c> calls.
    /// </summary>
    internal List<PropertyConfiguration> PropertyConfigs { get; } = new();

    /// <summary>
    /// Gets or sets the custom factory expression (from <c>.BuildWith()</c>).
    /// </summary>
    internal LambdaExpression? FactoryExpression { get; set; }

    /// <summary>
    /// Gets or sets the type-level condition (from <c>.When()</c>).
    /// </summary>
    internal LambdaExpression? Condition { get; set; }

    /// <summary>
    /// Gets or sets the pre-mapping hook (from <c>.OnMapping()</c>).
    /// </summary>
    internal Delegate? OnMappingHook { get; set; }

    /// <summary>
    /// Gets or sets the post-mapping hook (from <c>.OnMapped()</c>).
    /// </summary>
    internal Delegate? OnMappedHook { get; set; }

    /// <summary>
    /// Gets or sets whether this binding is bidirectional.
    /// </summary>
    internal bool IsBidirectional { get; set; }

    /// <summary>
    /// Gets or sets the maximum recursion depth.
    /// </summary>
    internal int? MaxDepth { get; set; }

    /// <summary>
    /// Gets or sets whether circular reference tracking is enabled.
    /// </summary>
    internal bool TrackReferences { get; set; }

    /// <summary>
    /// Gets or sets the type-level fallback value.
    /// </summary>
    internal object? FallbackValue { get; set; }

    /// <summary>
    /// Gets or sets whether a fallback has been explicitly set.
    /// </summary>
    internal bool HasFallback { get; set; }

    /// <summary>
    /// Gets or sets the type-level transformer type.
    /// </summary>
    internal Type? TransformerType { get; set; }

    /// <summary>
    /// Gets or sets whether strict required member validation is enabled.
    /// </summary>
    internal bool StrictMode { get; set; }

    /// <summary>
    /// Gets the explicit derived pairs registered via <c>.ExtendWith()</c>.
    /// </summary>
    internal List<TypePair> ExplicitDerivedPairs { get; } = new();

    /// <summary>
    /// Gets or sets the base pair for blueprint inheritance via <c>.InheritFrom()</c>.
    /// </summary>
    internal TypePair? InheritFromPair { get; set; }

    /// <summary>
    /// Gets or sets the discriminator configuration via <c>.DiscriminateBy()</c>.
    /// </summary>
    internal DiscriminatorConfig? Discriminator { get; set; }

    /// <summary>
    /// Gets or sets the concrete materialization type for interface/abstract targets.
    /// </summary>
    internal Type? MaterializeType { get; set; }

    /// <summary>
    /// Initializes a new <see cref="BindingConfiguration"/> for the given type pair.
    /// </summary>
    internal BindingConfiguration(TypePair typePair)
    {
        TypePair = typePair;
    }
}
