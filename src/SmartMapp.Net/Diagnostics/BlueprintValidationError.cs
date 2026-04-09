namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Describes a single validation error or warning found during blueprint validation.
/// </summary>
public sealed record BlueprintValidationError
{
    /// <summary>
    /// Gets the origin type involved in the error.
    /// </summary>
    public required Type OriginType { get; init; }

    /// <summary>
    /// Gets the target type involved in the error.
    /// </summary>
    public required Type TargetType { get; init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the severity of this validation finding.
    /// </summary>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Error;

    /// <inheritdoc />
    public override string ToString() =>
        $"[{Severity}] {OriginType.Name} -> {TargetType.Name}: {Message}";
}

/// <summary>
/// Severity level for a blueprint validation finding.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// A warning that does not prevent mapping but may indicate a configuration issue.
    /// </summary>
    Warning,

    /// <summary>
    /// An error that prevents correct mapping.
    /// </summary>
    Error,
}
