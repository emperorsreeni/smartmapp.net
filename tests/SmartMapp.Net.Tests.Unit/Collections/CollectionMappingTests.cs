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

    // ──────────────── HashSet Complex Elements (S5-T05) ────────────────

    [Fact]
    public void HashSet_ComplexElements_MapsViaDelegate()
    {
        var blueprint = AutoBlueprint<ComplexHashSetOrigin, ComplexHashSetDto>();
        var mapper = Compile(blueprint);
        var origin = new ComplexHashSetOrigin
        {
            Id = 1,
            Items = new HashSet<OrderItem>
            {
                new() { Id = 10, ProductName = "Widget", Price = 9.99m },
                new() { Id = 20, ProductName = "Gadget", Price = 19.99m }
            }
        };

        var result = (ComplexHashSetDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(2);
        result.Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { 10, 20 });
        result.Items.Select(i => i.ProductName).Should().BeEquivalentTo(new[] { "Widget", "Gadget" });
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

    // ──────────────── Single-Element Array (S5-T01) ────────────────

    [Fact]
    public void Array_SingleElement_MapsCorrectly()
    {
        var blueprint = AutoBlueprint<OrderWithArray, OrderWithArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithArray { Id = 1, Tags = new[] { 42 } };

        var result = (OrderWithArrayDto)mapper(origin, new MappingScope());

        result.Tags.Should().HaveCount(1);
        result.Tags[0].Should().Be(42);
    }

    // ──────────────── Jagged Array int[][] (S5-T09) ────────────────

    [Fact]
    public void JaggedArray_CopiesInnerArrays()
    {
        var blueprint = AutoBlueprint<JaggedArrayOrigin, JaggedArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new JaggedArrayOrigin
        {
            Id = 1,
            Matrix = new[] { new[] { 1, 2 }, new[] { 3, 4, 5 } }
        };

        var result = (JaggedArrayDto)mapper(origin, new MappingScope());

        result.Matrix.Should().HaveCount(2);
        result.Matrix[0].Should().BeEquivalentTo(new[] { 1, 2 });
        result.Matrix[1].Should().BeEquivalentTo(new[] { 3, 4, 5 });
    }

    // ──────────────── List<int[]> → List<int[]> (S5-T09) ────────────────

    [Fact]
    public void ListOfArrays_MapsInnerArrays()
    {
        var blueprint = AutoBlueprint<ListOfIntArrayOrigin, ListOfIntArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new ListOfIntArrayOrigin
        {
            Id = 1,
            Groups = new List<int[]>
            {
                new[] { 1, 2 },
                new[] { 3, 4, 5 },
            }
        };

        var result = (ListOfIntArrayDto)mapper(origin, new MappingScope());

        result.Groups.Should().HaveCount(2);
        result.Groups[0].Should().BeEquivalentTo(new[] { 1, 2 });
        result.Groups[1].Should().BeEquivalentTo(new[] { 3, 4, 5 });
    }

    // ──────────────── IDictionary<K,V> Target (S5-T06) ────────────────

    [Fact]
    public void IDictionary_Target_ReturnsConcreteDict()
    {
        var blueprint = AutoBlueprint<Metadata, IDictionaryTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new Metadata
        {
            Id = 1,
            Properties = new Dictionary<string, int> { ["X"] = 10 }
        };

        var result = (IDictionaryTargetDto)mapper(origin, new MappingScope());

        result.Properties.Should().ContainKey("X").WhoseValue.Should().Be(10);
        result.Properties.Should().BeAssignableTo<IDictionary<string, int>>();
    }

    // ──────────────── IReadOnlyDictionary<K,V> Target (S5-T06) ────────────────

    [Fact]
    public void IReadOnlyDictionary_Target_ReturnsConcreteDict()
    {
        var blueprint = AutoBlueprint<Metadata, IReadOnlyDictionaryTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new Metadata
        {
            Id = 1,
            Properties = new Dictionary<string, int> { ["Y"] = 20 }
        };

        var result = (IReadOnlyDictionaryTargetDto)mapper(origin, new MappingScope());

        result.Properties.Should().ContainKey("Y").WhoseValue.Should().Be(20);
        result.Properties.Should().BeAssignableTo<IReadOnlyDictionary<string, int>>();
    }

    // ──────────────── Dictionary Null → Null (S5-T06) ────────────────

    [Fact]
    public void Dictionary_NullSource_ReturnsNull()
    {
        var blueprint = AutoBlueprint<Metadata, MetadataDto>();
        var mapper = Compile(blueprint);
        var origin = new Metadata { Id = 1, Properties = null! };

        var result = (MetadataDto)mapper(origin, new MappingScope());

        result.Properties.Should().BeNull();
    }

    // ──────────────── ISet<T> Target (S5-T05) ────────────────

    [Fact]
    public void ISet_Target_ReturnsConcreteHashSet()
    {
        var blueprint = AutoBlueprint<TagCloud, ISetTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new TagCloud { Id = 1, Tags = new HashSet<string> { "X", "Y" } };

        var result = (ISetTargetDto)mapper(origin, new MappingScope());

        result.Tags.Should().BeEquivalentTo(new[] { "X", "Y" });
        result.Tags.Should().BeAssignableTo<ISet<string>>();
    }

    // ──────────────── IImmutableList<T> Target (S5-T08) ────────────────

    [Fact]
    public void IImmutableList_Target_ReturnsImmutableList()
    {
        var blueprint = AutoBlueprint<ImmutableOrder, IImmutableListTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new ImmutableOrder { Id = 1, Values = new List<int> { 5, 6 } };

        var result = (IImmutableListTargetDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 5, 6 });
        result.Values.Should().BeAssignableTo<System.Collections.Immutable.IImmutableList<int>>();
    }

    // ──────────────── IReadOnlyCollection<T> Target (S5-T04) ────────────────

    [Fact]
    public void IReadOnlyCollection_Target_ReturnsConcreteList()
    {
        var blueprint = AutoBlueprint<OrderWithICollection, IReadOnlyCollectionTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithICollection { Id = 1, Values = new List<int> { 7, 8, 9 } };

        var result = (IReadOnlyCollectionTargetDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 7, 8, 9 });
        result.Values.Should().BeAssignableTo<IReadOnlyCollection<int>>();
    }

    // ──────────────── string[] Array.Copy Fast-Path (S5-T01) ────────────────

    [Fact]
    public void Array_StringArray_CopiesAllElements()
    {
        var blueprint = AutoBlueprint<StringArrayOrigin, StringArrayDto>();
        var mapper = Compile(blueprint);
        var origin = new StringArrayOrigin { Id = 1, Names = new[] { "Alice", "Bob", "Charlie" } };

        var result = (StringArrayDto)mapper(origin, new MappingScope());

        result.Names.Should().BeEquivalentTo(new[] { "Alice", "Bob", "Charlie" });
    }

    // ──────────────── List<int> Simple Copy (S5-T02) ────────────────

    [Fact]
    public void List_SimpleIntList_CopiesAll()
    {
        var blueprint = AutoBlueprint<LargeListOrigin, LargeListDto>();
        var mapper = Compile(blueprint);
        var origin = new LargeListOrigin { Id = 1, Values = new List<int> { 10, 20, 30 } };

        var result = (LargeListDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    // ──────────────── IList<T> Target (S5-T02) ────────────────

    [Fact]
    public void IList_Target_ReturnsConcreteList()
    {
        var blueprint = AutoBlueprint<OrderWithICollection, IListTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithICollection { Id = 1, Values = new List<int> { 1, 2, 3 } };

        var result = (IListTargetDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        result.Values.Should().BeAssignableTo<IList<int>>();
    }

    // ──────────────── HashSet Duplicate Handling (S5-T05) ────────────────

    [Fact]
    public void HashSet_DuplicateSourceElements_HandledBySetSemantics()
    {
        var blueprint = AutoBlueprint<IntHashSetOrigin, IntHashSetDto>();
        var mapper = Compile(blueprint);
        var origin = new IntHashSetOrigin { Id = 1, Values = new List<int> { 1, 2, 2, 3, 3, 3 } };

        var result = (IntHashSetDto)mapper(origin, new MappingScope());

        result.Values.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        result.Values.Should().HaveCount(3);
    }

    // ──────────────── Dictionary Key Mapping int→long (S5-T06) ────────────────

    [Fact]
    public void Dictionary_KeyMapping_IntToLong_MapsKeys()
    {
        var blueprint = AutoBlueprint<IntKeyDictOrigin, LongKeyDictDto>();
        var mapper = Compile(blueprint);
        var origin = new IntKeyDictOrigin
        {
            Id = 1,
            Lookup = new Dictionary<int, string> { [1] = "one", [2] = "two" }
        };

        var result = (LongKeyDictDto)mapper(origin, new MappingScope());

        result.Lookup.Should().HaveCount(2);
        result.Lookup[1L].Should().Be("one");
        result.Lookup[2L].Should().Be("two");
    }

    // ──────────────── Dictionary Both Keys+Values Mapped (S5-T06) ────────────────

    [Fact]
    public void Dictionary_BothKeysAndValues_MappedCorrectly()
    {
        var blueprint = AutoBlueprint<IntKeyIntValueDictOrigin, LongKeyLongValueDictDto>();
        var mapper = Compile(blueprint);
        var origin = new IntKeyIntValueDictOrigin
        {
            Id = 1,
            Data = new Dictionary<int, int> { [10] = 100, [20] = 200 }
        };

        var result = (LongKeyLongValueDictDto)mapper(origin, new MappingScope());

        result.Data.Should().HaveCount(2);
        result.Data[10L].Should().Be(100L);
        result.Data[20L].Should().Be(200L);
    }

    // ──────────────── Null ImmutableArray Source (S5-T08) ────────────────

    [Fact]
    public void ImmutableArray_NullSource_ReturnsDefault()
    {
        var blueprint = AutoBlueprint<ImmutableArrayOrder, ImmutableArrayOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ImmutableArrayOrder { Id = 1, Values = null! };

        var result = (ImmutableArrayOrderDto)mapper(origin, new MappingScope());

        // ImmutableArray<T> is a struct; null source array → default ImmutableArray (IsDefault = true)
        result.Values.IsDefault.Should().BeTrue();
    }

    // ──────────────── IEnumerable<Order> → List<OrderDto> Complex Elements (S5-T03) ────────────────

    [Fact]
    public void IEnumerable_ComplexElements_MapsEachElement()
    {
        var blueprint = AutoBlueprint<EnumerableComplexOrder, EnumerableComplexOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new EnumerableComplexOrder
        {
            Id = 1,
            Items = new List<OrderItem>
            {
                new() { Id = 10, ProductName = "Widget", Price = 9.99m },
                new() { Id = 20, ProductName = "Gadget", Price = 19.99m }
            }
        };

        var result = (EnumerableComplexOrderDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(10);
        result.Items[0].ProductName.Should().Be("Widget");
        result.Items[1].Id.Should().Be(20);
        result.Items[1].Price.Should().Be(19.99m);
    }

    // ──────────────── List<Order> → ImmutableList<OrderDto> Complex Elements (S5-T08) ────────────────

    [Fact]
    public void ImmutableList_ComplexElements_MapsViaDelegate()
    {
        var blueprint = AutoBlueprint<ImmutableComplexOrder, ImmutableComplexOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ImmutableComplexOrder
        {
            Id = 1,
            Items = new List<OrderItem>
            {
                new() { Id = 100, ProductName = "Alpha", Price = 5.00m },
                new() { Id = 200, ProductName = "Beta", Price = 15.00m }
            }
        };

        var result = (ImmutableComplexOrderDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(2);
        result.Items[0].Id.Should().Be(100);
        result.Items[0].ProductName.Should().Be("Alpha");
        result.Items[1].Id.Should().Be(200);
        result.Items[1].Price.Should().Be(15.00m);
    }

    // ──────────────── List<Order> → ReadOnlyCollection<OrderDto> Complex Elements (S5-T08) ────────────────

    [Fact]
    public void ReadOnlyCollection_ComplexElements_MapsAndWraps()
    {
        var blueprint = AutoBlueprint<ReadOnlyComplexOrder, ReadOnlyComplexOrderDto>();
        var mapper = Compile(blueprint);
        var origin = new ReadOnlyComplexOrder
        {
            Id = 1,
            Items = new List<OrderItem>
            {
                new() { Id = 30, ProductName = "Gamma", Price = 7.50m }
            }
        };

        var result = (ReadOnlyComplexOrderDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(30);
        result.Items[0].ProductName.Should().Be("Gamma");
        result.Items.Should().BeOfType<ReadOnlyCollection<OrderItemDto>>();
    }

    // ──────────────── List<Order> → IReadOnlyCollection<OrderDto> Complex Elements (S5-T04) ────────────────

    [Fact]
    public void IReadOnlyCollection_ComplexElements_MapsCorrectly()
    {
        var blueprint = AutoBlueprint<OrderWithIReadOnlyCollectionComplex, IReadOnlyCollectionComplexTargetDto>();
        var mapper = Compile(blueprint);
        var origin = new OrderWithIReadOnlyCollectionComplex
        {
            Id = 1,
            Items = new List<OrderItem>
            {
                new() { Id = 10, ProductName = "Widget", Price = 9.99m },
                new() { Id = 20, ProductName = "Gadget", Price = 19.99m }
            }
        };

        var result = (IReadOnlyCollectionComplexTargetDto)mapper(origin, new MappingScope());

        result.Items.Should().HaveCount(2);
        result.Items.Should().BeAssignableTo<IReadOnlyCollection<OrderItemDto>>();
        var items = result.Items.ToList();
        items[0].Id.Should().Be(10);
        items[0].ProductName.Should().Be("Widget");
        items[1].Id.Should().Be(20);
        items[1].Price.Should().Be(19.99m);
    }

    private static IEnumerable<int> YieldValues()
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
