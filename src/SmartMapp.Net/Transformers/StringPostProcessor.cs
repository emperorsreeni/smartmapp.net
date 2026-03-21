using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Transformers;

/// <summary>
/// Post-processing transformer that applies <see cref="StringTransformationOptions"/>
/// (trim, null-to-empty, custom transforms) to string values after any string-producing transformation.
/// <para>
/// This transformer is not looked up via <see cref="TypeTransformerRegistry.GetTransformer"/>;
/// instead, it is invoked as a post-processing step by the expression compiler (Sprint 4)
/// when the target property is <c>string</c>.
/// </para>
/// </summary>
public sealed class StringPostProcessor : ITypeTransformer<string, string>
{
    private readonly StringTransformationOptions _options;

    /// <summary>
    /// Initializes a new instance with the specified string transformation options.
    /// </summary>
    /// <param name="options">The string transformation options to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public StringPostProcessor(StringTransformationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public bool CanTransform(Type originType, Type targetType)
        => originType == typeof(string) && targetType == typeof(string);

    /// <summary>
    /// Applies the configured <see cref="StringTransformationOptions"/> pipeline to the input string.
    /// </summary>
    /// <param name="origin">The input string value.</param>
    /// <param name="scope">The current mapping scope.</param>
    /// <returns>The processed string.</returns>
    public string Transform(string origin, MappingScope scope)
    {
        return _options.Process(origin)!;
    }

    /// <summary>
    /// Applies the configured <see cref="StringTransformationOptions"/> pipeline to a possibly-null string.
    /// Use this overload when the origin may be <c>null</c> and you need nullable semantics.
    /// </summary>
    /// <param name="origin">The input string value (may be <c>null</c>).</param>
    /// <returns>The processed string.</returns>
    public string? ProcessNullable(string? origin)
    {
        return _options.Process(origin);
    }

    /// <summary>
    /// Gets whether the underlying options have any processing configured.
    /// When <c>false</c>, calling <see cref="Transform"/> is a no-op and can be skipped.
    /// </summary>
    public bool HasProcessing => _options.HasProcessing;
}
