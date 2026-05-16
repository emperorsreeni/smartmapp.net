using FluentAssertions;
using SmartMapp.Net;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class SculptorRuntimeTests
{
    private static ISculptor ForgeOrderSculptor()
    {
        var b = new SculptorBuilder();
        b.Bind<Sprint7Order, Sprint7OrderDto>();
        return b.Forge();
    }

    [Fact]
    public void Map_Generic_MapsAllProperties()
    {
        var sculptor = ForgeOrderSculptor();
        var dto = sculptor.Map<Sprint7Order, Sprint7OrderDto>(new Sprint7Order
        {
            Id = 11,
            CustomerName = "Ada",
            Total = 99.99m,
        });

        dto.Id.Should().Be(11);
        dto.CustomerName.Should().Be("Ada");
        dto.Total.Should().Be(99.99m);
    }

    [Fact]
    public void Map_Null_Origin_ReturnsDefault()
    {
        var sculptor = ForgeOrderSculptor();
        var dto = sculptor.Map<Sprint7Order, Sprint7OrderDto>(null!);
        dto.Should().BeNull();
    }

    [Fact]
    public void Map_RuntimeTyped_Works()
    {
        var sculptor = ForgeOrderSculptor();
        var origin = new Sprint7Order { Id = 2 };
        var result = sculptor.Map(origin, typeof(Sprint7Order), typeof(Sprint7OrderDto));

        result.Should().BeOfType<Sprint7OrderDto>();
        ((Sprint7OrderDto)result).Id.Should().Be(2);
    }

    [Fact]
    public void Map_UnknownPair_Throws_MappingConfigurationException()
    {
        var sculptor = ForgeOrderSculptor();
        var act = () => sculptor.Map<Sprint7Customer, Sprint7OrderDto>(new Sprint7Customer());
        act.Should().Throw<MappingConfigurationException>();
    }

    [Fact]
    public void MapAll_ProducesList_InOrder()
    {
        var sculptor = ForgeOrderSculptor();
        var origins = Enumerable.Range(0, 5).Select(i => new Sprint7Order { Id = i }).ToList();

        var results = sculptor.MapAll<Sprint7Order, Sprint7OrderDto>(origins);
        results.Select(r => r.Id).Should().BeEquivalentTo(new[] { 0, 1, 2, 3, 4 }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void MapToArray_ProducesArray_InOrder()
    {
        var sculptor = ForgeOrderSculptor();
        var origins = new[] { new Sprint7Order { Id = 1 }, new Sprint7Order { Id = 2 } };

        var arr = sculptor.MapToArray<Sprint7Order, Sprint7OrderDto>(origins);
        arr.Should().BeAssignableTo<Sprint7OrderDto[]>();
        arr.Length.Should().Be(2);
        arr[0].Id.Should().Be(1);
        arr[1].Id.Should().Be(2);
    }

    [Fact]
    public void MapLazy_DefersExecution()
    {
        var sculptor = ForgeOrderSculptor();

        var hitCounter = new int[1];
        IEnumerable<Sprint7Order> SourceWithCounter()
        {
            hitCounter[0]++;
            yield return new Sprint7Order { Id = 1 };
            hitCounter[0]++;
            yield return new Sprint7Order { Id = 2 };
        }

        var lazy = sculptor.MapLazy<Sprint7Order, Sprint7OrderDto>(SourceWithCounter());
        hitCounter[0].Should().Be(0); // nothing executed yet

        using var e = lazy.GetEnumerator();
        e.MoveNext();
        hitCounter[0].Should().Be(1);
        e.MoveNext();
        hitCounter[0].Should().Be(2);
    }

    [Fact]
    public async Task MapStream_RespectsCancellation()
    {
        var sculptor = ForgeOrderSculptor();

        async IAsyncEnumerable<Sprint7Order> InfiniteSource(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            for (var i = 0; i < 10_000; i++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new Sprint7Order { Id = i };
                await Task.Yield();
            }
        }

        using var cts = new CancellationTokenSource();
        var count = 0;
        var act = async () =>
        {
            await foreach (var _ in sculptor.MapStream<Sprint7Order, Sprint7OrderDto>(InfiniteSource(cts.Token), cts.Token))
            {
                count++;
                if (count >= 5) cts.Cancel();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        count.Should().BeGreaterThan(0).And.BeLessThan(10_000);
    }

    [Fact]
    public void SelectAs_ProducesProjectedQueryable()
    {
        var sculptor = ForgeOrderSculptor();
        var source = new[]
        {
            new Sprint7Order { Id = 1, CustomerName = "A", Total = 1m },
            new Sprint7Order { Id = 2, CustomerName = "B", Total = 2m },
        }.AsQueryable();

        var projected = sculptor.SelectAs<Sprint7OrderDto>(source);
        var list = projected.ToList();

        list.Should().HaveCount(2);
        list.Select(d => d.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void GetProjection_IsCached_PerPair()
    {
        var sculptor = ForgeOrderSculptor();

        var first = sculptor.GetProjection<Sprint7Order, Sprint7OrderDto>();
        var second = sculptor.GetProjection<Sprint7Order, Sprint7OrderDto>();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetProjection_CompilesAndRuns()
    {
        var sculptor = ForgeOrderSculptor();
        var projection = sculptor.GetProjection<Sprint7Order, Sprint7OrderDto>();
        var del = projection.Compile();

        var dto = del(new Sprint7Order { Id = 99, CustomerName = "Z", Total = 5m });
        dto.Id.Should().Be(99);
    }

    [Fact]
    public void Compose_SingleOrigin_Maps()
    {
        var sculptor = ForgeOrderSculptor();
        var origin = new Sprint7Order { Id = 42 };

        var dto = sculptor.Compose<Sprint7OrderDto>(origin);
        dto.Id.Should().Be(42);
    }

    [Fact]
    public void Compose_MultipleOrigins_WithoutRegisteredComposition_Throws_MappingConfigurationException()
    {
        // Sprint 7 originally asserted NotSupportedException here; Sprint 8 · S8-T08 wired
        // multi-origin dispatch through CompositionDispatcher, which now produces an actionable
        // MappingConfigurationException pointing at the missing options.Compose<T>() registration.
        // A two-origin call with the same type also surfaces the ambiguous-match guard — either
        // way, the old NotSupportedException is gone.
        var sculptor = ForgeOrderSculptor();
        var a = new Sprint7Order { Id = 1 };
        var b = new Sprint7Order { Id = 2 };

        var act = () => sculptor.Compose<Sprint7OrderDto>(a, b);
        act.Should().Throw<SmartMapp.Net.MappingConfigurationException>();
    }

    [Fact]
    public void Compose_EmptyOrigins_Throws_Argument()
    {
        var sculptor = ForgeOrderSculptor();
        var act = () => sculptor.Compose<Sprint7OrderDto>();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compose_NullOrigins_Throws_ArgumentNull()
    {
        var sculptor = ForgeOrderSculptor();
        var act = () => sculptor.Compose<Sprint7OrderDto>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Builder_Compose_ReturnsNonNullStub()
    {
        var builder = new SculptorBuilder();
        var rule = builder.Compose<Sprint7OrderDto>();
        rule.Should().NotBeNull();
    }
}
