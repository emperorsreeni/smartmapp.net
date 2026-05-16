namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Top-level origin aggregate used by the inline-bind, blueprint, collections, and
/// bidirectional scenarios.
/// </summary>
public sealed class Order
{
    public int Id { get; init; }
    public DateTime PlacedAt { get; init; }
    public Customer Customer { get; init; } = new();
    public List<OrderLine> Lines { get; init; } = new();
}
