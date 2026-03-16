using FluentAssertions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class TypePairTests
{
    [Fact]
    public void SameTypes_AreEqual()
    {
        var pair1 = new TypePair(typeof(Order), typeof(OrderDto));
        var pair2 = new TypePair(typeof(Order), typeof(OrderDto));

        pair1.Should().Be(pair2);
    }

    [Fact]
    public void SwappedTypes_AreNotEqual()
    {
        var pair1 = new TypePair(typeof(Order), typeof(OrderDto));
        var pair2 = new TypePair(typeof(OrderDto), typeof(Order));

        pair1.Should().NotBe(pair2);
    }

    [Fact]
    public void DifferentTypes_AreNotEqual()
    {
        var pair1 = new TypePair(typeof(Order), typeof(OrderDto));
        var pair2 = new TypePair(typeof(Customer), typeof(SimpleDto));

        pair1.Should().NotBe(pair2);
    }

    [Fact]
    public void GetHashCode_IsConsistentWithEquals()
    {
        var pair1 = new TypePair(typeof(Order), typeof(OrderDto));
        var pair2 = new TypePair(typeof(Order), typeof(OrderDto));

        pair1.GetHashCode().Should().Be(pair2.GetHashCode());
    }

    [Fact]
    public void Of_ReturnsCorrectPair()
    {
        var pair = TypePair.Of<Order, OrderDto>();

        pair.OriginType.Should().Be(typeof(Order));
        pair.TargetType.Should().Be(typeof(OrderDto));
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        var pair = TypePair.Of<Order, OrderDto>();

        pair.ToString().Should().Be("Order -> OrderDto");
    }

    [Fact]
    public void CanBeUsedAsDictionaryKey()
    {
        var dict = new Dictionary<TypePair, string>();
        var pair = TypePair.Of<Order, OrderDto>();

        dict[pair] = "test";

        dict[TypePair.Of<Order, OrderDto>()].Should().Be("test");
    }
}
