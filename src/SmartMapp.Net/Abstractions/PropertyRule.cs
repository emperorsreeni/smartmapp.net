using System.Linq.Expressions;
using System.Reflection;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Internal implementation of <see cref="IPropertyRule{TOrigin,TTarget,TMember}"/>.
/// Accumulates property-level configuration into a <see cref="PropertyConfiguration"/>.
/// </summary>
internal sealed class PropertyRule<TOrigin, TTarget, TMember> : IPropertyRule<TOrigin, TTarget, TMember>
{
    private readonly PropertyConfiguration _config;

    /// <summary>
    /// Initializes a new <see cref="PropertyRule{TOrigin,TTarget,TMember}"/>.
    /// </summary>
    /// <param name="config">The property configuration to populate.</param>
    internal PropertyRule(PropertyConfiguration config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> From(Expression<Func<TOrigin, TMember>> originExpr)
    {
        if (_config.IsSkipped)
            throw new InvalidOperationException(
                $"Cannot call From() on property '{_config.TargetMember.Name}' — it has already been marked as Skip().");

        _config.OriginExpression = originExpr;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> From<TProvider>() where TProvider : class, IValueProvider
    {
        if (_config.IsSkipped)
            throw new InvalidOperationException(
                $"Cannot call From<TProvider>() on property '{_config.TargetMember.Name}' — it has already been marked as Skip().");

        _config.ProviderType = typeof(TProvider);
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> Skip()
    {
        if (_config.OriginExpression is not null || _config.ProviderType is not null)
            throw new InvalidOperationException(
                $"Cannot call Skip() on property '{_config.TargetMember.Name}' — it already has a From() configuration.");

        _config.IsSkipped = true;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> FallbackTo(TMember defaultValue)
    {
        _config.FallbackValue = defaultValue;
        _config.HasFallback = true;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> When(Expression<Func<TOrigin, bool>> predicate)
    {
        _config.Condition = predicate;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> OnlyIf(Expression<Func<TOrigin, bool>> predicate)
    {
        _config.PreCondition = predicate;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> TransformWith<TTransformer>()
        where TTransformer : class, ITypeTransformer
    {
        _config.TransformerType = typeof(TTransformer);
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> TransformWith(Expression<Func<TMember, TMember>> transform)
    {
        _config.InlineTransform = transform;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> SetOrder(int order)
    {
        _config.Order = order;
        return this;
    }

    /// <inheritdoc />
    public IPropertyRule<TOrigin, TTarget, TMember> PostProcess(Expression<Func<TMember, TMember>> postProcess)
    {
        _config.PostProcess = postProcess;
        return this;
    }
}
