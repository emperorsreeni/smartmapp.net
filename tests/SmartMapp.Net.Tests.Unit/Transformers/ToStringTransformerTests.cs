using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class ToStringTransformerTests
{
    private readonly ToStringTransformer _transformer = new();
    private readonly MappingScope _scope = new();

    [Fact]
    public void CanTransform_AnyToString_ReturnsTrue()
    {
        _transformer.CanTransform(typeof(int), typeof(string)).Should().BeTrue();
        _transformer.CanTransform(typeof(DateTime), typeof(string)).Should().BeTrue();
    }

    [Fact]
    public void CanTransform_StringToInt_ReturnsFalse()
    {
        _transformer.CanTransform(typeof(string), typeof(int)).Should().BeFalse();
    }

    [Fact]
    public void Transform_Int_ReturnsString()
    {
        _transformer.Transform(42, _scope).Should().Be("42");
    }

    [Fact]
    public void Transform_DateTime_UsesIFormattable()
    {
        var dt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var result = _transformer.Transform(dt, _scope);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Transform_Null_ReturnsNull()
    {
        _transformer.Transform(null, _scope).Should().BeNull();
    }

    [Fact]
    public void Transform_CustomObject_UsesToString()
    {
        var obj = new TestTypes.Money { Amount = 99.99m, Currency = "EUR" };
        var result = _transformer.Transform(obj, _scope);
        result.Should().Be("EUR 99.99");
    }
}
