namespace SmartMapp.Net.Attributes;

/// <summary>
/// Declares that the decorated enum value maps into a specific target value (enum member or integer)
/// when enum-to-enum or enum-to-number mapping is applied.
/// </summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class MapsIntoEnumAttribute : Attribute
{
    /// <summary>
    /// Gets the target value this enum field maps into.
    /// </summary>
    public object TargetValue { get; }

    /// <summary>
    /// Initializes a new <see cref="MapsIntoEnumAttribute"/>.
    /// </summary>
    /// <param name="targetValue">The target value (typically an enum member or integer).</param>
    public MapsIntoEnumAttribute(object targetValue)
    {
        TargetValue = targetValue ?? throw new ArgumentNullException(nameof(targetValue));
    }
}
