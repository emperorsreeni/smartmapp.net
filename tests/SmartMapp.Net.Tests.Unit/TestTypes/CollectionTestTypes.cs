using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace SmartMapp.Net.Tests.Unit.TestTypes;

// ── Simple element types for collection tests ──

public class OrderItem
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// ── Types with collection properties ──

public class OrderWithLines
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<OrderItem> Lines { get; set; } = new();
}

public class OrderWithLinesDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<OrderItemDto> Lines { get; set; } = new();
}

public class OrderWithArray
{
    public int Id { get; set; }
    public int[] Tags { get; set; } = Array.Empty<int>();
}

public class OrderWithArrayDto
{
    public int Id { get; set; }
    public int[] Tags { get; set; } = Array.Empty<int>();
}

public class OrderWithComplexArray
{
    public int Id { get; set; }
    public OrderItem[] Items { get; set; } = Array.Empty<OrderItem>();
}

public class OrderWithComplexArrayDto
{
    public int Id { get; set; }
    public OrderItemDto[] Items { get; set; } = Array.Empty<OrderItemDto>();
}

// ── Interface collection targets ──

public class OrderWithICollection
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class OrderWithICollectionDto
{
    public int Id { get; set; }
    public ICollection<int> Values { get; set; } = new List<int>();
}

public class OrderWithIReadOnlyList
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class OrderWithIReadOnlyListDto
{
    public int Id { get; set; }
    public IReadOnlyList<int> Values { get; set; } = new List<int>();
}

// ── HashSet types ──

public class TagCloud
{
    public int Id { get; set; }
    public HashSet<string> Tags { get; set; } = new();
}

public class TagCloudDto
{
    public int Id { get; set; }
    public HashSet<string> Tags { get; set; } = new();
}

// ── Complex-element HashSet types (S5-T05) ──

public class ComplexHashSetOrigin
{
    public int Id { get; set; }
    public HashSet<OrderItem> Items { get; set; } = new();
}

public class ComplexHashSetDto
{
    public int Id { get; set; }
    public HashSet<OrderItemDto> Items { get; set; } = new();
}

// ── Complex nested collection types (S5-T09) ──

public class DictWithComplexListOrigin
{
    public int Id { get; set; }
    public Dictionary<string, List<OrderItem>> Groups { get; set; } = new();
}

public class DictWithComplexListDto
{
    public int Id { get; set; }
    public Dictionary<string, List<OrderItemDto>> Groups { get; set; } = new();
}

// ── Dictionary types ──

public class Metadata
{
    public int Id { get; set; }
    public Dictionary<string, int> Properties { get; set; } = new();
}

public class MetadataDto
{
    public int Id { get; set; }
    public Dictionary<string, int> Properties { get; set; } = new();
}

public class ComplexDictOrigin
{
    public int Id { get; set; }
    public Dictionary<string, OrderItem> Items { get; set; } = new();
}

public class ComplexDictDto
{
    public int Id { get; set; }
    public Dictionary<string, OrderItemDto> Items { get; set; } = new();
}

// ── Immutable collection types ──

public class ImmutableOrder
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class ImmutableOrderDto
{
    public int Id { get; set; }
    public ImmutableList<int> Values { get; set; } = ImmutableList<int>.Empty;
}

public class ImmutableArrayOrder
{
    public int Id { get; set; }
    public int[] Values { get; set; } = Array.Empty<int>();
}

public class ImmutableArrayOrderDto
{
    public int Id { get; set; }
    public ImmutableArray<int> Values { get; set; }
}

// ── Observable / ReadOnly collection types ──

public class ObservableOrder
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class ObservableOrderDto
{
    public int Id { get; set; }
    public ObservableCollection<int> Values { get; set; } = new();
}

public class ReadOnlyOrder
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class ReadOnlyOrderDto
{
    public int Id { get; set; }
    public ReadOnlyCollection<int> Values { get; set; } = new List<int>().AsReadOnly();
}

// ── Nested collections ──

public class NestedListOrder
{
    public int Id { get; set; }
    public List<List<int>> Matrix { get; set; } = new();
}

public class NestedListOrderDto
{
    public int Id { get; set; }
    public List<List<int>> Matrix { get; set; } = new();
}

// ── Flattening test types ──

public class FlattenOrigin
{
    public int Id { get; set; }
    public FlattenCustomer Customer { get; set; } = new();
}

public class FlattenCustomer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public FlattenAddress Address { get; set; } = new();
}

public class FlattenAddress
{
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
}

public class FlattenTargetDto
{
    public int Id { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
    public string CustomerAddressStreet { get; set; } = string.Empty;
    public string CustomerAddressZipCode { get; set; } = string.Empty;
}

// ── Unflatten test types (reverse of above) ──

public class UnflattenOriginDto
{
    public int Id { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
}

public class UnflattenTarget
{
    public int Id { get; set; }
    public FlattenCustomer Customer { get; set; } = new();
}

// ── Dictionary ↔ Object mapping types ──

public class PersonForDict
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

// ── ValueTuple ↔ Object mapping types ──

public class PersonForTuple
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

// ── Complex-element IEnumerable (S5-T03) ──

public class EnumerableComplexOrder
{
    public int Id { get; set; }
    public IEnumerable<OrderItem> Items { get; set; } = Enumerable.Empty<OrderItem>();
}

public class EnumerableComplexOrderDto
{
    public int Id { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

// ── Complex-element ImmutableList (S5-T08) ──

public class ImmutableComplexOrder
{
    public int Id { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class ImmutableComplexOrderDto
{
    public int Id { get; set; }
    public ImmutableList<OrderItemDto> Items { get; set; } = ImmutableList<OrderItemDto>.Empty;
}

// ── Complex-element ReadOnlyCollection (S5-T08) ──

public class ReadOnlyComplexOrder
{
    public int Id { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class ReadOnlyComplexOrderDto
{
    public int Id { get; set; }
    public ReadOnlyCollection<OrderItemDto> Items { get; set; } = new List<OrderItemDto>().AsReadOnly();
}

// ── Dictionary with duplicate-producing key mapping (S5-T06) ──

public class DuplicateKeyDictOrigin
{
    public int Id { get; set; }
    public Dictionary<string, int> Data { get; set; } = new();
}

public class DuplicateKeyDictDto
{
    public int Id { get; set; }
    public Dictionary<string, int> Data { get; set; } = new();
}

// ── IEnumerable source types ──

public class EnumerableOrder
{
    public int Id { get; set; }
    public IEnumerable<int> Values { get; set; } = Enumerable.Empty<int>();
}

public class EnumerableOrderDto
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class EnumerableToArrayDto
{
    public int Id { get; set; }
    public int[] Values { get; set; } = Array.Empty<int>();
}

// ── Large collection types (edge case tests) ──

public class LargeListOrigin
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class LargeListDto
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

// ── 3-level nested ──

public class ThreeLevelNestedOrigin
{
    public int Id { get; set; }
    public List<List<List<int>>> Cube { get; set; } = new();
}

public class ThreeLevelNestedDto
{
    public int Id { get; set; }
    public List<List<List<int>>> Cube { get; set; } = new();
}

// ── Dict with List values (nested) ──

public class DictWithListOrigin
{
    public int Id { get; set; }
    public Dictionary<string, List<int>> Groups { get; set; } = new();
}

public class DictWithListDto
{
    public int Id { get; set; }
    public Dictionary<string, List<int>> Groups { get; set; } = new();
}

// ── Null element collection types ──

public class NullElementsOrigin
{
    public int Id { get; set; }
    public List<string?> Items { get; set; } = new();
}

public class NullElementsDto
{
    public int Id { get; set; }
    public List<string?> Items { get; set; } = new();
}

// ── Empty DTO for edge case tests ──

public class EmptyDto { }

// ── Jagged array types (S5-T09) ──

public class JaggedArrayOrigin
{
    public int Id { get; set; }
    public int[][] Matrix { get; set; } = Array.Empty<int[]>();
}

public class JaggedArrayDto
{
    public int Id { get; set; }
    public int[][] Matrix { get; set; } = Array.Empty<int[]>();
}

// ── List of arrays (S5-T09) ──

public class ListOfArrayOrigin
{
    public int Id { get; set; }
    public List<OrderItem[]> Groups { get; set; } = new();
}

public class ListOfArrayDto
{
    public int Id { get; set; }
    public List<OrderItemDto[]> Groups { get; set; } = new();
}

public class ListOfIntArrayOrigin
{
    public int Id { get; set; }
    public List<int[]> Groups { get; set; } = new();
}

public class ListOfIntArrayDto
{
    public int Id { get; set; }
    public List<int[]> Groups { get; set; } = new();
}

// ── Interface dictionary targets (S5-T06) ──

public class IDictionaryTargetDto
{
    public int Id { get; set; }
    public IDictionary<string, int> Properties { get; set; } = new Dictionary<string, int>();
}

public class IReadOnlyDictionaryTargetDto
{
    public int Id { get; set; }
    public IReadOnlyDictionary<string, int> Properties { get; set; } = new Dictionary<string, int>();
}

// ── ISet<T> target (S5-T05) ──

public class ISetTargetDto
{
    public int Id { get; set; }
    public ISet<string> Tags { get; set; } = new HashSet<string>();
}

// ── IImmutableList<T> target (S5-T08) ──

public class IImmutableListTargetDto
{
    public int Id { get; set; }
    public System.Collections.Immutable.IImmutableList<int> Values { get; set; } = System.Collections.Immutable.ImmutableList<int>.Empty;
}

// ── IReadOnlyCollection<T> target (S5-T04) ──

public class IReadOnlyCollectionTargetDto
{
    public int Id { get; set; }
    public IReadOnlyCollection<int> Values { get; set; } = new List<int>();
}

// ── IReadOnlyCollection<OrderDto> complex-element target (S5-T04) ──

public class OrderWithIReadOnlyCollectionComplex
{
    public int Id { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class IReadOnlyCollectionComplexTargetDto
{
    public int Id { get; set; }
    public IReadOnlyCollection<OrderItemDto> Items { get; set; } = new List<OrderItemDto>();
}

// ── Unflatten target with null intermediates (S5-T07) ──

public class UnflattenTargetNullInit
{
    public int Id { get; set; }
    public FlattenCustomer? Customer { get; set; }
}

// ── Full 3-level unflatten origin (S5-T07) ──

public class Unflatten3LevelOriginDto
{
    public int Id { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
    public string CustomerAddressStreet { get; set; } = string.Empty;
    public string CustomerAddressZipCode { get; set; } = string.Empty;
}

// ── string[] array types (S5-T01) ──

public class StringArrayOrigin
{
    public int Id { get; set; }
    public string[] Names { get; set; } = Array.Empty<string>();
}

public class StringArrayDto
{
    public int Id { get; set; }
    public string[] Names { get; set; } = Array.Empty<string>();
}

// ── IList<T> target (S5-T02) ──

public class IListTargetDto
{
    public int Id { get; set; }
    public IList<int> Values { get; set; } = new List<int>();
}

// ── HashSet<int> for duplicate test (S5-T05) ──

public class IntHashSetOrigin
{
    public int Id { get; set; }
    public List<int> Values { get; set; } = new();
}

public class IntHashSetDto
{
    public int Id { get; set; }
    public HashSet<int> Values { get; set; } = new();
}

// ── 2-level flatten types (S5-T07) ──

public class Flatten2LevelTargetDto
{
    public int Id { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
}

// ── Unnamed tuple (S5-T10) ──

public class PersonForUnnamedTuple
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

// ── Dictionary key-mapping types (S5-T06) ──

public class IntKeyDictOrigin
{
    public int Id { get; set; }
    public Dictionary<int, string> Lookup { get; set; } = new();
}

public class LongKeyDictDto
{
    public int Id { get; set; }
    public Dictionary<long, string> Lookup { get; set; } = new();
}

public class IntKeyIntValueDictOrigin
{
    public int Id { get; set; }
    public Dictionary<int, int> Data { get; set; } = new();
}

public class LongKeyLongValueDictDto
{
    public int Id { get; set; }
    public Dictionary<long, long> Data { get; set; } = new();
}

// ── Mixed flatten/unflatten with collection (S5-T07) ──

public class MixedFlattenOrigin
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FlattenCustomer Customer { get; set; } = new();
    public List<int> Tags { get; set; } = new();
}

public class MixedFlattenTargetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public List<int> Tags { get; set; } = new();
}
