using SmartMapp.Net;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Exception thrown when a type transformation fails during mapping.
/// Contains diagnostic information about the failed conversion attempt.
/// </summary>
public class TransformationException : SmartMappException
{
    /// <summary>
    /// Gets the value that failed to transform.
    /// </summary>
    public object? OriginValue { get; }

    /// <summary>
    /// Gets the source type of the failed transformation.
    /// </summary>
    public Type? OriginType { get; }

    /// <summary>
    /// Gets the intended target type of the failed transformation.
    /// </summary>
    public Type? TargetType { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="TransformationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public TransformationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TransformationException"/> with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TransformationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="TransformationException"/> with full diagnostic context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="originValue">The value that failed to transform.</param>
    /// <param name="originType">The source type.</param>
    /// <param name="targetType">The target type.</param>
    /// <param name="innerException">The optional inner exception.</param>
    public TransformationException(
        string message,
        object? originValue,
        Type? originType,
        Type? targetType,
        Exception? innerException = null)
        : base(message, innerException)
    {
        OriginValue = originValue;
        OriginType = originType;
        TargetType = targetType;
    }
}
