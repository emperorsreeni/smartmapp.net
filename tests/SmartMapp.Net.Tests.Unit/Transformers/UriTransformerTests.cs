using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class UriTransformerTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void StringToUri_Absolute_Parses()
    {
        var transformer = new StringToUriTransformer();

        var result = transformer.Transform("https://example.com/api", _scope);

        result.Should().Be(new Uri("https://example.com/api"));
    }

    [Fact]
    public void StringToUri_Relative_Parses()
    {
        var transformer = new StringToUriTransformer();

        var result = transformer.Transform("/api/orders", _scope);

        result.OriginalString.Should().Be("/api/orders");
    }

    [Fact]
    public void UriToString_ReturnsOriginalString()
    {
        var transformer = new UriToStringTransformer();
        var uri = new Uri("https://example.com/path?q=1");

        var result = transformer.Transform(uri, _scope);

        result.Should().Be("https://example.com/path?q=1");
    }

    [Fact]
    public void StringToUri_EmptyString_Throws()
    {
        var transformer = new StringToUriTransformer();

        var act = () => transformer.Transform("", _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void StringToUri_Null_ReturnsNull()
    {
        var transformer = new StringToUriTransformer();

        var result = transformer.Transform(null!, _scope);

        result.Should().BeNull();
    }

    [Fact]
    public void UriToString_Null_ReturnsNull()
    {
        var transformer = new UriToStringTransformer();

        var result = transformer.Transform(null!, _scope);

        result.Should().BeNull();
    }
}
