using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Attributes;
using SmartMapp.Net.Discovery;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class AttributeTests
{
    [Fact]
    public void AttributeReader_FindsMappedByOriginType()
    {
        var origins = AttributeReader.GetMappedByOriginTypes(typeof(Sprint7AttributedOrderDto));
        origins.Should().Contain(typeof(Sprint7Order));
    }

    [Fact]
    public void AssemblyScanner_DiscoversAttributedPair()
    {
        var result = new AssemblyScanner()
            .ScanContaining(typeof(Sprint7AttributedOrderDto));

        result.AttributedPairs.Should().Contain(p =>
            p.OriginType == typeof(Sprint7Order)
            && p.TargetType == typeof(Sprint7AttributedOrderDto)
            && p.Source == AttributeSource.MappedBy);
    }

    [Fact]
    public void Forge_WithAttributes_MapsUsingLinkedFromAndUnmapped()
    {
        var sculptor = new SculptorBuilder()
            .ScanAssembliesContaining<Sprint7AttributedOrderDto>()
            .Forge();

        var origin = new Sprint7Order
        {
            Id = 10,
            Customer = new Sprint7Customer { FirstName = "Ada", LastName = "Lovelace" },
            InternalCode = "secret",
        };

        var dto = sculptor.Map<Sprint7Order, Sprint7AttributedOrderDto>(origin);
        dto.Id.Should().Be(10);
        dto.FirstName.Should().Be("Ada");
        dto.InternalCode.Should().BeNull();
    }

    [Fact]
    public void AttributeConvention_Unmapped_PlusLinkedFrom_OnSameMember_Throws()
    {
        // A direct build should surface the conflict error via the convention pipeline.
        var convention = new SmartMapp.Net.Conventions.AttributeConvention(
            SmartMapp.Net.Caching.TypeModelCache.Default);

        var target = typeof(ConflictedDto).GetProperty(nameof(ConflictedDto.Value))!;
        var originModel = SmartMapp.Net.Caching.TypeModelCache.Default.GetOrAdd(typeof(Sprint7Order));

        var act = () => convention.TryLink(target, originModel, out _);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unmapped*LinkedFrom*");
    }

    public record ConflictedDto
    {
        [Unmapped]
        [LinkedFrom("Id")]
        public string Value { get; init; } = string.Empty;
    }
}
