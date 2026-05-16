// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 review — regression tests covering the two substantive gaps closed in the
/// Sprint 8 holistic review pass:
/// <list type="bullet">
/// <item><description><b>Gap 1 — S8-T01 Technical Considerations bullet 4:</b> "Log Info on first
/// Forge with assembly count + pair count." <c>ForgedSculptorHost</c> now resolves an optional
/// <see cref="ILogger{TCategoryName}"/> and emits a one-shot Information event when the lazy
/// forge fires.</description></item>
/// <item><description><b>Gap 2 — S8-T05 Outputs bullet 3:</b> "Default <see cref="SculptorOptions.ValidateOnStartup"/>
/// is true when <c>IHostEnvironment.IsDevelopment()</c>, false otherwise."
/// <see cref="SculptorStartupValidator"/> now consults an optional <see cref="IHostEnvironment"/>
/// and skips validation in non-Development hosts unless the user opted in explicitly via the
/// option setter.</description></item>
/// </list>
/// </summary>
public class SprintEightReviewTests
{
    // ============================================================================================
    // Gap 1 — Info-log on first Forge
    // ============================================================================================

    [Fact]
    public void ForgeHost_FirstResolve_EmitsSingleInformationLog_WithAssemblyAndPairCounts()
    {
        // Spec §S8-T01 Tech-Cons bullet 4 — operator visibility into the lazy forge event.
        var sink = new CapturingLogger();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new SingleLoggerFactory(sink));
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        services.AddSculptor(opts => opts.ValidateOnStartup = false);   // keep test hermetic

        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ISculptor>();                   // force the lazy forge
        _ = provider.GetRequiredService<ISculptor>();                   // second resolve must not re-log

        var forgeEntries = sink.Entries
            .Where(e => e.Category.Contains("ForgedSculptorHost", StringComparison.Ordinal))
            .Where(e => e.Message.Contains("forged ISculptor", StringComparison.OrdinalIgnoreCase))
            .ToList();

        forgeEntries.Should().HaveCount(1, "the Info log must fire exactly once regardless of how many times ISculptor is resolved.");
        forgeEntries[0].Level.Should().Be(LogLevel.Information);
        forgeEntries[0].Message.Should().MatchRegex(@"\d+ assemblies");
        forgeEntries[0].Message.Should().MatchRegex(@"\d+ blueprint pair");
        forgeEntries[0].Message.Should().MatchRegex(@"\d+(\.\d+)? ms");
    }

    [Fact]
    public void ForgeHost_NoLoggerRegistered_DoesNotThrow()
    {
        // Logging is optional — the DI integration must not mandate ILoggerFactory registration.
        var services = new ServiceCollection();
        services.AddSculptor(opts => opts.ValidateOnStartup = false);

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<ISculptor>();
        act.Should().NotThrow("ForgedSculptorHost must fall through when no ILoggerFactory is registered.");
    }

    // ============================================================================================
    // Gap 2 — Environment-aware ValidateOnStartup default
    // ============================================================================================

    [Fact]
    public async Task Validator_NonDevelopmentEnv_FlagAtDefault_SkipsValidation()
    {
        // Default (unset) ValidateOnStartup + Production environment → validator skips silently.
        var fake = Substitute.For<ISculptorConfiguration>();
        fake.ValidateConfiguration().Returns<ValidationResult>(_ => throw new InvalidOperationException("must not be called"));
        fake.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        var services = BuildValidatorServices(fake, new SculptorOptions(), "Production");
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IHostedService>();

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);
        fake.DidNotReceive().ValidateConfiguration();
    }

    [Fact]
    public async Task Validator_DevelopmentEnv_FlagAtDefault_Validates()
    {
        // Default (unset) ValidateOnStartup + Development environment → validator runs.
        var fake = Substitute.For<ISculptorConfiguration>();
        fake.ValidateConfiguration().Returns(new ValidationResult(new BlueprintValidationResult()));
        fake.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        var services = BuildValidatorServices(fake, new SculptorOptions(), Environments.Development);
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IHostedService>();

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);
        fake.Received(1).ValidateConfiguration();
    }

    [Fact]
    public async Task Validator_NonDevelopmentEnv_ExplicitTrue_StillValidates()
    {
        // User explicitly opts in via options.ValidateOnStartup = true → env-default is bypassed.
        var fake = Substitute.For<ISculptorConfiguration>();
        fake.ValidateConfiguration().Returns(new ValidationResult(new BlueprintValidationResult()));
        fake.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        var options = new SculptorOptions { ValidateOnStartup = true };
        var services = BuildValidatorServices(fake, options, "Production");
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IHostedService>();

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);
        fake.Received(1).ValidateConfiguration();
    }

    [Fact]
    public async Task Validator_DevelopmentEnv_ExplicitFalse_Skips()
    {
        // User explicitly opts out → Development env must not override the off flag.
        var fake = Substitute.For<ISculptorConfiguration>();
        fake.ValidateConfiguration().Returns<ValidationResult>(_ => throw new InvalidOperationException("must not be called"));

        var options = new SculptorOptions { ValidateOnStartup = false };
        var services = BuildValidatorServices(fake, options, Environments.Development);
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IHostedService>();

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);
        fake.DidNotReceive().ValidateConfiguration();
    }

    [Fact]
    public async Task Validator_NoHostEnvironment_FlagAtDefault_FallsThroughToOptions()
    {
        // Builder-only / non-hosted DI root — no IHostEnvironment registered. Validator must
        // honour the options flag verbatim (default true → validates) so unit test harnesses
        // without a host keep fail-fast semantics.
        var fake = Substitute.For<ISculptorConfiguration>();
        fake.ValidateConfiguration().Returns(new ValidationResult(new BlueprintValidationResult()));
        fake.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        var services = BuildValidatorServices(fake, new SculptorOptions(), envName: null);
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IHostedService>();

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);
        fake.Received(1).ValidateConfiguration();
    }

    [Fact]
    public void Options_IsValidateOnStartupExplicitlySet_StartsFalse_FlipsTrueOnAssignment()
    {
        var options = new SculptorOptions();
        options.IsValidateOnStartupExplicitlySet.Should().BeFalse(
            "fresh options must report the default as non-explicit so the validator applies the env-aware default.");

        options.ValidateOnStartup = true;
        options.IsValidateOnStartupExplicitlySet.Should().BeTrue(
            "assigning the setter must flip the explicit-set flag — even to the default-matching value.");
    }

    // ============================================================================================
    // Helpers
    // ============================================================================================

    private static ServiceCollection BuildValidatorServices(
        ISculptorConfiguration config,
        SculptorOptions options,
        string? envName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(config);
        services.AddSingleton(options);

        if (envName is not null)
        {
            services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(envName));
        }

        services.AddSingleton<IHostedService>(sp => new SculptorStartupValidator(
            sp.GetRequiredService<ISculptorConfiguration>(),
            sp.GetRequiredService<SculptorOptions>(),
            sp.GetRequiredService<ILogger<SculptorStartupValidator>>(),
            sp.GetService<IHostEnvironment>()));

        return services;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "SmartMapp.Net.Tests.Unit";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed record LogEntry(string Category, LogLevel Level, string Message);

    private sealed class CapturingLogger : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Scoped(this, categoryName);
        public void Dispose() { }

        private sealed class Scoped : ILogger
        {
            private readonly CapturingLogger _owner;
            private readonly string _category;
            public Scoped(CapturingLogger owner, string category) { _owner = owner; _category = category; }
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_owner.Entries) { _owner.Entries.Add(new LogEntry(_category, logLevel, formatter(state, exception))); }
            }
        }
    }

    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly CapturingLogger _provider;
        public SingleLoggerFactory(CapturingLogger provider) { _provider = provider; }
        public void AddProvider(ILoggerProvider provider) { /* no-op */ }
        public ILogger CreateLogger(string categoryName) => _provider.CreateLogger(categoryName);
        public void Dispose() => _provider.Dispose();
    }
}
