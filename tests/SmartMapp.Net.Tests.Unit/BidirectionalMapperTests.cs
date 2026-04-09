using FluentAssertions;
using NSubstitute;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class BidirectionalMapperTests
{
    [Fact]
    public void GenerateInverseBlueprints_NoBidirectional_ReturnsEmpty()
    {
        var mapper = new BidirectionalMapper();
        var config = new BindingConfiguration(TypePair.Of<BidiProduct, BidiProductDto>());
        // IsBidirectional defaults to false

        var bp = Blueprint.Empty(TypePair.Of<BidiProduct, BidiProductDto>());
        var result = mapper.GenerateInverseBlueprints(new[] { config }, new[] { bp });

        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateInverseBlueprints_WithBidirectional_GeneratesInverse()
    {
        var mapper = new BidirectionalMapper();
        var config = new BindingConfiguration(TypePair.Of<BidiProduct, BidiProductDto>())
        {
            IsBidirectional = true,
        };

        var link = new PropertyLink
        {
            TargetMember = typeof(BidiProductDto).GetProperty("Name")!,
            Provider = new DirectMemberProvider(typeof(BidiProduct).GetProperty("Name")!),
            LinkedBy = ConventionMatch.ExactName("Name"),
        };

        var bp = new Blueprint
        {
            OriginType = typeof(BidiProduct),
            TargetType = typeof(BidiProductDto),
            Links = new[] { link },
        };

        var result = mapper.GenerateInverseBlueprints(new[] { config }, new[] { bp });

        result.Should().HaveCount(1);
        result[0].OriginType.Should().Be(typeof(BidiProductDto));
        result[0].TargetType.Should().Be(typeof(BidiProduct));
    }

    [Fact]
    public void GenerateInverseBlueprints_InverseLinks_AreInverted()
    {
        var mapper = new BidirectionalMapper();
        var config = new BindingConfiguration(TypePair.Of<BidiProduct, BidiProductDto>())
        {
            IsBidirectional = true,
        };

        var link = new PropertyLink
        {
            TargetMember = typeof(BidiProductDto).GetProperty("Name")!,
            Provider = new DirectMemberProvider(typeof(BidiProduct).GetProperty("Name")!),
            LinkedBy = ConventionMatch.ExactName("Name"),
        };

        var bp = new Blueprint
        {
            OriginType = typeof(BidiProduct),
            TargetType = typeof(BidiProductDto),
            Links = new[] { link },
        };

        var result = mapper.GenerateInverseBlueprints(new[] { config }, new[] { bp });

        result[0].Links.Should().HaveCount(1);
        result[0].Links[0].TargetMember.Name.Should().Be("Name");
    }

    [Fact]
    public void GenerateInverseBlueprints_SkippedProperties_NotIncludedInInverse()
    {
        var mapper = new BidirectionalMapper();
        var config = new BindingConfiguration(TypePair.Of<BidiProduct, BidiProductDto>())
        {
            IsBidirectional = true,
        };

        var link = new PropertyLink
        {
            TargetMember = typeof(BidiProductDto).GetProperty("Name")!,
            Provider = new DirectMemberProvider(typeof(BidiProduct).GetProperty("Name")!),
            LinkedBy = ConventionMatch.ExactName("Name"),
            IsSkipped = true,
        };

        var bp = new Blueprint
        {
            OriginType = typeof(BidiProduct),
            TargetType = typeof(BidiProductDto),
            Links = new[] { link },
        };

        var result = mapper.GenerateInverseBlueprints(new[] { config }, new[] { bp });

        result[0].Links.Should().BeEmpty();
    }

    [Fact]
    public void GenerateInverseBlueprints_DoesNotOverwriteExplicitReverse()
    {
        var mapper = new BidirectionalMapper();
        var config = new BindingConfiguration(TypePair.Of<BidiProduct, BidiProductDto>())
        {
            IsBidirectional = true,
        };

        var forwardBp = Blueprint.Empty(TypePair.Of<BidiProduct, BidiProductDto>());
        var reverseBp = Blueprint.Empty(TypePair.Of<BidiProductDto, BidiProduct>());

        var result = mapper.GenerateInverseBlueprints(
            new[] { config },
            new[] { forwardBp, reverseBp });

        result.Should().BeEmpty("reverse binding already exists");
    }

    [Fact]
    public void GenerateInverseBlueprints_InheritsMaxDepthAndTrackReferences()
    {
        var mapper = new BidirectionalMapper();
        var config = new BindingConfiguration(TypePair.Of<BidiProduct, BidiProductDto>())
        {
            IsBidirectional = true,
        };

        var link = new PropertyLink
        {
            TargetMember = typeof(BidiProductDto).GetProperty("Id")!,
            Provider = new DirectMemberProvider(typeof(BidiProduct).GetProperty("Id")!),
            LinkedBy = ConventionMatch.ExactName("Id"),
        };

        var bp = new Blueprint
        {
            OriginType = typeof(BidiProduct),
            TargetType = typeof(BidiProductDto),
            Links = new[] { link },
            MaxDepth = 5,
            TrackReferences = true,
        };

        var result = mapper.GenerateInverseBlueprints(new[] { config }, new[] { bp });

        result[0].MaxDepth.Should().Be(5);
        result[0].TrackReferences.Should().BeTrue();
    }
}
