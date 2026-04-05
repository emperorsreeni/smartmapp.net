namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Internal implementation of <see cref="IDiscriminatorRule{TOrigin,TTarget,TDiscriminator}"/>.
/// Accumulates When/Otherwise clauses into a <see cref="DiscriminatorConfig"/>.
/// </summary>
internal sealed class DiscriminatorRule<TOrigin, TTarget, TDiscriminator>
    : IDiscriminatorRule<TOrigin, TTarget, TDiscriminator>
{
    private readonly IBindingRule<TOrigin, TTarget> _parentRule;
    private readonly DiscriminatorConfig _config;

    /// <summary>
    /// Initializes a new <see cref="DiscriminatorRule{TOrigin,TTarget,TDiscriminator}"/>.
    /// </summary>
    internal DiscriminatorRule(IBindingRule<TOrigin, TTarget> parentRule, DiscriminatorConfig config)
    {
        _parentRule = parentRule;
        _config = config;
    }

    /// <inheritdoc />
    public IDiscriminatorRule<TOrigin, TTarget, TDiscriminator> When<TDerivedTarget>(TDiscriminator value)
        where TDerivedTarget : TTarget
    {
        var targetPair = new TypePair(typeof(TOrigin), typeof(TDerivedTarget));
        _config.AddWhen(value!, targetPair);
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> Otherwise<TFallbackTarget>()
        where TFallbackTarget : TTarget
    {
        _config.OtherwisePair = new TypePair(typeof(TOrigin), typeof(TFallbackTarget));
        return _parentRule;
    }
}
