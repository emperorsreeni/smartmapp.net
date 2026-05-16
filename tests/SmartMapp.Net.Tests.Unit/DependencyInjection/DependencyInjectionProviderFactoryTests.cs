using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T04 — unit tests for <see cref="DependencyInjectionProviderFactory"/>,
/// the DI-aware <see cref="SmartMapp.Net.Abstractions.IProviderResolver"/> that prefers
/// container registrations and falls back to <c>ActivatorUtilities.CreateInstance</c>.
/// </summary>
public class DependencyInjectionProviderFactoryTests
{
    [Fact]
    public void Ctor_NullRootServiceProvider_Throws()
    {
        var act = () => new DependencyInjectionProviderFactory(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("rootServiceProvider");
    }

    [Fact]
    public void Resolve_NullType_Throws()
    {
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();
        var factory = new DependencyInjectionProviderFactory(sp);

        var act = () => factory.Resolve(null!, sp);

        act.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    [Fact]
    public void Resolve_DiHit_ReturnsContainerInstance()
    {
        var services = new ServiceCollection();
        var known = new S8T04FixedTaxService(0.08m);
        services.AddSingleton<IS8T04TaxService>(known);
        using var sp = services.BuildServiceProvider();
        var factory = new DependencyInjectionProviderFactory(sp);

        var resolved = factory.Resolve(typeof(IS8T04TaxService), sp);

        resolved.Should().BeSameAs(known);
    }

    [Fact]
    public void Resolve_UnregisteredType_UsesActivatorUtilitiesWithCtorInjection()
    {
        // Register the dependency but NOT the provider itself — ActivatorUtilities must
        // construct the provider using the DI-registered dependency.
        var services = new ServiceCollection();
        services.AddSingleton<IS8T04TaxService>(new S8T04FixedTaxService(0.15m));
        using var sp = services.BuildServiceProvider();
        var factory = new DependencyInjectionProviderFactory(sp);

        var resolved = factory.Resolve(typeof(S8T04TaxProvider), sp);

        resolved.Should().BeOfType<S8T04TaxProvider>();
    }

    [Fact]
    public void Resolve_AbstractType_Throws()
    {
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();
        var factory = new DependencyInjectionProviderFactory(sp);

        var act = () => factory.Resolve(typeof(IS8T04TaxService), sp);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*abstract or an interface*");
    }

    [Fact]
    public void Resolve_TypeWithUnresolvableCtorDeps_ThrowsWithDiagnosticHint()
    {
        // No IS8T04TaxService registration — ActivatorUtilities cannot satisfy the provider's ctor.
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();
        var factory = new DependencyInjectionProviderFactory(sp);

        var act = () => factory.Resolve(typeof(S8T04TaxProvider), sp);

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("ActivatorUtilities.CreateInstance failed")
                      && ex.Message.Contains("Ensure all constructor dependencies"),
                "diagnostic must point at missing registration as the likely cause.");
    }

    [Fact]
    public void Resolve_WithNullServiceProvider_FallsBackToRoot()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IS8T04TaxService>(new S8T04FixedTaxService(0.2m));
        using var root = services.BuildServiceProvider();
        var factory = new DependencyInjectionProviderFactory(root);

        // serviceProvider argument is null (e.g. forge-time transformer resolution) — the
        // captured root must kick in.
        var resolved = factory.Resolve(typeof(S8T04TaxProvider), serviceProvider: null);

        resolved.Should().BeOfType<S8T04TaxProvider>();
    }

    [Fact]
    public void Resolve_AmbientScopeOverridesRoot()
    {
        var services = new ServiceCollection();
        services.AddScoped<IS8T04TaxService>(_ => new S8T04FixedTaxService(0.5m));
        using var root = services.BuildServiceProvider();
        using var scope = root.CreateScope();

        var factory = new DependencyInjectionProviderFactory(root);

        // Passing the scope's IServiceProvider must cause scoped services to be resolved from
        // that scope rather than the root.
        var resolved = (IS8T04TaxService)factory.Resolve(typeof(IS8T04TaxService), scope.ServiceProvider);

        resolved.ComputeTax(100m).Should().Be(50m);
    }

    [Fact]
    public void Resolve_ActivatorFallback_EmitsOneShotTraceWarning_WhenTypeHasRegisteredCtorDeps()
    {
        // Spec §S8-T04 Technical Considerations bullet 4: "Log a warning when activator fallback
        // is used for a type that has registered constructor dependencies — likely misconfiguration."
        // IS8T04TaxService is registered; S8T04TaxProvider (which depends on it) is NOT registered,
        // so ActivatorUtilities constructs it — and the factory emits a one-shot warning.
        using var listener = new TestTraceListener();
        DependencyInjectionProviderFactory.TraceSource.Listeners.Add(listener);
        DependencyInjectionProviderFactory.TraceSource.Switch = new SourceSwitch("SmartMapp.Net.DependencyInjection", "Warning");

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IS8T04TaxService>(new S8T04FixedTaxService(0.1m));
            using var sp = services.BuildServiceProvider();
            var factory = new DependencyInjectionProviderFactory(sp);

            // Use a local sentinel type to avoid one-shot cache contamination from other tests.
            _ = factory.Resolve(typeof(S8T04TaxProviderWarningProbe), sp);
            _ = factory.Resolve(typeof(S8T04TaxProviderWarningProbe), sp);

            // xUnit runs tests in parallel and the TraceSource is static, so the listener may
            // pick up warnings emitted by unrelated-but-concurrent tests in the same process.
            // Filter to warnings that mention this test's dedicated probe type and assert only
            // one of those fired (the one-shot-per-type invariant we care about).
            var probeWarnings = listener.Warnings
                .Where(w => w.Contains(nameof(S8T04TaxProviderWarningProbe)))
                .ToArray();
            probeWarnings.Should().ContainSingle(
                "the warning must fire exactly once per type across repeated activator fallbacks.");
            probeWarnings[0].Should().Contain("likely a misconfiguration");
        }
        finally
        {
            DependencyInjectionProviderFactory.TraceSource.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void Resolve_ActivatorFallback_NoWarning_WhenTypeHasParameterlessCtor()
    {
        using var listener = new TestTraceListener();
        DependencyInjectionProviderFactory.TraceSource.Listeners.Add(listener);
        DependencyInjectionProviderFactory.TraceSource.Switch = new SourceSwitch("SmartMapp.Net.DependencyInjection", "Warning");

        try
        {
            var services = new ServiceCollection();
            using var sp = services.BuildServiceProvider();
            var factory = new DependencyInjectionProviderFactory(sp);

            _ = factory.Resolve(typeof(S8T04NoDepsProviderWarningProbe), sp);

            // Filter to warnings mentioning this probe — xUnit parallelism may pipe warnings
            // from other tests into this listener via the shared static TraceSource.
            listener.Warnings
                .Where(w => w.Contains(nameof(S8T04NoDepsProviderWarningProbe)))
                .Should().BeEmpty("parameterless-ctor types are not 'misconfigured' — no warning should fire.");
        }
        finally
        {
            DependencyInjectionProviderFactory.TraceSource.Listeners.Remove(listener);
        }
    }

    private sealed class TestTraceListener : TraceListener
    {
        internal List<string> Warnings { get; } = new();

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
        {
            if (eventType == TraceEventType.Warning && format is not null)
            {
                Warnings.Add(args is null || args.Length == 0 ? format : string.Format(format, args));
            }
        }

        public override void Write(string? message) { }
        public override void WriteLine(string? message) { }
    }
}

// Separate probe types to avoid the once-per-type cache interfering with other tests that
// resolve the shared S8T04TaxProvider / S8T04NoDepsProvider fixtures.
public sealed class S8T04TaxProviderWarningProbe
{
    public S8T04TaxProviderWarningProbe(IS8T04TaxService tax) => _ = tax;
}

public sealed class S8T04NoDepsProviderWarningProbe
{
}
