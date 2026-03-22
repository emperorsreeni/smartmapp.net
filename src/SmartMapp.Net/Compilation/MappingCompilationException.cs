namespace SmartMapp.Net.Compilation;

/// <summary>
/// Exception thrown when the expression compiler encounters an unrecoverable error
/// while compiling a <see cref="Blueprint"/> into a mapping delegate.
/// </summary>
public sealed class MappingCompilationException : Exception
{
    /// <summary>
    /// Gets the <see cref="SmartMapp.Net.TypePair"/> that failed compilation, if available.
    /// </summary>
    public TypePair? TypePair { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="MappingCompilationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    public MappingCompilationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of <see cref="MappingCompilationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="typePair">The type pair that failed compilation.</param>
    public MappingCompilationException(string message, TypePair typePair) : base(message)
    {
        TypePair = typePair;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MappingCompilationException"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public MappingCompilationException(string message, Exception innerException) : base(message, innerException) { }
}
