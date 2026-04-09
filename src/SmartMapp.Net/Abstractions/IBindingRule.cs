using System.Linq.Expressions;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Fluent configuration interface for a single <c>(TOrigin, TTarget)</c> type pair binding.
/// Returned by <see cref="IBlueprintBuilder.Bind{TOrigin,TTarget}"/>.
/// All methods return <c>this</c> for chaining except <see cref="Property{TMember}"/> which returns an <see cref="IPropertyRule{TOrigin,TTarget,TMember}"/>.
/// </summary>
/// <typeparam name="TOrigin">The source type.</typeparam>
/// <typeparam name="TTarget">The destination type.</typeparam>
public interface IBindingRule<TOrigin, TTarget>
{
    /// <summary>
    /// Configures an individual target property via a property rule callback.
    /// </summary>
    /// <typeparam name="TMember">The target member type.</typeparam>
    /// <param name="targetExpr">Expression selecting the target member.</param>
    /// <param name="config">Configuration callback for the property.</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> Property<TMember>(
        Expression<Func<TTarget, TMember>> targetExpr,
        Action<IPropertyRule<TOrigin, TTarget, TMember>> config);

    /// <summary>
    /// Sets a custom factory expression for constructing target instances.
    /// </summary>
    /// <param name="factory">A factory expression that creates a target from the origin.</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> BuildWith(Expression<Func<TOrigin, TTarget>> factory);

    /// <summary>
    /// Sets a type-level condition. The mapping is only applied when the predicate returns <c>true</c>.
    /// </summary>
    /// <param name="predicate">A predicate evaluated against the origin.</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> When(Expression<Func<TOrigin, bool>> predicate);

    /// <summary>
    /// Registers a pre-mapping hook invoked before property assignment begins.
    /// </summary>
    /// <param name="action">An action receiving (origin, target).</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> OnMapping(Action<TOrigin, TTarget> action);

    /// <summary>
    /// Registers a post-mapping hook invoked after all property assignments complete.
    /// </summary>
    /// <param name="action">An action receiving (origin, target).</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> OnMapped(Action<TOrigin, TTarget> action);

    /// <summary>
    /// Marks this binding as bidirectional — an inverse <c>(TTarget, TOrigin)</c> Blueprint is auto-generated.
    /// </summary>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> Bidirectional();

    /// <summary>
    /// Sets the maximum recursion depth for nested mappings within this binding.
    /// </summary>
    /// <param name="maxDepth">The depth limit.</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> DepthLimit(int maxDepth);

    /// <summary>
    /// Enables circular reference tracking for this binding.
    /// </summary>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> TrackReferences();

    /// <summary>
    /// Registers an explicit derived type pair for polymorphic dispatch.
    /// </summary>
    /// <typeparam name="TDerivedOrigin">The derived origin type.</typeparam>
    /// <typeparam name="TDerivedTarget">The derived target type.</typeparam>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> ExtendWith<TDerivedOrigin, TDerivedTarget>()
        where TDerivedOrigin : TOrigin
        where TDerivedTarget : TTarget;

    /// <summary>
    /// Inherits all property links from a base Blueprint.
    /// </summary>
    /// <typeparam name="TBaseOrigin">The base origin type.</typeparam>
    /// <typeparam name="TBaseTarget">The base target type.</typeparam>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> InheritFrom<TBaseOrigin, TBaseTarget>()
        where TBaseOrigin : class
        where TBaseTarget : class;

    /// <summary>
    /// Sets a type-level fallback value used when the origin is <c>null</c>.
    /// </summary>
    /// <param name="defaultValue">The fallback value.</param>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> FallbackTo(TTarget defaultValue);

    /// <summary>
    /// Registers a type-level transformer applied to the mapping result.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer type implementing <see cref="ITypeTransformer{TOrigin,TTarget}"/>.</typeparam>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> TransformWith<TTransformer>()
        where TTransformer : class, ITypeTransformer;

    /// <summary>
    /// Initiates discriminator-based polymorphic dispatch.
    /// </summary>
    /// <typeparam name="TDiscriminator">The discriminator value type.</typeparam>
    /// <param name="discriminatorExpr">An expression extracting the discriminator value from the origin.</param>
    /// <returns>A discriminator rule for configuring When/Otherwise clauses.</returns>
    IDiscriminatorRule<TOrigin, TTarget, TDiscriminator> DiscriminateBy<TDiscriminator>(
        Expression<Func<TOrigin, TDiscriminator>> discriminatorExpr);

    /// <summary>
    /// Specifies a concrete type to use when the target is an interface or abstract class.
    /// </summary>
    /// <typeparam name="TConcrete">The concrete type that implements <typeparamref name="TTarget"/>.</typeparam>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> Materialize<TConcrete>() where TConcrete : TTarget;

    /// <summary>
    /// Enables strict required member validation for this binding.
    /// </summary>
    /// <returns>This binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> StrictMode();
}
