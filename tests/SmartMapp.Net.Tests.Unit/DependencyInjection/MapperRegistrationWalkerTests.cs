using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T03 — unit tests for <see cref="MapperRegistrationWalker"/>, the diagnostic
/// helper that enumerates which pairs will resolve as <see cref="IMapper{TOrigin, TTarget}"/>
/// for a forged sculptor.
/// </summary>
public class MapperRegistrationWalkerTests
{
    private static ISculptor BuildSculptor()
        => new SculptorBuilder()
            .UseBlueprint<S8T03OrderBlueprint>()
            .Forge();

    [Fact]
    public void EnumerateResolvablePairs_NullSculptor_Throws()
    {
        var act = () => MapperRegistrationWalker.EnumerateResolvablePairs(null!).ToList();

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sculptor");
    }

    [Fact]
    public void EnumerateResolvablePairs_YieldsEveryRegisteredPair()
    {
        var sculptor = BuildSculptor();

        var pairs = MapperRegistrationWalker
            .EnumerateResolvablePairs(sculptor)
            .Select(p => p.Pair)
            .ToList();

        pairs.Should().Contain(TypePair.Of<S8T03Order, S8T03OrderDto>());
        pairs.Should().Contain(TypePair.Of<S8T03Order, S8T03OrderDtoFlat>());
        pairs.Should().Contain(TypePair.Of<S8T03Customer, S8T03CustomerDto>());
    }

    [Fact]
    public void EnumerateResolvablePairs_ProducesClosedGenericIMapperType()
    {
        var sculptor = BuildSculptor();

        var tuple = MapperRegistrationWalker
            .EnumerateResolvablePairs(sculptor)
            .First(p => p.Pair == TypePair.Of<S8T03Order, S8T03OrderDto>());

        tuple.MapperServiceType.Should().Be(typeof(IMapper<S8T03Order, S8T03OrderDto>));
    }

    [Fact]
    public void GetOrMakeClosedMapperType_CachesResult()
    {
        var pair = TypePair.Of<S8T03Order, S8T03OrderDto>();

        var t1 = MapperRegistrationWalker.GetOrMakeClosedMapperType(pair);
        var t2 = MapperRegistrationWalker.GetOrMakeClosedMapperType(pair);

        t1.Should().BeSameAs(t2, "closed-generic Type instances must be cached to amortise MakeGenericType cost.");
    }

    [Fact]
    public void TryResolve_KnownPair_ReturnsTrueAndMapper()
    {
        var sculptor = BuildSculptor();

        var ok = MapperRegistrationWalker.TryResolve(
            sculptor, TypePair.Of<S8T03Order, S8T03OrderDto>(), out var mapper);

        ok.Should().BeTrue();
        mapper.Should().BeAssignableTo<IMapper<S8T03Order, S8T03OrderDto>>();
    }

    [Fact]
    public void TryResolve_UnknownPair_ReturnsFalseAndNull()
    {
        var sculptor = BuildSculptor();

        var ok = MapperRegistrationWalker.TryResolve(
            sculptor, new TypePair(typeof(string), typeof(int)), out var mapper);

        ok.Should().BeFalse();
        mapper.Should().BeNull();
    }

    [Fact]
    public void PopulateCache_NullSculptor_Throws()
    {
        var cache = new ConcurrentDictionary<TypePair, object>();

        var act = () => MapperRegistrationWalker.PopulateCache(null!, cache);

        act.Should().Throw<ArgumentNullException>().WithParameterName("sculptor");
    }

    [Fact]
    public void PopulateCache_NullCache_Throws()
    {
        var sculptor = BuildSculptor();

        var act = () => MapperRegistrationWalker.PopulateCache(sculptor, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void PopulateCache_AddsOneIMapperEntryPerBlueprint()
    {
        var sculptor = BuildSculptor();
        var cache = new ConcurrentDictionary<TypePair, object>();

        MapperRegistrationWalker.PopulateCache(sculptor, cache);

        cache.Should().ContainKey(TypePair.Of<S8T03Order, S8T03OrderDto>());
        cache.Should().ContainKey(TypePair.Of<S8T03Order, S8T03OrderDtoFlat>());
        cache.Should().ContainKey(TypePair.Of<S8T03Customer, S8T03CustomerDto>());

        cache[TypePair.Of<S8T03Order, S8T03OrderDto>()]
            .Should().BeAssignableTo<IMapper<S8T03Order, S8T03OrderDto>>();
    }

    [Fact]
    public void PopulateCache_IsIdempotent_OnRepeatInvocation()
    {
        var sculptor = BuildSculptor();
        var cache = new ConcurrentDictionary<TypePair, object>();

        MapperRegistrationWalker.PopulateCache(sculptor, cache);
        var first = cache[TypePair.Of<S8T03Order, S8T03OrderDto>()];

        MapperRegistrationWalker.PopulateCache(sculptor, cache);
        var second = cache[TypePair.Of<S8T03Order, S8T03OrderDto>()];

        second.Should().BeSameAs(first,
            "PopulateCache uses GetOrAdd so repeated invocations are no-ops for known pairs.");
    }
}
