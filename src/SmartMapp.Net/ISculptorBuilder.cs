using System.Reflection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Configuration;

namespace SmartMapp.Net;

/// <summary>
/// Fluent builder entry point that accumulates sculptor configuration and produces an immutable
/// <see cref="ISculptor"/> via <see cref="Forge"/>. Mirrors the surface documented in spec §14.3.
/// <para>
/// Typical usage:
/// <code>
/// var sculptor = new SculptorBuilder()
///     .ScanAssembliesContaining&lt;OrderBlueprint&gt;()
///     .UseBlueprint&lt;OrderBlueprint&gt;()
///     .Bind&lt;Order, OrderDto&gt;()
///     .Forge();
/// </code>
/// </para>
/// </summary>
public interface ISculptorBuilder
{
    /// <summary>
    /// Gets the underlying <see cref="SculptorOptions"/> — useful for invoking global
    /// configuration via <c>builder.Options.Conventions.OriginPrefixesAdd(...)</c>.
    /// </summary>
    SculptorOptions Options { get; }

    /// <summary>
    /// Registers a binding for the specified origin/target type pair and returns a fluent rule
    /// for configuration. Duplicates throw <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <typeparam name="TOrigin">The source type.</typeparam>
    /// <typeparam name="TTarget">The destination type.</typeparam>
    /// <returns>A fluent binding rule.</returns>
    IBindingRule<TOrigin, TTarget> Bind<TOrigin, TTarget>();

    /// <summary>
    /// Registers a multi-origin composition rule for the specified target type.
    /// </summary>
    /// <typeparam name="TTarget">The composed target type.</typeparam>
    /// <returns>A fluent composition rule (stub until Sprint 15).</returns>
    ICompositionRule<TTarget> Compose<TTarget>();

    /// <summary>
    /// Queues a <see cref="MappingBlueprint"/> subclass for instantiation and registration.
    /// </summary>
    /// <typeparam name="TBlueprint">The blueprint type (requires parameterless constructor).</typeparam>
    /// <returns>This builder for chaining.</returns>
    ISculptorBuilder UseBlueprint<TBlueprint>() where TBlueprint : MappingBlueprint, new();

    /// <summary>
    /// Registers a pre-built <see cref="MappingBlueprint"/> instance.
    /// </summary>
    /// <param name="blueprint">The blueprint instance.</param>
    /// <returns>This builder for chaining.</returns>
    ISculptorBuilder UseBlueprint(MappingBlueprint blueprint);

    /// <summary>
    /// Queues a transformer type for registration with the sculptor's transformer registry.
    /// </summary>
    /// <typeparam name="TTransformer">The transformer type (must implement <see cref="ITypeTransformer"/>).</typeparam>
    /// <returns>This builder for chaining.</returns>
    ISculptorBuilder AddTransformer<TTransformer>() where TTransformer : class, ITypeTransformer;

    /// <summary>
    /// Queues one or more assemblies to be scanned for blueprints, providers, transformers, and attributed types.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan.</param>
    /// <returns>This builder for chaining.</returns>
    ISculptorBuilder ScanAssemblies(params Assembly[] assemblies);

    /// <summary>
    /// Queues the assembly containing <typeparamref name="T"/> for scanning.
    /// </summary>
    /// <typeparam name="T">A marker type whose assembly should be scanned.</typeparam>
    /// <returns>This builder for chaining.</returns>
    ISculptorBuilder ScanAssembliesContaining<T>();

    /// <summary>
    /// Mutates the global <see cref="SculptorOptions"/> via a configuration callback.
    /// </summary>
    /// <param name="configure">A callback invoked with the live options instance.</param>
    /// <returns>This builder for chaining.</returns>
    ISculptorBuilder Configure(Action<SculptorOptions> configure);

    /// <summary>
    /// Freezes configuration, runs discovery, validates, compiles delegates, and returns an
    /// immutable <see cref="ISculptor"/>. Calling <c>Forge()</c> more than once throws
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>The forged sculptor.</returns>
    ISculptor Forge();
}
