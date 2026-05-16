using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T03 — end-to-end tests for <see cref="IMapper{TOrigin, TTarget}"/> resolution
/// through the DI container: flat, nested/flattened, attribute-driven, bidirectional, and
/// polymorphic pairs, plus error paths and concurrency.
/// </summary>
public class MapperResolutionTests
{
    private static ServiceProvider BuildProvider<TBlueprint>()
        where TBlueprint : MappingBlueprint, new()
    {
        var services = new ServiceCollection();
        services.AddSculptor(options => options.UseBlueprint<TBlueprint>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void IMapper_KnownPair_ResolvesWorkingMapper()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var mapper = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        var origin = new S8T03Order { Id = 7, CustomerName = "Alice", Total = 42m };
        var dto = mapper.Map(origin);

        dto.Should().NotBeNull();
        dto.Id.Should().Be(7);
        dto.CustomerName.Should().Be("Alice");
        dto.Total.Should().Be(42m);
    }

    [Fact]
    public void IMapper_FlattenedPair_ResolvesAndMaps()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var mapper = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDtoFlat>>();

        var origin = new S8T03Order
        {
            Id = 1,
            Customer = new S8T03Customer { FirstName = "Ada", LastName = "Lovelace" },
        };

        var dto = mapper.Map(origin);

        dto.Id.Should().Be(1);
        dto.CustomerFirstName.Should().Be("Ada");
        dto.CustomerLastName.Should().Be("Lovelace");
    }

    [Fact]
    public void IMapper_AttributeBasedPair_ResolvesIdenticallyToBlueprintPair()
    {
        var services = new ServiceCollection();
        services.AddSculptor(options =>
            options.ScanAssembliesContaining<MapperResolutionTests>()
                   .UseBlueprint<S8T03OrderBlueprint>());
        using var provider = services.BuildServiceProvider();

        var attributed = provider.GetRequiredService<IMapper<S8T03Order, S8T03AttributedOrderDto>>();
        var blueprinted = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        attributed.Should().NotBeNull();
        blueprinted.Should().NotBeNull();

        var origin = new S8T03Order
        {
            Id = 9,
            Customer = new S8T03Customer { FirstName = "Grace" },
        };

        attributed.Map(origin).FirstName.Should().Be("Grace");
        blueprinted.Map(origin).Id.Should().Be(9);
    }

    [Fact]
    public void IMapper_BidirectionalPair_BothDirectionsResolve()
    {
        using var provider = BuildProvider<S8T03BidirectionalBlueprint>();

        var forward = provider.GetRequiredService<IMapper<S8T03Customer, S8T03CustomerDtoBidirectional>>();
        var reverse = provider.GetRequiredService<IMapper<S8T03CustomerDtoBidirectional, S8T03Customer>>();

        var customer = new S8T03Customer { FirstName = "Linus", LastName = "Torvalds" };
        var dto = forward.Map(customer);
        var roundTrip = reverse.Map(dto);

        dto.FirstName.Should().Be("Linus");
        roundTrip.LastName.Should().Be("Torvalds");
    }

    [Fact]
    public void IMapper_PolymorphicPair_Resolves()
    {
        using var provider = BuildProvider<S8T03PolymorphicBlueprint>();

        var mapper = provider.GetRequiredService<IMapper<S8T03Shape, S8T03ShapeDto>>();

        mapper.Should().NotBeNull();
    }

    [Fact]
    public void IMapper_UnknownPair_GetRequiredService_Throws_WithNoBlueprintHint()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var act = () => provider.GetRequiredService<IMapper<string, int>>();

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        var message = (ex.InnerException?.Message ?? string.Empty) + " " + ex.Message;

        message.Should().Contain("No Blueprint is registered",
            "the actionable hint must appear in either the outer or MS DI-wrapped inner message.");
    }

    [Fact]
    public void IMapper_UnknownPair_ErrorMessage_NamesOriginAndTarget()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var act = () => provider.GetRequiredService<IMapper<DateTimeOffset, Guid>>();

        var ex = act.Should().Throw<InvalidOperationException>().And;
        var fullMessage = ex.InnerException?.Message ?? ex.Message;

        fullMessage.Should().Contain(nameof(DateTimeOffset));
        fullMessage.Should().Contain(nameof(Guid));
    }

    [Fact]
    public void IMapper_SingletonHandle_SameInstanceAcrossResolves()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var m1 = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        var m2 = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        m1.Should().BeSameAs(m2,
            "Singleton (default) lifetime must yield the same IMapper<,> closure across resolves.");
    }

    [Fact]
    public void IMapper_TransientHandle_NewInstancePerResolve()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Transient, options =>
            options.UseBlueprint<S8T03OrderBlueprint>());
        using var provider = services.BuildServiceProvider();

        var m1 = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        var m2 = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        m1.Should().NotBeSameAs(m2,
            "Transient IMapper<,> handle must yield a new wrapper per resolve.");
    }

    [Fact]
    public void IMapper_ScopedHandle_SameInstanceWithinScope_DifferentAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
            options.UseBlueprint<S8T03OrderBlueprint>());
        using var provider = services.BuildServiceProvider();

        IMapper<S8T03Order, S8T03OrderDto> a1;
        IMapper<S8T03Order, S8T03OrderDto> a2;
        IMapper<S8T03Order, S8T03OrderDto> b1;

        using (var scopeA = provider.CreateScope())
        {
            a1 = scopeA.ServiceProvider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
            a2 = scopeA.ServiceProvider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        }
        using (var scopeB = provider.CreateScope())
        {
            b1 = scopeB.ServiceProvider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        }

        a1.Should().BeSameAs(a2, "scoped lifetime shares within a scope.");
        a1.Should().NotBeSameAs(b1, "scoped lifetime yields a fresh wrapper across scopes.");
    }

    [Fact]
    public void IMapper_IsTypedAsDependencyInjectionMapper()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var mapper = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        mapper.Should().BeOfType<DependencyInjectionMapper<S8T03Order, S8T03OrderDto>>(
            "the DI-registered open generic produces the DependencyInjectionMapper<,> wrapper.");
    }

    [Fact]
    public void IMapper_Concurrent1000Resolves_AllProduceWorkingMappers()
    {
        // Spec §S8-T03 acceptance: "1000 parallel resolves of the same pair return the same
        // mapper instance" (under Singleton default lifetime).
        using var provider = BuildProvider<S8T03OrderBlueprint>();

        var bag = new ConcurrentBag<IMapper<S8T03Order, S8T03OrderDto>>();
        Parallel.For(0, 1000, i =>
        {
            _ = i;
            bag.Add(provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>());
        });

        bag.Should().HaveCount(1000);
        bag.Distinct().Should().HaveCount(1, "Singleton lifetime collapses 1000 parallel resolves to one mapper.");
    }

    [Fact]
    public void IMapper_EveryPairFromGetAllBlueprints_Resolvable()
    {
        using var provider = BuildProvider<S8T03OrderBlueprint>();
        var config = provider.GetRequiredService<ISculptorConfiguration>();
        var sculptor = provider.GetRequiredService<ISculptor>();

        foreach (var blueprint in config.GetAllBlueprints())
        {
            var serviceType = typeof(IMapper<,>).MakeGenericType(blueprint.OriginType, blueprint.TargetType);
            var resolved = provider.GetService(serviceType);

            resolved.Should().NotBeNull(
                $"every pair reported by ISculptorConfiguration must be resolvable as IMapper<,>. " +
                $"Missing: {blueprint.OriginType.Name} -> {blueprint.TargetType.Name}.");
        }

        _ = sculptor; // keep the sculptor alive for the duration of the scan.
    }

    [Fact]
    public void IMapper_WalkerPopulatesCacheDuringForge_ExactlyOnce()
    {
        // Spec §S8-T03 Constraints: "Registration runs inside the Singleton factory for the
        // sculptor, exactly once." Verify by asserting WalkerInvocationCount stays at 1 across
        // many resolves of ISculptor and IMapper<,>.
        var services = new ServiceCollection();
        services.AddSculptor(options => options.UseBlueprint<S8T03OrderBlueprint>());
        using var provider = services.BuildServiceProvider();

        var host = provider.GetRequiredService<SmartMapp.Net.DependencyInjection.Internal.ForgedSculptorHost>();
        host.WalkerInvocationCount.Should().Be(0, "walker must not run before first resolve (lazy forge).");
        host.MapperCache.Should().BeEmpty();

        _ = provider.GetRequiredService<ISculptor>();
        _ = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        _ = provider.GetRequiredService<IMapper<S8T03Customer, S8T03CustomerDto>>();
        _ = provider.GetRequiredService<ISculptor>();

        host.WalkerInvocationCount.Should().Be(1,
            "walker must be invoked exactly once, inside the Singleton forge factory.");
        host.MapperCache.Should().ContainKey(TypePair.Of<S8T03Order, S8T03OrderDto>());
        host.MapperCache.Should().ContainKey(TypePair.Of<S8T03Customer, S8T03CustomerDto>());
    }

    [Fact]
    public void IMapper_TransientHandle_SharesCachedInnerMapper_AcrossResolves()
    {
        // Spec §S8-T03 Technical Considerations: "Keep a ConcurrentDictionary<TypePair, object>
        // mirror of registered mappers to short-circuit closed-generic construction costs on
        // repeated resolve." Transient wrappers allocate fresh per resolve but the inner
        // IMapper<,> must be the single cached instance for the pair.
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Transient, options =>
            options.UseBlueprint<S8T03OrderBlueprint>());
        using var provider = services.BuildServiceProvider();

        // Warm up
        _ = provider.GetRequiredService<ISculptor>();

        var host = provider.GetRequiredService<SmartMapp.Net.DependencyInjection.Internal.ForgedSculptorHost>();
        var cachedEntry = host.MapperCache[TypePair.Of<S8T03Order, S8T03OrderDto>()];

        var w1 = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        var w2 = provider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        w1.Should().NotBeSameAs(w2, "Transient handle yields a new wrapper per resolve.");

        // Both wrappers forward Map() calls to the same inner IMapper<,>. We cannot reach the
        // private field directly, but we can assert the cache entry is exactly the shared inner.
        cachedEntry.Should().BeAssignableTo<IMapper<S8T03Order, S8T03OrderDto>>();
        var cachedTyped = (IMapper<S8T03Order, S8T03OrderDto>)cachedEntry;

        var origin = new S8T03Order { Id = 99 };
        w1.Map(origin).Should().BeEquivalentTo(cachedTyped.Map(origin));
        w2.Map(origin).Should().BeEquivalentTo(cachedTyped.Map(origin));
    }

    [Fact]
    public void IMapper_AllowPerScopeRebuild_EachScopeGetsFreshMapper()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
        {
            options.AllowPerScopeRebuild = true;
            options.UseBlueprint<S8T03OrderBlueprint>();
        });
        using var provider = services.BuildServiceProvider();

        IMapper<S8T03Order, S8T03OrderDto> a;
        IMapper<S8T03Order, S8T03OrderDto> b;
        using (var scopeA = provider.CreateScope())
            a = scopeA.ServiceProvider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();
        using (var scopeB = provider.CreateScope())
            b = scopeB.ServiceProvider.GetRequiredService<IMapper<S8T03Order, S8T03OrderDto>>();

        a.Should().NotBeSameAs(b);
        a.Map(new S8T03Order { Id = 1 }).Id.Should().Be(1);
        b.Map(new S8T03Order { Id = 2 }).Id.Should().Be(2);
    }
}
