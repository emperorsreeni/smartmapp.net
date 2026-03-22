using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class GuidTransformerTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void GuidToString_ValidGuid_ReturnsDFormat()
    {
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        var transformer = new GuidToStringTransformer();

        var result = transformer.Transform(guid, _scope);

        result.Should().Be("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void GuidToString_Empty_ReturnsEmptyGuidString()
    {
        var transformer = new GuidToStringTransformer();

        var result = transformer.Transform(Guid.Empty, _scope);

        result.Should().Be("00000000-0000-0000-0000-000000000000");
    }

    [Fact]
    public void StringToGuid_Valid_Parses()
    {
        var transformer = new StringToGuidTransformer();

        var result = transformer.Transform("12345678-1234-1234-1234-123456789abc", _scope);

        result.Should().Be(Guid.Parse("12345678-1234-1234-1234-123456789abc"));
    }

    [Fact]
    public void StringToGuid_Invalid_Throws()
    {
        var transformer = new StringToGuidTransformer();

        var act = () => transformer.Transform("not-a-guid", _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void StringToGuid_Null_Throws()
    {
        var transformer = new StringToGuidTransformer();

        var act = () => transformer.Transform(null!, _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void GuidRoundTrip_PreservesValue()
    {
        var original = Guid.NewGuid();
        var toString = new GuidToStringTransformer();
        var fromString = new StringToGuidTransformer();

        var str = toString.Transform(original, _scope);
        var result = fromString.Transform(str, _scope);

        result.Should().Be(original);
    }

    [Fact]
    public void StringToGuid_Whitespace_Throws()
    {
        var transformer = new StringToGuidTransformer();

        var act = () => transformer.Transform("   ", _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void StringToGuid_EmptyString_Throws()
    {
        var transformer = new StringToGuidTransformer();

        var act = () => transformer.Transform("", _scope);

        act.Should().Throw<TransformationException>();
    }
}
