namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Structured result returned by <see cref="ISculptorConfiguration.Validate"/>.
/// Wraps a <see cref="BlueprintValidationResult"/> and flattens errors/warnings to a simple,
/// consumable surface for programmatic post-forge checks (§12.1, §14.2).
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Initializes a new <see cref="ValidationResult"/> from a <see cref="BlueprintValidationResult"/>.
    /// </summary>
    /// <param name="inner">The underlying blueprint validation result.</param>
    public ValidationResult(BlueprintValidationResult inner)
    {
        Inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <summary>
    /// Gets the underlying blueprint validation result.
    /// </summary>
    public BlueprintValidationResult Inner { get; }

    /// <summary>
    /// Gets a value indicating whether validation produced no errors.
    /// </summary>
    public bool IsValid => Inner.IsValid;

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<BlueprintValidationError> Errors => Inner.Errors;

    /// <summary>
    /// Gets the validation warnings.
    /// </summary>
    public IReadOnlyList<BlueprintValidationError> Warnings => Inner.Warnings;

    /// <inheritdoc />
    public override string ToString() => Inner.ToString();
}
