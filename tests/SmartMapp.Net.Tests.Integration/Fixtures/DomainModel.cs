// SPDX-License-Identifier: MIT
// Sprint 8 · S8-T11 — shared domain fixtures for the integration suite. Kept independent from
// the MinimalApi sample's models so these tests don't inadvertently exercise sample-private
// semantics (the sample's entities use EF-friendly mutable property patterns; the integration
// fixtures use the same shape but own their own namespace).

namespace SmartMapp.Net.Tests.Integration.Fixtures;

public sealed class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
}

public sealed class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}

public sealed class Order
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new();
}

public sealed class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class OrderListDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
}

public sealed class OrderDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
}

public sealed class OrderLineDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

// Dashboard composition fixtures — used by the Compose suite. Three distinct origin types so
// the 2- and 3-origin dispatch paths can be exercised against a realistic shape.
public sealed class UserProfile
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class OrderSummary
{
    public int OpenOrders { get; set; }
    public decimal LifetimeValue { get; set; }
}

public sealed class CompanyInfo
{
    public string CompanyName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
}

public sealed class DashboardViewModel
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int OpenOrders { get; set; }
    public decimal LifetimeValue { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Plan { get; set; } = string.Empty;
}
