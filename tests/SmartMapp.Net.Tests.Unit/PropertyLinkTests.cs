using FluentAssertions;
using NSubstitute;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class PropertyLinkTests
{
    [Fact]
    public void CanConstructWithAllProperties()
    {
        var targetMember = typeof(SimpleDto).GetProperty("Id")!;
        var provider = Substitute.For<IValueProvider>();
        var transformer = Substitute.For<ITypeTransformer>();
        var linkedBy = ConventionMatch.ExactName("Id");

        var link = new PropertyLink
        {
            TargetMember = targetMember,
            Provider = provider,
            Transformer = transformer,
            LinkedBy = linkedBy,
            IsSkipped = false,
            Fallback = 0,
            Order = 1,
            Condition = _ => true,
            PreCondition = _ => true,
        };

        link.TargetMember.Should().BeSameAs(targetMember);
        link.Provider.Should().BeSameAs(provider);
        link.Transformer.Should().BeSameAs(transformer);
        link.LinkedBy.Should().Be(linkedBy);
        link.IsSkipped.Should().BeFalse();
        link.Fallback.Should().Be(0);
        link.Order.Should().Be(1);
        link.Condition.Should().NotBeNull();
        link.PreCondition.Should().NotBeNull();
    }

    [Fact]
    public void IsImmutable_RecordSemantics()
    {
        var link = CreateMinimalLink();
        var modified = link with { IsSkipped = true };

        link.IsSkipped.Should().BeFalse();
        modified.IsSkipped.Should().BeTrue();
    }

    [Fact]
    public void IsSkipped_DefaultsToFalse()
    {
        var link = CreateMinimalLink();
        link.IsSkipped.Should().BeFalse();
    }

    [Fact]
    public void Fallback_DefaultsToNull()
    {
        var link = CreateMinimalLink();
        link.Fallback.Should().BeNull();
    }

    [Fact]
    public void Order_DefaultsToZero()
    {
        var link = CreateMinimalLink();
        link.Order.Should().Be(0);
    }

    private static PropertyLink CreateMinimalLink() => new()
    {
        TargetMember = typeof(SimpleDto).GetProperty("Id")!,
        Provider = Substitute.For<IValueProvider>(),
        LinkedBy = ConventionMatch.ExactName("Id"),
    };
}
