using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Diagnostics;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class MappingInspectionTests
{
    [Fact]
    public void Build_FlatPair_EmitsHeader_And_Lines()
    {
        var sculptor = Forge<Sprint7Order, Sprint7OrderDto>();
        var inspection = sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();

        inspection.TypePair.Should().Be(TypePair.Of<Sprint7Order, Sprint7OrderDto>());
        inspection.Blueprint.Should().NotBeNull();
        inspection.LinkCount.Should().BeGreaterThan(0);
        inspection.Links.Should().NotBeEmpty();

        var render = inspection.ToString();
        render.Should().StartWith("Sprint7Order -> Sprint7OrderDto (Strategy: ExpressionCompiled");
        render.Should().Contain("links)");
    }

    [Fact]
    public void Build_SkippedMember_RendersWithSkippedMarker()
    {
        var sculptor = Forge<NestedInspectOrigin, NestedInspectDto>();
        var inspection = sculptor.Inspect<NestedInspectOrigin, NestedInspectDto>();

        inspection.SkippedMembers.Should().Contain(nameof(NestedInspectDto.InternalCode));
        inspection.ToString().Should().Contain("[SKIPPED]");
    }

    [Fact]
    public void Build_NestedMapping_AnnotatedInLine()
    {
        var sculptor = Forge<NestedInspectOrigin, NestedInspectDto>();
        var inspection = sculptor.Inspect<NestedInspectOrigin, NestedInspectDto>();

        // Customer (nested complex type on both sides → nested annotation)
        var nestedLine = inspection.Links.SingleOrDefault(l =>
            l.TargetMember.Name == nameof(NestedInspectDto.Customer));

        nestedLine.Should().NotBeNull();
        nestedLine!.NestedOriginType.Should().Be(typeof(InspectCustomer));
        nestedLine.NestedTargetType.Should().Be(typeof(InspectCustomerDto));
        nestedLine.ToString().Should().Contain("(NestedMapping: InspectCustomer -> InspectCustomerDto)");
    }

    [Fact]
    public void Inspect_Cached_SameInstanceReturned()
    {
        var sculptor = Forge<Sprint7Order, Sprint7OrderDto>();
        var first = sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();
        var second = sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Inspect_UnknownPair_ThrowsActionable()
    {
        var sculptor = new SculptorBuilder().Forge();
        var act = () => sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();
        act.Should().Throw<MappingConfigurationException>()
            .WithMessage("*no blueprint registered*");
    }

    private static ISculptor Forge<TOrigin, TTarget>()
    {
        var b = new SculptorBuilder();
        b.Bind<TOrigin, TTarget>();
        return b.Forge();
    }

    // ---- Fixtures ----

    public class InspectCustomer
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class InspectCustomerDto
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
    }

    public class NestedInspectOrigin
    {
        public int Id { get; set; }
        public InspectCustomer Customer { get; set; } = new();
        public string InternalCode { get; set; } = string.Empty;
    }

    public class NestedInspectDto
    {
        public int Id { get; set; }
        public InspectCustomerDto Customer { get; set; } = new();

        [SmartMapp.Net.Attributes.Unmapped]
        public string? InternalCode { get; set; }
    }
}
