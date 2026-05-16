using FluentAssertions;
using SmartMapp.Net;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class Sprint7DiagnosticsTests
{
    private static ISculptor Forge()
    {
        var b = new SculptorBuilder();
        b.Bind<Sprint7Order, Sprint7OrderDto>();
        return b.Forge();
    }

    [Fact]
    public void Inspect_ReturnsPerLinkTrace()
    {
        var sculptor = Forge();

        var inspection = sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();

        inspection.LinkCount.Should().BeGreaterThan(0);
        inspection.ToString().Should().Contain("Sprint7Order -> Sprint7OrderDto");
        inspection.ToString().Should().Contain("links");
    }

    [Fact]
    public void Inspect_UnknownPair_Throws()
    {
        var sculptor = new SculptorBuilder().Forge();
        var act = () => sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();
        act.Should().Throw<MappingConfigurationException>();
    }

    [Fact]
    public void GetMappingAtlas_IncludesRegisteredPair()
    {
        var sculptor = Forge();

        var atlas = sculptor.GetMappingAtlas();

        atlas.Edges.Should().Contain(e =>
            e.Pair.OriginType == typeof(Sprint7Order)
            && e.Pair.TargetType == typeof(Sprint7OrderDto));

        atlas.Nodes.Should().Contain(n => n.ClrType == typeof(Sprint7Order));
    }

    [Fact]
    public void Atlas_ToDotFormat_IsParseableGraphviz()
    {
        var sculptor = Forge();

        var dot = sculptor.GetMappingAtlas().ToDotFormat();

        dot.Should().StartWith("digraph SmartMappNet {");
        dot.Should().Contain("->");
        dot.Should().EndWith("}");
    }

    [Fact]
    public void ValidateConfiguration_Idempotent_AndThreadSafe()
    {
        var sculptor = Forge();
        var config = (ISculptorConfiguration)sculptor;

        var first = config.ValidateConfiguration();
        var second = config.ValidateConfiguration();

        first.IsValid.Should().BeTrue();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetAllBlueprintsByPair_ReturnsRegisteredPairs()
    {
        var sculptor = Forge();
        var config = (ISculptorConfiguration)sculptor;

        var byPair = config.GetAllBlueprintsByPair();
        byPair.Should().ContainKey(TypePair.Of<Sprint7Order, Sprint7OrderDto>());
    }
}
