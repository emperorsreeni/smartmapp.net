using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T01 — unit tests for <see cref="SculptorBuilderFactory"/>.
/// These assertions lock the factory's responsibilities in isolation from the
/// <see cref="IServiceCollection"/> plumbing.
/// </summary>
public class SculptorBuilderFactoryTests
{
    [Fact]
    public void Ctor_NullAssemblies_Throws()
    {
        var act = () => new SculptorBuilderFactory(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("assemblies");
    }

    [Fact]
    public void Assemblies_ExposesConstructorArgument()
    {
        var assemblies = new[] { typeof(SculptorBuilderFactoryTests).Assembly };

        var factory = new SculptorBuilderFactory(assemblies);

        factory.Assemblies.Should().BeEquivalentTo(assemblies);
    }

    [Fact]
    public void HasConfigure_FalseWhenNoCallbackProvided()
    {
        var factory = new SculptorBuilderFactory(Array.Empty<Assembly>());

        factory.HasConfigure.Should().BeFalse();
    }

    [Fact]
    public void HasConfigure_TrueWhenCallbackProvided()
    {
        var factory = new SculptorBuilderFactory(
            Array.Empty<Assembly>(),
            _ => { });

        factory.HasConfigure.Should().BeTrue();
    }

    [Fact]
    public void Build_ReturnsBuilderWithScannedAssembliesQueued()
    {
        var asm = typeof(SculptorBuilderFactoryTests).Assembly;
        var factory = new SculptorBuilderFactory(new[] { asm });

        var builder = factory.Build();

        builder.Options.Assemblies.Should().Contain(asm);
    }

    [Fact]
    public void Build_InvokesConfigureCallbackOnce()
    {
        var invocationCount = 0;
        var factory = new SculptorBuilderFactory(
            Array.Empty<Assembly>(),
            _ => invocationCount++);

        _ = factory.Build();

        invocationCount.Should().Be(1);
    }

    [Fact]
    public void Build_CallbackReceivesLiveOptionsInstance()
    {
        SculptorOptions? captured = null;
        var factory = new SculptorBuilderFactory(
            Array.Empty<Assembly>(),
            opts => captured = opts);

        var builder = factory.Build();

        captured.Should().NotBeNull();
        captured.Should().BeSameAs(builder.Options);
    }

    [Fact]
    public void Build_CalledTwice_ProducesIndependentBuilders()
    {
        var factory = new SculptorBuilderFactory(
            new[] { typeof(SculptorBuilderFactoryTests).Assembly });

        var b1 = factory.Build();
        var b2 = factory.Build();

        b1.Should().NotBeSameAs(b2);
        b1.Options.Should().NotBeSameAs(b2.Options);
    }

    [Fact]
    public void Build_EmptyAssemblyList_StillProducesValidBuilder()
    {
        var factory = new SculptorBuilderFactory(Array.Empty<Assembly>());

        var builder = factory.Build();

        builder.Should().NotBeNull();
        builder.Options.Assemblies.Should().BeEmpty();
    }
}
