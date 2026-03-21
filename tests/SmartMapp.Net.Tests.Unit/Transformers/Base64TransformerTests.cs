using FluentAssertions;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class Base64TransformerTests
{
    private readonly MappingScope _scope = new();

    [Fact]
    public void ByteArrayToBase64_ValidData_EncodesCorrectly()
    {
        var data = new byte[] { 72, 101, 108, 108, 111 }; // "Hello"
        var transformer = new ByteArrayToBase64Transformer();

        var result = transformer.Transform(data, _scope);

        result.Should().Be("SGVsbG8=");
    }

    [Fact]
    public void Base64ToByteArray_ValidBase64_DecodesCorrectly()
    {
        var transformer = new Base64ToByteArrayTransformer();

        var result = transformer.Transform("SGVsbG8=", _scope);

        result.Should().BeEquivalentTo(new byte[] { 72, 101, 108, 108, 111 });
    }

    [Fact]
    public void RoundTrip_PreservesData()
    {
        var original = new byte[] { 0, 1, 2, 255, 128, 64 };
        var toBase64 = new ByteArrayToBase64Transformer();
        var fromBase64 = new Base64ToByteArrayTransformer();

        var encoded = toBase64.Transform(original, _scope);
        var decoded = fromBase64.Transform(encoded, _scope);

        decoded.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ByteArrayToBase64_Null_ReturnsNull()
    {
        var transformer = new ByteArrayToBase64Transformer();

        var result = transformer.Transform(null!, _scope);

        result.Should().BeNull();
    }

    [Fact]
    public void Base64ToByteArray_Null_ReturnsNull()
    {
        var transformer = new Base64ToByteArrayTransformer();

        var result = transformer.Transform(null!, _scope);

        result.Should().BeNull();
    }

    [Fact]
    public void ByteArrayToBase64_EmptyArray_ReturnsEmptyString()
    {
        var transformer = new ByteArrayToBase64Transformer();

        var result = transformer.Transform([], _scope);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Base64ToByteArray_EmptyString_ReturnsEmptyArray()
    {
        var transformer = new Base64ToByteArrayTransformer();

        var result = transformer.Transform("", _scope);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Base64ToByteArray_InvalidBase64_Throws()
    {
        var transformer = new Base64ToByteArrayTransformer();

        var act = () => transformer.Transform("not-valid-base64!!!", _scope);

        act.Should().Throw<TransformationException>();
    }
}
