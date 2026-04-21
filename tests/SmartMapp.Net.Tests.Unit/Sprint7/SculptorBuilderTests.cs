using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Diagnostics;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class SculptorBuilderTests
{
    private static ISculptorConfiguration AsConfig(ISculptor sculptor)
        => (ISculptorConfiguration)sculptor;

    [Fact]
    public void Forge_WithSingleBind_ProducesWorkingSculptor()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        var sculptor = builder.Forge();

        var result = sculptor.Map<Sprint7Order, Sprint7OrderDto>(new Sprint7Order
        {
            Id = 7,
            CustomerName = "Alice",
            Total = 42.5m,
        });

        result.Should().NotBeNull();
        result.Id.Should().Be(7);
        result.CustomerName.Should().Be("Alice");
        result.Total.Should().Be(42.5m);
    }

    [Fact]
    public void Forge_CalledTwice_Throws()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        _ = builder.Forge();

        var act = () => builder.Forge();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Builder_Mutation_AfterForge_Throws()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        _ = builder.Forge();

        var act = () => builder.Bind<Sprint7Customer, Sprint7OrderDto>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UseBlueprint_Type_Instantiated_And_Applied()
    {
        var sculptor = new SculptorBuilder()
            .UseBlueprint<Sprint7SimpleBlueprint>()
            .Forge();

        AsConfig(sculptor).HasBinding<Sprint7Order, Sprint7OrderDto>().Should().BeTrue();
    }

    [Fact]
    public void Options_Bind_Inline_ProducesBlueprint()
    {
        var sculptor = new SculptorBuilder()
            .Configure(o => o.Bind<Sprint7Order, Sprint7OrderDto>(rule => { }))
            .Forge();

        AsConfig(sculptor).HasBinding<Sprint7Order, Sprint7OrderDto>().Should().BeTrue();
    }

    [Fact]
    public void ScanAssemblies_PicksUpBlueprintSubclass()
    {
        var sculptor = new SculptorBuilder()
            .ScanAssembliesContaining<Sprint7SimpleBlueprint>()
            .Forge();

        AsConfig(sculptor).HasBinding<Sprint7Order, Sprint7OrderDto>().Should().BeTrue();
    }
}
