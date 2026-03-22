namespace SmartMapp.Net.Transformers;

/// <summary>
/// Configuration options for enum-to-enum and enum-to-string transformations.
/// Surfaced via <c>SculptorOptions.Enums</c> in Sprint 7.
/// </summary>
public sealed class EnumTransformerOptions
{
    /// <summary>
    /// Gets or sets the strategy used for enum-to-enum mapping. Default is <see cref="EnumMappingStrategy.ByName"/>.
    /// </summary>
    public EnumMappingStrategy Strategy { get; set; } = EnumMappingStrategy.ByName;

    /// <summary>
    /// Gets or sets whether name-based enum matching is case-insensitive. Default is <c>true</c>.
    /// </summary>
    public bool CaseInsensitive { get; set; } = true;

    /// <summary>
    /// Gets or sets whether <c>[Description]</c> attribute values should be used for enum-to-string conversion.
    /// Default is <c>false</c>.
    /// </summary>
    public bool UseDescriptionAttribute { get; set; }

    private readonly Dictionary<Type, object> _fallbackValues = new();

    /// <summary>
    /// Sets a fallback value for a specific enum target type, used when no matching member is found.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="value">The fallback value.</param>
    public void FallbackValue<T>(T value) where T : struct, Enum
    {
        _fallbackValues[typeof(T)] = value;
    }

    /// <summary>
    /// Attempts to retrieve the configured fallback value for the given enum type.
    /// </summary>
    /// <param name="enumType">The target enum type.</param>
    /// <param name="fallback">The fallback value if configured.</param>
    /// <returns><c>true</c> if a fallback is configured; otherwise <c>false</c>.</returns>
    public bool TryGetFallback(Type enumType, out object? fallback)
    {
        return _fallbackValues.TryGetValue(enumType, out fallback);
    }
}
