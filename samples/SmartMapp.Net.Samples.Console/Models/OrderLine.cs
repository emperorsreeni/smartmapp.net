namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Origin-side line item used by the collection-mapping scenario.
/// </summary>
public sealed class OrderLine
{
    public string Sku { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

/// <summary>
/// Target DTO for <see cref="OrderLine"/>.
/// </summary>
public sealed class OrderLineDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
