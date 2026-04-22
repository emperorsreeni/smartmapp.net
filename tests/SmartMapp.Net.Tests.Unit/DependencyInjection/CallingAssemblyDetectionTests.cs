using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Internal;
using SmartMapp.Net.Tests.ExternalAssemblyFixture;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T01 — verifies
/// <see cref="SculptorServiceCollectionExtensions.AddSculptor(IServiceCollection)"/>
/// correctly infers the calling assembly via <see cref="System.Reflection.Assembly.GetCallingAssembly"/>.
/// Uses a trampoline in <c>SmartMapp.Net.Tests.ExternalAssemblyFixture</c> so the caller
/// is a distinct assembly from the test itself.
/// </summary>
public class CallingAssemblyDetectionTests
{
    [Fact]
    public void AddSculptor_InvokedFromTestAssembly_ScansTestAssembly()
    {
        var services = new ServiceCollection();
        services.AddSculptor();

        var host = services
            .BuildServiceProvider()
            .GetRequiredService<ForgedSculptorHost>();

        host.Factory.Assemblies.Should().ContainSingle()
            .Which.Should().BeSameAs(typeof(CallingAssemblyDetectionTests).Assembly);
    }

    [Fact]
    public void AddSculptor_InvokedFromExternalAssembly_ScansExternalAssembly()
    {
        var services = new ServiceCollection();

        // Trampoline lives in SmartMapp.Net.Tests.ExternalAssemblyFixture — calling-assembly
        // detection should pick that up, not this test assembly.
        ExternalCaller.CallAddSculptor(services);

        var host = services
            .BuildServiceProvider()
            .GetRequiredService<ForgedSculptorHost>();

        host.Factory.Assemblies.Should().ContainSingle()
            .Which.Should().BeSameAs(typeof(ExternalCaller).Assembly);
        host.Factory.Assemblies.Single()
            .Should().NotBeSameAs(typeof(CallingAssemblyDetectionTests).Assembly);
    }
}
