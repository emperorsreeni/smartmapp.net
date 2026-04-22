namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Target DTO for <see cref="Customer"/>. Same-name flat members are auto-linked by convention;
/// <see cref="AddressCity"/> / <see cref="AddressPostalCode"/> demonstrate flattening.
/// </summary>
public sealed class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
    public string AddressPostalCode { get; set; } = string.Empty;
}
