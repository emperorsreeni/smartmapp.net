namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// The result of validating all registered blueprints.
/// Contains accumulated errors and warnings.
/// </summary>
public sealed class BlueprintValidationResult
{
    private readonly List<BlueprintValidationError> _errors = new();

    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<BlueprintValidationError> Errors => _errors.Where(e => e.Severity == ValidationSeverity.Error).ToList();

    /// <summary>
    /// Gets all validation warnings.
    /// </summary>
    public IReadOnlyList<BlueprintValidationError> Warnings => _errors.Where(e => e.Severity == ValidationSeverity.Warning).ToList();

    /// <summary>
    /// Gets all findings (errors and warnings).
    /// </summary>
    public IReadOnlyList<BlueprintValidationError> All => _errors;

    /// <summary>
    /// Gets a value indicating whether the configuration is valid (no errors, warnings are allowed).
    /// </summary>
    public bool IsValid => !_errors.Any(e => e.Severity == ValidationSeverity.Error);

    /// <summary>
    /// Adds a validation finding.
    /// </summary>
    internal void Add(BlueprintValidationError error) => _errors.Add(error);

    /// <summary>
    /// Adds a validation error.
    /// </summary>
    internal void AddError(Type originType, Type targetType, string message)
    {
        _errors.Add(new BlueprintValidationError
        {
            OriginType = originType,
            TargetType = targetType,
            Message = message,
            Severity = ValidationSeverity.Error,
        });
    }

    /// <summary>
    /// Adds a validation warning.
    /// </summary>
    internal void AddWarning(Type originType, Type targetType, string message)
    {
        _errors.Add(new BlueprintValidationError
        {
            OriginType = originType,
            TargetType = targetType,
            Message = message,
            Severity = ValidationSeverity.Warning,
        });
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsValid && _errors.Count == 0)
            return "Validation passed: no errors or warnings.";

        var lines = new List<string>();
        if (!IsValid)
            lines.Add($"Validation FAILED with {Errors.Count} error(s) and {Warnings.Count} warning(s):");
        else
            lines.Add($"Validation passed with {Warnings.Count} warning(s):");

        foreach (var error in _errors)
        {
            lines.Add($"  {error}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
