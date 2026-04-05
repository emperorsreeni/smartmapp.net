using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class PolymorphicCollectionMappingTests
{
    [Fact]
    public void RequiresPolymorphicDispatch_NoDerivedPairs_ReturnsFalse()
    {
        var resolver = new InheritanceResolver();
        var cache = new MappingDelegateCache();
        var mapper = new PolymorphicCollectionMapper(resolver, cache);

        var pair = TypePair.Of<Shape, ShapeDto>();
        resolver.BuildDerivedPairLookup(new[] { pair });

        mapper.RequiresPolymorphicDispatch(pair).Should().BeFalse();
    }

    [Fact]
    public void RequiresPolymorphicDispatch_WithDerivedPairs_ReturnsTrue()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });
        var cache = new MappingDelegateCache();
        var mapper = new PolymorphicCollectionMapper(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        mapper.RequiresPolymorphicDispatch(basePair).Should().BeTrue();
    }

    [Fact]
    public void BuildPolymorphicElementMapper_NoDerived_ReturnsBaseDelegate()
    {
        var resolver = new InheritanceResolver();
        var cache = new MappingDelegateCache();
        var mapper = new PolymorphicCollectionMapper(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto();
        var result = mapper.BuildPolymorphicElementMapper(basePair, baseDelegate, _ => baseDelegate);

        result.Should().BeSameAs(baseDelegate);
    }

    [Fact]
    public void BuildPolymorphicElementMapper_WithDerived_DispatchesCorrectly()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto),
        });
        var cache = new MappingDelegateCache();
        var mapper = new PolymorphicCollectionMapper(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        var rectPair = TypePair.Of<Rectangle, RectangleDto>();
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.RegisterExplicitDerivedPair(basePair, rectPair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        Func<object, MappingScope, object> circleDelegate = (o, s) => new CircleDto { Id = ((Circle)o).Id, Radius = ((Circle)o).Radius };
        Func<object, MappingScope, object> rectDelegate = (o, s) => new RectangleDto { Id = ((Rectangle)o).Id, Width = ((Rectangle)o).Width };

        var elementMapper = mapper.BuildPolymorphicElementMapper(basePair, baseDelegate, pair =>
        {
            if (pair == circlePair) return circleDelegate;
            if (pair == rectPair) return rectDelegate;
            return baseDelegate;
        });

        // Test Circle dispatch
        var circleResult = elementMapper(new Circle { Id = 1, Radius = 5.0 }, new MappingScope());
        circleResult.Should().BeOfType<CircleDto>();
        ((CircleDto)circleResult).Radius.Should().Be(5.0);

        // Test Rectangle dispatch
        var rectResult = elementMapper(new Rectangle { Id = 2, Width = 10 }, new MappingScope());
        rectResult.Should().BeOfType<RectangleDto>();
        ((RectangleDto)rectResult).Width.Should().Be(10);

        // Test base fallback
        var baseResult = elementMapper(new Shape { Id = 3 }, new MappingScope());
        baseResult.Should().BeOfType<ShapeDto>();
        ((ShapeDto)baseResult).Id.Should().Be(3);
    }

    [Fact]
    public void BuildPolymorphicElementMapper_NullElement_ReturnsNull()
    {
        var resolver = new InheritanceResolver(new[] { typeof(Shape), typeof(Circle), typeof(ShapeDto), typeof(CircleDto) });
        var cache = new MappingDelegateCache();
        var mapper = new PolymorphicCollectionMapper(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto();
        var elementMapper = mapper.BuildPolymorphicElementMapper(basePair, baseDelegate, _ => baseDelegate);

        var result = elementMapper(null!, new MappingScope());
        result.Should().BeNull();
    }

    [Fact]
    public void MapPolymorphicCollection_MixedTypes_DispatchesPerElement()
    {
        Func<object, MappingScope, object> elementMapper = (o, scope) =>
        {
            return o switch
            {
                Circle c => new CircleDto { Id = c.Id, Radius = c.Radius },
                Rectangle r => new RectangleDto { Id = r.Id, Width = r.Width },
                Shape s => new ShapeDto { Id = s.Id },
                _ => throw new InvalidOperationException(),
            };
        };

        var elements = new Shape[]
        {
            new Circle { Id = 1, Radius = 5 },
            new Rectangle { Id = 2, Width = 10 },
            new Shape { Id = 3 },
        };

        var result = PolymorphicCollectionMapper.MapPolymorphicCollection<ShapeDto>(
            elements, elementMapper, new MappingScope());

        result.Should().HaveCount(3);
        result[0].Should().BeOfType<CircleDto>();
        result[1].Should().BeOfType<RectangleDto>();
        result[2].Should().BeOfType<ShapeDto>();
    }

    [Fact]
    public void MapPolymorphicCollection_NullElements_PreservedAsDefault()
    {
        Func<object, MappingScope, object> elementMapper = (o, scope) => new ShapeDto();

        var elements = new Shape?[] { new Shape { Id = 1 }, null, new Shape { Id = 3 } };
        var result = PolymorphicCollectionMapper.MapPolymorphicCollection<ShapeDto>(
            elements, elementMapper, new MappingScope());

        result.Should().HaveCount(3);
        result[0].Should().NotBeNull();
        result[1].Should().BeNull();
        result[2].Should().NotBeNull();
    }
}
