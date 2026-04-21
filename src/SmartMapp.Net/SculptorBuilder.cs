using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net;

/// <summary>
/// Default implementation of <see cref="ISculptorBuilder"/>. Accumulates configuration
/// and orchestrates the <c>Forge()</c> pipeline (implemented in <see cref="SculptorBuildPipeline"/>).
/// </summary>
public sealed class SculptorBuilder : ISculptorBuilder
{
    private readonly SculptorOptions _options;
    private readonly BlueprintBuilder _blueprintBuilder = new();
    private readonly AssemblyScanner _scanner;
    private bool _isForged;

    /// <summary>
    /// Initializes a new <see cref="SculptorBuilder"/> with default options and a fresh
    /// <see cref="AssemblyScanner"/>.
    /// </summary>
    public SculptorBuilder()
        : this(new SculptorOptions(), new AssemblyScanner())
    {
    }

    /// <summary>
    /// Initializes a new <see cref="SculptorBuilder"/> with the supplied options.
    /// </summary>
    /// <param name="options">Pre-populated options instance.</param>
    public SculptorBuilder(SculptorOptions options)
        : this(options, new AssemblyScanner())
    {
    }

    /// <summary>
    /// Test constructor — accepts a custom <see cref="AssemblyScanner"/>.
    /// </summary>
    internal SculptorBuilder(SculptorOptions options, AssemblyScanner scanner)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
    }

    /// <inheritdoc />
    public SculptorOptions Options
    {
        get
        {
            ThrowIfForged();
            return _options;
        }
    }

    /// <inheritdoc />
    public IBindingRule<TOrigin, TTarget> Bind<TOrigin, TTarget>()
    {
        ThrowIfForged();
        return _blueprintBuilder.Bind<TOrigin, TTarget>();
    }

    /// <inheritdoc />
    public ICompositionRule<TTarget> Compose<TTarget>()
    {
        ThrowIfForged();
        return _blueprintBuilder.Compose<TTarget>();
    }

    /// <inheritdoc />
    public ISculptorBuilder UseBlueprint<TBlueprint>() where TBlueprint : MappingBlueprint, new()
    {
        ThrowIfForged();
        _options.UseBlueprint<TBlueprint>();
        return this;
    }

    /// <inheritdoc />
    public ISculptorBuilder UseBlueprint(MappingBlueprint blueprint)
    {
        ThrowIfForged();
        _options.UseBlueprint(blueprint);
        return this;
    }

    /// <inheritdoc />
    public ISculptorBuilder AddTransformer<TTransformer>() where TTransformer : class, ITypeTransformer
    {
        ThrowIfForged();
        _options.AddTransformer<TTransformer>();
        return this;
    }

    /// <inheritdoc />
    public ISculptorBuilder ScanAssemblies(params Assembly[] assemblies)
    {
        ThrowIfForged();
        _options.ScanAssemblies(assemblies);
        return this;
    }

    /// <inheritdoc />
    public ISculptorBuilder ScanAssembliesContaining<T>()
    {
        ThrowIfForged();
        _options.ScanAssembliesContaining<T>();
        return this;
    }

    /// <inheritdoc />
    public ISculptorBuilder Configure(Action<SculptorOptions> configure)
    {
        ThrowIfForged();
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        configure(_options);
        return this;
    }

    /// <inheritdoc />
    public ISculptor Forge()
    {
        ThrowIfForged();
        _isForged = true;

        var inputs = new SculptorBuildInputs
        {
            Options = _options,
            Assemblies = _options.Assemblies,
            BlueprintTypes = _options.BlueprintTypes,
            BlueprintInstances = _options.BlueprintInstances,
            TransformerTypes = _options.TransformerTypes,
            InlineBindings = _options.InlineBindings,
            BlueprintBuilder = _blueprintBuilder,
            BlueprintBuilderImpl = _blueprintBuilder,
        };

        var pipeline = new SculptorBuildPipeline(_scanner);
        return pipeline.Execute(inputs);
    }

    private void ThrowIfForged()
    {
        if (_isForged)
            throw new InvalidOperationException(
                "SculptorBuilder has already been forged — configuration is immutable after Forge() returns.");
    }
}
