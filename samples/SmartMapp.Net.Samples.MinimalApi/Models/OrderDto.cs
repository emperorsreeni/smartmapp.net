namespace SmartMapp.Net.Samples.MinimalApi.Models;

/// <summary>
/// Detail DTO for <c>/orders/{id}</c>. Populated via <c>ISculptor.Map&lt;Order, OrderDto&gt;</c>
/// so the DI-resolved <c>TaxCalculatorProvider</c> can contribute the <see cref="Tax"/> and
/// <see cref="Total"/> members per spec §11.4 / §S8-T04.
/// </summary>
public sealed class OrderDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>Sum of <c>Quantity × UnitPrice</c> across <see cref="Lines"/>.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>Tax amount produced by the DI-resolved <c>TaxCalculatorProvider</c>.</summary>
    public decimal Tax { get; set; }

    /// <summary>Grand total (<see cref="Subtotal"/> + <see cref="Tax"/>).</summary>
    public decimal Total { get; set; }

    public List<OrderLineDto> Lines { get; set; } = new();
}

/// <summary>
/// Detail DTO for a single line item — straight convention-mapped from <see cref="OrderLine"/>.
/// </summary>
public sealed class OrderLineDto
{
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
