namespace SmartMapp.Net.Samples.MinimalApi.Models;

/// <summary>
/// Lightweight list-view DTO for <c>/orders</c>. Intentionally uses flat, server-translatable
/// member shapes so <c>db.Orders.SelectAs&lt;OrderListDto&gt;(sculptor).ToListAsync()</c>
/// yields a single EF Core SELECT with the customer join inlined — no N+1 lazy-loads.
/// </summary>
public sealed class OrderListDto
{
    public int Id { get; set; }
    public DateTime PlacedAt { get; set; }
    public string CustomerFirstName { get; set; } = string.Empty;
    public string CustomerLastName { get; set; } = string.Empty;
    public string CustomerAddressCity { get; set; } = string.Empty;
}
