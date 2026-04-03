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
