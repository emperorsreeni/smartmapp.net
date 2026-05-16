// SPDX-License-Identifier: MIT
// <copyright file="SculptorServiceCollectionExtensions.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Internal;
using SmartMapp.Net.Discovery;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods that register
/// <see cref="ISculptor"/>, <see cref="ISculptorConfiguration"/>, and supporting infrastructure
/// for the SmartMapp.Net mapping runtime. Mirrors the surface documented in spec §11.1 and §14.5.
/// </summary>
public static class SculptorServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="ISculptor"/> and its <see cref="ISculptorConfiguration"/> in the
    /// service collection using the calling assembly as the default scan scope. All registrations
    /// are <see cref="ServiceLifetime.Singleton"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// The <c>Forge()</c> pipeline runs lazily on the first resolve of <see cref="ISculptor"/>,
    /// not during this call — so <c>AddSculptor()</c> can be invoked before any other services
    /// (including those the sculptor ultimately depends upon) are registered.
    /// </para>
    /// <para>
    /// Calling <c>AddSculptor()</c> a second time on the same service collection throws
    /// <see cref="InvalidOperationException"/>. Multi-sculptor scenarios will be supported via
    /// keyed registrations in Sprint 16 (spec §11.5).
    /// </para>
    /// <para>
    /// <b>NativeAOT / trimmer note:</b> the calling-assembly inference relies on
    /// <see cref="Assembly.GetCallingAssembly"/>, which is unreliable under NativeAOT because
    /// stack walking is limited. The method is marked <see cref="MethodImplOptions.NoInlining"/>
    /// so the JIT cannot collapse the frame, but AOT publications should prefer the explicit
    /// <c>AddSculptor(options =&gt; options.ScanAssembliesContaining&lt;T&gt;())</c> overload
    /// (landing in S8-T02) to avoid depending on the behaviour of the intrinsic in an AOT world.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddSculptor()</c> has already been called on this <paramref name="services"/> instance.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddSculptor(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        // NoInlining on this method guarantees Assembly.GetCallingAssembly() returns the
        // assembly of the user-code frame that called services.AddSculptor(), not our own
        // assembly. Under NativeAOT this intrinsic can return the current assembly — users
        // should prefer the explicit-assembly overload in that scenario (see remarks).
        var callingAssembly = Assembly.GetCallingAssembly();
        return AddSculptorCore(services, new[] { callingAssembly }, configure: null, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Registers an <see cref="ISculptor"/> using a <see cref="SmartMapp.Net.Configuration.SculptorOptions"/>
    /// configuration callback. Scan scope defaults to the calling assembly unless the callback
    /// explicitly scans via <c>options.ScanAssemblies(...)</c> or
    /// <c>options.ScanAssembliesContaining&lt;T&gt;()</c>, in which case the user-supplied
    /// assemblies fully replace the default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options configuration callback. Invoked exactly once on first resolve.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddSculptor()</c> has already been called on this collection, or when
    /// the <paramref name="configure"/> callback throws (wrapped).
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddSculptor(
        this IServiceCollection services,
        Action<SmartMapp.Net.Configuration.SculptorOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var callingAssembly = Assembly.GetCallingAssembly();
        return AddSculptorCore(services, new[] { callingAssembly }, configure, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Registers an <see cref="ISculptor"/> with a specific <see cref="ServiceLifetime"/>.
    /// The underlying sculptor is always forged once per <see cref="IServiceProvider"/> root
    /// (Singleton-internal) unless <see cref="SmartMapp.Net.Configuration.SculptorOptions.AllowPerScopeRebuild"/>
    /// is set — in which case <see cref="ServiceLifetime.Scoped"/> forges a fresh sculptor per
    /// scope and <see cref="ServiceLifetime.Transient"/> forges one per resolve.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">Lifetime of the <see cref="ISculptor"/> service handle.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddSculptor()</c> has already been called on this collection.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddSculptor(
        this IServiceCollection services,
        ServiceLifetime lifetime)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        var callingAssembly = Assembly.GetCallingAssembly();
        return AddSculptorCore(services, new[] { callingAssembly }, configure: null, lifetime);
    }

    /// <summary>
    /// Registers an <see cref="ISculptor"/> with a specific <see cref="ServiceLifetime"/>
    /// and a <see cref="SmartMapp.Net.Configuration.SculptorOptions"/> configuration callback.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">Lifetime of the <see cref="ISculptor"/> service handle.</param>
    /// <param name="configure">The options configuration callback. Invoked exactly once on first resolve.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <c>AddSculptor()</c> has already been called on this collection.
    /// </exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection AddSculptor(
        this IServiceCollection services,
        ServiceLifetime lifetime,
        Action<SmartMapp.Net.Configuration.SculptorOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var callingAssembly = Assembly.GetCallingAssembly();
        return AddSculptorCore(services, new[] { callingAssembly }, configure, lifetime);
    }

    /// <summary>
    /// Core registration helper shared by every public <c>AddSculptor</c> overload. Internal so
    /// tests can reuse the same pipeline.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Default assemblies to scan when the configure callback does not scan explicitly.</param>
    /// <param name="configure">Optional <see cref="SmartMapp.Net.Configuration.SculptorOptions"/> mutation callback.</param>
    /// <param name="lifetime">Lifetime of the registered <see cref="ISculptor"/> handle.</param>
    /// <returns>The same service collection for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a sculptor has already been registered in this collection.
    /// </exception>
    internal static IServiceCollection AddSculptorCore(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies,
        Action<SmartMapp.Net.Configuration.SculptorOptions>? configure,
        ServiceLifetime lifetime)
    {
        ThrowIfAlreadyRegistered(services);

        // Sentinel — single registration per collection.
        services.AddSingleton(SculptorRegistrationState.Instance);

        var factory = new SculptorBuilderFactory(assemblies, configure);

        // Host is always registered as a shared singleton so the global forge happens at most
        // once per IServiceProvider, regardless of the handle lifetime chosen by the caller.
        // Factory-registered (rather than instance-registered) so the optional
        // ILogger<ForgedSculptorHost> for the "forged sculptor" Information event (spec
        // §S8-T01 Technical Considerations bullet 4) is resolved from DI at first access.
        services.TryAddSingleton(sp =>
            new ForgedSculptorHost(factory, sp.GetService<ILogger<ForgedSculptorHost>>()));

        // S8-T07: Singleton lifecycle service that installs SculptorAmbient.Current on ctor
        // and clears it on dispose. Resolved inside ResolveSculptor on first ISculptor resolve
        // so the ambient is ready before any .MapTo<T>() / ambient-SelectAs<T>() call.
        services.TryAddSingleton<AmbientSculptorLifecycle>(sp =>
            new AmbientSculptorLifecycle(sp.GetRequiredService<ForgedSculptorHost>().Sculptor));

        // Factory delegate per spec §S8-T04:
        //   - Captures the root IServiceProvider so forge-time transformer resolution can use
        //     ActivatorUtilities (first-writer-wins; later scope-resolves don't override).
        //   - Forces the global forge via host.Sculptor (also installs DependencyInjectionProviderFactory
        //     on SculptorOptions.ProviderResolver before options freeze).
        //   - Wraps the resulting ISculptor in DependencyInjectionSculptor so every Map call
        //     pushes the resolving IServiceProvider as ambient for request-scoped providers.
        //   - Side-effect resolves AmbientSculptorLifecycle (S8-T07) so the ambient accessor
        //     is installed before the user reaches for MapTo<T>() / ambient-SelectAs<T>().
        static ISculptor ResolveSculptor(IServiceProvider sp)
        {
            var host = sp.GetRequiredService<ForgedSculptorHost>();
            host.TrySetRootServiceProvider(sp);

            var inner = host.Options.AllowPerScopeRebuild
                ? host.Factory.Build().Forge()
                : host.Sculptor;

            // Force ambient-lifecycle creation so the Singleton runs its install + sp-dispose-clear
            // semantics. TryAddSingleton ensures this is a no-op if the user already resolved it.
            _ = sp.GetRequiredService<AmbientSculptorLifecycle>();

            return new DependencyInjectionSculptor(inner, sp);
        }

        var sculptorDescriptor = new ServiceDescriptor(typeof(ISculptor), ResolveSculptor, lifetime);
        services.TryAdd(sculptorDescriptor);

        // ISculptorConfiguration shares the sculptor's lifetime and identity — DependencyInjectionSculptor
        // implements both ISculptor and ISculptorConfiguration (delegating to the inner Sculptor),
        // so the cast holds regardless of whether the resolved ISculptor is the wrapper or the raw sculptor.
        static ISculptorConfiguration ResolveConfiguration(IServiceProvider sp) =>
            (ISculptorConfiguration)sp.GetRequiredService<ISculptor>();

        var configDescriptor = new ServiceDescriptor(
            typeof(ISculptorConfiguration), ResolveConfiguration, lifetime);
        services.TryAdd(configDescriptor);

        // S8-T04: auto-register discovered IValueProvider and ITypeTransformer implementations
        // as Transient so constructor injection works out of the box. User-supplied lifetimes
        // (e.g. pre-registered Singleton transformers) are preserved via TryAddTransient.
        RegisterScannedProvidersAndTransformers(services, assemblies);

        // IMapper<TOrigin, TTarget> — open-generic registration per S8-T03. DI will construct a
        // closed-generic DependencyInjectionMapper<,> on resolve which extracts the forged
        // configuration from the ambient ISculptor (scope-appropriate) and wraps a
        // Mapper<TOrigin, TTarget> pre-compiled delegate. Lifetime matches ISculptor so scoped
        // sculptors yield scoped mappers (especially important when AllowPerScopeRebuild=true).
        var mapperDescriptor = new ServiceDescriptor(
            typeof(IMapper<,>),
            typeof(DependencyInjectionMapper<,>),
            lifetime);
        services.TryAdd(mapperDescriptor);

        // S8-T05: register the forged SculptorOptions as a Singleton so consumers (notably
        // SculptorStartupValidator) can inject it directly. Resolving this service forces the
        // lazy forge — same semantic as ISculptor resolution.
        services.TryAddSingleton<SculptorOptions>(sp => sp.GetRequiredService<ForgedSculptorHost>().Options);

        // S8-T05: always register the IHostedService validator. Gating the registration on an
        // at-registration-time probe of the configure callback would break the T02 lazy-forge
        // invariant ("configure callback must be deferred until first resolve"). Instead, the
        // validator itself short-circuits inside StartAsync when
        // SculptorOptions.ValidateOnStartup is false — see SculptorStartupValidator.
        // Users who want zero forge-at-startup impact on a non-validating deployment can
        // remove the IHostedService via services.RemoveAll<...>() after AddSculptor.
        //
        // Factory registration (rather than open-generic) so the optional IHostEnvironment
        // parameter can be resolved via GetService<>() — MSDI's default ctor selection throws
        // on unresolved params, so the factory is the clean way to pass null when no host is
        // wired in (builder-only / test harnesses per spec §S8-T05 Outputs bullet 3).
        services.AddSingleton<IHostedService>(sp => new SculptorStartupValidator(
            sp.GetRequiredService<ISculptorConfiguration>(),
            sp.GetRequiredService<SculptorOptions>(),
            sp.GetRequiredService<ILogger<SculptorStartupValidator>>(),
            sp.GetService<IHostEnvironment>()));

        return services;
    }

    /// <summary>
    /// Auto-registers every <see cref="IValueProvider"/> and <see cref="ITypeTransformer"/>
    /// implementation discovered by the <see cref="AssemblyScanner"/> in the supplied
    /// <paramref name="assemblies"/> as <see cref="ServiceLifetime.Transient"/>. Uses
    /// <c>TryAdd</c> so user-supplied registrations (e.g. pre-registered Singleton transformers)
    /// win and are not overwritten. Implements spec §11.2 / S8-T04 Constraints bullet 1.
    /// </summary>
    private static void RegisterScannedProvidersAndTransformers(
        IServiceCollection services,
        IReadOnlyList<Assembly> assemblies)
    {
        if (assemblies.Count == 0) return;

        var scanner = new AssemblyScanner();
        var array = new Assembly[assemblies.Count];
        for (var i = 0; i < assemblies.Count; i++) array[i] = assemblies[i];

        var result = scanner.Scan(array);

        // Value providers: concrete implementations of IValueProvider<,,> (the closed-generic
        // carries too many type parameters to register meaningfully as a service type; callers
        // reach these via p.From<T>() / [ProvideWith], which resolve the concrete type from DI).
        foreach (var provider in result.ValueProviders)
        {
            var implType = provider.ImplementationType;
            if (implType.IsAbstract || implType.IsGenericTypeDefinition) continue;
            services.TryAddTransient(implType);
        }

        // Type transformers: same pattern — concrete implementations registered as themselves.
        foreach (var transformer in result.TypeTransformers)
        {
            var implType = transformer.ImplementationType;
            if (implType.IsAbstract || implType.IsGenericTypeDefinition) continue;
            services.TryAddTransient(implType);
        }
    }

    private static void ThrowIfAlreadyRegistered(IServiceCollection services)
    {
        // Per spec Technical Considerations: detect duplicates via the sentinel's
        // ImplementationInstance identity, not by ServiceType match. This is tighter than a
        // type check — a rogue descriptor that happened to carry ServiceType =
        // SculptorRegistrationState but a different instance (e.g. via Decorate / Replace)
        // will not be mistaken for a prior AddSculptor() call.
        for (var i = 0; i < services.Count; i++)
        {
            if (services[i].ImplementationInstance is SculptorRegistrationState)
            {
                throw new InvalidOperationException(
                    "AddSculptor() has already been called on this IServiceCollection. " +
                    "Only a single default (un-keyed) sculptor registration is supported in v1.0. " +
                    "Multi-sculptor scenarios via keyed services (AddSculptor(string key, ...)) arrive in Sprint 16.");
            }
        }
    }
}
