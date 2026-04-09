using FluentAssertions;
using NSubstitute;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class BlueprintInheritanceResolverTests
{
    [Fact]
    public void ResolveAll_NoInheritance_ReturnsSameBlueprints()
    {
        var resolver = new InheritanceResolver();
        var bp = Blueprint.Empty(TypePair.Of<SimpleClass, SimpleDto>());

        var inheritResolver = new BlueprintInheritanceResolver(resolver, new[] { bp });
        var result = inheritResolver.ResolveAll();

        result.Should().HaveCount(1);
        result[0].Should().Be(bp);
    }

    [Fact]
    public void MergeLinks_DerivedOverridesBase()
    {
        var baseLink = new PropertyLink
        {
            TargetMember = typeof(CarDto).GetProperty("Make")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Make"),
        };

        var derivedLink = new PropertyLink
        {
            TargetMember = typeof(CarDto).GetProperty("Make")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.Explicit("CustomMake"),
        };

        var baseBp = new Blueprint
        {
            OriginType = typeof(Vehicle),
            TargetType = typeof(VehicleDto),
            Links = new[] { baseLink },
        };

        var derivedBp = new Blueprint
        {
            OriginType = typeof(Car),
            TargetType = typeof(CarDto),
            Links = new[] { derivedLink },
        };

        var merged = BlueprintInheritanceResolver.MergeLinks(baseBp, derivedBp);

        merged.Links.Should().HaveCount(1);
        merged.Links[0].LinkedBy.ConventionName.Should().Be("ExplicitBinding");
    }

    [Fact]
    public void MergeLinks_InheritedLinksIncluded()
    {
        var baseLink = new PropertyLink
        {
            TargetMember = typeof(VehicleDto).GetProperty("Make")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Make"),
        };

        var derivedLink = new PropertyLink
        {
            TargetMember = typeof(CarDto).GetProperty("Doors")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Doors"),
        };

        var baseBp = new Blueprint
        {
            OriginType = typeof(Vehicle),
            TargetType = typeof(VehicleDto),
            Links = new[] { baseLink },
        };

        var derivedBp = new Blueprint
        {
            OriginType = typeof(Car),
            TargetType = typeof(CarDto),
            Links = new[] { derivedLink },
        };

        var merged = BlueprintInheritanceResolver.MergeLinks(baseBp, derivedBp);

        merged.Links.Should().HaveCount(2);
        merged.Links.Should().Contain(l => l.TargetMember.Name == "Make");
        merged.Links.Should().Contain(l => l.TargetMember.Name == "Doors");
    }

    [Fact]
    public void MergeLinks_SortsByOrder()
    {
        var baseLink = new PropertyLink
        {
            TargetMember = typeof(VehicleDto).GetProperty("Make")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Make"),
            Order = 2,
        };

        var derivedLink = new PropertyLink
        {
            TargetMember = typeof(CarDto).GetProperty("Doors")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Doors"),
            Order = 1,
        };

        var baseBp = new Blueprint
        {
            OriginType = typeof(Vehicle),
            TargetType = typeof(VehicleDto),
            Links = new[] { baseLink },
        };

        var derivedBp = new Blueprint
        {
            OriginType = typeof(Car),
            TargetType = typeof(CarDto),
            Links = new[] { derivedLink },
        };

        var merged = BlueprintInheritanceResolver.MergeLinks(baseBp, derivedBp);

        merged.Links[0].TargetMember.Name.Should().Be("Doors");
        merged.Links[1].TargetMember.Name.Should().Be("Make");
    }

    [Fact]
    public void ResolveAll_WithInheritance_MergesLinks()
    {
        var resolver = new InheritanceResolver();
        var basePair = TypePair.Of<Vehicle, VehicleDto>();
        var derivedPair = TypePair.Of<Car, CarDto>();
        resolver.RegisterInheritFrom(derivedPair, basePair);

        var baseLink = new PropertyLink
        {
            TargetMember = typeof(VehicleDto).GetProperty("Make")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Make"),
        };

        var derivedLink = new PropertyLink
        {
            TargetMember = typeof(CarDto).GetProperty("Doors")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Doors"),
        };

        var baseBp = new Blueprint
        {
            OriginType = typeof(Vehicle),
            TargetType = typeof(VehicleDto),
            Links = new[] { baseLink },
        };

        var derivedBp = new Blueprint
        {
            OriginType = typeof(Car),
            TargetType = typeof(CarDto),
            Links = new[] { derivedLink },
        };

        var inheritResolver = new BlueprintInheritanceResolver(resolver, new[] { baseBp, derivedBp });
        var result = inheritResolver.ResolveAll();

        var resolvedDerived = result.First(b => b.TypePair == derivedPair);
        resolvedDerived.Links.Should().HaveCount(2);
    }

    [Fact]
    public void ResolveAll_CircularInheritance_Throws()
    {
        var resolver = new InheritanceResolver();
        var pairA = TypePair.Of<SimpleClass, SimpleDto>();
        var pairB = TypePair.Of<Order, OrderDto>();

        resolver.RegisterInheritFrom(pairA, pairB);
        resolver.RegisterInheritFrom(pairB, pairA);

        var bpA = Blueprint.Empty(pairA);
        var bpB = Blueprint.Empty(pairB);

        var inheritResolver = new BlueprintInheritanceResolver(resolver, new[] { bpA, bpB });

        var act = () => inheritResolver.ResolveAll();
        act.Should().Throw<BlueprintValidationException>()
            .WithMessage("*Circular*");
    }
}
