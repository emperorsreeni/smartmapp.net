using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class InheritanceResolverTests
{
    [Fact]
    public void Constructor_WithKnownTypes_BuildsHierarchy()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle), typeof(Square),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto), typeof(SquareDto),
        });

        // Should not throw
        resolver.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyTypes_DoesNotThrow()
    {
        var resolver = new InheritanceResolver();
        resolver.Should().NotBeNull();
    }

    [Fact]
    public void BuildDerivedPairLookup_AutoDiscoversDerivedPairs()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto),
        });

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var allPairs = new[]
        {
            basePair,
            TypePair.Of<Circle, CircleDto>(),
            TypePair.Of<Rectangle, RectangleDto>(),
        };

        resolver.BuildDerivedPairLookup(allPairs);

        resolver.HasDerivedPairs(basePair).Should().BeTrue();
        var derivedPairs = resolver.GetDerivedPairs(basePair);
        derivedPairs.Should().NotBeEmpty();
    }

    [Fact]
    public void RegisterExplicitDerivedPair_TakesPriority()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var derivedPair = TypePair.Of<Circle, CircleDto>();

        resolver.RegisterExplicitDerivedPair(basePair, derivedPair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        var derived = resolver.GetDerivedPairs(basePair);
        derived.Should().Contain(derivedPair);
    }

    [Fact]
    public void RegisterExplicitDerivedPair_NoDuplicates()
    {
        var resolver = new InheritanceResolver();
        var basePair = TypePair.Of<Shape, ShapeDto>();
        var derivedPair = TypePair.Of<Circle, CircleDto>();

        resolver.RegisterExplicitDerivedPair(basePair, derivedPair);
        resolver.RegisterExplicitDerivedPair(basePair, derivedPair);

        resolver.BuildDerivedPairLookup(new[] { basePair });

        var derived = resolver.GetDerivedPairs(basePair);
        derived.Should().HaveCount(1);
    }

    [Fact]
    public void ResolveBestPair_ReturnsNull_WhenRuntimeTypeMatchesBase()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        var result = resolver.ResolveBestPair(basePair, typeof(Shape));
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveBestPair_ReturnsDerivedPair_WhenRuntimeTypeIsDerived()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        var result = resolver.ResolveBestPair(basePair, typeof(Circle));
        result.Should().Be(circlePair);
    }

    [Fact]
    public void ResolveBestPair_ReturnsNull_WhenNoDerivedPairsExist()
    {
        var resolver = new InheritanceResolver();
        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.BuildDerivedPairLookup(new[] { basePair });

        var result = resolver.ResolveBestPair(basePair, typeof(Circle));
        result.Should().BeNull();
    }

    [Fact]
    public void RegisterDiscriminator_StoresConfig()
    {
        var resolver = new InheritanceResolver();
        var basePair = TypePair.Of<Shape, ShapeDto>();
        var config = new DiscriminatorConfig(
            System.Linq.Expressions.Expression.Lambda(
                System.Linq.Expressions.Expression.Constant("test")));

        resolver.RegisterDiscriminator(basePair, config);

        resolver.GetDiscriminator(basePair).Should().Be(config);
    }

    [Fact]
    public void GetDiscriminator_ReturnsNull_WhenNotConfigured()
    {
        var resolver = new InheritanceResolver();
        var basePair = TypePair.Of<Shape, ShapeDto>();

        resolver.GetDiscriminator(basePair).Should().BeNull();
    }

    [Fact]
    public void RegisterMaterializeType_StoresType()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<PersonSource, IPersonDto>();

        resolver.RegisterMaterializeType(pair, typeof(PersonDtoImpl));

        resolver.GetMaterializeType(pair).Should().Be(typeof(PersonDtoImpl));
    }

    [Fact]
    public void GetMaterializeType_ReturnsNull_WhenNotConfigured()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<PersonSource, IPersonDto>();

        resolver.GetMaterializeType(pair).Should().BeNull();
    }

    [Fact]
    public void RegisterInheritFrom_StoresRelationship()
    {
        var resolver = new InheritanceResolver();
        var derivedPair = TypePair.Of<Car, CarDto>();
        var basePair = TypePair.Of<Vehicle, VehicleDto>();

        resolver.RegisterInheritFrom(derivedPair, basePair);

        resolver.GetInheritFromPair(derivedPair).Should().Be(basePair);
    }

    [Fact]
    public void GetInheritFromPair_ReturnsNull_WhenNotConfigured()
    {
        var resolver = new InheritanceResolver();
        var pair = TypePair.Of<Car, CarDto>();

        resolver.GetInheritFromPair(pair).Should().BeNull();
    }

    [Fact]
    public void DerivedPairs_SortedBySpecificity_MostDerivedFirst()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle), typeof(Square),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto), typeof(SquareDto),
        });

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Rectangle, RectangleDto>());
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Square, SquareDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        var derived = resolver.GetDerivedPairs(basePair);
        // Square (depth 4) should come before Circle/Rectangle (depth 3)
        derived.Should().NotBeEmpty();
        if (derived.Count >= 2)
        {
            var squareIndex = derived.ToList().FindIndex(p => p.OriginType == typeof(Square));
            var circleIndex = derived.ToList().FindIndex(p => p.OriginType == typeof(Circle));
            if (squareIndex >= 0 && circleIndex >= 0)
            {
                squareIndex.Should().BeLessThan(circleIndex, "Square is more derived than Circle");
            }
        }
    }
}
