using FluentAssertions;

namespace SmartMapp.Net.Tests.Unit;

public class ConventionMatchTests
{
    [Fact]
    public void ExactName_CreatesCorrectInstance()
    {
        var match = ConventionMatch.ExactName("Id");

        match.ConventionName.Should().Be("ExactName");
        match.OriginMemberPath.Should().Be("Id");
        match.Confidence.Should().Be(1.0);
        match.IsExplicit.Should().BeFalse();
    }

    [Fact]
    public void Flattened_CreatesCorrectInstance()
    {
        var match = ConventionMatch.Flattened("Customer.Address.City");

        match.ConventionName.Should().Be("Flattening");
        match.OriginMemberPath.Should().Be("Customer.Address.City");
        match.Confidence.Should().Be(1.0);
        match.IsExplicit.Should().BeFalse();
    }

    [Fact]
    public void Explicit_CreatesExplicitInstance()
    {
        var match = ConventionMatch.Explicit("FullName");

        match.ConventionName.Should().Be("ExplicitBinding");
        match.IsExplicit.Should().BeTrue();
        match.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void CustomProvider_CreatesCorrectInstance()
    {
        var match = ConventionMatch.CustomProvider(typeof(string));

        match.ConventionName.Should().Be("CustomProvider:String");
        match.OriginMemberPath.Should().BeEmpty();
        match.IsExplicit.Should().BeTrue();
    }

    [Fact]
    public void ToString_AutoDiscovered_ShowsPathAndConfidence()
    {
        var match = ConventionMatch.ExactName("Name");

        match.ToString().Should().Contain("ExactName");
        match.ToString().Should().Contain("Name");
    }

    [Fact]
    public void ToString_Explicit_ShowsConventionNameOnly()
    {
        var match = ConventionMatch.Explicit("Name");

        match.ToString().Should().Be("ExplicitBinding");
    }

    [Fact]
    public void Confidence_DefaultsToOne()
    {
        var match = new ConventionMatch
        {
            ConventionName = "Test",
            OriginMemberPath = "Path"
        };

        match.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void IsImmutable_WithSyntax()
    {
        var match = ConventionMatch.ExactName("Id");
        var modified = match with { Confidence = 0.8 };

        match.Confidence.Should().Be(1.0);
        modified.Confidence.Should().Be(0.8);
    }
}
