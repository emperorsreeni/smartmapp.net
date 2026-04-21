using System.Diagnostics;
using System.Reflection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Configuration;

/// <summary>
/// The global configuration bag for a sculptor builder. Mutable during the
/// configuration phase and frozen by <see cref="Freeze"/> (called by <c>SculptorBuilder.Forge()</c>).
/// Covers the surface documented in spec §6.4 and §12.1.
/// </summary>
public sealed class SculptorOptions
{
    private readonly List<Assembly> _assemblies = new();
    private readonly List<Type> _blueprintTypes = new();
    private readonly List<MappingBlueprint> _blueprintInstances = new();
    private readonly List<Type> _transformerTypes = new();
    private readonly List<InlineBindingRegistration> _inlineBindings = new();

    /// <summary>
    /// Initializes a new <see cref="SculptorOptions"/> with spec-aligned defaults.
    /// </summary>
    public SculptorOptions()
    {
        Conventions = new ConventionOptions(this);
        Nulls = new NullOptions(this);
        Throughput = new ThroughputOptions(this);
        Logging = new LoggingOptions(this);
    }

    /// <summary>
    /// Gets the convention configuration.
    /// </summary>
    public ConventionOptions Conventions { get; }

    /// <summary>
    /// Gets the null-handling configuration.
    /// </summary>
    public NullOptions Nulls { get; }

    /// <summary>
    /// Gets the throughput / performance configuration.
    /// </summary>
    public ThroughputOptions Throughput { get; }

    /// <summary>
    /// Gets the logging configuration.
    /// </summary>
    public LoggingOptions Logging { get; }

    /// <summary>
    /// Gets or sets the maximum nested mapping recursion depth. Default is 10.
    /// </summary>
    public int MaxRecursionDepth
    {
        get => _maxRecursionDepth;
        set
        {
            ThrowIfFrozen();
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Depth must be positive.");
            _maxRecursionDepth = value;
        }
    }
    private int _maxRecursionDepth = 10;

    /// <summary>
    /// Gets or sets a value indicating whether all blueprints are validated when the sculptor is forged.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateOnStartup
    {
        get => _validateOnStartup;
        set { ThrowIfFrozen(); _validateOnStartup = value; }
    }
    private bool _validateOnStartup = true;

    /// <summary>
    /// Gets or sets a value indicating whether unlinked target members are reported as errors.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool StrictMode
    {
        get => _strictMode;
        set { ThrowIfFrozen(); _strictMode = value; }
    }
    private bool _strictMode;

    /// <summary>
    /// Gets or sets a value indicating whether validation throws if any target member is unlinked.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool ThrowOnUnlinkedMembers
    {
        get => _throwOnUnlinkedMembers;
        set { ThrowIfFrozen(); _throwOnUnlinkedMembers = value; }
    }
    private bool _throwOnUnlinkedMembers;

    /// <summary>
    /// Gets a value indicating whether the options have been frozen (post-<c>Forge()</c>).
    /// </summary>
    public bool IsFrozen { get; private set; }

    /// <summary>
    /// Gets the assemblies queued for scanning.
    /// </summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>
    /// Gets the blueprint types queued for instantiation.
    /// </summary>
    public IReadOnlyList<Type> BlueprintTypes => _blueprintTypes;

    /// <summary>
    /// Gets the blueprint instances queued for use.
    /// </summary>
    public IReadOnlyList<MappingBlueprint> BlueprintInstances => _blueprintInstances;

    /// <summary>
    /// Gets the transformer types queued for registration.
    /// </summary>
    public IReadOnlyList<Type> TransformerTypes => _transformerTypes;

    /// <summary>
    /// Gets the inline bindings queued via <see cref="Bind{TOrigin,TTarget}(Action{IBindingRule{TOrigin,TTarget}})"/>.
    /// </summary>
    internal IReadOnlyList<InlineBindingRegistration> InlineBindings => _inlineBindings;

    /// <summary>
    /// Queues one or more assemblies to be scanned by the <see cref="Discovery.AssemblyScanner"/>.
    /// Duplicates are silently ignored.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions ScanAssemblies(params Assembly[] assemblies)
    {
        ThrowIfFrozen();
        if (assemblies is null) return this;
        foreach (var asm in assemblies)
        {
            if (asm is not null && !_assemblies.Contains(asm))
                _assemblies.Add(asm);
        }
        return this;
    }

    /// <summary>
    /// Queues the assembly containing <typeparamref name="T"/> for scanning.
    /// </summary>
    /// <typeparam name="T">A marker type whose assembly should be scanned.</typeparam>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions ScanAssembliesContaining<T>()
    {
        ThrowIfFrozen();
        return ScanAssemblies(typeof(T).Assembly);
    }

    /// <summary>
    /// Queues a <see cref="MappingBlueprint"/> subclass for instantiation and registration.
    /// </summary>
    /// <typeparam name="TBlueprint">The blueprint type.</typeparam>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions UseBlueprint<TBlueprint>() where TBlueprint : MappingBlueprint, new()
    {
        ThrowIfFrozen();
        if (!_blueprintTypes.Contains(typeof(TBlueprint)))
            _blueprintTypes.Add(typeof(TBlueprint));
        return this;
    }

    /// <summary>
    /// Queues a <see cref="MappingBlueprint"/> instance for registration.
    /// </summary>
    /// <param name="blueprint">The blueprint instance.</param>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions UseBlueprint(MappingBlueprint blueprint)
    {
        ThrowIfFrozen();
        if (blueprint is null) throw new ArgumentNullException(nameof(blueprint));
        _blueprintInstances.Add(blueprint);
        return this;
    }

    /// <summary>
    /// Queues a transformer type for registration with the <see cref="Transformers.TypeTransformerRegistry"/>.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer type (must implement <see cref="ITypeTransformer"/>).</typeparam>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions AddTransformer<TTransformer>() where TTransformer : class, ITypeTransformer
    {
        ThrowIfFrozen();
        if (!_transformerTypes.Contains(typeof(TTransformer)))
            _transformerTypes.Add(typeof(TTransformer));
        return this;
    }

    /// <summary>
    /// Queues a transformer type for registration with the <see cref="Transformers.TypeTransformerRegistry"/>.
    /// </summary>
    /// <param name="transformerType">The transformer type.</param>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions AddTransformer(Type transformerType)
    {
        ThrowIfFrozen();
        if (transformerType is null) throw new ArgumentNullException(nameof(transformerType));
        if (!typeof(ITypeTransformer).IsAssignableFrom(transformerType))
            throw new ArgumentException(
                $"Type '{transformerType.FullName}' does not implement ITypeTransformer.",
                nameof(transformerType));
        if (!_transformerTypes.Contains(transformerType))
            _transformerTypes.Add(transformerType);
        return this;
    }

    /// <summary>
    /// Registers an inline binding for <c>(TOrigin, TTarget)</c>. The callback receives an
    /// <see cref="IBindingRule{TOrigin,TTarget}"/> and is executed during <c>Forge()</c>.
    /// </summary>
    /// <typeparam name="TOrigin">The origin type.</typeparam>
    /// <typeparam name="TTarget">The target type.</typeparam>
    /// <param name="configure">Binding configuration callback.</param>
    /// <returns>This options instance for chaining.</returns>
    public SculptorOptions Bind<TOrigin, TTarget>(Action<IBindingRule<TOrigin, TTarget>> configure)
    {
        ThrowIfFrozen();
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var pair = TypePair.Of<TOrigin, TTarget>();
        _inlineBindings.Add(new InlineBindingRegistration(pair, builder =>
        {
            var rule = builder.Bind<TOrigin, TTarget>();
            configure(rule);
        }));
        return this;
    }

    /// <summary>
    /// Freezes the options — subsequent mutator calls throw <see cref="InvalidOperationException"/>.
    /// Called by <c>SculptorBuilder.Forge()</c>.
    /// </summary>
    public void Freeze()
    {
        IsFrozen = true;
    }

    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when <see cref="IsFrozen"/> is true.
    /// </summary>
#if NET8_0_OR_GREATER
    [StackTraceHidden]
#endif
    internal void ThrowIfFrozen()
    {
        if (IsFrozen)
            throw new InvalidOperationException(
                "SculptorOptions are frozen — configuration cannot be modified after SculptorBuilder.Forge() has been called.");
    }
}
