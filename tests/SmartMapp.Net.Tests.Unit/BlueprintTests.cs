using FluentAssertions;
using NSubstitute;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class BlueprintTests
{
    [Fact]
    public void CanConstructWithAllProperties()
    {
        var filter = Substitute.For<IMappingFilter>();
        var link = CreateLink();

        var blueprint = new Blueprint
        {
            OriginType = typeof(Order),
            TargetType = typeof(OrderDto),
            Links = new[] { link },
            Strategy = MappingStrategy.ILEmit,
            IsParallelEligible = true,
            Filters = new[] { filter },
            MaxDepth = 5,
            TrackReferences = true,
            TargetFactory = _ => new OrderDto(),
            OnMapping = (_, _) => { },
            OnMapped = (_, _) => { },
        };

        blueprint.OriginType.Should().Be(typeof(Order));
        blueprint.TargetType.Should().Be(typeof(OrderDto));
        blueprint.Links.Should().HaveCount(1);
        blueprint.Strategy.Should().Be(MappingStrategy.ILEmit);
        blueprint.IsParallelEligible.Should().BeTrue();
        blueprint.Filters.Should().HaveCount(1);
        blueprint.MaxDepth.Should().Be(5);
        blueprint.TrackReferences.Should().BeTrue();
        blueprint.TargetFactory.Should().NotBeNull();
        blueprint.OnMapping.Should().NotBeNull();
        blueprint.OnMapped.Should().NotBeNull();
    }

    [Fact]
    public void TypePair_IsDerivedFromOriginAndTargetTypes()
    {
        var blueprint = new Blueprint
        {
            OriginType = typeof(Order),
            TargetType = typeof(OrderDto),
        };

        blueprint.TypePair.Should().Be(TypePair.Of<Order, OrderDto>());
    }

    [Fact]
    public void IsImmutable_RecordSemantics()
    {
        var blueprint = Blueprint.Empty(TypePair.Of<Order, OrderDto>());
        var modified = blueprint with { MaxDepth = 10 };

        blueprint.MaxDepth.Should().Be(int.MaxValue);
        modified.MaxDepth.Should().Be(10);
    }

    [Fact]
    public void Empty_CreatesValidBlueprint()
    {
        var pair = TypePair.Of<Order, OrderDto>();
        var blueprint = Blueprint.Empty(pair);

        blueprint.OriginType.Should().Be(typeof(Order));
        blueprint.TargetType.Should().Be(typeof(OrderDto));
        blueprint.Links.Should().BeEmpty();
        blueprint.Strategy.Should().Be(MappingStrategy.ExpressionCompiled);
        blueprint.MaxDepth.Should().Be(int.MaxValue);
        blueprint.TrackReferences.Should().BeFalse();
    }

    [Fact]
    public void Strategy_DefaultsToExpressionCompiled()
    {
        var blueprint = Blueprint.Empty(TypePair.Of<Order, OrderDto>());
        blueprint.Strategy.Should().Be(MappingStrategy.ExpressionCompiled);
    }

    [Fact]
    public void DebuggerDisplay_ShowsExpectedFormat()
    {
        var link = CreateLink();
        var blueprint = new Blueprint
        {
            OriginType = typeof(Order),
            TargetType = typeof(OrderDto),
            Links = new[] { link },
            Strategy = MappingStrategy.ExpressionCompiled,
        };

        // Access the private DebugView via reflection for testing
        var debugView = typeof(Blueprint)
            .GetProperty("DebugView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(blueprint) as string;

        debugView.Should().Be("Order -> OrderDto [ExpressionCompiled] (1 links)");
    }

    private static PropertyLink CreateLink() => new()
    {
        TargetMember = typeof(OrderDto).GetProperty("Id")!,
        Provider = Substitute.For<IValueProvider>(),
        LinkedBy = ConventionMatch.ExactName("Id"),
    };
}
