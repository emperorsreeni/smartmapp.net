using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class MappingBlueprintTests
{
    [Fact]
    public void Design_IsCalledWithBuilder()
    {
        var blueprint = new TestMappingBlueprint();
        var builder = new BlueprintBuilder();

        blueprint.Design(builder);

        builder.Bindings.Should().HaveCount(1);
        builder.Bindings[0].TypePair.Should().Be(TypePair.Of<SimpleClass, SimpleDto>());
    }

    [Fact]
    public void Design_WithPropertyConfig_AccumulatesCorrectly()
    {
        var blueprint = new DetailedMappingBlueprint();
        var builder = new BlueprintBuilder();

        blueprint.Design(builder);

        var blueprints = builder.Build();
        blueprints.Should().HaveCount(1);
        blueprints[0].Links.Should().HaveCount(2);
    }

    [Fact]
    public void Design_MultipleBindings_AllRegistered()
    {
        var blueprint = new MultiBindingBlueprint();
        var builder = new BlueprintBuilder();

        blueprint.Design(builder);

        builder.Bindings.Should().HaveCount(2);
    }

    [Fact]
    public void Design_WithInheritance_SetsInheritFromPair()
    {
        var blueprint = new InheritingBlueprint();
        var builder = new BlueprintBuilder();

        blueprint.Design(builder);

        builder.Bindings.Should().HaveCount(1);
        builder.Bindings[0].InheritFromPair.Should().Be(TypePair.Of<Vehicle, VehicleDto>());
    }

    [Fact]
    public void Design_WithBidirectional_SetsFlag()
    {
        var blueprint = new BidirectionalBlueprint();
        var builder = new BlueprintBuilder();

        blueprint.Design(builder);

        builder.Bindings[0].IsBidirectional.Should().BeTrue();
    }

    // --- Test blueprint subclasses ---

    private class TestMappingBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<SimpleClass, SimpleDto>();
        }
    }

    private class DetailedMappingBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<SimpleClass, SimpleDto>()
                .Property(d => d.Id, p => p.From(s => s.Id))
                .Property(d => d.Name, p => p.From(s => s.Name));
        }
    }

    private class MultiBindingBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<SimpleClass, SimpleDto>();
            plan.Bind<BidiProduct, BidiProductDto>();
        }
    }

    private class InheritingBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<Car, CarDto>()
                .InheritFrom<Vehicle, VehicleDto>();
        }
    }

    private class BidirectionalBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<BidiProduct, BidiProductDto>()
                .Bidirectional();
        }
    }
}
