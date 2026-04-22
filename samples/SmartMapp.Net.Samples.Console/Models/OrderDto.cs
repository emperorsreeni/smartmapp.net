namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Target DTO for <see cref="Order"/>. <see cref="Total"/> is populated by an inline
/// <c>.From(...)</c> expression (computed projection); <see cref="CustomerName"/> and
/// <see cref="CustomerAddressCity"/> exercise flattening; <see cref="Lines"/> is a
/// nested-collection projection.
/// </summary>
public sealed class OrderDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<OrderLineDto> Lines { get; set; } = new();
}
