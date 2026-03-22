using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

public class TargetConstructionResolverTests
{
    private readonly TypeModelCache _cache = new();
    private readonly TargetConstructionResolver _resolver;

    public TargetConstructionResolverTests()
    {
        _resolver = new TargetConstructionResolver(_cache);
    }

    [Fact]
    public void ResolveStrategy_WithFactory_ReturnsFactory()
    {
        var model = _cache.GetOrAdd<FlatOrderDto>();
        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            TargetFactory = _ => new FlatOrderDto(),
        };

        var strategy = _resolver.ResolveStrategy(model, blueprint);

        strategy.Should().Be(ConstructionStrategy.Factory);
    }

    [Fact]
    public void ResolveStrategy_WithParameterlessCtor_ReturnsParameterless()
    {
        var model = _cache.GetOrAdd<FlatOrderDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, FlatOrderDto>());

        var strategy = _resolver.ResolveStrategy(model, blueprint);

        strategy.Should().Be(ConstructionStrategy.Parameterless);
    }

    [Fact]
    public void ResolveStrategy_WithPrimaryConstructor_ReturnsPrimaryConstructor()
    {
        var model = _cache.GetOrAdd<RecordOrderDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, RecordOrderDto>());

        var strategy = _resolver.ResolveStrategy(model, blueprint);

        strategy.Should().Be(ConstructionStrategy.PrimaryConstructor);
    }

    [Fact]
    public void ResolveStrategy_WithOnlyParameterizedCtors_ReturnsBestMatch()
    {
        var model = _cache.GetOrAdd<CtorOrderDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, CtorOrderDto>());

        var strategy = _resolver.ResolveStrategy(model, blueprint);

        // CtorOrderDto has a single ctor with params and no parameterless ctor
        strategy.Should().Be(ConstructionStrategy.PrimaryConstructor);
    }

    [Fact]
    public void ResolveStrategy_MultipleCtorsWithParameterless_ReturnsParameterless()
    {
        var model = _cache.GetOrAdd<MultiCtorClass>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, MultiCtorClass>());

        var strategy = _resolver.ResolveStrategy(model, blueprint);

        strategy.Should().Be(ConstructionStrategy.Parameterless);
    }

    [Fact]
    public void ConstructorScoring_MatchesByNameCaseInsensitive()
    {
        var originModel = _cache.GetOrAdd<FlatOrder>();
        var targetModel = _cache.GetOrAdd<CtorOrderDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, CtorOrderDto>());

        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var originParam = System.Linq.Expressions.Expression.Variable(typeof(FlatOrder), "origin");

        var (expr, consumed) = _resolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, originParam, scopeParam);

        // CtorOrderDto(int id, string name) should match FlatOrder.Id and FlatOrder.Name
        consumed.Should().Contain("id");
        consumed.Should().Contain("name");
    }

    [Fact]
    public void ConstructorScoring_HighestMatchWins()
    {
        // MultipleCtorDto has only parameterized ctors (1, 2, 3 params) — no parameterless
        var targetModel = _cache.GetOrAdd<MultipleCtorDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, MultipleCtorDto>());

        var strategy = _resolver.ResolveStrategy(targetModel, blueprint);

        // No parameterless ctor, so best-match is selected
        strategy.Should().Be(ConstructionStrategy.BestMatchConstructor);
    }

    [Fact]
    public void BuildConstruction_NoViableCtor_UsesDefaultsForUnmatchedParams()
    {
        // NoMatchCtorDto has a single ctor (int zzz, string yyy) — params don't match
        // origin props. As the only ctor, it's marked as primary and built with defaults.
        var originModel = _cache.GetOrAdd<FlatOrder>();
        var targetModel = _cache.GetOrAdd<NoMatchCtorDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, NoMatchCtorDto>());

        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var originParam = System.Linq.Expressions.Expression.Variable(typeof(FlatOrder), "origin");

        var (expr, consumed) = _resolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, originParam, scopeParam);

        expr.Should().NotBeNull();
        consumed.Should().BeEmpty(); // no params matched
    }

    [Fact]
    public void BestMatch_OverloadedCtors_SelectsHighestMatchCount()
    {
        // BestMatchOverloadDto has 3 ctors: (int), (int, string), (int, string, decimal)
        // FlatOrder has Id, Name, Total — all 3 match the 3-param ctor
        var originModel = _cache.GetOrAdd<FlatOrder>();
        var targetModel = _cache.GetOrAdd<BestMatchOverloadDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, BestMatchOverloadDto>());

        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var originParam = System.Linq.Expressions.Expression.Variable(typeof(FlatOrder), "origin");

        var (expr, consumed) = _resolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, originParam, scopeParam);

        // Should pick the 3-param ctor (highest match count)
        consumed.Should().HaveCount(3);
        consumed.Should().Contain("id");
        consumed.Should().Contain("name");
        consumed.Should().Contain("total");
    }

    [Fact]
    public void ResolveStrategy_RecordStruct_ReturnsPrimaryConstructor()
    {
        var model = _cache.GetOrAdd<RecordStructDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, RecordStructDto>());

        var strategy = _resolver.ResolveStrategy(model, blueprint);

        strategy.Should().Be(ConstructionStrategy.PrimaryConstructor);
    }

    [Fact]
    public void IsTypeCompatible_SameType_ReturnsTrue()
    {
        TargetConstructionResolver.IsTypeCompatible(typeof(int), typeof(int)).Should().BeTrue();
    }

    [Fact]
    public void IsTypeCompatible_Assignable_ReturnsTrue()
    {
        TargetConstructionResolver.IsTypeCompatible(typeof(string), typeof(object)).Should().BeTrue();
    }

    [Fact]
    public void IsTypeCompatible_NumericWidening_ReturnsTrue()
    {
        TargetConstructionResolver.IsTypeCompatible(typeof(int), typeof(long)).Should().BeTrue();
    }

    [Fact]
    public void IsTypeCompatible_NullableWrap_ReturnsTrue()
    {
        TargetConstructionResolver.IsTypeCompatible(typeof(int), typeof(int?)).Should().BeTrue();
    }

    [Fact]
    public void IsTypeCompatible_Incompatible_ReturnsFalse()
    {
        TargetConstructionResolver.IsTypeCompatible(typeof(DateTime), typeof(List<int>)).Should().BeFalse();
    }

    [Fact]
    public void BestMatch_ZeroMatches_ThrowsMappingCompilationException()
    {
        // ZeroMatchMultiCtorDto has 2 ctors: (int xxx), (int xxx, string yyy)
        // FlatOrder has Id, Name, Total... none match xxx/yyy
        var originModel = _cache.GetOrAdd<FlatOrder>();
        var targetModel = _cache.GetOrAdd<ZeroMatchMultiCtorDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, ZeroMatchMultiCtorDto>());

        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var originParam = System.Linq.Expressions.Expression.Variable(typeof(FlatOrder), "origin");

        var act = () => _resolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, originParam, scopeParam);

        act.Should().Throw<MappingCompilationException>();
    }

    [Fact]
    public void BestMatch_RemainingPropsAfterCtor_ConsumedSetCorrectly()
    {
        // BestMatchWithRemainingDto has ctor(int id, string name) + settable Total, Category
        var originModel = _cache.GetOrAdd<FlatOrder>();
        var targetModel = _cache.GetOrAdd<BestMatchWithRemainingDto>();
        var blueprint = Blueprint.Empty(TypePair.Of<FlatOrder, BestMatchWithRemainingDto>());

        var scopeParam = System.Linq.Expressions.Expression.Parameter(typeof(MappingScope), "scope");
        var originParam = System.Linq.Expressions.Expression.Variable(typeof(FlatOrder), "origin");

        var (expr, consumed) = _resolver.BuildConstructionExpression(
            targetModel, originModel, blueprint, originParam, scopeParam);

        consumed.Should().Contain("id");
        consumed.Should().Contain("name");
        consumed.Should().NotContain("Total"); // settable prop, not consumed by ctor
        consumed.Should().NotContain("Category");
    }
}
