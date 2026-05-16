using SmartMapp.Net.Conventions;

namespace SmartMapp.Net.Configuration;

/// <summary>
/// Global convention configuration. Mutated during <c>AddSculptor(options => ...)</c>
/// and frozen by <c>SculptorBuilder.Forge()</c>.
/// </summary>
public sealed class ConventionOptions
{
    private readonly SculptorOptions _owner;
    private readonly List<string> _originPrefixes = new();
    private readonly List<string> _targetSuffixes = new();
    private readonly Dictionary<string, string> _abbreviations = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IPropertyConvention> _customConventions = new();

    internal ConventionOptions(SculptorOptions owner)
    {
        _owner = owner;
    }

    /// <summary>
    /// Gets a value indicating whether snake-case matching is enabled.
    /// </summary>
    public bool SnakeCaseMatchingEnabled { get; private set; }

    /// <summary>
    /// Gets a value indicating whether abbreviation expansion is enabled.
    /// </summary>
    public bool AbbreviationExpansionEnabled { get; private set; }

    /// <summary>
    /// Gets the origin member name prefixes to strip (e.g., <c>"Get"</c>, <c>"m_"</c>).
    /// </summary>
    public IReadOnlyList<string> OriginPrefixes => _originPrefixes;

    /// <summary>
    /// Gets the target member name suffixes to strip (e.g., <c>"Field"</c>, <c>"Property"</c>).
    /// </summary>
    public IReadOnlyList<string> TargetSuffixes => _targetSuffixes;

    /// <summary>
    /// Gets the abbreviation → expansion dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Abbreviations => _abbreviations;

    /// <summary>
    /// Gets the user-registered custom conventions.
    /// </summary>
    public IReadOnlyList<IPropertyConvention> CustomConventions => _customConventions;

    /// <summary>
    /// Accumulates origin member name prefixes to strip.
    /// </summary>
    /// <param name="prefixes">The prefixes.</param>
    /// <returns>This options instance for chaining.</returns>
    public ConventionOptions OriginPrefixesAdd(params string[] prefixes)
    {
        _owner.ThrowIfFrozen();
        foreach (var p in prefixes)
        {
            if (!string.IsNullOrWhiteSpace(p) && !_originPrefixes.Contains(p))
                _originPrefixes.Add(p);
        }
        return this;
    }

    /// <summary>
    /// Accumulates target member name suffixes to strip.
    /// </summary>
    /// <param name="suffixes">The suffixes.</param>
    /// <returns>This options instance for chaining.</returns>
    public ConventionOptions TargetSuffixesAdd(params string[] suffixes)
    {
        _owner.ThrowIfFrozen();
        foreach (var s in suffixes)
        {
            if (!string.IsNullOrWhiteSpace(s) && !_targetSuffixes.Contains(s))
                _targetSuffixes.Add(s);
        }
        return this;
    }

    /// <summary>
    /// Enables case-insensitive snake-case / camelCase matching.
    /// </summary>
    /// <returns>This options instance for chaining.</returns>
    public ConventionOptions EnableSnakeCaseMatching()
    {
        _owner.ThrowIfFrozen();
        SnakeCaseMatchingEnabled = true;
        return this;
    }

    /// <summary>
    /// Enables abbreviation expansion with a user-provided alias dictionary builder.
    /// </summary>
    /// <param name="configure">Callback populating the alias dictionary.</param>
    /// <returns>This options instance for chaining.</returns>
    public ConventionOptions EnableAbbreviationExpansion(Action<IDictionary<string, string>> configure)
    {
        _owner.ThrowIfFrozen();
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        AbbreviationExpansionEnabled = true;
        configure(_abbreviations);
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IPropertyConvention"/> implementation.
    /// </summary>
    /// <typeparam name="TConvention">The convention type (requires parameterless constructor).</typeparam>
    /// <returns>This options instance for chaining.</returns>
    public ConventionOptions Add<TConvention>() where TConvention : IPropertyConvention, new()
    {
        _owner.ThrowIfFrozen();
        _customConventions.Add(new TConvention());
        return this;
    }

    /// <summary>
    /// Registers a custom <see cref="IPropertyConvention"/> instance.
    /// </summary>
    /// <param name="convention">The convention instance.</param>
    /// <returns>This options instance for chaining.</returns>
    public ConventionOptions Add(IPropertyConvention convention)
    {
        _owner.ThrowIfFrozen();
        if (convention is null) throw new ArgumentNullException(nameof(convention));
        _customConventions.Add(convention);
        return this;
    }
}
