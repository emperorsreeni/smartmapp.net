using System.Linq.Expressions;
using System.Reflection;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Internal implementation of <see cref="IBindingRule{TOrigin,TTarget}"/>.
/// Accumulates type-pair-level configuration into a <see cref="BindingConfiguration"/>.
/// </summary>
internal sealed class BindingRule<TOrigin, TTarget> : IBindingRule<TOrigin, TTarget>
{
    private readonly BindingConfiguration _config;

    /// <summary>
    /// Gets the underlying configuration.
    /// </summary>
    internal BindingConfiguration Configuration => _config;

    /// <summary>
    /// Initializes a new <see cref="BindingRule{TOrigin,TTarget}"/>.
    /// </summary>
    internal BindingRule(BindingConfiguration config)
    {
        _config = config;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> Property<TMember>(
        Expression<Func<TTarget, TMember>> targetExpr,
        Action<IPropertyRule<TOrigin, TTarget, TMember>> config)
    {
        var memberInfo = ExtractMemberInfo(targetExpr);
        var propConfig = new PropertyConfiguration { TargetMember = memberInfo };
        var rule = new PropertyRule<TOrigin, TTarget, TMember>(propConfig);
        config(rule);
        _config.PropertyConfigs.Add(propConfig);
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> BuildWith(Expression<Func<TOrigin, TTarget>> factory)
    {
        _config.FactoryExpression = factory;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> When(Expression<Func<TOrigin, bool>> predicate)
    {
        _config.Condition = predicate;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> OnMapping(Action<TOrigin, TTarget> action)
    {
        _config.OnMappingHook = action;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> OnMapped(Action<TOrigin, TTarget> action)
    {
        _config.OnMappedHook = action;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> Bidirectional()
    {
        _config.IsBidirectional = true;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> DepthLimit(int maxDepth)
    {
        _config.MaxDepth = maxDepth;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> TrackReferences()
    {
        _config.TrackReferences = true;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> ExtendWith<TDerivedOrigin, TDerivedTarget>()
        where TDerivedOrigin : TOrigin
        where TDerivedTarget : TTarget
    {
        var derivedPair = new TypePair(typeof(TDerivedOrigin), typeof(TDerivedTarget));
        if (!_config.ExplicitDerivedPairs.Contains(derivedPair))
            _config.ExplicitDerivedPairs.Add(derivedPair);
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> InheritFrom<TBaseOrigin, TBaseTarget>()
        where TBaseOrigin : class
        where TBaseTarget : class
    {
        _config.InheritFromPair = new TypePair(typeof(TBaseOrigin), typeof(TBaseTarget));
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> FallbackTo(TTarget defaultValue)
    {
        _config.FallbackValue = defaultValue;
        _config.HasFallback = true;
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> TransformWith<TTransformer>()
        where TTransformer : class, ITypeTransformer
    {
        _config.TransformerType = typeof(TTransformer);
        return this;
    }

    /// <inheritdoc />
    public IDiscriminatorRule<TOrigin, TTarget, TDiscriminator> DiscriminateBy<TDiscriminator>(
        Expression<Func<TOrigin, TDiscriminator>> discriminatorExpr)
    {
        var discConfig = new DiscriminatorConfig(discriminatorExpr);
        _config.Discriminator = discConfig;
        return new DiscriminatorRule<TOrigin, TTarget, TDiscriminator>(this, discConfig);
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> Materialize<TConcrete>() where TConcrete : TTarget
    {
        _config.MaterializeType = typeof(TConcrete);
        return this;
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> StrictMode()
    {
        _config.StrictMode = true;
        return this;
    }

    /// <summary>
    /// Extracts the <see cref="MemberInfo"/> from a member access expression.
    /// </summary>
    private static MemberInfo ExtractMemberInfo<TMember>(Expression<Func<TTarget, TMember>> expr)
    {
        var body = expr.Body;

        // Unwrap Convert/ConvertChecked for value type members
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        if (body is MemberExpression memberExpr)
            return memberExpr.Member;

        throw new ArgumentException(
            $"Expression '{expr}' does not refer to a property or field. " +
            "The expression must be a simple member access like 'd => d.PropertyName'.",
            nameof(expr));
    }
}
