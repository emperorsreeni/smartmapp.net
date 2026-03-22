using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class BoolIntTransformerTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void BoolToInt_True_Returns1()
    {
        new BoolToIntTransformer().Transform(true, _scope).Should().Be(1);
    }

    [Fact]
    public void BoolToInt_False_Returns0()
    {
        new BoolToIntTransformer().Transform(false, _scope).Should().Be(0);
    }

    [Fact]
    public void IntToBool_1_ReturnsTrue()
    {
        new IntToBoolTransformer().Transform(1, _scope).Should().BeTrue();
    }

    [Fact]
    public void IntToBool_0_ReturnsFalse()
    {
        new IntToBoolTransformer().Transform(0, _scope).Should().BeFalse();
    }

    [Fact]
    public void IntToBool_42_ReturnsTrue()
    {
        new IntToBoolTransformer().Transform(42, _scope).Should().BeTrue();
    }

    [Fact]
    public void IntToBool_Negative1_ReturnsTrue()
    {
        new IntToBoolTransformer().Transform(-1, _scope).Should().BeTrue();
    }
}
