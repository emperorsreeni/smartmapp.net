namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Origin-side customer aggregate used by the zero-config and flattening scenarios.
/// </summary>
public sealed class Customer
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public Address Address { get; init; } = new();
}
