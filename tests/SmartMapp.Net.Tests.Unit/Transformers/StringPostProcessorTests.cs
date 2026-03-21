using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class StringPostProcessorTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void Transform_WithTrimAll_TrimsWhitespace()
    {
        var options = new StringTransformationOptions { TrimAll = true };
        var processor = new StringPostProcessor(options);

        processor.Transform("  hello  ", _scope).Should().Be("hello");
    }

    [Fact]
    public void ProcessNullable_WithNullToEmpty_ConvertsNull()
    {
        var options = new StringTransformationOptions { NullToEmpty = true };
        var processor = new StringPostProcessor(options);

        processor.ProcessNullable(null).Should().Be(string.Empty);
    }

    [Fact]
    public void Transform_WithCustomTransform_AppliesTransform()
    {
        var options = new StringTransformationOptions();
        options.Apply(s => s.ToUpperInvariant());
        var processor = new StringPostProcessor(options);

        processor.Transform("hello", _scope).Should().Be("HELLO");
    }

    [Fact]
    public void HasProcessing_WhenNoOptions_ReturnsFalse()
    {
        var processor = new StringPostProcessor(new StringTransformationOptions());

        processor.HasProcessing.Should().BeFalse();
    }

    [Fact]
    public void HasProcessing_WhenTrimAll_ReturnsTrue()
    {
        var processor = new StringPostProcessor(new StringTransformationOptions { TrimAll = true });

        processor.HasProcessing.Should().BeTrue();
    }

    [Fact]
    public void CanTransform_StringToString_ReturnsTrue()
    {
        var processor = new StringPostProcessor(new StringTransformationOptions());

        processor.CanTransform(typeof(string), typeof(string)).Should().BeTrue();
    }

    [Fact]
    public void CanTransform_IntToString_ReturnsFalse()
    {
        var processor = new StringPostProcessor(new StringTransformationOptions());

        processor.CanTransform(typeof(int), typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new StringPostProcessor(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
