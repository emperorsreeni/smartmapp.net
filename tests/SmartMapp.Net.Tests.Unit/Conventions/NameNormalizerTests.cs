using FluentAssertions;
using SmartMapp.Net.Conventions;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("FirstName", new[] { "First", "Name" })]
    [InlineData("firstName", new[] { "first", "Name" })]
    [InlineData("first_name", new[] { "first", "name" })]
    [InlineData("FIRST_NAME", new[] { "FIRST", "NAME" })]
    [InlineData("first-name", new[] { "first", "name" })]
    [InlineData("XMLParser", new[] { "XML", "Parser" })]
    [InlineData("getHTTPResponse", new[] { "get", "HTTP", "Response" })]
    [InlineData("ID", new[] { "ID" })]
    [InlineData("id", new[] { "id" })]
    [InlineData("A", new[] { "A" })]
    public void Segment_SplitsCorrectly(string input, string[] expected)
    {
        NameNormalizer.Segment(input).Should().BeEquivalentTo(expected, opts => opts.WithStrictOrdering());
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Segment_EmptyOrNull_ReturnsEmpty(string? input)
    {
        NameNormalizer.Segment(input!).Should().BeEmpty();
    }

    [Fact]
    public void Segment_UnderscorePrefix()
    {
        var result = NameNormalizer.Segment("_name");
        result.Should().BeEquivalentTo(new[] { "name" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Segment_DoubleUnderscore()
    {
        var result = NameNormalizer.Segment("first__name");
        result.Should().BeEquivalentTo(new[] { "first", "name" }, opts => opts.WithStrictOrdering());
    }

    [Theory]
    [InlineData(new[] { "first", "name" }, "FirstName")]
    [InlineData(new[] { "FIRST", "NAME" }, "FirstName")]
    [InlineData(new[] { "First", "Name" }, "FirstName")]
    [InlineData(new[] { "id" }, "Id")]
    public void ToPascalCase_JoinsCorrectly(string[] segments, string expected)
    {
        NameNormalizer.ToPascalCase(segments).Should().Be(expected);
    }

    [Fact]
    public void ToPascalCase_EmptySegments_ReturnsEmpty()
    {
        NameNormalizer.ToPascalCase(Array.Empty<string>()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("first_name", "FirstName", true)]
    [InlineData("firstName", "FirstName", true)]
    [InlineData("FIRST_NAME", "FirstName", true)]
    [InlineData("first-name", "FirstName", true)]
    [InlineData("FirstName", "first_name", true)]
    [InlineData("FirstName", "LastName", false)]
    [InlineData("First", "FirstName", false)]
    [InlineData("Name", "Name", true)]
    public void AreEquivalent_CorrectResult(string name1, string name2, bool expected)
    {
        NameNormalizer.AreEquivalent(name1, name2).Should().Be(expected);
    }
}
