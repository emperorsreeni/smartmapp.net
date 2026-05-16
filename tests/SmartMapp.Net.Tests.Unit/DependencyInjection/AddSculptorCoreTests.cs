using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T01 — core DI registration behaviour for
/// <see cref="SculptorServiceCollectionExtensions.AddSculptor(IServiceCollection)"/>.
/// </summary>
public class AddSculptorCoreTests
{
    [Fact]
    public void AddSculptor_RegistersISculptorSingleton()
    {
        var services = new ServiceCollection();

        services.AddSculptor();

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptor) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSculptor_RegistersISculptorConfigurationSingleton()
    {
        var services = new ServiceCollection();

        services.AddSculptor();

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ISculptorConfiguration)
            && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSculptor_RegistersForgedSculptorHostAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddSculptor();

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(ForgedSculptorHost) && d.Lifetime == ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddSculptor_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();

        var returned = services.AddSculptor();

        returned.Should().BeSameAs(services);
    }

    [Fact]
    public void AddSculptor_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddSculptor();

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("services");
    }

    [Fact]
    public void AddSculptor_DoesNotForgeEagerly_ForgeRunsOnFirstResolve()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<ForgedSculptorHost>();

        host.IsForged.Should().BeFalse(
            "Forge() must not run until the first resolve of ISculptor.");

        _ = provider.GetRequiredService<ISculptor>();
        host.IsForged.Should().BeTrue();
    }

    [Fact]
    public void AddSculptor_ResolvedSculptor_IsSingletonAcrossResolves()
    {
        var services = new ServiceCollection();
        services.AddSculptor();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<ISculptor>();
        var second = provider.GetRequiredService<ISculptor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddSculptor_ResolvedSculptorConfiguration_IsSameInstanceAsSculptor()
    {
        var services = new ServiceCollection();
        services.AddSculptor();
        using var provider = services.BuildServiceProvider();

        var sculptor = provider.GetRequiredService<ISculptor>();
        var config = provider.GetRequiredService<ISculptorConfiguration>();

        config.Should().BeSameAs(sculptor,
            "Sculptor implements both ISculptor and ISculptorConfiguration; DI must return the same instance.");
    }

    [Fact]
    public void AddSculptor_CalledTwice_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        var act = () => services.AddSculptor();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been called*")
            .WithMessage("*keyed services*");
    }

    [Fact]
    public void AddSculptor_DuplicateRegistration_MentionsSprint16MigrationPath()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        var act = () => services.AddSculptor();

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("Sprint 16", StringComparison.Ordinal));
    }

    [Fact]
    public void AddSculptor_OnEmptyServiceCollection_ResolvesSculptor()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        using var provider = services.BuildServiceProvider();

        // No blueprints registered — forge still succeeds, yielding an empty sculptor.
        var sculptor = provider.GetRequiredService<ISculptor>();
        sculptor.Should().NotBeNull();
    }

    [Fact]
    public void AddSculptor_RegistersSentinelAsImplementationInstance()
    {
        // The duplicate guard relies on ImplementationInstance identity (spec Technical
        // Considerations) — this test locks the descriptor shape so future refactors
        // cannot silently downgrade detection to a type-only match.
        var services = new ServiceCollection();

        services.AddSculptor();

        services.Should().ContainSingle(d =>
            d.ServiceType == typeof(SculptorRegistrationState)
            && ReferenceEquals(d.ImplementationInstance, SculptorRegistrationState.Instance));
    }
}
