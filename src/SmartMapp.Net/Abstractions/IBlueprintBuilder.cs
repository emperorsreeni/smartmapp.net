namespace SmartMapp.Net.Abstractions;

/// <summary>
/// Builder interface provided to <see cref="MappingBlueprint.Design"/> for registering type bindings.
/// Each <c>Bind&lt;S,D&gt;()</c> call registers a type pair and returns a fluent <see cref="IBindingRule{TOrigin,TTarget}"/>.
/// </summary>
public interface IBlueprintBuilder
{
    /// <summary>
    /// Registers a binding for the specified origin/target type pair and returns a fluent rule for configuration.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>A fluent binding rule for configuring the type pair.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the same type pair is registered twice.</exception>
    IBindingRule<TOrigin, TTarget> Bind<TOrigin, TTarget>();

    /// <summary>
    /// Registers a multi-origin composition rule for the specified target type.
    /// </summary>
    /// <typeparam name="TTarget">The destination type composed from multiple origins.</typeparam>
    /// <returns>A fluent composition rule.</returns>
    ICompositionRule<TTarget> Compose<TTarget>();
}
