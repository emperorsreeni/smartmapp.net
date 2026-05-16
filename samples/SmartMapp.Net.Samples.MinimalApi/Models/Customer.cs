namespace SmartMapp.Net.Samples.MinimalApi.Models;

/// <summary>
/// EF Core entity representing a customer. Uses mutable properties so EF Core's change
/// tracker can hydrate instances; DTOs below use <c>init</c>-only members instead.
/// </summary>
public sealed class Customer
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
}

/// <summary>
/// Nested value object used to demonstrate <c>Customer.Address.City</c> →
/// <c>CustomerAddressCity</c> flattening in the <c>OrderListDto</c> projection.
/// </summary>
public sealed class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}
