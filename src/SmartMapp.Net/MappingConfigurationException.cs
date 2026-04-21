namespace SmartMapp.Net;

/// <summary>
/// Thrown when a mapping cannot proceed because no <see cref="Blueprint"/> is registered for the
/// requested type pair, or the sculptor configuration is otherwise incomplete at runtime.
/// </summary>
public sealed class MappingConfigurationException : SmartMappException
{
    /// <summary>
    /// Gets the type pair that caused the failure, when available.
    /// </summary>
    public TypePair? TypePair { get; }

    /// <summary>
    /// Initializes a new <see cref="MappingConfigurationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MappingConfigurationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="MappingConfigurationException"/> with a type pair context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="typePair">The type pair that caused the failure.</param>
    public MappingConfigurationException(string message, TypePair typePair) : base(message)
    {
        TypePair = typePair;
    }

    /// <summary>
    /// Initializes a new <see cref="MappingConfigurationException"/> with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MappingConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
