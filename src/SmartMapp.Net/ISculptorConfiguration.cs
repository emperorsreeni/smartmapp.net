namespace SmartMapp.Net;

/// <summary>
/// Provides read-only access to the sculptor's configuration after it has been forged.
/// Used for diagnostics, validation, and introspection.
/// </summary>
public interface ISculptorConfiguration
{
    /// <summary>
    /// Gets all registered blueprints.
    /// </summary>
    /// <returns>A read-only list of all blueprints.</returns>
    IReadOnlyList<Blueprint> GetAllBlueprints();

    /// <summary>
    /// Gets the blueprint for a specific type pair.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>The blueprint, or <c>null</c> if no binding exists.</returns>
    Blueprint? GetBlueprint<TOrigin, TTarget>();

    /// <summary>
    /// Validates the entire configuration, throwing if any issues are found.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid.</exception>
    void Validate();

    /// <summary>
    /// Checks whether a binding exists for the specified type pair.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns><c>true</c> if a binding is registered; otherwise <c>false</c>.</returns>
    bool HasBinding<TOrigin, TTarget>();
}
