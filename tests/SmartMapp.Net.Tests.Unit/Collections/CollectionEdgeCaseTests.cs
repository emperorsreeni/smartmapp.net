using System.Collections.Immutable;
using System.Collections.ObjectModel;
using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Collections;

/// <summary>
/// Edge case tests for collection mapping (S5-T11).
/// Covers: large collections, null element refs, thread safety,
/// deferred IEnumerable, empty collections across all types,
/// nested Dict&lt;string, List&lt;int&gt;&gt;, 3-level nesting.
/// </summary>
public sealed class CollectionEdgeCaseTests
{
    private readonly TypeModelCache _typeModelCache = new();
    private readonly MappingDelegateCache _delegateCache = new();

    private Func<object, MappingScope, object> Compile(Blueprint blueprint)
    {
        var compiler = new BlueprintCompiler(_typeModelCache, _delegateCache);
        return compiler.Compile(blueprint);
    }

    private Blueprint AutoBlueprint<TOrigin, TTarget>()
    {
        var originModel = _typeModelCache.GetOrAdd(typeof(TOrigin));
        var targetModel = _typeModelCache.GetOrAdd(typeof(TTarget));

        var links = new List<PropertyLink>();
        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = new DirectMemberProvider(originMember.MemberInfo),
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
            });
        }

        return new Blueprint
        {
            OriginType = typeof(TOrigin),
            TargetType = typeof(TTarget),
            Links = links,
        };
    }

    // ──────────────── Large Collection Correctness (100K) ────────────────

    [Fact]
    public void LargeList_100K_MapsCorrectly()
    {
        var blueprint = AutoBlueprint<LargeListOrigin, LargeListDto>();
        var mapper = Compile(blueprint);

        var origin = new LargeListOrigin
        {
            Id = 1,
            Values = Enumerable.Range(0, 100_000).ToList()
        };
        var result = (LargeListDto)mapper(origin, new MappingScope());

        result.Values.Should().HaveCount(100_000);
        result.Values[0].Should().Be(0);
        result.Values[99_999].Should().Be(99_999);
    }

    [Fact]
    public void LargeDictionary_10K_MapsCorrectly()
    {
        var blueprint = AutoBlueprint<Metadata, MetadataDto>();
        var mapper = Compile(blueprint);

        var dict = new Dictionary<string, int>();
        for (var i = 0; i < 10_000; i++)
            dict[$"key{i}"] = i;

        var origin = new Metadata { Id = 1, Properties = dict };
        var result = (MetadataDto)mapper(origin, new MappingScope());

        result.Properties.Should().HaveCount(10_000);
        result.Properties["key0"].Should().Be(0);
        result.Properties["key9999"].Should().Be(9999);
    }

    // ──────────────── Collection of Null References ────────────────

    [Fact]
    public void ListOfAllNulls_PreservesNullElements()
    {
        var blueprint = AutoBlueprint<NullElementsOrigin, NullElementsDto>();
        var mapper = Compile(blueprint);

        var origin = new NullElementsOrigin { Id = 1, Items = new List<string?> { null, null, null } };
        var result = (NullElementsDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(3);
        result.Items.Should().AllBeEquivalentTo((string?)null);
    }

    // ──────────────── Thread Safety ────────────────

    [Fact]
    public async Task ConcurrentArrayMapping_NoCorruption()
    {
        var blueprint = AutoBlueprint<OrderWithArray, OrderWithArrayDto>();
        var mapper = Compile(blueprint);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            var origin = new OrderWithArray { Id = i, Tags = Enumerable.Range(i * 10, 50).ToArray() };
            var result = (OrderWithArrayDto)mapper(origin, new MappingScope());
            result.Id.Should().Be(i);
            result.Tags.Should().HaveCount(50);
            result.Tags[0].Should().Be(i * 10);
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentListMapping_NoCorruption()
    {
        var blueprint = AutoBlueprint<OrderWithLines, OrderWithLinesDto>();
        var mapper = Compile(blueprint);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            var origin = new OrderWithLines
            {
                Id = i,
                Lines = new List<OrderItem>
                {
                    new() { Id = i, ProductName = $"Item{i}", Price = i * 1.5m }
                }
            };
            var result = (OrderWithLinesDto)mapper(origin, new MappingScope());
            result.Id.Should().Be(i);
            result.Lines.Should().HaveCount(1);
            result.Lines[0].Id.Should().Be(i);
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    // ──────────────── Deferred IEnumerable ────────────────

    [Fact]
    public void DeferredEnumerable_EnumeratesExactlyOnce()
    {
        var blueprint = AutoBlueprint<EnumerableOrder, EnumerableOrderDto>();
        var mapper = Compile(blueprint);

        var enumerateCount = 0;
        IEnumerable<int> DeferredSource()
        {
            enumerateCount++;
            yield return 1;
            yield return 2;
            yield return 3;
        }

        var origin = new EnumerableOrder { Id = 1, Values = DeferredSource() };
        var result = (EnumerableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        enumerateCount.Should().Be(1);
    }

    // ──────────────── Empty Collections Across All Types ────────────────

    [Fact]
    public void EmptyIEnumerableToList_ReturnsEmptyList()
    {
        var blueprint = AutoBlueprint<EnumerableOrder, EnumerableOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new EnumerableOrder { Id = 1, Values = Enumerable.Empty<int>() };
        var result = (EnumerableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().NotBeNull();
        result.Values.Should().BeEmpty();
    }

    [Fact]
    public void EmptyIEnumerableToArray_ReturnsEmptyArray()
    {
        var blueprint = AutoBlueprint<EnumerableOrder, EnumerableToArrayDto>();
        var mapper = Compile(blueprint);

        var origin = new EnumerableOrder { Id = 1, Values = Enumerable.Empty<int>() };
        var result = (EnumerableToArrayDto)mapper(origin, new MappingScope());

        result.Values.Should().NotBeNull();
        result.Values.Should().BeEmpty();
    }

    [Fact]
    public void EmptyObservableCollection_ReturnsEmpty()
    {
        var blueprint = AutoBlueprint<ObservableOrder, ObservableOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new ObservableOrder { Id = 1, Values = new() };
        var result = (ObservableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().NotBeNull();
        result.Values.Should().BeEmpty();
    }

    [Fact]
    public void EmptyReadOnlyCollection_ReturnsEmpty()
    {
        var blueprint = AutoBlueprint<ReadOnlyOrder, ReadOnlyOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new ReadOnlyOrder { Id = 1, Values = new() };
        var result = (ReadOnlyOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().NotBeNull();
        result.Values.Should().BeEmpty();
    }

    [Fact]
    public void EmptyImmutableArray_ReturnsEmpty()
    {
        var blueprint = AutoBlueprint<ImmutableArrayOrder, ImmutableArrayOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new ImmutableArrayOrder { Id = 1, Values = Array.Empty<int>() };
        var result = (ImmutableArrayOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEmpty();
    }

    // ──────────────── Null Source for All Collection Types ────────────────

    [Fact]
    public void NullHashSet_ReturnsNull()
    {
        var blueprint = AutoBlueprint<TagCloud, TagCloudDto>();
        var mapper = Compile(blueprint);

        var origin = new TagCloud { Id = 1, Tags = null! };
        var result = (TagCloudDto)mapper(origin, new MappingScope());

        result.Tags.Should().BeNull();
    }

    [Fact]
    public void NullImmutableList_ReturnsNull()
    {
        var blueprint = AutoBlueprint<ImmutableOrder, ImmutableOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new ImmutableOrder { Id = 1, Values = null! };
        var result = (ImmutableOrderDto)mapper(origin, new MappingScope());

        // ImmutableList<T> is a reference type, null source → null target
        result.Values.Should().BeNull();
    }

    [Fact]
    public void NullObservable_ReturnsNull()
    {
        var blueprint = AutoBlueprint<ObservableOrder, ObservableOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new ObservableOrder { Id = 1, Values = null! };
        var result = (ObservableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeNull();
    }

    [Fact]
    public void NullIEnumerable_ReturnsNull()
    {
        var blueprint = AutoBlueprint<EnumerableOrder, EnumerableOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new EnumerableOrder { Id = 1, Values = null! };
        var result = (EnumerableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeNull();
    }

    // ──────────────── Nested: Dict<string, List<int>> ────────────────

    [Fact]
    public void DictOfList_MapsInnerLists()
    {
        var blueprint = AutoBlueprint<DictWithListOrigin, DictWithListDto>();
        var mapper = Compile(blueprint);

        var origin = new DictWithListOrigin
        {
            Id = 1,
            Groups = new Dictionary<string, List<int>>
            {
                ["a"] = new() { 1, 2 },
                ["b"] = new() { 3, 4, 5 }
            }
        };
        var result = (DictWithListDto)mapper(origin, new MappingScope());

        result.Groups.Should().HaveCount(2);
        result.Groups["a"].Should().BeEquivalentTo(new[] { 1, 2 });
        result.Groups["b"].Should().BeEquivalentTo(new[] { 3, 4, 5 });
    }

    // ──────────────── 3-Level Nesting ────────────────

    [Fact]
    public void ThreeLevelNested_MapsCorrectly()
    {
        var blueprint = AutoBlueprint<ThreeLevelNestedOrigin, ThreeLevelNestedDto>();
        var mapper = Compile(blueprint);

        var origin = new ThreeLevelNestedOrigin
        {
            Id = 1,
            Cube = new List<List<List<int>>>
            {
                new() { new() { 1, 2 }, new() { 3 } },
                new() { new() { 4, 5, 6 } }
            }
        };
        var result = (ThreeLevelNestedDto)mapper(origin, new MappingScope());

        result.Cube.Should().HaveCount(2);
        result.Cube[0].Should().HaveCount(2);
        result.Cube[0][0].Should().BeEquivalentTo(new[] { 1, 2 });
        result.Cube[0][1].Should().BeEquivalentTo(new[] { 3 });
        result.Cube[1][0].Should().BeEquivalentTo(new[] { 4, 5, 6 });
    }

    // ──────────────── Null Inner Collection in Nested ────────────────

    [Fact]
    public void NullInnerList_PreservesNull()
    {
        var blueprint = AutoBlueprint<NestedListOrder, NestedListOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new NestedListOrder
        {
            Id = 1,
            Matrix = new List<List<int>> { new() { 1, 2 }, null! }
        };
        var result = (NestedListOrderDto)mapper(origin, new MappingScope());

        result.Matrix.Should().HaveCount(2);
        result.Matrix[0].Should().BeEquivalentTo(new[] { 1, 2 });
        result.Matrix[1].Should().BeNull();
    }

    [Fact]
    public void EmptyInnerList_PreservesEmpty()
    {
        var blueprint = AutoBlueprint<NestedListOrder, NestedListOrderDto>();
        var mapper = Compile(blueprint);

        var origin = new NestedListOrder
        {
            Id = 1,
            Matrix = new List<List<int>> { new(), new() { 1 } }
        };
        var result = (NestedListOrderDto)mapper(origin, new MappingScope());

        result.Matrix.Should().HaveCount(2);
        result.Matrix[0].Should().BeEmpty();
        result.Matrix[1].Should().BeEquivalentTo(new[] { 1 });
    }

    // ──────────────── Duplicate Keys Throw (S5-T06) ────────────────

    [Fact]
    public void Dictionary_DuplicateSourceKeys_CopiedViaCopyConstructor()
    {
        // Standard Dictionary<K,V> does not allow duplicate keys, so source cannot
        // have duplicates. The copy-constructor fast-path (same key+value types)
        // faithfully copies all entries. This test verifies the contract holds:
        // if source has N unique keys, target has N entries.
        var blueprint = AutoBlueprint<DuplicateKeyDictOrigin, DuplicateKeyDictDto>();
        var mapper = Compile(blueprint);
        var origin = new DuplicateKeyDictOrigin
        {
            Id = 1,
            Data = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2, ["c"] = 3 }
        };

        var result = (DuplicateKeyDictDto)mapper(origin, new MappingScope());

        result.Data.Should().HaveCount(3);
        result.Data["a"].Should().Be(1);
        result.Data["b"].Should().Be(2);
        result.Data["c"].Should().Be(3);
    }

    // ──────────────── List<Order[]> → List<OrderDto[]> Complex Inner Elements (S5-T09) ────────────────

    [Fact]
    public void ListOfComplexArrays_MapsInnerElements()
    {
        var blueprint = AutoBlueprint<ListOfArrayOrigin, ListOfArrayDto>();
        var mapper = Compile(blueprint);

        var origin = new ListOfArrayOrigin
        {
            Id = 1,
            Groups = new List<OrderItem[]>
            {
                new[] { new OrderItem { Id = 10, ProductName = "A", Price = 1.0m } },
                new[] { new OrderItem { Id = 20, ProductName = "B", Price = 2.0m }, new OrderItem { Id = 30, ProductName = "C", Price = 3.0m } }
            }
        };
        var result = (ListOfArrayDto)mapper(origin, new MappingScope());

        result.Groups.Should().HaveCount(2);
        result.Groups[0].Should().HaveCount(1);
        result.Groups[0][0].Id.Should().Be(10);
        result.Groups[0][0].ProductName.Should().Be("A");
        result.Groups[1].Should().HaveCount(2);
        result.Groups[1][0].Id.Should().Be(20);
        result.Groups[1][1].Id.Should().Be(30);
    }

    // ──────────────── Dict<string, List<Order>> → Dict<string, List<OrderDto>> (S5-T09) ────────────────

    [Fact]
    public void DictOfComplexList_MapsInnerElements()
    {
        var blueprint = AutoBlueprint<DictWithComplexListOrigin, DictWithComplexListDto>();
        var mapper = Compile(blueprint);

        var origin = new DictWithComplexListOrigin
        {
            Id = 1,
            Groups = new Dictionary<string, List<OrderItem>>
            {
                ["team1"] = new() { new OrderItem { Id = 100, ProductName = "Alpha", Price = 5.0m } },
                ["team2"] = new() { new OrderItem { Id = 200, ProductName = "Beta", Price = 10.0m }, new OrderItem { Id = 300, ProductName = "Gamma", Price = 15.0m } }
            }
        };
        var result = (DictWithComplexListDto)mapper(origin, new MappingScope());

        result.Groups.Should().HaveCount(2);
        result.Groups["team1"].Should().HaveCount(1);
        result.Groups["team1"][0].Id.Should().Be(100);
        result.Groups["team1"][0].ProductName.Should().Be("Alpha");
        result.Groups["team2"].Should().HaveCount(2);
        result.Groups["team2"][0].Id.Should().Be(200);
        result.Groups["team2"][1].Id.Should().Be(300);
        result.Groups["team2"][1].ProductName.Should().Be("Gamma");
    }
}
