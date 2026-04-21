namespace SmartMapp.Net.Discovery;

/// <summary>
/// Identifies which mapping attribute produced a <see cref="ScannedTypePair"/>.
/// </summary>
public enum AttributeSource
{
    /// <summary>
    /// Produced by <see cref="Attributes.MappedByAttribute"/> or its generic form.
    /// </summary>
    MappedBy,

    /// <summary>
    /// Produced by <see cref="Attributes.MapsIntoAttribute"/> or its generic form.
    /// </summary>
    MapsInto,
}
