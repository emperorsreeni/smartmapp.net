using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class TypeConventionTests
{
    private readonly TypeModelCache _cache = new();
    private readonly NameSuffixTypeConvention _convention;

    public TypeConventionTests()
    {
        _convention = new NameSuffixTypeConvention(
            new StructuralSimilarityScorer(), _cache);
    }

    [Fact]
    public void TryBind_ProductToProductDto_Matches()
    {
        _convention.TryBind(typeof(Product), typeof(ProductDto), out _).Should().BeTrue();
    }

    [Fact]
    public void TryBind_ProductToProductViewModel_Matches()
    {
        _convention.TryBind(typeof(Product), typeof(ProductViewModel), out _).Should().BeTrue();
    }

    [Fact]
    public void TryBind_ProductEntityToProductDto_Matches()
    {
        _convention.TryBind(typeof(ProductEntity), typeof(ProductDto), out _).Should().BeTrue();
    }

    [Fact]
    public void TryBind_ProductToInvoiceDto_DoesNotMatch()
    {
        _convention.TryBind(typeof(Product), typeof(InvoiceDto), out _).Should().BeFalse();
    }

    [Fact]
    public void TryBind_CustomSuffixPair()
    {
        var custom = new NameSuffixTypeConvention(
            new StructuralSimilarityScorer(), _cache,
            customSuffixPairs: new[] { ("Record", "View") });

        custom.SuffixPairs.Should().HaveCountGreaterThan(9);
    }

    [Fact]
    public void TryBind_BelowThreshold_Rejected()
    {
        // Use a very high threshold so nothing passes
        var strict = new NameSuffixTypeConvention(
            new StructuralSimilarityScorer(), _cache, minScore: 2.0);

        strict.TryBind(typeof(Product), typeof(ProductDto), out _).Should().BeFalse();
    }

    [Fact]
    public void TryBind_ReturnsNullBlueprint()
    {
        _convention.TryBind(typeof(Product), typeof(ProductDto), out var blueprint).Should().BeTrue();
        blueprint.Should().BeNull(); // Sprint 2: always null, pipeline builds links later
    }

    [Fact]
    public void AddSuffixPair_AddsPair()
    {
        _convention.AddSuffixPair("Record", "View");
        _convention.SuffixPairs.Should().Contain(("Record", "View"));
    }

    [Fact]
    public void DefaultSuffixPairs_Contains9Pairs()
    {
        _convention.SuffixPairs.Should().HaveCount(9);
    }
}
