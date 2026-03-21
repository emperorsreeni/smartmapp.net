namespace SmartMapp.Net.Transformers;

/// <summary>
/// Defines the strategy used when mapping between enum types.
/// </summary>
public enum EnumMappingStrategy
{
    /// <summary>
    /// Match enum members by name (case-insensitive by default).
    /// </summary>
    ByName = 0,

    /// <summary>
    /// Match enum members by their underlying numeric value.
    /// </summary>
    ByValue = 1,

    /// <summary>
    /// Match enum members using <c>[MapsIntoEnum]</c> attributes.
    /// </summary>
    ByAttribute = 2,
}
