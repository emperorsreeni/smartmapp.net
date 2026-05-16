// SPDX-License-Identifier: MIT
// <copyright file="ForgedSculptorHost.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Holds the lazy, thread-safe <see cref="ISculptor"/> produced by the
/// <see cref="SculptorBuilderFactory"/>. Registered as a singleton in the
/// DI container and dereferenced on first resolve of <see cref="ISculptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/> guarantees that concurrent
/// first-time resolves collapse to exactly one <c>Forge()</c> execution and all resolvers
/// receive the same <see cref="ISculptor"/> instance.
/// </para>
/// <para>
/// The host also stashes the <see cref="SculptorOptions"/> instance used during the global
/// forge so that non-Singleton registration paths can inspect
/// <see cref="SculptorOptions.AllowPerScopeRebuild"/> without re-running the configure
/// callback.
/// </para>
/// <para>
/// On the first (and only) <c>Forge()</c> invocation the host emits a single
/// <see cref="LogLevel.Information"/> event via <see cref="ILogger{TCategoryName}"/> naming
/// the assembly count, discovered-pair count, and forge duration — the operator-visibility
/// signal called for by spec §S8-T01 Technical Considerations bullet 4. Logging is no-op
/// when no logger factory is registered in DI (the host accepts an optional
/// <see cref="ILogger{TCategoryName}"/>).
/// </para>
/// </remarks>
internal sealed class ForgedSculptorHost
{
    private readonly SculptorBuilderFactory _factory;
    private readonly Lazy<(ISculptor Sculptor, SculptorOptions Options)> _lazy;
    private readonly ConcurrentDictionary<TypePair, object> _mapperCache = new();
    private readonly ILogger<ForgedSculptorHost>? _logger;
    private int _walkerInvocationCount;
    private IServiceProvider? _rootServiceProvider;

    /// <summary>
    /// Initializes a new <see cref="ForgedSculptorHost"/>.
    /// </summary>
    /// <param name="factory">Factory that produces the <see cref="SculptorBuilder"/> used to forge.</param>
    /// <param name="logger">
    /// Optional <see cref="ILogger{TCategoryName}"/> used to emit the one-shot Information
    /// event on first forge (spec §S8-T01 Technical Considerations bullet 4). May be <c>null</c>
    /// in builder-only / test contexts where no logger factory is available.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is <c>null</c>.</exception>
    internal ForgedSculptorHost(SculptorBuilderFactory factory, ILogger<ForgedSculptorHost>? logger = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger;
        _lazy = new Lazy<(ISculptor, SculptorOptions)>(
            ForgeOnce,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Gets the forged sculptor, invoking <c>Forge()</c> exactly once on first access.
    /// </summary>
    internal ISculptor Sculptor => _lazy.Value.Sculptor;

    /// <summary>
    /// Gets the options used to forge <see cref="Sculptor"/>. Accessing this property forces
    /// the global forge if it has not run yet.
    /// </summary>
    internal SculptorOptions Options => _lazy.Value.Options;

    /// <summary>
    /// Gets a value indicating whether <see cref="Sculptor"/> has been materialised yet.
    /// </summary>
    internal bool IsForged => _lazy.IsValueCreated;

    /// <summary>
    /// Gets the builder factory — exposed for tests that assert on captured assemblies and
    /// for non-Singleton registration paths that need to forge additional sculptors per scope
    /// when <see cref="SculptorOptions.AllowPerScopeRebuild"/> is set.
    /// </summary>
    internal SculptorBuilderFactory Factory => _factory;

    /// <summary>
    /// Installs the root <see cref="IServiceProvider"/> used by the DI-aware
    /// <see cref="DependencyInjectionProviderFactory"/> to satisfy forge-time transformer
    /// constructor dependencies and to back <c>ActivatorUtilities</c> fallback when no
    /// ambient request scope is active. Call before <see cref="Sculptor"/> is first accessed
    /// — subsequent calls are ignored (first-writer-wins) so late scope-resolves don't
    /// replace the root capture.
    /// </summary>
    /// <param name="rootServiceProvider">The application's root <see cref="IServiceProvider"/>.</param>
    internal void TrySetRootServiceProvider(IServiceProvider rootServiceProvider)
    {
        if (rootServiceProvider is null) return;
        System.Threading.Interlocked.CompareExchange(ref _rootServiceProvider, rootServiceProvider, null);
    }

    /// <summary>
    /// Gets the per-pair mapper cache populated by <see cref="MapperRegistrationWalker"/>
    /// during <see cref="ForgeOnce"/>. Keys are <see cref="TypePair"/>; values are
    /// <see cref="IMapper{TOrigin, TTarget}"/> closed-generic instances stored as <see cref="object"/>
    /// so the dictionary can hold heterogeneous pairs.
    /// </summary>
    /// <remarks>
    /// Per spec §S8-T03 Technical Considerations: "Keep a <c>ConcurrentDictionary&lt;TypePair, object&gt;</c>
    /// mirror of registered mappers to short-circuit closed-generic construction costs on repeated
    /// resolve". <see cref="DependencyInjectionMapper{TOrigin, TTarget}"/> reads from this cache on
    /// every construction so Transient/Scoped wrappers share the single pre-compiled
    /// <see cref="Mapper{TOrigin, TTarget}"/> instance built during forge.
    /// </remarks>
    internal ConcurrentDictionary<TypePair, object> MapperCache => _mapperCache;

    /// <summary>
    /// Gets the number of times the walker populated the cache. Always <c>1</c> after the first
    /// forge completes, <c>0</c> before. Exposed for the "walker invoked exactly once" regression
    /// test per spec §S8-T03 Constraints.
    /// </summary>
    internal int WalkerInvocationCount => Volatile.Read(ref _walkerInvocationCount);

    private (ISculptor Sculptor, SculptorOptions Options) ForgeOnce()
    {
        var builder = _factory.Build();
        var options = builder.Options;

        // Per spec §S8-T04 Constraints bullet 1/2: when a root IServiceProvider is available,
        // install the DI-aware resolver on SculptorOptions BEFORE Forge freezes the options.
        // This lets forge-time transformer resolution use ActivatorUtilities with the captured
        // root SP, and mapping-time DeferredValueProvider resolution use the ambient scope.
        if (_rootServiceProvider is not null
            && ReferenceEquals(options.ProviderResolver, DefaultProviderResolver.Instance))
        {
            options.ProviderResolver = new DependencyInjectionProviderFactory(_rootServiceProvider);
        }

        // Measure forge duration for the operator-visibility Information log below (spec
        // §S8-T01 Technical Considerations bullet 4). Stopwatch is cheap; wraps both Forge()
        // and the walker's cache population since both contribute to "time to first Map".
        var stopwatch = Stopwatch.StartNew();

        var sculptor = builder.Forge();

        // Per spec §S8-T03 Constraints: "Registration runs inside the Singleton factory for the
        // sculptor, exactly once." The walker pre-builds a Mapper<TOrigin, TTarget> for every pair
        // known to the forged configuration and parks them in MapperCache for O(1) lookup from
        // DependencyInjectionMapper<,>.
        MapperRegistrationWalker.PopulateCache(sculptor, _mapperCache);
        Interlocked.Increment(ref _walkerInvocationCount);

        stopwatch.Stop();

        // Spec §S8-T01 Technical Considerations bullet 4: emit a single Information event when
        // the lazy forge fires so operators see assembly count + pair count + duration without
        // having to attach a debugger. The host runs forge exactly once (LazyThreadSafetyMode
        // .ExecutionAndPublication) so this log is naturally one-shot — no extra gating needed.
        if (_logger is not null)
        {
            var assemblyCount = _factory.Assemblies.Count;
            var pairCount = ((ISculptorConfiguration)sculptor).GetAllBlueprints().Count;
            Log.SculptorForged(_logger, assemblyCount, pairCount, stopwatch.Elapsed.TotalMilliseconds, null);
        }

        return (sculptor, options);
    }

    /// <summary>
    /// Source-generated logging sink. Kept private so the string format is owned in one place.
    /// </summary>
    private static class Log
    {
        private static readonly Action<ILogger, int, int, double, Exception?> _sculptorForged =
            LoggerMessage.Define<int, int, double>(
                LogLevel.Information,
                new EventId(1, nameof(SculptorForged)),
                "SmartMapp.Net: forged ISculptor from {AssemblyCount} assemblies; discovered {PairCount} blueprint pair(s) in {DurationMs:F2} ms.");

        internal static void SculptorForged(ILogger logger, int assemblyCount, int pairCount, double durationMs, Exception? exception)
            => _sculptorForged(logger, assemblyCount, pairCount, durationMs, exception);
    }
}
