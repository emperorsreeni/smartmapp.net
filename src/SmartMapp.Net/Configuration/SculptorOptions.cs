using System.Diagnostics;
using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Runtime;

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
    /// <remarks>
    /// Spec §S8-T05 Outputs bullet 3 documents the intended default as "true when
    /// <c>IHostEnvironment.IsDevelopment()</c>, false otherwise". The option itself always reads
    /// <c>true</c> so builder-only (non-host) consumers keep fail-fast semantics; the
    /// environment-aware gating is applied by <c>SculptorStartupValidator</c> in the
    /// <c>SmartMapp.Net.DependencyInjection</c> package and only kicks in when the user did
    /// not set this property explicitly (tracked via <see cref="IsValidateOnStartupExplicitlySet"/>).
    /// </remarks>
    public bool ValidateOnStartup
    {
        get => _validateOnStartup;
        set
        {
            ThrowIfFrozen();
            _validateOnStartup = value;
            _validateOnStartupExplicit = true;
        }
    }
    private bool _validateOnStartup = true;
    private bool _validateOnStartupExplicit;

    /// <summary>
    /// Gets a value indicating whether <see cref="ValidateOnStartup"/> has been assigned by the
    /// user. Consumed by <c>SculptorStartupValidator</c> (in
    /// <c>SmartMapp.Net.DependencyInjection</c>) to decide whether the environment-aware default
    /// (skip in non-Development hosts) should be applied. Stays <c>false</c> when the option
    /// retains its constructed default (<c>true</c>).
    /// </summary>
    public bool IsValidateOnStartupExplicitlySet => _validateOnStartupExplicit;

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
    /// Gets or sets a value indicating whether a new sculptor should be forged for every
    /// DI scope or transient resolve when the sculptor is registered via
    /// <c>AddSculptor(ServiceLifetime.Scoped)</c> or <c>AddSculptor(ServiceLifetime.Transient)</c>.
    /// Defaults to <c>false</c> — a single global sculptor is shared across scopes/resolves
    /// even when the handle lifetime is not Singleton. Set to <c>true</c> only when a mapping
    /// configuration legitimately varies per request or per scope.
    /// </summary>
    /// <remarks>
    /// Per spec §11.2. When the handle is registered as <c>ServiceLifetime.Singleton</c>,
    /// this flag has no effect. A future analyzer (Sprint 20) will warn if this is enabled
    /// together with <c>ServiceLifetime.Singleton</c>.
    /// </remarks>
    public bool AllowPerScopeRebuild
    {
        get => _allowPerScopeRebuild;
        set { ThrowIfFrozen(); _allowPerScopeRebuild = value; }
    }
    private bool _allowPerScopeRebuild;

    /// <summary>
    /// Gets or sets the strategy used to instantiate DI-deferred
    /// <see cref="IValueProvider"/> and <see cref="ITypeTransformer"/> types referenced via
    /// <c>p.From&lt;T&gt;()</c>, <c>p.TransformWith&lt;T&gt;()</c>, <c>[ProvideWith]</c>, and
    /// <c>[TransformWith]</c>. Defaults to <see cref="DefaultProviderResolver.Instance"/>,
    /// which tries <see cref="IServiceProvider.GetService(Type)"/> and falls back to
    /// <see cref="Activator.CreateInstance(Type)"/> for parameterless-constructor types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Introduced in Sprint 8 · S8-T04 per spec §11.4. The
    /// <c>SmartMapp.Net.DependencyInjection</c> package installs a richer resolver
    /// (<c>DependencyInjectionProviderFactory</c>) that uses
    /// <c>Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance</c> so
    /// types with non-default constructor dependencies can still be activated when they are
    /// not registered in the container. Third-party containers (Autofac, Lamar, …) can plug
    /// in their own <see cref="IProviderResolver"/> by assigning this property before calling
    /// <c>SculptorBuilder.Forge()</c>.
    /// </para>
    /// <para>
    /// Setting the property to <c>null</c> reverts to <see cref="DefaultProviderResolver.Instance"/>.
    /// </para>
    /// </remarks>
    public IProviderResolver ProviderResolver
    {
        get => _providerResolver;
        set { ThrowIfFrozen(); _providerResolver = value ?? DefaultProviderResolver.Instance; }
    }
    private IProviderResolver _providerResolver = DefaultProviderResolver.Instance;

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

    private readonly List<InlineCompositionRegistration> _inlineCompositions = new();

    /// <summary>
    /// Gets the inline composition registrations queued via
    /// <see cref="Compose{TTarget}(Action{ICompositionRule{TTarget}})"/> — consumed by the
    /// forge pipeline (Sprint 8 · S8-T08) to produce
    /// <see cref="Composition.CompositionBlueprint"/> records on the forged configuration.
    /// </summary>
    internal IReadOnlyList<InlineCompositionRegistration> InlineCompositions => _inlineCompositions;

    /// <summary>
    /// Registers a multi-origin composition rule for <typeparamref name="TTarget"/>. The
    /// callback receives an <see cref="ICompositionRule{TTarget}"/> on which the user can call
    /// <see cref="ICompositionRule{TTarget}.FromOrigin{TOrigin}"/> for each contributing origin
    /// type. Executed during <c>SculptorBuilder.Forge()</c> against the shared builder, so
    /// rules registered via <c>options.Compose&lt;T&gt;(...)</c> behave identically to those
    /// registered via <c>SculptorBuilder.Compose&lt;T&gt;()</c> — spec §S8-T08 Acceptance
    /// bullet 7.
    /// </summary>
    /// <typeparam name="TTarget">The composed target type.</typeparam>
    /// <param name="configure">Composition rule configuration callback.</param>
    /// <returns>This options instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <c>null</c>.</exception>
    public SculptorOptions Compose<TTarget>(Action<ICompositionRule<TTarget>> configure)
    {
        ThrowIfFrozen();
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        _inlineCompositions.Add(new InlineCompositionRegistration(typeof(TTarget), builder =>
        {
            var rule = builder.Compose<TTarget>();
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
