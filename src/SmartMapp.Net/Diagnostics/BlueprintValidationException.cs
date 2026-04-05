namespace SmartMapp.Net.Diagnostics;

/// <summary>
/// Exception thrown when blueprint validation fails and <c>ValidateOnStartup</c> is enabled.
/// Wraps a <see cref="BlueprintValidationResult"/> containing all accumulated errors.
/// </summary>
public sealed class BlueprintValidationException : Exception
{
    /// <summary>
    /// Gets the validation result containing all errors and warnings.
    /// </summary>
    public BlueprintValidationResult ValidationResult { get; }

    /// <summary>
    /// Initializes a new <see cref="BlueprintValidationException"/> with the given validation result.
    /// </summary>
    /// <param name="result">The validation result.</param>
    public BlueprintValidationException(BlueprintValidationResult result)
        : base(result.ToString())
    {
        ValidationResult = result;
    }

    /// <summary>
    /// Initializes a new <see cref="BlueprintValidationException"/> with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public BlueprintValidationException(string message)
        : base(message)
    {
        ValidationResult = new BlueprintValidationResult();
    }
}
