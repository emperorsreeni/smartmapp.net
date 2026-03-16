using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class TypeModelCacheTests
{
    [Fact]
    public void GetOrAdd_ReturnsSameInstanceForSameType()
    {
        var cache = new TypeModelCache();

        var model1 = cache.GetOrAdd(typeof(SimpleClass));
        var model2 = cache.GetOrAdd(typeof(SimpleClass));

        model1.Should().BeSameAs(model2);
    }

    [Fact]
    public void GetOrAdd_Generic_Works()
    {
        var cache = new TypeModelCache();

        var model = cache.GetOrAdd<SimpleClass>();

        model.ClrType.Should().Be(typeof(SimpleClass));
    }

    [Fact]
    public void GetOrAdd_ThreadSafe_ConcurrentAccess()
    {
        var cache = new TypeModelCache();
        var results = new TypeModel[100];

        Parallel.For(0, 100, i =>
        {
            results[i] = cache.GetOrAdd(typeof(SimpleClass));
        });

        // All results should be the exact same instance
        results.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public void Clear_EmptiesCache()
    {
        var cache = new TypeModelCache();
        cache.GetOrAdd<SimpleClass>();
        cache.GetOrAdd<Order>();

        cache.Count.Should().Be(2);

        cache.Clear();

        cache.Count.Should().Be(0);
    }

    [Fact]
    public void Count_ReturnsCorrectNumber()
    {
        var cache = new TypeModelCache();

        cache.Count.Should().Be(0);

        cache.GetOrAdd<SimpleClass>();
        cache.Count.Should().Be(1);

        cache.GetOrAdd<Order>();
        cache.Count.Should().Be(2);

        // Same type doesn't increase count
        cache.GetOrAdd<SimpleClass>();
        cache.Count.Should().Be(2);
    }

    [Fact]
    public void Default_IsSingleton()
    {
        TypeModelCache.Default.Should().BeSameAs(TypeModelCache.Default);
    }
}
