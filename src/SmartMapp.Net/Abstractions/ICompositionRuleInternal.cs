namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Non-generic internal façade exposed by <c>CompositionRuleBuilder&lt;TTarget&gt;</c>
/// (<see cref="Composition.CompositionRuleBuilder{TTarget}"/>) so the forge pipeline can
/// iterate composition rules without knowing <c>TTarget</c> at compile time.
/// Pair with <see cref="ICompositionRule{TTarget}"/> on every implementation — the public
/// fluent surface stays generic while the pipeline uses the non-generic view.
/// </summary>
internal interface ICompositionRuleInternal
{
    /// <summary>
    /// Gets the composed target type.
    /// </summary>
    Type TargetType { get; }

    /// <summary>
    /// Gets the ordered list of per-origin binding configurations.
    /// </summary>
    IReadOnlyList<(Type OriginType, BindingConfiguration Config)> Origins { get; }
}
