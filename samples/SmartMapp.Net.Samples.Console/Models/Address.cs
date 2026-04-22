namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Nested value object used to demonstrate <c>Customer.Address.City</c> → <c>CustomerAddressCity</c>
/// flattening in the "Flattening" scenario.
/// </summary>
public sealed class Address
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}
