using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SmartMapp.Net;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T05 — host smoke tests. Builds a real
/// <see cref="Microsoft.Extensions.Hosting.IHost"/> with a fake
/// <see cref="ISculptorConfiguration"/> so <see cref="IHost.StartAsync"/> exercises the
/// full <see cref="IHostedService"/> pipeline including
/// <see cref="SculptorStartupValidator"/>.
/// </summary>
public class SculptorStartupValidatorHostIntegrationTests
{
    private static IHost BuildHostWith(ISculptorConfiguration config, SculptorOptions options)
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            // Register the fake config + options so the validator resolves them directly
            // without triggering a real forge (keeps the test hermetic and fast).
            services.AddSingleton(config);
            services.AddSingleton(options);
            services.AddSingleton<IHostedService, SculptorStartupValidator>();
        });
        return builder.Build();
    }

    [Fact]
    public async Task HostStartAsync_ValidConfiguration_Succeeds()
    {
        var config = Substitute.For<ISculptorConfiguration>();
        config.ValidateConfiguration().Returns(new ValidationResult(new BlueprintValidationResult()));
        config.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        using var host = BuildHostWith(config, new SculptorOptions { ValidateOnStartup = true });

        var ct = Xunit.TestContext.Current.CancellationToken;
        await host.StartAsync(ct);
        await host.StopAsync(ct);
    }

    [Fact]
    public async Task HostStartAsync_InvalidConfiguration_ThrowsSculptorStartupValidationException()
    {
        var inner = new BlueprintValidationResult();
        inner.AddError(typeof(int), typeof(string), "intentional-failure");
        var config = Substitute.For<ISculptorConfiguration>();
        config.ValidateConfiguration().Returns(new ValidationResult(inner));
        config.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        using var host = BuildHostWith(config, new SculptorOptions { ValidateOnStartup = true });
        var ct = Xunit.TestContext.Current.CancellationToken;

        var act = () => host.StartAsync(ct);

        var ex = await act.Should().ThrowAsync<SculptorStartupValidationException>();
        ex.Which.Message.Should().Contain("intentional-failure");
    }

    [Fact]
    public async Task HostStartAsync_ValidateOnStartupFalse_ShortCircuits_NoFailureEvenOnBadConfig()
    {
        // The validator is registered but ValidateOnStartup=false causes it to short-circuit
        // inside StartAsync without touching the configuration. Even a broken config is tolerated.
        var config = Substitute.For<ISculptorConfiguration>();
        config.ValidateConfiguration().Returns<ValidationResult>(_ => throw new InvalidOperationException("never call"));

        using var host = BuildHostWith(config, new SculptorOptions { ValidateOnStartup = false });

        var ct = Xunit.TestContext.Current.CancellationToken;
        await host.StartAsync(ct);
        await host.StopAsync(ct);

        config.DidNotReceive().ValidateConfiguration();
    }

    [Fact]
    public async Task HostStartAsync_WithAddSculptorAndInvalidConfig_ThrowsSculptorStartupValidationException()
    {
        // Spec §S8-T05 Unit-Tests bullet 4: "Host smoke test: build a host with AddSculptor and
        // invalid config → host.StartAsync() throws". Registering AddSculptor first gives us the
        // full registration wiring (host, Singleton ForgedSculptorHost, validator); we then Replace
        // the ISculptorConfiguration with a fake that returns an invalid ValidationResult so the
        // AddSculptor-wired validator resolves the fake and throws without triggering a real forge.
        var inner = new BlueprintValidationResult();
        inner.AddError(typeof(int), typeof(string), "AddSculptor integration failure");
        var fakeConfig = Substitute.For<ISculptorConfiguration>();
        fakeConfig.ValidateConfiguration().Returns(new ValidationResult(inner));
        fakeConfig.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        var builder = Host.CreateDefaultBuilder();
        // S8-T05 review: `Host.CreateDefaultBuilder()` defaults to `Production` which the
        // post-review environment-aware default skips. Opting in via explicit
        // `ValidateOnStartup = true` in the options callback flips
        // `IsValidateOnStartupExplicitlySet` so the validator runs regardless of env — avoids
        // flipping env to `Development` (which also enables `ValidateOnBuild=true` and would
        // reject the scanner-auto-registered S8T04 fixture types that have per-test ctor deps).
        builder.ConfigureServices(services =>
        {
            services.AddSculptor(options => options.ValidateOnStartup = true);
            services.Replace(ServiceDescriptor.Singleton(fakeConfig));
        });

        using var host = builder.Build();
        var ct = Xunit.TestContext.Current.CancellationToken;

        var act = () => host.StartAsync(ct);

        var ex = await act.Should().ThrowAsync<SculptorStartupValidationException>();
        ex.Which.Message.Should().Contain("AddSculptor integration failure");
    }

    [Fact]
    public async Task HostStartAsync_EmitsActivity_WithExpectedTags()
    {
        // Spec §S8-T05 Tech-Cons. bullet 2: "Emit an Activity (smartmappnet.startup.validation)
        // around the validation call — enables OTel to capture startup duration once Sprint 17
        // instrumentation lands." Regression guard: subscribe an ActivityListener and assert the
        // Activity is started, completed, and carries the expected tags.
        var config = Substitute.For<ISculptorConfiguration>();
        config.ValidateConfiguration().Returns(new ValidationResult(new BlueprintValidationResult()));
        var fakeBlueprints = new[]
        {
            new Blueprint { OriginType = typeof(int), TargetType = typeof(string) },
            new Blueprint { OriginType = typeof(long), TargetType = typeof(string) },
        };
        config.GetAllBlueprints().Returns(fakeBlueprints);

        Activity? captured = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == SculptorStartupValidator.ActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                if (a.OperationName == "smartmappnet.startup.validation") captured = a;
            },
        };
        ActivitySource.AddActivityListener(listener);

        using var host = BuildHostWith(config, new SculptorOptions { ValidateOnStartup = true });
        var ct = Xunit.TestContext.Current.CancellationToken;
        await host.StartAsync(ct);
        await host.StopAsync(ct);

        captured.Should().NotBeNull("the validator must emit smartmappnet.startup.validation on StartAsync.");
        captured!.Status.Should().Be(ActivityStatusCode.Ok);
        captured.GetTagItem("smartmappnet.pair_count").Should().Be(2);
        captured.GetTagItem("smartmappnet.error_count").Should().Be(0);
        captured.GetTagItem("smartmappnet.warning_count").Should().Be(0);
        captured.GetTagItem("smartmappnet.duration_ms").Should().BeOfType<double>();
    }
}
