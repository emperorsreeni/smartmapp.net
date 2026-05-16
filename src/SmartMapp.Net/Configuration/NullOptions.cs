namespace SmartMapp.Net.Configuration;

/// <summary>
/// Global null-handling configuration. Mutated during <c>AddSculptor(options => ...)</c>
/// and frozen by <c>SculptorBuilder.Forge()</c>.
/// </summary>
public sealed class NullOptions
{
    private readonly SculptorOptions _owner;
    private string? _fallbackForStrings;
    private bool _throwOnNullOrigin;
    private bool _useDefaultForNullTarget = true;

    internal NullOptions(SculptorOptions owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Gets or sets the fallback value used when a string origin member is null.
    /// Default is <c>null</c> (preserve null).
    /// </summary>
    public string? FallbackForStrings
    {
        get => _fallbackForStrings;
        set { _owner.ThrowIfFrozen(); _fallbackForStrings = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether mapping throws when the origin is <c>null</c>.
    /// Default is <c>false</c>.
    /// </summary>
    public bool ThrowOnNullOrigin
    {
        get => _throwOnNullOrigin;
        set { _owner.ThrowIfFrozen(); _throwOnNullOrigin = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether nullable targets receive <c>default</c> when the origin is null.
    /// Default is <c>true</c>.
    /// </summary>
    public bool UseDefaultForNullTarget
    {
        get => _useDefaultForNullTarget;
        set { _owner.ThrowIfFrozen(); _useDefaultForNullTarget = value; }
    }
}
