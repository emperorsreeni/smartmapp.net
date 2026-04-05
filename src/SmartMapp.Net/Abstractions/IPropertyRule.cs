using System.Linq.Expressions;

namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Fluent configuration interface for a single target property within a binding.
/// Returned by the callback in <see cref="IBindingRule{TOrigin,TTarget}.Property{TMember}"/>.
/// All methods return <c>this</c> for chaining.
/// </summary>
/// <typeparam name="TOrigin">The source type.</typeparam>
/// <typeparam name="TTarget">The destination type.</typeparam>
/// <typeparam name="TMember">The target member type.</typeparam>
public interface IPropertyRule<TOrigin, TTarget, TMember>
{
    /// <summary>
    /// Sets the origin expression that provides the value for this target member.
    /// Mutually exclusive with <see cref="Skip"/>.
    /// </summary>
    /// <param name="originExpr">An expression extracting the value from the origin.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> From(Expression<Func<TOrigin, TMember>> originExpr);

    /// <summary>
    /// Sets a DI-resolved value provider type for this target member.
    /// </summary>
    /// <typeparam name="TProvider">The provider type implementing <see cref="IValueProvider{TOrigin,TTarget,TMember}"/>.</typeparam>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> From<TProvider>()
        where TProvider : class, IValueProvider;

    /// <summary>
    /// Marks this property as explicitly unmapped (skipped).
    /// Mutually exclusive with <see cref="From(Expression{Func{TOrigin,TMember}})"/>.
    /// </summary>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> Skip();

    /// <summary>
    /// Sets a fallback value used when the origin value is <c>null</c>.
    /// </summary>
    /// <param name="defaultValue">The fallback value.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> FallbackTo(TMember defaultValue);

    /// <summary>
    /// Sets a condition predicate. The property is mapped only when this returns <c>true</c>;
    /// otherwise <c>default(TMember)</c> is used.
    /// </summary>
    /// <param name="predicate">A predicate evaluated against the origin.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> When(Expression<Func<TOrigin, bool>> predicate);

    /// <summary>
    /// Sets a pre-condition predicate. If <c>false</c>, the property resolution is skipped entirely
    /// (no assignment at all, not even default).
    /// </summary>
    /// <param name="predicate">A predicate evaluated against the origin.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> OnlyIf(Expression<Func<TOrigin, bool>> predicate);

    /// <summary>
    /// Registers a typed type transformer for this property.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer type.</typeparam>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> TransformWith<TTransformer>()
        where TTransformer : class, ITypeTransformer;

    /// <summary>
    /// Registers an inline transform expression for this property.
    /// </summary>
    /// <param name="transform">A transform applied to the value before assignment.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> TransformWith(Expression<Func<TMember, TMember>> transform);

    /// <summary>
    /// Sets the explicit execution order for this property within the blueprint.
    /// Lower values execute first. Default is 0.
    /// </summary>
    /// <param name="order">The order value.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> SetOrder(int order);

    /// <summary>
    /// Registers a post-assignment transform applied after the value is set on the target.
    /// </summary>
    /// <param name="postProcess">A transform applied to the assigned value.</param>
    /// <returns>This property rule for further chaining.</returns>
    IPropertyRule<TOrigin, TTarget, TMember> PostProcess(Expression<Func<TMember, TMember>> postProcess);
}
