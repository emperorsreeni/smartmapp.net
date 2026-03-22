namespace SmartMapp.Net;

/// <summary>
/// Base exception for all SmartMapp.Net library exceptions.
/// Provides a common catch target for callers who want to handle any mapping-related error.
/// </summary>
public class SmartMappException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="SmartMappException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public SmartMappException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SmartMappException"/> with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SmartMappException(string message, Exception? innerException)
        : base(message, innerException!)
    {
    }
}
