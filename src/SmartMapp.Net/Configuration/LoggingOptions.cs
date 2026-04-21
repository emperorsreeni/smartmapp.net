namespace SmartMapp.Net.Configuration;

/// <summary>
/// Global logging configuration. Mutated during <c>AddSculptor(options => ...)</c>
/// and frozen by <c>SculptorBuilder.Forge()</c>.
/// </summary>
public sealed class LoggingOptions
{
    private readonly SculptorOptions _owner;
    private SculptorLogLevel _minimumLevel = SculptorLogLevel.Warning;
    private bool _logBlueprints;

    internal LoggingOptions(SculptorOptions owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Gets or sets the minimum log level. Default is <see cref="SculptorLogLevel.Warning"/>.
    /// </summary>
    public SculptorLogLevel MinimumLevel
    {
        get => _minimumLevel;
        set { _owner.ThrowIfFrozen(); _minimumLevel = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether full blueprints should be logged at creation time.
    /// </summary>
    public bool LogBlueprints
    {
        get => _logBlueprints;
        set { _owner.ThrowIfFrozen(); _logBlueprints = value; }
    }
}

/// <summary>
/// Log level enumeration used by <see cref="LoggingOptions"/>. Decoupled from
/// <c>Microsoft.Extensions.Logging</c> to avoid a dependency in the core package.
/// </summary>
public enum SculptorLogLevel
{
    /// <summary>Diagnostic trace (per-property links, cache hits).</summary>
    Trace,
    /// <summary>Debug information.</summary>
    Debug,
    /// <summary>Informational messages (blueprint creation, promotions).</summary>
    Information,
    /// <summary>Warnings (unlinked members, structural similarity matches).</summary>
    Warning,
    /// <summary>Errors.</summary>
    Error,
    /// <summary>Critical failures.</summary>
    Critical,
    /// <summary>Disables logging.</summary>
    None,
}
