namespace SmartMapp.Net.Samples.MinimalApi.Models;

/// <summary>
/// EF Core entity representing an order aggregate.
/// </summary>
public sealed class Order
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = new();
    public List<OrderLine> Lines { get; set; } = new();
}

/// <summary>
/// EF Core entity representing a single line item on an <see cref="Order"/>.
/// </summary>
public sealed class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
