using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net;

/// <summary>
/// Provides read-only access to the sculptor's configuration after it has been forged.
/// Used for diagnostics, validation, and introspection. Mirrors spec §14.2.
/// </summary>
public interface ISculptorConfiguration
{
    /// <summary>
    /// Gets all registered blueprints.
    /// </summary>
    /// <returns>A read-only list of all blueprints.</returns>
    IReadOnlyList<Blueprint> GetAllBlueprints();

    /// <summary>
    /// Gets all registered blueprints indexed by type pair.
    /// </summary>
    /// <returns>A read-only dictionary from <see cref="TypePair"/> to <see cref="Blueprint"/>.</returns>
    IReadOnlyDictionary<TypePair, Blueprint> GetAllBlueprintsByPair();

    /// <summary>
    /// Gets the blueprint for a specific type pair.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>The blueprint, or <c>null</c> if no binding exists.</returns>
    Blueprint? GetBlueprint<TOrigin, TTarget>();

    /// <summary>
    /// Gets the blueprint for a specific type pair at runtime.
    /// </summary>
    /// <param name="originType">The source type.</param>
    /// <param name="targetType">The destination type.</param>
    /// <returns>The blueprint, or <c>null</c> if no binding exists.</returns>
    Blueprint? GetBlueprint(Type originType, Type targetType);

    /// <summary>
    /// Validates the entire configuration, throwing <see cref="BlueprintValidationException"/>
    /// if any errors are found.
    /// </summary>
    /// <exception cref="BlueprintValidationException">Thrown when configuration is invalid.</exception>
    void Validate();

    /// <summary>
    /// Returns a structured <see cref="ValidationResult"/> with accumulated errors and warnings.
    /// Side-effect free and idempotent — result is cached after first call.
    /// </summary>
    /// <returns>A <see cref="ValidationResult"/>.</returns>
    ValidationResult ValidateConfiguration();

    /// <summary>
    /// Checks whether a binding exists for the specified type pair.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns><c>true</c> if a binding is registered; otherwise <c>false</c>.</returns>
    bool HasBinding<TOrigin, TTarget>();

    /// <summary>
    /// Checks whether a binding exists for the specified runtime type pair.
    /// </summary>
    /// <param name="originType">The source type.</param>
    /// <param name="targetType">The destination type.</param>
    /// <returns><c>true</c> if a binding is registered; otherwise <c>false</c>.</returns>
    bool HasBinding(Type originType, Type targetType);
}
