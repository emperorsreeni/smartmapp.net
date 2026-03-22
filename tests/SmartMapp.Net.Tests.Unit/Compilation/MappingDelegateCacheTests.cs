using FluentAssertions;
using SmartMapp.Net.Caching;

namespace SmartMapp.Net.Tests.Unit.Compilation;

public class MappingDelegateCacheTests
{
    [Fact]
    public void GetOrCompile_FirstCall_CompilesAndCaches()
    {
        var cache = new MappingDelegateCache();
        var pair = TypePair.Of<string, int>();
        var factoryCallCount = 0;

        Func<object, MappingScope, object> factory(TypePair tp)
        {
            factoryCallCount++;
            return (origin, scope) => 42;
        }

        var del = cache.GetOrCompile(pair, factory);

        del.Should().NotBeNull();
        factoryCallCount.Should().Be(1);
        del("hello", new MappingScope()).Should().Be(42);
    }

    [Fact]
    public void GetOrCompile_SecondCall_ReturnsCached()
    {
        var cache = new MappingDelegateCache();
        var pair = TypePair.Of<string, int>();
        var factoryCallCount = 0;

        Func<object, MappingScope, object> factory(TypePair tp)
        {
            factoryCallCount++;
            return (origin, scope) => 42;
        }

        var del1 = cache.GetOrCompile(pair, factory);
        var del2 = cache.GetOrCompile(pair, factory);

        del1.Should().BeSameAs(del2);
        factoryCallCount.Should().Be(1);
    }

    [Fact]
    public void GetOrCompile_ConcurrentAccess_ThreadSafe()
    {
        var cache = new MappingDelegateCache();
        var pair = TypePair.Of<string, int>();
        var factoryCallCount = 0;

        Func<object, MappingScope, object> factory(TypePair tp)
        {
            Interlocked.Increment(ref factoryCallCount);
            Thread.Sleep(10); // simulate work
            return (origin, scope) => 42;
        }

        var results = new Func<object, MappingScope, object>[100];
        Parallel.For(0, 100, i =>
        {
            results[i] = cache.GetOrCompile(pair, factory);
        });

        // Lazy ensures factory called exactly once
        factoryCallCount.Should().Be(1);

        // All results should be the same delegate
        for (var i = 1; i < results.Length; i++)
        {
            results[i].Should().BeSameAs(results[0]);
        }
    }

    [Fact]
    public void TryGet_NotCompiled_ReturnsFalse()
    {
        var cache = new MappingDelegateCache();
        var pair = TypePair.Of<string, int>();

        cache.TryGet(pair, out var del).Should().BeFalse();
        del.Should().BeNull();
    }

    [Fact]
    public void TryGet_AfterCompile_ReturnsTrue()
    {
        var cache = new MappingDelegateCache();
        var pair = TypePair.Of<string, int>();

        cache.GetOrCompile(pair, _ => (origin, scope) => 42);

        cache.TryGet(pair, out var del).Should().BeTrue();
        del.Should().NotBeNull();
    }

    [Fact]
    public void GetCachedPairs_ReturnsAllStoredPairs()
    {
        var cache = new MappingDelegateCache();
        var pair1 = TypePair.Of<string, int>();
        var pair2 = TypePair.Of<int, string>();

        cache.GetOrCompile(pair1, _ => (origin, scope) => 1);
        cache.GetOrCompile(pair2, _ => (origin, scope) => "a");

        var pairs = cache.GetCachedPairs();

        pairs.Should().HaveCount(2);
        pairs.Should().Contain(pair1);
        pairs.Should().Contain(pair2);
    }

    [Fact]
    public void Count_ReflectsEntries()
    {
        var cache = new MappingDelegateCache();

        cache.Count.Should().Be(0);

        cache.GetOrCompile(TypePair.Of<string, int>(), _ => (origin, scope) => 1);
        cache.Count.Should().Be(1);

        cache.GetOrCompile(TypePair.Of<int, string>(), _ => (origin, scope) => "a");
        cache.Count.Should().Be(2);
    }

    [Fact]
    public void Clear_RemovesAll()
    {
        var cache = new MappingDelegateCache();
        cache.GetOrCompile(TypePair.Of<string, int>(), _ => (origin, scope) => 1);

        cache.Clear();

        cache.Count.Should().Be(0);
        cache.TryGet(TypePair.Of<string, int>(), out _).Should().BeFalse();
    }
}
