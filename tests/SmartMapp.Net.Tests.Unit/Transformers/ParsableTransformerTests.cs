using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class ParsableTransformerTests
{
    private readonly ParsableTransformer _transformer = new();
    private readonly MappingScope _scope = new();

    [Fact]
    public void CanTransform_StringToInt_ReturnsTrue()
    {
        _transformer.CanTransform(typeof(string), typeof(int)).Should().BeTrue();
    }

    [Fact]
    public void CanTransform_IntToString_ReturnsFalse()
    {
        _transformer.CanTransform(typeof(int), typeof(string)).Should().BeFalse();
    }

    [Fact]
    public void CanTransform_StringToNonParsable_ReturnsFalse()
    {
        _transformer.CanTransform(typeof(string), typeof(object)).Should().BeFalse();
    }

    [Fact]
    public void Transform_StringToInt_Valid()
    {
        var result = _transformer.Transform("42", typeof(int), _scope);
        result.Should().Be(42);
    }

    [Fact]
    public void Transform_StringToInt_Invalid_Throws()
    {
        var act = () => _transformer.Transform("abc", typeof(int), _scope);
        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void Transform_StringToDecimal_Valid()
    {
        var result = _transformer.Transform("123.45", typeof(decimal), _scope);
        result.Should().Be(123.45m);
    }

    [Fact]
    public void Transform_StringToBool_True()
    {
        var result = _transformer.Transform("True", typeof(bool), _scope);
        result.Should().Be(true);
    }

    [Fact]
    public void Transform_NullString_ValueType_ReturnsDefault()
    {
        var result = _transformer.Transform(null, typeof(int), _scope);
        result.Should().Be(0);
    }

    [Fact]
    public void Transform_StringToLong_Valid()
    {
        var result = _transformer.Transform("9999999999", typeof(long), _scope);
        result.Should().Be(9999999999L);
    }

    [Fact]
    public void Transform_StringToDouble_Valid()
    {
        var result = _transformer.Transform("3.14", typeof(double), _scope);
        ((double)result).Should().BeApproximately(3.14, 0.001);
    }

    [Fact]
    public void Transform_StringToFloat_Valid()
    {
        var result = _transformer.Transform("2.5", typeof(float), _scope);
        ((float)result).Should().BeApproximately(2.5f, 0.01f);
    }

    [Fact]
    public void Transform_StringToInt_MinValue()
    {
        var result = _transformer.Transform(int.MinValue.ToString(), typeof(int), _scope);
        result.Should().Be(int.MinValue);
    }

    [Fact]
    public void Transform_StringToInt_MaxValue()
    {
        var result = _transformer.Transform(int.MaxValue.ToString(), typeof(int), _scope);
        result.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Transform_EmptyString_ToInt_Throws()
    {
        var act = () => _transformer.Transform("", typeof(int), _scope);
        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void Transform_StringToShort_Valid()
    {
        var result = _transformer.Transform("123", typeof(short), _scope);
        result.Should().Be((short)123);
    }

    [Fact]
    public void Transform_StringToByte_Valid()
    {
        var result = _transformer.Transform("255", typeof(byte), _scope);
        result.Should().Be((byte)255);
    }

    [Fact]
    public void Transform_StringToBool_Lowercase_True()
    {
        var result = _transformer.Transform("true", typeof(bool), _scope);
        result.Should().Be(true);
    }

    [Fact]
    public void Transform_StringToBool_False()
    {
        var result = _transformer.Transform("false", typeof(bool), _scope);
        result.Should().Be(false);
    }

    [Fact]
    public void Transform_StringToDateOnly_Valid()
    {
        var result = _transformer.Transform("2024-06-15", typeof(DateOnly), _scope);
        result.Should().Be(new DateOnly(2024, 6, 15));
    }

    [Fact]
    public void Transform_StringToTimeOnly_Valid()
    {
        var result = _transformer.Transform("14:30:00", typeof(TimeOnly), _scope);
        result.Should().Be(new TimeOnly(14, 30, 0));
    }

    [Fact]
    public void CanTransform_StringToDateOnly_ReturnsTrue()
    {
        _transformer.CanTransform(typeof(string), typeof(DateOnly)).Should().BeTrue();
    }

    [Fact]
    public void CanTransform_StringToTimeOnly_ReturnsTrue()
    {
        _transformer.CanTransform(typeof(string), typeof(TimeOnly)).Should().BeTrue();
    }
}
