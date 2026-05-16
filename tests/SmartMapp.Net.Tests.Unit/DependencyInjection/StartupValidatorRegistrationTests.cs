using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using SmartMapp.Net.DependencyInjection;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T05 — verifies the registration-time gating in
/// <c>SculptorServiceCollectionExtensions</c>: the <see cref="IHostedService"/> for
/// <see cref="SculptorStartupValidator"/> is added when <c>ValidateOnStartup</c> is
/// <c>true</c> (including the default), and omitted when the user opts out.
/// </summary>
public class StartupValidatorRegistrationTests
{
    private static bool HasStartupValidator(IServiceCollection services)
    {
        // S8-T05 review: registration switched to factory form so the validator's optional
        // IHostEnvironment dependency can be resolved via IServiceProvider.GetService<>(). The
        // descriptor therefore has ImplementationFactory set (not ImplementationType), so we
        // probe the factory by invoking it against an empty provider and asserting the returned
        // instance type. ImplementationInstance is also accepted for robustness against future
        // registration-shape changes.
        return services.Any(d => d.ServiceType == typeof(IHostedService) && IsStartupValidatorDescriptor(d));
    }

    private static bool IsStartupValidatorDescriptor(ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationType == typeof(SculptorStartupValidator)) return true;
        if (descriptor.ImplementationInstance is SculptorStartupValidator) return true;

        // Factory form — probe the descriptor's delegate against a minimal provider so the
        // helper can distinguish the SmartMapp.Net validator from any other IHostedService.
        if (descriptor.ImplementationFactory is null) return false;

        var probeServices = new ServiceCollection();
        probeServices.AddLogging();                                    // satisfy ILogger<> ctor dep
        probeServices.AddSculptor();                                   // registers dependencies
        probeServices.RemoveAll<IHostedService>();                     // don't recurse into ourselves
        using var probe = probeServices.BuildServiceProvider();
        try
        {
            var instance = descriptor.ImplementationFactory(probe);
            return instance is SculptorStartupValidator;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public void AddSculptor_Default_RegistersStartupValidator()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        HasStartupValidator(services).Should().BeTrue(
            "the hosted service is always registered; ValidateOnStartup is checked at StartAsync time.");
    }

    [Fact]
    public void AddSculptor_WithValidateOnStartupFalse_StillRegistersValidator_ValidatorShortCircuitsAtStartAsync()
    {
        // S8-T05 design note: gating the IHostedService registration on the configure flag
        // would force the user callback to run at AddSculptor time, breaking the T02 lazy-forge
        // invariant. Instead, the validator itself short-circuits inside StartAsync when
        // SculptorOptions.ValidateOnStartup=false (see SculptorStartupValidatorTests.StartAsync_ValidateOnStartupFalse_ShortCircuits).
        var services = new ServiceCollection();
        services.AddSculptor(options => options.ValidateOnStartup = false);

        HasStartupValidator(services).Should().BeTrue(
            "the hosted service is always registered; the validator honours ValidateOnStartup at StartAsync time.");
    }

    [Fact]
    public void AddSculptor_Configure_IsDeferredUntilFirstResolve()
    {
        // Guards the T02 lazy-forge invariant against regressions when T05 wiring is touched:
        // no form of probe-at-registration may invoke the configure callback.
        var invocations = 0;
        var services = new ServiceCollection();
        services.AddSculptor(options =>
        {
            System.Threading.Interlocked.Increment(ref invocations);
            options.ValidateOnStartup = false;
        });

        invocations.Should().Be(0,
            "T05 registration must not dry-run the configure callback — forge stays lazy.");
    }

    [Fact]
    public void AddSculptor_RegistersSculptorOptionsAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        services.Should().Contain(d =>
            d.ServiceType == typeof(SmartMapp.Net.Configuration.SculptorOptions)
            && d.Lifetime == ServiceLifetime.Singleton,
            "S8-T05: SculptorOptions is registered so validators / future consumers can inject it.");
    }

    [Fact]
    public void AddSculptor_UserCanRemoveValidator_AfterAddSculptor()
    {
        // Documented escape hatch for deployments that truly need zero forge-at-startup work:
        // remove the hosted service via the standard IServiceCollection.RemoveAll<T>() API.
        var services = new ServiceCollection();
        services.AddSculptor();

        services.RemoveAll<IHostedService>();

        HasStartupValidator(services).Should().BeFalse();
    }
}
