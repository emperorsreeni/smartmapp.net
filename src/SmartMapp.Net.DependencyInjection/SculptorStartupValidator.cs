// SPDX-License-Identifier: MIT
// <copyright file="SculptorStartupValidator.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.DependencyInjection;

/// <summary>
/// <see cref="IHostedService"/> that validates the forged <see cref="ISculptorConfiguration"/>
/// during host start-up per spec §12.1 and Sprint 8 · S8-T05. Fails host start-up with a
/// <see cref="SculptorStartupValidationException"/> when the configuration contains one or
/// more validation errors — so misconfigurations surface as a noisy container boot failure
/// rather than a mapping-time 500 in production.
/// </summary>
/// <remarks>
/// <para>
/// Registration is gated on <see cref="SculptorOptions.ValidateOnStartup"/>: the hosted service
/// is added to the <c>IServiceCollection</c> only when the flag is <c>true</c> at the time of
/// <c>AddSculptor</c>. The validator itself additionally re-checks the flag inside
/// <see cref="StartAsync"/> so late-frozen option changes are honoured gracefully.
/// </para>
/// <para>
/// Warnings are emitted through <see cref="ILogger"/> at
/// <see cref="LogLevel.Warning"/> and do not fail startup. When
/// <see cref="SculptorOptions.StrictMode"/> is enabled, warnings are promoted to startup
/// failures (spec §12.1).
/// </para>
/// <para>
/// A <see cref="System.Diagnostics.Activity"/> named <c>smartmappnet.startup.validation</c> is
/// emitted around the validation call so OpenTelemetry consumers (Sprint 17) can capture
/// startup duration without touching this class further.
/// </para>
/// </remarks>
public sealed class SculptorStartupValidator : IHostedService
{
    /// <summary>
    /// Shared <see cref="ActivitySource"/> for startup-validation spans. Exposed so tests and
    /// future diagnostics components (Sprint 17) can subscribe without reflection.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("SmartMapp.Net.DependencyInjection");

    private readonly ISculptorConfiguration _configuration;
    private readonly SculptorOptions _options;
    private readonly ILogger<SculptorStartupValidator> _logger;
    private readonly IHostEnvironment? _hostEnvironment;

    /// <summary>
    /// Initializes a new <see cref="SculptorStartupValidator"/>. The DI container resolves all
    /// dependencies; user code should not instantiate this type directly.
    /// </summary>
    /// <param name="configuration">The forged sculptor configuration. Resolving this triggers the lazy <c>Forge()</c>.</param>
    /// <param name="options">The frozen <see cref="SculptorOptions"/> produced during forge.</param>
    /// <param name="logger">Structured logger sink for success, warning, and error events.</param>
    /// <param name="hostEnvironment">
    /// Optional <see cref="IHostEnvironment"/>. When present and
    /// <see cref="SculptorOptions.IsValidateOnStartupExplicitlySet"/> is <c>false</c>, the
    /// validator applies the spec §S8-T05-documented environment-aware default: validate in
    /// <c>Development</c>, skip otherwise. When absent (e.g. non-hosted DI root or unit tests
    /// that don't register hosting) the validator falls back to the options flag verbatim —
    /// preserving Sprint 8-pre-review fail-fast semantics.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when any required argument is <c>null</c>.</exception>
    public SculptorStartupValidator(
        ISculptorConfiguration configuration,
        SculptorOptions options,
        ILogger<SculptorStartupValidator> logger,
        IHostEnvironment? hostEnvironment = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!ShouldValidate())
        {
            // Defensive: when ValidateOnStartup is flipped off after registration OR the
            // spec §S8-T05 environment-aware default gates us out (non-Development host, flag
            // not explicitly set), skip quietly so no Forge work happens on behalf of a disabled
            // flag. Emit a single Debug log for operator traceability.
            Log.StartupValidationSkipped(_logger, _hostEnvironment?.EnvironmentName ?? "(no IHostEnvironment)", null);
            return Task.CompletedTask;
        }

        using var activity = ActivitySource.StartActivity("smartmappnet.startup.validation", ActivityKind.Internal);
        var stopwatch = Stopwatch.StartNew();

        ValidationResult result;
        try
        {
            // Resolving ValidateConfiguration() triggers the lazy Forge if it has not run yet,
            // so the first host start performs exactly one forge + one validation pass.
            result = _configuration.ValidateConfiguration();
        }
        catch (Exception ex)
        {
            // Unexpected validator exception (not a validation *error*) — surface verbatim;
            // this is distinct from the aggregated SculptorStartupValidationException below.
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Log.StartupValidationFailedToExecute(_logger, ex);
            throw;
        }

        stopwatch.Stop();
        var pairCount = _configuration.GetAllBlueprints().Count;
        activity?.SetTag("smartmappnet.pair_count", pairCount);
        activity?.SetTag("smartmappnet.error_count", result.Errors.Count);
        activity?.SetTag("smartmappnet.warning_count", result.Warnings.Count);
        activity?.SetTag("smartmappnet.duration_ms", stopwatch.Elapsed.TotalMilliseconds);

        foreach (var warning in result.Warnings)
        {
            Log.StartupValidationWarning(_logger, warning.OriginType, warning.TargetType, warning.Message, null);
        }

        // Fail startup when the configuration has errors, or when StrictMode promotes any
        // warnings into failures (spec §12.1 + §6.4 StrictMode).
        var strictModeFailure = _options.StrictMode && result.Warnings.Count > 0;
        if (!result.IsValid || strictModeFailure)
        {
            activity?.SetStatus(ActivityStatusCode.Error,
                !result.IsValid ? "Validation errors" : "StrictMode warnings promoted to errors");
            Log.StartupValidationFailed(_logger, result.Errors.Count, result.Warnings.Count, null);

            throw new SculptorStartupValidationException(result);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        Log.StartupValidationSucceeded(_logger, pairCount, stopwatch.Elapsed.TotalMilliseconds, result.Warnings.Count, null);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Encapsulates the "should the validator run this start-up?" decision per spec §S8-T05
    /// Outputs bullet 3. Three precedence levels, in order:
    ///   (1) <see cref="SculptorOptions.ValidateOnStartup"/> explicitly set by the user — honour
    ///       it verbatim (both <c>true</c> and <c>false</c>). This is the escape hatch for a
    ///       caller who wants fail-fast validation in production.
    ///   (2) No <see cref="IHostEnvironment"/> registered — fall through to the options flag
    ///       (defaults to <c>true</c>). Builder-only consumers and unit tests without a host
    ///       therefore keep fail-fast semantics without extra ceremony.
    ///   (3) <see cref="IHostEnvironment"/> available and the flag was not explicitly set —
    ///       validate only in <c>Development</c>, skip otherwise. This is the spec-documented
    ///       environment-aware default.
    /// </summary>
    private bool ShouldValidate()
    {
        if (_options.IsValidateOnStartupExplicitlySet)
        {
            return _options.ValidateOnStartup;
        }

        if (_hostEnvironment is null)
        {
            return _options.ValidateOnStartup;
        }

        return _hostEnvironment.IsDevelopment();
    }

    /// <summary>
    /// Structured logger messages. Extracted to a private static holder so every log call uses
    /// the compiled <see cref="Microsoft.Extensions.Logging.LoggerMessage"/> delegate for
    /// allocation-free emission on the hot path (spec §7.4 perf guidance).
    /// </summary>
    private static class Log
    {
        internal static readonly Action<ILogger, int, double, int, Exception?> StartupValidationSucceeded =
            LoggerMessage.Define<int, double, int>(
                LogLevel.Information,
                new EventId(1, nameof(StartupValidationSucceeded)),
                "SmartMapp.Net startup validation succeeded: {PairCount} pair(s) forged in {DurationMs:F2} ms with {WarningCount} warning(s).");

        internal static readonly Action<ILogger, Type, Type, string, Exception?> StartupValidationWarning =
            LoggerMessage.Define<Type, Type, string>(
                LogLevel.Warning,
                new EventId(2, nameof(StartupValidationWarning)),
                "SmartMapp.Net startup validation warning on pair {OriginType} -> {TargetType}: {ValidationMessage}");

        internal static readonly Action<ILogger, int, int, Exception?> StartupValidationFailed =
            LoggerMessage.Define<int, int>(
                LogLevel.Error,
                new EventId(3, nameof(StartupValidationFailed)),
                "SmartMapp.Net startup validation failed: {ErrorCount} error(s), {WarningCount} warning(s). Throwing SculptorStartupValidationException.");

        internal static readonly Action<ILogger, Exception?> StartupValidationFailedToExecute =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(4, nameof(StartupValidationFailedToExecute)),
                "SmartMapp.Net startup validation threw while resolving the configuration.");

        internal static readonly Action<ILogger, string, Exception?> StartupValidationSkipped =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(5, nameof(StartupValidationSkipped)),
                "SmartMapp.Net startup validation skipped for host environment '{EnvironmentName}' (spec §S8-T05 environment-aware default — validate only in Development unless SculptorOptions.ValidateOnStartup is set explicitly).");
    }
}
