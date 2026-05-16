// SPDX-License-Identifier: MIT
// Sprint 8 · S8-T12 — shared fixtures for the Sprint 8 benchmark suite. Kept in one file so
// the six benchmark classes don't duplicate DTO shapes; each class builds its own sculptor
// in [GlobalSetup] to isolate the cold-forge cost from the per-call measurement loop.

using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Benchmarks.Sprint8;

public sealed class FlatSource
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public Guid ExternalId { get; set; }
    public string Country { get; set; } = string.Empty;
}

public sealed class FlatTarget
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public Guid ExternalId { get; set; }
    public string Country { get; set; } = string.Empty;
}

public sealed class FlatBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan) => plan.Bind<FlatSource, FlatTarget>();
}

// Three-level nested graph used by NestedMappingBenchmark and SelectAsProjectionBenchmark.
public sealed class NestedOrder
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public NestedCustomer Customer { get; set; } = new();
}

public sealed class NestedCustomer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public NestedAddress Address { get; set; } = new();
}

public sealed class NestedAddress
{
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

public sealed class NestedOrderFlatDto
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public int CustomerId { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
    public string CustomerAddressCountry { get; set; } = string.Empty;
    public string CustomerAddressPostalCode { get; set; } = string.Empty;
}

public sealed class NestedBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan) => plan.Bind<NestedOrder, NestedOrderFlatDto>();
}

// Collection element fixtures.
public sealed class Item
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public sealed class ItemDto
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public sealed class ItemBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan) => plan.Bind<Item, ItemDto>();
}

// Forge-benchmark pair generator: builds N distinct types via closed generics so ForgeBenchmark
// can measure the cost of Forge() over a realistic pair count (spec Acceptance bullet 2
// "Forge for 100 pairs").
public sealed class ForgeSourceA { public int Id { get; set; } public string Value { get; set; } = ""; }
public sealed class ForgeSourceB { public int Id { get; set; } public string Value { get; set; } = ""; }
public sealed class ForgeSourceC { public int Id { get; set; } public string Value { get; set; } = ""; }
public sealed class ForgeTargetA { public int Id { get; set; } public string Value { get; set; } = ""; }
public sealed class ForgeTargetB { public int Id { get; set; } public string Value { get; set; } = ""; }
public sealed class ForgeTargetC { public int Id { get; set; } public string Value { get; set; } = ""; }

public static class Fixtures
{
    public static FlatSource CreateFlatSource() => new()
    {
        Id = 42, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com",
        Age = 33, Balance = 1234.56m, CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        IsActive = true, ExternalId = Guid.NewGuid(), Country = "UK",
    };

    public static NestedOrder CreateNestedOrder() => new()
    {
        Id = 1, Total = 99.99m,
        Customer = new NestedCustomer
        {
            Id = 7, FirstName = "Alice", LastName = "Smith",
            Address = new NestedAddress { City = "London", Country = "UK", PostalCode = "NW1" },
        },
    };

    public static List<Item> CreateItems(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Item { Id = i, Sku = $"SKU-{i:D5}", Price = i * 1.25m, Quantity = i % 10 })
            .ToList();
}
