using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class TransformationExceptionTests
{
    [Fact]
    public void InheritsFromSmartMappException()
    {
        var ex = new TransformationException("test");

        ex.Should().BeAssignableTo<SmartMappException>();
        ex.Should().BeAssignableTo<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WithFullContext_SetsAllProperties()
    {
        var inner = new FormatException("parse failed");
        var ex = new TransformationException(
            "Cannot parse 'abc' as Int32.",
            originValue: "abc",
            originType: typeof(string),
            targetType: typeof(int),
            innerException: inner);

        ex.Message.Should().Be("Cannot parse 'abc' as Int32.");
        ex.OriginValue.Should().Be("abc");
        ex.OriginType.Should().Be(typeof(string));
        ex.TargetType.Should().Be(typeof(int));
        ex.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void Constructor_MessageOnly_HasNullDiagnostics()
    {
        var ex = new TransformationException("something failed");

        ex.Message.Should().Be("something failed");
        ex.OriginValue.Should().BeNull();
        ex.OriginType.Should().BeNull();
        ex.TargetType.Should().BeNull();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithInnerException_Preserves()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new TransformationException("outer", inner);

        ex.InnerException.Should().BeSameAs(inner);
    }
}
