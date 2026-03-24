using System.Collections.Immutable;
using System.Collections.ObjectModel;
using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Collections;

public sealed class CollectionMappingTests
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

    // ──────────────── Array Mapping ────────────────

    [Fact]
    public void Array_SameType_IntArray_CopiesAllElements()
    {
        var blueprint = AutoBlueprint<OrderWithArray, OrderWithArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithArray { Id = 1, Tags = new[] { 10, 20, 30 } };

        var result = (OrderWithArrayDto)mapper(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Tags.Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    [Fact]
    public void Array_SameType_EmptyArray_ReturnsEmptyArray()
    {
        var blueprint = AutoBlueprint<OrderWithArray, OrderWithArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithArray { Id = 1, Tags = Array.Empty<int>() };

        var result = (OrderWithArrayDto)mapper(origin, new MappingScope());

        result.Tags.Should().NotBeNull();
        result.Tags.Should().BeEmpty();
    }

    [Fact]
    public void Array_SameType_NullSource_ReturnsNull()
    {
        var blueprint = AutoBlueprint<OrderWithArray, OrderWithArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithArray { Id = 1, Tags = null! };

        var result = (OrderWithArrayDto)mapper(origin, new MappingScope());

        result.Tags.Should().BeNull();
    }

    [Fact]
    public void Array_ComplexElements_MapsEachElement()
    {
        var blueprint = AutoBlueprint<OrderWithComplexArray, OrderWithComplexArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithComplexArray
        {
            Id = 1,
            Items = new[]
            {
                new OrderItem { Id = 1, ProductName = "Widget", Price = 9.99m },
                new OrderItem { Id = 2, ProductName = "Gadget", Price = 19.99m },
            }
        };

        var result = (OrderWithComplexArrayDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(1);
        result.Items[0].ProductName.Should().Be("Widget");
        result.Items[1].Id.Should().Be(2);
        result.Items[1].ProductName.Should().Be("Gadget");
    }

    [Fact]
    public void Array_LargeArray_MapsCorrectly()
    {
        var blueprint = AutoBlueprint<OrderWithArray, OrderWithArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithArray { Id = 1, Tags = Enumerable.Range(0, 10_000).ToArray() };

        var result = (OrderWithArrayDto)mapper(origin, new MappingScope());

        result.Tags.Should().HaveCount(10_000);
        result.Tags.Should().BeEquivalentTo(Enumerable.Range(0, 10_000));
    }

    // ──────────────── List Mapping ────────────────

    [Fact]
    public void List_SameElementType_CopiesAll()
    {
        var blueprint = AutoBlueprint<OrderWithLines, OrderWithLinesDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithLines
        {
            Id = 1,
            Name = "Order1",
            Lines = new List<OrderItem>
            {
                new() { Id = 1, ProductName = "Widget", Price = 10m },
                new() { Id = 2, ProductName = "Gadget", Price = 20m },
            }
        };

        var result = (OrderWithLinesDto)mapper(origin, new MappingScope());

        result.Id.Should().Be(1);
        result.Lines.Should().HaveCount(2);
        result.Lines[0].ProductName.Should().Be("Widget");
        result.Lines[1].ProductName.Should().Be("Gadget");
    }

    [Fact]
    public void List_EmptyList_ReturnsEmptyList()
    {
        var blueprint = AutoBlueprint<OrderWithLines, OrderWithLinesDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithLines { Id = 1, Name = "Empty", Lines = new List<OrderItem>() };

        var result = (OrderWithLinesDto)mapper(origin, new MappingScope());

        result.Lines.Should().NotBeNull();
        result.Lines.Should().BeEmpty();
    }

    [Fact]
    public void List_NullSource_ReturnsNull()
    {
        var blueprint = AutoBlueprint<OrderWithLines, OrderWithLinesDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithLines { Id = 1, Name = "Null", Lines = null! };

        var result = (OrderWithLinesDto)mapper(origin, new MappingScope());

        result.Lines.Should().BeNull();
    }

    // ──────────────── ICollection / IReadOnlyList ────────────────

    [Fact]
    public void ICollection_Target_ReturnsConcreteList()
    {
        var blueprint = AutoBlueprint<OrderWithICollection, OrderWithICollectionDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithICollection { Id = 1, Values = new List<int> { 1, 2, 3 } };

        var result = (OrderWithICollectionDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        result.Values.Should().BeAssignableTo<ICollection<int>>();
    }

    [Fact]
    public void IReadOnlyList_Target_ReturnsConcreteList()
    {
        var blueprint = AutoBlueprint<OrderWithIReadOnlyList, OrderWithIReadOnlyListDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithIReadOnlyList { Id = 1, Values = new List<int> { 4, 5, 6 } };

        var result = (OrderWithIReadOnlyListDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 4, 5, 6 });
        result.Values.Should().BeAssignableTo<IReadOnlyList<int>>();
    }

    // ──────────────── HashSet Mapping ────────────────

    [Fact]
    public void HashSet_PreservesUniqueValues()
    {
        var blueprint = AutoBlueprint<TagCloud, TagCloudDto>();
        var mapper = Compile(blueprint);
        var origin = new TagCloud { Id = 1, Tags = new HashSet<string> { "A", "B", "C" } };

        var result = (TagCloudDto)mapper(origin, new MappingScope());

        result.Tags.Should().BeEquivalentTo(new[] { "A", "B", "C" });
    }

    [Fact]
    public void HashSet_EmptySet_ReturnsEmptySet()
    {
        var blueprint = AutoBlueprint<TagCloud, TagCloudDto>();
        var mapper = Compile(blueprint);
        var origin = new TagCloud { Id = 1, Tags = new HashSet<string>() };

        var result = (TagCloudDto)mapper(origin, new MappingScope());

        result.Tags.Should().NotBeNull();
        result.Tags.Should().BeEmpty();
    }

    // ──────────────── Dictionary Mapping ────────────────

    [Fact]
    public void Dictionary_SameKeyValueTypes_CopiesAll()
    {
        var blueprint = AutoBlueprint<Metadata, MetadataDto>();
        var mapper = Compile(blueprint);
        var origin = new Metadata
        {
            Id = 1,
            Properties = new Dictionary<string, int> { ["Width"] = 100, ["Height"] = 200 }
        };

        var result = (MetadataDto)mapper(origin, new MappingScope());

        result.Properties.Should().ContainKey("Width").WhoseValue.Should().Be(100);
        result.Properties.Should().ContainKey("Height").WhoseValue.Should().Be(200);
    }

    [Fact]
    public void Dictionary_ComplexValues_MapsValues()
    {
        var blueprint = AutoBlueprint<ComplexDictOrigin, ComplexDictDto>();
        var mapper = Compile(blueprint);
        var origin = new ComplexDictOrigin
        {
            Id = 1,
            Items = new Dictionary<string, OrderItem>
            {
                ["first"] = new() { Id = 1, ProductName = "Widget", Price = 9.99m }
            }
        };

        var result = (ComplexDictDto)mapper(origin, new MappingScope());

        result.Items.Should().ContainKey("first");
        result.Items["first"].Id.Should().Be(1);
        result.Items["first"].ProductName.Should().Be("Widget");
    }

    [Fact]
    public void Dictionary_Empty_ReturnsEmptyDictionary()
    {
        var blueprint = AutoBlueprint<Metadata, MetadataDto>();
        var mapper = Compile(blueprint);
        var origin = new Metadata { Id = 1, Properties = new Dictionary<string, int>() };

        var result = (MetadataDto)mapper(origin, new MappingScope());

        result.Properties.Should().NotBeNull();
        result.Properties.Should().BeEmpty();
    }

    // ──────────────── ImmutableList Mapping ────────────────

    [Fact]
    public void ImmutableList_MapsUsingBuilder()
    {
        var blueprint = AutoBlueprint<ImmutableOrder, ImmutableOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ImmutableOrder { Id = 1, Values = new List<int> { 1, 2, 3 } };

        var result = (ImmutableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        result.Values.Should().BeOfType<ImmutableList<int>>();
    }

    [Fact]
    public void ImmutableList_Empty_ReturnsEmptyImmutableList()
    {
        var blueprint = AutoBlueprint<ImmutableOrder, ImmutableOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ImmutableOrder { Id = 1, Values = new List<int>() };

        var result = (ImmutableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEmpty();
    }

    // ──────────────── ImmutableArray Mapping ────────────────

    [Fact]
    public void ImmutableArray_MapsUsingBuilder()
    {
        var blueprint = AutoBlueprint<ImmutableArrayOrder, ImmutableArrayOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ImmutableArrayOrder { Id = 1, Values = new[] { 10, 20, 30 } };

        var result = (ImmutableArrayOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    // ──────────────── ObservableCollection Mapping ────────────────

    [Fact]
    public void ObservableCollection_MapsAllElements()
    {
        var blueprint = AutoBlueprint<ObservableOrder, ObservableOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ObservableOrder { Id = 1, Values = new List<int> { 7, 8, 9 } };

        var result = (ObservableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeOfType<ObservableCollection<int>>();
        result.Values.Should().BeEquivalentTo(new[] { 7, 8, 9 });
    }

    // ──────────────── ReadOnlyCollection Mapping ────────────────

    [Fact]
    public void ReadOnlyCollection_WrapsListCorrectly()
    {
        var blueprint = AutoBlueprint<ReadOnlyOrder, ReadOnlyOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ReadOnlyOrder { Id = 1, Values = new List<int> { 11, 22, 33 } };

        var result = (ReadOnlyOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeOfType<ReadOnlyCollection<int>>();
        result.Values.Should().BeEquivalentTo(new[] { 11, 22, 33 });
    }

    // ──────────────── Nested Collections ────────────────

    [Fact]
    public void NestedList_ListOfList_MapsInnerLists()
    {
        var blueprint = AutoBlueprint<NestedListOrder, NestedListOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new NestedListOrder
        {
            Id = 1,
            Matrix = new List<List<int>>
            {
                new() { 1, 2, 3 },
                new() { 4, 5, 6 },
            }
        };

        var result = (NestedListOrderDto)mapper(origin, new MappingScope());

        result.Matrix.Should().HaveCount(2);
        result.Matrix[0].Should().BeEquivalentTo(new[] { 1, 2, 3 });
        result.Matrix[1].Should().BeEquivalentTo(new[] { 4, 5, 6 });
    }

    // ──────────────── IEnumerable Materialization ────────────────

    [Fact]
    public void IEnumerable_ToList_Materializes()
    {
        var blueprint = AutoBlueprint<EnumerableOrder, EnumerableOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new EnumerableOrder { Id = 1, Values = YieldValues() };

        var result = (EnumerableOrderDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void IEnumerable_ToArray_Materializes()
    {
        var blueprint = AutoBlueprint<EnumerableOrder, EnumerableToArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new EnumerableOrder { Id = 1, Values = new List<int> { 10, 20 } };

        var result = (EnumerableToArrayDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 10, 20 });
    }

    private static IEnumerable<int> YieldValues()
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
