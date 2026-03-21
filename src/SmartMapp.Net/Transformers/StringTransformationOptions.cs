namespace SmartMapp.Net.Transformers;

/// <summary>
/// Configuration options for global string post-processing applied after any string-producing transformation.
/// Surfaced via <c>SculptorOptions.Strings</c> in Sprint 7.
/// </summary>
public sealed class StringTransformationOptions
{
    /// <summary>
    /// Gets or sets whether to trim whitespace from all mapped string values. Default is <c>false</c>.
    /// </summary>
    public bool TrimAll { get; set; }

    /// <summary>
    /// Gets or sets whether to convert <c>null</c> string values to <see cref="string.Empty"/>. Default is <c>false</c>.
    /// </summary>
    public bool NullToEmpty { get; set; }

    private readonly List<Func<string, string>> _transforms = [];

    /// <summary>
    /// Adds a custom string transformation to the processing pipeline.
    /// Transforms are applied in registration order after <see cref="TrimAll"/> and <see cref="NullToEmpty"/>.
    /// </summary>
    /// <param name="transform">The transformation function.</param>
    public void Apply(Func<string, string> transform)
    {
        if (transform is null) throw new ArgumentNullException(nameof(transform));
        _transforms.Add(transform);
    }

    /// <summary>
    /// Processes a string value through the full transformation pipeline:
    /// <see cref="NullToEmpty"/> → <see cref="TrimAll"/> → custom transforms.
    /// </summary>
    /// <param name="input">The input string (may be <c>null</c>).</param>
    /// <returns>The processed string.</returns>
    public string? Process(string? input)
    {
        if (input is null)
            return NullToEmpty ? string.Empty : null;

        var result = TrimAll ? input.Trim() : input;

        for (int i = 0; i < _transforms.Count; i++)
        {
            result = _transforms[i](result);
        }

        return result;
    }

    /// <summary>
    /// Gets whether any processing is configured (trim, null-to-empty, or custom transforms).
    /// When <c>false</c>, the post-processor can be skipped entirely.
    /// </summary>
    public bool HasProcessing => TrimAll || NullToEmpty || _transforms.Count > 0;
}
