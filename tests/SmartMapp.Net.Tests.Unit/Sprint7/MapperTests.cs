using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Runtime;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class Sprint7MapperTests
{
    private static Sculptor ForgeSculptor()
    {
        var b = new SculptorBuilder();
        b.Bind<Sprint7Order, Sprint7OrderDto>();
        return (Sculptor)b.Forge();
    }

    [Fact]
    public void Mapper_Map_MatchesSculptorMap()
    {
        var sculptor = ForgeSculptor();
        var mapper = sculptor.GetMapper<Sprint7Order, Sprint7OrderDto>();

        var origin = new Sprint7Order { Id = 1, CustomerName = "Bob", Total = 9.99m };
        var viaSculptor = sculptor.Map<Sprint7Order, Sprint7OrderDto>(origin);
        var viaMapper = mapper.Map(origin);

        viaMapper.Should().BeEquivalentTo(viaSculptor);
    }

    [Fact]
    public void Mapper_UnknownPair_ThrowsAtConstruction()
    {
        var sculptor = ForgeSculptor();

        var act = () => sculptor.GetMapper<Sprint7Customer, Sprint7OrderDto>();
        act.Should().Throw<MappingConfigurationException>();
    }

    [Fact]
    public async Task Sculptor_Concurrent_Map_IsThreadSafe()
    {
        ISculptor sculptor = ForgeSculptor();

        var tasks = Enumerable.Range(0, 200).Select(i => Task.Run(() =>
        {
            var origin = new Sprint7Order { Id = i, CustomerName = $"C{i}", Total = i };
            var dto = sculptor.Map<Sprint7Order, Sprint7OrderDto>(origin);
            dto.Id.Should().Be(i);
        })).ToArray();

        await Task.WhenAll(tasks);
    }
}
