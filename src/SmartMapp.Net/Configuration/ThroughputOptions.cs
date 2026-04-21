namespace SmartMapp.Net.Configuration;

/// <summary>
/// Global performance configuration. Mutated during <c>AddSculptor(options => ...)</c>
/// and frozen by <c>SculptorBuilder.Forge()</c>.
/// </summary>
public sealed class ThroughputOptions
{
    private readonly SculptorOptions _owner;
    private int _parallelCollectionThreshold = 1000;
    private int _maxDegreeOfParallelism = Environment.ProcessorCount;
    private bool _enableILEmit = true;
    private bool _enableAdaptivePromotion = true;
    private int _adaptivePromotionThreshold = 10;
    private bool _lazyBlueprintCompilation;

    internal ThroughputOptions(SculptorOptions owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Gets or sets the minimum collection size for parallel mapping. Default is 1000.
    /// </summary>
    public int ParallelCollectionThreshold
    {
        get => _parallelCollectionThreshold;
        set
        {
            _owner.ThrowIfFrozen();
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Threshold must be positive.");
            _parallelCollectionThreshold = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum degree of parallelism. Default is <see cref="Environment.ProcessorCount"/>.
    /// </summary>
    public int MaxDegreeOfParallelism
    {
        get => _maxDegreeOfParallelism;
        set
        {
            _owner.ThrowIfFrozen();
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Parallelism must be >= 1.");
            _maxDegreeOfParallelism = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the IL emit backend is enabled (Sprint 9+).
    /// Default is <c>true</c>. Currently a no-op until Sprint 9 ships.
    /// </summary>
    public bool EnableILEmit
    {
        get => _enableILEmit;
        set { _owner.ThrowIfFrozen(); _enableILEmit = value; }
    }

    /// <summary>
    /// Gets or sets a value indicating whether adaptive promotion (Expression → IL) is enabled.
    /// Default is <c>true</c>. Currently a no-op until Sprint 9 ships.
    /// </summary>
    public bool EnableAdaptivePromotion
    {
        get => _enableAdaptivePromotion;
        set { _owner.ThrowIfFrozen(); _enableAdaptivePromotion = value; }
    }

    /// <summary>
    /// Gets or sets the invocation count after which a pair is promoted to IL Emit. Default is 10.
    /// </summary>
    public int AdaptivePromotionThreshold
    {
        get => _adaptivePromotionThreshold;
        set
        {
            _owner.ThrowIfFrozen();
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Threshold must be positive.");
            _adaptivePromotionThreshold = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether blueprint compilation should be deferred
    /// until the first mapping instead of happening eagerly during <c>Forge()</c>.
    /// Default is <c>false</c>.
    /// </summary>
    public bool LazyBlueprintCompilation
    {
        get => _lazyBlueprintCompilation;
        set { _owner.ThrowIfFrozen(); _lazyBlueprintCompilation = value; }
    }
}
