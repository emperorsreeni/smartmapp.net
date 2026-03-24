using System.Collections.Immutable;
using System.Collections.ObjectModel;
using FluentAssertions;
using SmartMapp.Net.Collections;

namespace SmartMapp.Net.Tests.Unit.Collections;

public sealed class CollectionCategoryResolverTests
{
    [Fact]
    public void Resolve_Array_ReturnsArray()
    {
        CollectionCategoryResolver.Resolve(typeof(int[])).Should().Be(CollectionCategory.Array);
        CollectionCategoryResolver.Resolve(typeof(string[])).Should().Be(CollectionCategory.Array);
        CollectionCategoryResolver.Resolve(typeof(object[])).Should().Be(CollectionCategory.Array);
    }

    [Fact]
    public void Resolve_List_ReturnsList()
    {
        CollectionCategoryResolver.Resolve(typeof(List<int>)).Should().Be(CollectionCategory.List);
        CollectionCategoryResolver.Resolve(typeof(List<string>)).Should().Be(CollectionCategory.List);
    }

    [Fact]
    public void Resolve_IList_ReturnsList()
    {
        CollectionCategoryResolver.Resolve(typeof(IList<int>)).Should().Be(CollectionCategory.List);
    }

    [Fact]
    public void Resolve_IEnumerable_ReturnsEnumerable()
    {
        CollectionCategoryResolver.Resolve(typeof(IEnumerable<int>)).Should().Be(CollectionCategory.Enumerable);
    }

    [Fact]
    public void Resolve_ICollection_ReturnsCollection()
    {
        CollectionCategoryResolver.Resolve(typeof(ICollection<int>)).Should().Be(CollectionCategory.Collection);
    }

    [Fact]
    public void Resolve_IReadOnlyList_ReturnsReadOnlyList()
    {
        CollectionCategoryResolver.Resolve(typeof(IReadOnlyList<int>)).Should().Be(CollectionCategory.ReadOnlyList);
    }

    [Fact]
    public void Resolve_IReadOnlyCollection_ReturnsReadOnlyCollection()
    {
        CollectionCategoryResolver.Resolve(typeof(IReadOnlyCollection<int>)).Should().Be(CollectionCategory.ReadOnlyCollection);
    }

    [Fact]
    public void Resolve_HashSet_ReturnsHashSet()
    {
        CollectionCategoryResolver.Resolve(typeof(HashSet<int>)).Should().Be(CollectionCategory.HashSet);
        CollectionCategoryResolver.Resolve(typeof(HashSet<string>)).Should().Be(CollectionCategory.HashSet);
    }

    [Fact]
    public void Resolve_ISet_ReturnsHashSet()
    {
        CollectionCategoryResolver.Resolve(typeof(ISet<int>)).Should().Be(CollectionCategory.HashSet);
    }

    [Fact]
    public void Resolve_Dictionary_ReturnsDictionary()
    {
        CollectionCategoryResolver.Resolve(typeof(Dictionary<string, int>)).Should().Be(CollectionCategory.Dictionary);
        CollectionCategoryResolver.Resolve(typeof(Dictionary<int, string>)).Should().Be(CollectionCategory.Dictionary);
    }

    [Fact]
    public void Resolve_IDictionary_ReturnsDictionary()
    {
        CollectionCategoryResolver.Resolve(typeof(IDictionary<string, int>)).Should().Be(CollectionCategory.Dictionary);
    }

    [Fact]
    public void Resolve_IReadOnlyDictionary_ReturnsDictionary()
    {
        CollectionCategoryResolver.Resolve(typeof(IReadOnlyDictionary<string, int>)).Should().Be(CollectionCategory.Dictionary);
    }

    [Fact]
    public void Resolve_ImmutableList_ReturnsImmutableList()
    {
        CollectionCategoryResolver.Resolve(typeof(ImmutableList<int>)).Should().Be(CollectionCategory.ImmutableList);
    }

    [Fact]
    public void Resolve_ImmutableArray_ReturnsImmutableArray()
    {
        CollectionCategoryResolver.Resolve(typeof(ImmutableArray<int>)).Should().Be(CollectionCategory.ImmutableArray);
    }

    [Fact]
    public void Resolve_ObservableCollection_ReturnsObservableCollection()
    {
        CollectionCategoryResolver.Resolve(typeof(ObservableCollection<int>)).Should().Be(CollectionCategory.ObservableCollection);
    }

    [Fact]
    public void Resolve_ReadOnlyCollection_ReturnsReadOnlyCollectionConcrete()
    {
        CollectionCategoryResolver.Resolve(typeof(ReadOnlyCollection<int>)).Should().Be(CollectionCategory.ReadOnlyCollectionConcrete);
    }

    [Fact]
    public void Resolve_String_ReturnsUnknown()
    {
        CollectionCategoryResolver.Resolve(typeof(string)).Should().Be(CollectionCategory.Unknown);
    }

    [Fact]
    public void Resolve_NonCollectionClass_ReturnsUnknown()
    {
        CollectionCategoryResolver.Resolve(typeof(object)).Should().Be(CollectionCategory.Unknown);
        CollectionCategoryResolver.Resolve(typeof(int)).Should().Be(CollectionCategory.Unknown);
        CollectionCategoryResolver.Resolve(typeof(DateTime)).Should().Be(CollectionCategory.Unknown);
    }

    [Fact]
    public void Resolve_IsCached_ReturnsSameResult()
    {
        var first = CollectionCategoryResolver.Resolve(typeof(List<int>));
        var second = CollectionCategoryResolver.Resolve(typeof(List<int>));
        first.Should().Be(second);
    }
}
