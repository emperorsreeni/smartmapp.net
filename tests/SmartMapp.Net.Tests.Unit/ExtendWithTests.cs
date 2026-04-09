using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class ExtendWithTests
{
    [Fact]
    public void ExtendWith_RegistersExplicitDerivedPair()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Shape, ShapeDto>()
            .ExtendWith<Circle, CircleDto>();

        builder.Bindings[0].ExplicitDerivedPairs.Should().HaveCount(1);
        builder.Bindings[0].ExplicitDerivedPairs[0].Should().Be(TypePair.Of<Circle, CircleDto>());
    }

    [Fact]
    public void ExtendWith_MultipleDerivedPairs_AllRegistered()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Shape, ShapeDto>()
            .ExtendWith<Circle, CircleDto>()
            .ExtendWith<Rectangle, RectangleDto>();

        builder.Bindings[0].ExplicitDerivedPairs.Should().HaveCount(2);
    }

    [Fact]
    public void ExtendWith_DuplicateIgnored()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Shape, ShapeDto>()
            .ExtendWith<Circle, CircleDto>()
            .ExtendWith<Circle, CircleDto>();

        builder.Bindings[0].ExplicitDerivedPairs.Should().HaveCount(1);
    }

    [Fact]
    public void ExtendWith_FedIntoInheritanceResolver_DuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Shape, ShapeDto>()
            .ExtendWith<Circle, CircleDto>();
        builder.Bind<Circle, CircleDto>();

        var blueprints = builder.Build(validate: false);
        blueprints.Should().HaveCount(2);
    }

    [Fact]
    public void ExtendWith_OverridesAutoDiscoveredPairs()
    {
        // When explicitly registered, the resolver should use the explicit pair
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto),
        });

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();

        // Explicitly register only Circle — Rectangle should still be auto-discovered
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        var derived = resolver.GetDerivedPairs(basePair);
        derived.Should().Contain(circlePair);
    }
}
