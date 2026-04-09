using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

/// <summary>
/// Tests for automatic polymorphic mapping dispatch (S6-T01).
/// Validates that PolymorphicDispatchBuilder correctly dispatches to derived mapping delegates
/// based on the runtime origin type.
/// </summary>
public class PolymorphicMappingTests
{
    private PolymorphicDispatchBuilder CreateBuilder(
        out InheritanceResolver resolver,
        out MappingDelegateCache cache,
        params Type[] knownTypes)
    {
        resolver = new InheritanceResolver(knownTypes);
        cache = new MappingDelegateCache();
        return new PolymorphicDispatchBuilder(resolver, cache);
    }

    [Fact]
    public void TwoLevelHierarchy_CircleDispatchesToCircleDto()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Rectangle, RectangleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair =>
        {
            if (pair.OriginType == typeof(Circle))
                return (o, s) => new CircleDto { Id = ((Circle)o).Id, Radius = ((Circle)o).Radius };
            if (pair.OriginType == typeof(Rectangle))
                return (o, s) => new RectangleDto { Id = ((Rectangle)o).Id, Width = ((Rectangle)o).Width };
            return baseDel;
        })!;

        var result = dispatch(new Circle { Id = 1, Radius = 5.0 }, new MappingScope());
        result.Should().BeOfType<CircleDto>();
        ((CircleDto)result).Radius.Should().Be(5.0);
    }

    [Fact]
    public void TwoLevelHierarchy_RectangleDispatchesToRectangleDto()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Rectangle, RectangleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair =>
        {
            if (pair.OriginType == typeof(Circle))
                return (o, s) => new CircleDto { Id = ((Circle)o).Id };
            if (pair.OriginType == typeof(Rectangle))
                return (o, s) => new RectangleDto { Id = ((Rectangle)o).Id, Width = ((Rectangle)o).Width };
            return baseDel;
        })!;

        var result = dispatch(new Rectangle { Id = 2, Width = 10 }, new MappingScope());
        result.Should().BeOfType<RectangleDto>();
        ((RectangleDto)result).Width.Should().Be(10);
    }

    [Fact]
    public void ThreeLevelHierarchy_FilledCircle_DispatchesToMostSpecific()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(Circle), typeof(FilledCircle),
            typeof(ShapeDto), typeof(CircleDto), typeof(FilledCircleDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        var filledPair = TypePair.Of<FilledCircle, FilledCircleDto>();
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.RegisterExplicitDerivedPair(basePair, filledPair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair =>
        {
            if (pair.OriginType == typeof(FilledCircle))
                return (o, s) => new FilledCircleDto
                {
                    Id = ((FilledCircle)o).Id,
                    Radius = ((FilledCircle)o).Radius,
                    FillColor = ((FilledCircle)o).FillColor,
                };
            if (pair.OriginType == typeof(Circle))
                return (o, s) => new CircleDto { Id = ((Circle)o).Id, Radius = ((Circle)o).Radius };
            return baseDel;
        })!;

        var fc = new FilledCircle { Id = 1, Radius = 3, FillColor = "red" };
        var result = dispatch(fc, new MappingScope());
        result.Should().BeOfType<FilledCircleDto>();
        ((FilledCircleDto)result).FillColor.Should().Be("red");
    }

    [Fact]
    public void BaseTypeInstance_UsesBaseMapping()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(Circle), typeof(ShapeDto), typeof(CircleDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair =>
            (o, s) => new CircleDto { Id = ((Circle)o).Id })!;

        // Exact base type should use base delegate
        var result = dispatch(new Shape { Id = 99 }, new MappingScope());
        result.Should().BeOfType<ShapeDto>();
        ((ShapeDto)result).Id.Should().Be(99);
    }

    [Fact]
    public void UnknownDerivedType_FallsBackToBase()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(Circle), typeof(ShapeDto), typeof(CircleDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        // Only register Circle, not Square
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair =>
            (o, s) => new CircleDto { Id = ((Circle)o).Id })!;

        // Square is not registered, should fall back to base
        var result = dispatch(new Square { Id = 5, Side = 10 }, new MappingScope());
        result.Should().BeOfType<ShapeDto>();
    }

    [Fact]
    public void NullOrigin_ReturnsNull()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(Circle), typeof(ShapeDto), typeof(CircleDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto();

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair => baseDel)!;

        var result = dispatch(null!, new MappingScope());
        result.Should().BeNull();
    }

    [Fact]
    public void NoDerivedPairs_ReturnsNull_NoDispatchNeeded()
    {
        var builder = CreateBuilder(out var resolver, out var cache,
            typeof(Shape), typeof(ShapeDto));

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto();
        var dispatch = builder.BuildDispatchDelegate(basePair, baseDel, pair => baseDel);

        // Should return null when no derived pairs exist (no dispatch needed)
        dispatch.Should().BeNull();
    }
}
