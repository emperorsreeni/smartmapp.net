using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

/// <summary>
/// Tests for InheritFrom (Blueprint Inheritance) via the fluent API (S6-T04).
/// Validates link copying, override, multi-level, and circular detection through Build().
/// </summary>
public class BlueprintInheritanceTests
{
    [Fact]
    public void InheritFrom_CopiesBaseLinks()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make))
            .Property(d => d.Model, p => p.From(s => s.Model));

        builder.Bind<Car, CarDto>()
            .InheritFrom<Vehicle, VehicleDto>()
            .Property(d => d.Doors, p => p.From(s => s.Doors));

        var blueprints = builder.Build(validate: false);
        var carBp = blueprints.First(b => b.OriginType == typeof(Car));

        carBp.Links.Should().HaveCount(3);
        carBp.Links.Should().Contain(l => l.TargetMember.Name == "Make");
        carBp.Links.Should().Contain(l => l.TargetMember.Name == "Model");
        carBp.Links.Should().Contain(l => l.TargetMember.Name == "Doors");
    }

    [Fact]
    public void InheritFrom_DerivedOverridesBase()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make))
            .Property(d => d.Model, p => p.From(s => s.Model));

        builder.Bind<Car, CarDto>()
            .InheritFrom<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make).FallbackTo("OverriddenMake"))
            .Property(d => d.Doors, p => p.From(s => s.Doors));

        var blueprints = builder.Build(validate: false);
        var carBp = blueprints.First(b => b.OriginType == typeof(Car));

        carBp.Links.Should().HaveCount(3);
        var makeLink = carBp.Links.First(l => l.TargetMember.Name == "Make");
        makeLink.Fallback.Should().Be("OverriddenMake");
    }

    [Fact]
    public void InheritFrom_MultiLevel_AllLinksMerged()
    {
        // Simulates 3-level: A inherits B inherits C
        // Use Shape → Circle → FilledCircle pattern
        var builder = new BlueprintBuilder();

        builder.Bind<Shape, ShapeDto>()
            .Property(d => d.Id, p => p.From(s => s.Id))
            .Property(d => d.Color, p => p.From(s => s.Color));

        builder.Bind<Circle, CircleDto>()
            .InheritFrom<Shape, ShapeDto>()
            .Property(d => d.Radius, p => p.From(s => s.Radius));

        builder.Bind<FilledCircle, FilledCircleDto>()
            .InheritFrom<Circle, CircleDto>()
            .Property(d => d.FillColor, p => p.From(s => s.FillColor));

        var blueprints = builder.Build(validate: false);
        var filledBp = blueprints.First(b => b.OriginType == typeof(FilledCircle));

        // Should have: Id + Color (from Shape), Radius (from Circle), FillColor (own)
        filledBp.Links.Should().HaveCount(4);
        filledBp.Links.Should().Contain(l => l.TargetMember.Name == "Id");
        filledBp.Links.Should().Contain(l => l.TargetMember.Name == "Color");
        filledBp.Links.Should().Contain(l => l.TargetMember.Name == "Radius");
        filledBp.Links.Should().Contain(l => l.TargetMember.Name == "FillColor");
    }

    [Fact]
    public void InheritFrom_CircularInheritance_DetectedDuringBuild()
    {
        // Create circular: A inherits B, B inherits A
        var resolver = new InheritanceResolver();
        var pairA = TypePair.Of<Shape, ShapeDto>();
        var pairB = TypePair.Of<Circle, CircleDto>();

        resolver.RegisterInheritFrom(pairA, pairB);
        resolver.RegisterInheritFrom(pairB, pairA);

        var blueprints = new[]
        {
            Blueprint.Empty(pairA),
            Blueprint.Empty(pairB),
        };

        var inheritResolver = new BlueprintInheritanceResolver(resolver, blueprints);

        var act = () => inheritResolver.ResolveAll();
        act.Should().Throw<Diagnostics.BlueprintValidationException>()
            .WithMessage("*Circular*");
    }

    [Fact]
    public void InheritFrom_BaseBlueprint_NotModified()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make));

        builder.Bind<Car, CarDto>()
            .InheritFrom<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make).FallbackTo("Override"))
            .Property(d => d.Doors, p => p.From(s => s.Doors));

        var blueprints = builder.Build(validate: false);
        var vehicleBp = blueprints.First(b => b.OriginType == typeof(Vehicle));

        // Base should still have only 1 link, unmodified
        vehicleBp.Links.Should().HaveCount(1);
        vehicleBp.Links[0].Fallback.Should().BeNull();
    }
}
