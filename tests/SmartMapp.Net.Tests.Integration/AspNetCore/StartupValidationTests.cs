// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.AspNetCore;

/// <summary>
/// Sprint 8 · S8-T11 — startup validation integration coverage. Builds real
/// <see cref="IHost"/> / <see cref="WebApplicationFactory{TEntryPoint}"/> instances to prove
/// the hosted <c>SculptorStartupValidator</c> behaves correctly in each scenario: valid
/// configuration starts, invalid fails fast, and <c>ValidateOnStartup=false</c> short-circuits.
/// </summary>
[Collection(AspNetCoreCollection.Name)]
public sealed class StartupValidationTests
{
    [Fact]
    public async Task ValidConfiguration_HostStarts_WithoutThrowing()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
            services.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>());
        });
        using var host = builder.Build();
        var ct = TestContext.Current.CancellationToken;

        await host.StartAsync(ct);
        await host.StopAsync(ct);
    }

    [Fact]
    public async Task InvalidBlueprint_HostStartAsync_ThrowsFailFastException()
    {
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
            services.AddSculptor(options =>
            {
                options.UseBlueprint<IntegrationBlueprint>();
                // Structurally-invalid binding: DiscriminateBy(...) without a required
                // .Otherwise<T>() clause → BlueprintValidator reports an ERROR, forge Stage 9
                // throws BlueprintValidationException which the hosted validator surfaces.
                options.Bind<Customer, Address>(rule => rule.DiscriminateBy(c => c.Id));
            });
        });
        using var host = builder.Build();
        var ct = TestContext.Current.CancellationToken;

        var act = () => host.StartAsync(ct);

        var thrown = await act.Should().ThrowAsync<Exception>();
        var chain = Unwind(thrown.Which).ToList();
        chain.Should().Contain(
            e => e is SculptorStartupValidationException || e is BlueprintValidationException,
            "invalid-blueprint fail-fast must surface either the validator's wrapper or the forge-time exception.");
    }

    [Fact]
    public async Task WarningsOnly_StrictModeOff_HostStartsCleanly()
    {
        // Spec §S8-T11 AC bullet 6 "valid/invalid/warnings" — warnings-path non-strict:
        // BlueprintValidator only emits warnings when StrictMode=true, so with StrictMode
        // off an unlinked optional target member surfaces no errors *and* no warnings,
        // and the host starts cleanly.
        // S8-T05 review: pin Development so the post-review env-aware ValidateOnStartup
        // default doesn't short-circuit the validator that this test exists to exercise.
        var builder = Host.CreateDefaultBuilder();
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
            services.AddSculptor(options =>
            {
                options.StrictMode = false;
                options.UseBlueprint<WarningsBlueprint>();
            });
        });
        using var host = builder.Build();
        var ct = TestContext.Current.CancellationToken;

        await host.StartAsync(ct);
        await host.StopAsync(ct);

        using var scope = host.Services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<ISculptorConfiguration>();
        var result = config.ValidateConfiguration();
        result.IsValid.Should().BeTrue("no errors expected on the warnings-only / non-strict path.");
    }

    [Fact]
    public async Task WarningsOnly_StrictModeOn_PromotesToStartupFailure()
    {
        // Spec §6.4 + §12.1: StrictMode promotes any warnings into startup failures. With
        // StrictMode=true, WarningsBlueprint's unlinked UnmappedNote triggers a warning
        // which SculptorStartupValidator surfaces as SculptorStartupValidationException.
        // S8-T05 review: pin Development so the post-review env-aware ValidateOnStartup
        // default doesn't short-circuit the validator that this test exists to exercise.
        var builder = Host.CreateDefaultBuilder();
        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
            services.AddSculptor(options =>
            {
                options.StrictMode = true;
                options.UseBlueprint<WarningsBlueprint>();
            });
        });
        using var host = builder.Build();
        var ct = TestContext.Current.CancellationToken;

        var act = () => host.StartAsync(ct);

        var thrown = await act.Should().ThrowAsync<Exception>();
        var chain = Unwind(thrown.Which).ToList();
        chain.Should().Contain(
            e => e is SculptorStartupValidationException,
            "StrictMode must promote unlinked-member warnings into a startup failure — spec §6.4 / §12.1.");
    }

    [Fact]
    public async Task ValidateOnStartupFalse_HostStarts_EvenWithBlueprintErrors()
    {
        // When ValidateOnStartup is explicitly disabled the hosted validator short-circuits;
        // host.StartAsync must not trigger a forge or surface any blueprint errors (spec §12.1
        // opt-out path).
        var builder = Host.CreateDefaultBuilder();
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
            services.AddSculptor(options =>
            {
                options.ValidateOnStartup = false;
                options.UseBlueprint<IntegrationBlueprint>();
                // Deliberately broken binding — would fail validation if ValidateOnStartup=true.
                options.Bind<Customer, Address>(rule => rule.DiscriminateBy(c => c.Id));
            });
        });
        using var host = builder.Build();
        var ct = TestContext.Current.CancellationToken;

        await host.StartAsync(ct);
        await host.StopAsync(ct);
    }

    [Fact]
    public async Task WebApplicationFactory_InvalidProfile_CreateClientThrowsFailFast()
    {
        // End-to-end proof that WebApplicationFactory surfaces the fail-fast startup path for
        // the real MinimalApi sample when `Sample:InvalidBlueprint` is set. CreateClient()
        // forces host startup, which triggers the hosted validator.
        await using var factory = new InvalidFactory($"s8t11-invalid-{Guid.NewGuid():N}");

        var act = () => factory.CreateClient();
        var thrown = act.Should().Throw<Exception>().Which;

        var chain = Unwind(thrown).ToList();
        chain.Should().Contain(
            e => e is SculptorStartupValidationException || e is BlueprintValidationException,
            "WebApplicationFactory-hosted pipelines must still fail-fast on blueprint validation errors.");

        await Task.CompletedTask;
    }

    private sealed class InvalidFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName;
        public InvalidFactory(string dbName) { _dbName = dbName; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Sample:DbName"] = _dbName,
                    ["Sample:InvalidBlueprint"] = "true",
                }));
            // The sample's Program.cs also reads SMARTMAPP_SAMPLE_INVALID — set it as a
            // belt-and-braces guard against configuration-chain ordering differences across
            // .NET SDK versions.
            Environment.SetEnvironmentVariable("SMARTMAPP_SAMPLE_INVALID", "true");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Environment.SetEnvironmentVariable("SMARTMAPP_SAMPLE_INVALID", null);
            base.Dispose(disposing);
        }
    }

    private static IEnumerable<Exception> Unwind(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    foreach (var nested in Unwind(inner)) yield return nested;
                }
            }
        }
    }
}
