namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Fluent interface for configuring discriminator-based polymorphic dispatch.
/// Returned by <see cref="IBindingRule{TOrigin,TTarget}.DiscriminateBy{TDiscriminator}"/>.
/// </summary>
/// <typeparam name="TOrigin">The base origin type.</typeparam>
/// <typeparam name="TTarget">The base target type.</typeparam>
/// <typeparam name="TDiscriminator">The discriminator value type.</typeparam>
public interface IDiscriminatorRule<TOrigin, TTarget, TDiscriminator>
{
    /// <summary>
    /// Registers a When-clause: when the discriminator equals <paramref name="value"/>,
    /// map using the specified derived target type.
    /// </summary>
    /// <typeparam name="TDerivedTarget">The derived target type to map to.</typeparam>
    /// <param name="value">The discriminator value to match.</param>
    /// <returns>This discriminator rule for further chaining.</returns>
    IDiscriminatorRule<TOrigin, TTarget, TDiscriminator> When<TDerivedTarget>(TDiscriminator value)
        where TDerivedTarget : TTarget;

    /// <summary>
    /// Sets the fallback target type used when no When-clause matches.
    /// This is mandatory — omitting it causes a validation error.
    /// </summary>
    /// <typeparam name="TFallbackTarget">The fallback target type.</typeparam>
    /// <returns>The parent binding rule for further chaining.</returns>
    IBindingRule<TOrigin, TTarget> Otherwise<TFallbackTarget>()
        where TFallbackTarget : TTarget;
}
