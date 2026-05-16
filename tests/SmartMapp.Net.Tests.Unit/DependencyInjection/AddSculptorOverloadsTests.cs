using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection.Internal;
using SmartMapp.Net.Tests.ExternalAssemblyFixture;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T02 — overload-specific DI registration behaviour: options callback,
/// <see cref="ServiceLifetime"/> argument, and the combined (lifetime + configure) form.
/// </summary>
public class AddSculptorOverloadsTests
{
    [Fact]
    public void AddSculptor_WithConfigure_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddSculptor((Action<SculptorOptions>)null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public void AddSculptor_WithConfigure_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddSculptor(_ => { });

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddSculptor_WithConfigure_ReturnsSameCollectionForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddSculptor(_ => { });

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSculptor_WithConfigure_InvokesCallbackExactlyOnce_Across100Resolves()
    {
        // Spec §S8-T02 unit-tests: "Options callback: invoked once across 100 resolves."
        var invocations = 0;
        var services = new ServiceCollection();
        services.AddSculptor(_ => Interlocked.Increment(ref invocations));

        // Pre-resolve assertion: the callback has not run yet (lazy forge).
        invocations.Should().Be(0, "configure callback must be deferred until first resolve.");

        using var provider = services.BuildServiceProvider();
        for (var i = 0; i < 100; i++)
        {
            _ = provider.GetRequiredService<ISculptor>();
            _ = provider.GetRequiredService<ISculptorConfiguration>();
        }

        invocations.Should().Be(1,
            "configure callback must be invoked exactly once across 100 resolves of ISculptor + ISculptorConfiguration.");
    }

    [Fact]
    public void AddSculptor_WithConfigure_ExplicitScan_OverridesCallingAssemblyDefault()
    {
        var externalAssembly = typeof(ExternalCaller).Assembly;

        var services = new ServiceCollection();
        services.AddSculptor(options =>
        {
            options.ScanAssemblies(externalAssembly);
        });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ISculptor>();
        var host = provider.GetRequiredService<ForgedSculptorHost>();

        host.Options.Assemblies.Should()
            .ContainSingle()
            .Which.Should().BeSameAs(externalAssembly,
                "explicit options.ScanAssemblies must replace the calling-assembly default (spec §11.1 / S8-T02).");
    }

    [Fact]
    public void AddSculptor_WithConfigure_NoExplicitScan_FallsBackToCallingAssembly()
    {
        var services = new ServiceCollection();
        services.AddSculptor(_ => { /* no scan */ });

        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ISculptor>();
        var host = provider.GetRequiredService<ForgedSculptorHost>();

        host.Options.Assemblies.Should()
            .ContainSingle()
            .Which.Should().BeSameAs(typeof(AddSculptorOverloadsTests).Assembly);
    }

    [Fact]
    public void AddSculptor_WithLifetime_Singleton_EquivalentToParameterless()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Singleton);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptor) && d.Lifetime == ServiceLifetime.Singleton);
        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptorConfiguration) && d.Lifetime == ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();
        var s1 = provider.GetRequiredService<ISculptor>();
        var s2 = provider.GetRequiredService<ISculptor>();
        s1.Should().BeSameAs(s2);
    }

    [Fact]
    public void AddSculptor_WithLifetime_Scoped_RegistersHandleAsScoped()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptor) && d.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptorConfiguration) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddSculptor_WithLifetime_Transient_RegistersHandleAsTransient()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Transient);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptor) && d.Lifetime == ServiceLifetime.Transient);
        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptorConfiguration) && d.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public void AddSculptor_WithLifetime_HostAlwaysRegisteredAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Transient);

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ForgedSculptorHost) && d.Lifetime == ServiceLifetime.Singleton,
            "the forged host must stay Singleton regardless of the user-chosen handle lifetime.");
    }

    [Fact]
    public void AddSculptor_WithLifetimeAndConfigure_NullArgs_Throw()
    {
        var services = new ServiceCollection();

        var nullConfigure = () => services.AddSculptor(ServiceLifetime.Singleton, (Action<SculptorOptions>)null!);
        nullConfigure.Should().Throw<ArgumentNullException>().WithParameterName("configure");

        IServiceCollection? nullServices = null;
        var nullSvc = () => nullServices!.AddSculptor(ServiceLifetime.Singleton, _ => { });
        nullSvc.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddSculptor_WithLifetimeAndConfigure_HonoursBoth()
    {
        var invocations = 0;
        var services = new ServiceCollection();

        services.AddSculptor(ServiceLifetime.Scoped, _ => Interlocked.Increment(ref invocations));

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptor) && d.Lifetime == ServiceLifetime.Scoped);

        using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            _ = scope.ServiceProvider.GetRequiredService<ISculptor>();
        }
        invocations.Should().Be(1, "configure callback runs once inside the global forge even when the handle lifetime is Scoped.");
    }

    [Fact]
    public void AddSculptor_Configure_ExceptionWrappedInInvalidOperationException()
    {
        // Spec §S8-T02 unit-tests: "Options callback exceptions surface at resolve time
        // wrapped in InvalidOperationException." Using ArgumentException (distinct from
        // InvalidOperationException) so the wrap is provable — if the factory did not wrap,
        // the user would see ArgumentException directly.
        var inner = new ArgumentException("configure boom", paramName: "bogus");
        var services = new ServiceCollection();
        services.AddSculptor(_ => throw inner);

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<ISculptor>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*configuration callback*")
            .WithInnerException<ArgumentException>()
            .Which.Should().BeSameAs(inner,
                "the original exception must be preserved in InnerException for diagnostics.");
    }

    [Fact]
    public void AddSculptor_Configure_ExceptionWrapped_EvenWhenInnerIsInvalidOperationException()
    {
        // Guard against a would-be-clever future optimisation that skips wrapping when the
        // user exception already has the right type — the spec is unconditional.
        var inner = new InvalidOperationException("already invalid-op");
        var services = new ServiceCollection();
        services.AddSculptor(_ => throw inner);

        using var provider = services.BuildServiceProvider();

        var act = () => provider.GetRequiredService<ISculptor>();

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => !ReferenceEquals(ex, inner)
                && ReferenceEquals(ex.InnerException, inner),
                "the wrapper is a new InvalidOperationException carrying the original as InnerException.");
    }

    [Fact]
    public void AddSculptor_WithConfigure_CalledTwice_ThrowsDuplicateRegistration()
    {
        var services = new ServiceCollection();
        services.AddSculptor(_ => { });

        var act = () => services.AddSculptor(_ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been called*");
    }

    [Fact]
    public void AddSculptor_ParameterlessThenOptions_ThrowsDuplicateRegistration()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        var act = () => services.AddSculptor(_ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been called*");
    }

    [Fact]
    public void AddSculptor_WithLifetime_CalledTwice_ThrowsDuplicateRegistration()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped);

        var act = () => services.AddSculptor(ServiceLifetime.Transient);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been called*");
    }
}
