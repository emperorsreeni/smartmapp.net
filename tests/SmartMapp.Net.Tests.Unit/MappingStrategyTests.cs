using FluentAssertions;

namespace SmartMapp.Net.Tests.Unit;

public class MappingStrategyTests
{
    [Fact]
    public void DefaultValue_IsExpressionCompiled()
    {
        default(MappingStrategy).Should().Be(MappingStrategy.ExpressionCompiled);
    }

    [Fact]
    public void HasAllFourMembers()
    {
        var values = Enum.GetValues<MappingStrategy>();

        values.Should().HaveCount(4);
        values.Should().Contain(MappingStrategy.ExpressionCompiled);
        values.Should().Contain(MappingStrategy.ILEmit);
        values.Should().Contain(MappingStrategy.SourceGenerated);
        values.Should().Contain(MappingStrategy.Interpreted);
    }

    [Fact]
    public void EnumParse_RoundTrips()
    {
        foreach (var value in Enum.GetValues<MappingStrategy>())
        {
            var name = value.ToString();
            var parsed = Enum.Parse<MappingStrategy>(name);
            parsed.Should().Be(value);
        }
    }
}
