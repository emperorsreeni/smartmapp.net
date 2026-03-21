using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class StringTransformationOptionsTests
{
    [Fact]
    public void TrimAll_TrimsWhitespace()
    {
        var options = new StringTransformationOptions { TrimAll = true };

        options.Process("  hello  ").Should().Be("hello");
    }

    [Fact]
    public void NullToEmpty_ConvertsNullToEmpty()
    {
        var options = new StringTransformationOptions { NullToEmpty = true };

        options.Process(null).Should().Be(string.Empty);
    }

    [Fact]
    public void NullToEmpty_False_PreservesNull()
    {
        var options = new StringTransformationOptions { NullToEmpty = false };

        options.Process(null).Should().BeNull();
    }

    [Fact]
    public void Apply_CustomTransform_Applied()
    {
        var options = new StringTransformationOptions();
        options.Apply(s => s.ToUpperInvariant());

        options.Process("hello").Should().Be("HELLO");
    }

    [Fact]
    public void Combined_TrimAll_NullToEmpty_Custom()
    {
        var options = new StringTransformationOptions
        {
            TrimAll = true,
            NullToEmpty = true,
        };
        options.Apply(s => s.Replace("world", "WORLD"));

        options.Process("  hello world  ").Should().Be("hello WORLD");
    }

    [Fact]
    public void HasProcessing_Default_ReturnsFalse()
    {
        var options = new StringTransformationOptions();

        options.HasProcessing.Should().BeFalse();
    }

    [Fact]
    public void HasProcessing_WithTrimAll_ReturnsTrue()
    {
        var options = new StringTransformationOptions { TrimAll = true };

        options.HasProcessing.Should().BeTrue();
    }

    [Fact]
    public void Apply_Null_Throws()
    {
        var options = new StringTransformationOptions();

        var act = () => options.Apply(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
