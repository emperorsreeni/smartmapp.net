using SmartMapp.Net.Samples.Console.Models;

namespace SmartMapp.Net.Samples.Console.Fixtures;

/// <summary>
/// Deterministic fixture data shared across scenarios so sample output stays stable for CI
/// stdout-substring assertions in <c>ConsoleSampleSmokeTests</c>.
/// </summary>
internal static class SampleData
{
    internal static Customer Customer() => new()
    {
        Id = 42,
        // Deliberate leading / trailing whitespace — scenario 6 (blueprint class) uses the
        // OrderBlueprint.OnMapped hook to trim it, making the hook's effect visible in output.
        Name = "  Alice Smith  ",
        Email = "alice@example.com",
        Address = new Address
        {
            Street = "221B Baker Street",
            City = "London",
            PostalCode = "NW1 6XE",
        },
    };

    internal static List<OrderLine> OrderLines() => new()
    {
        new OrderLine { Sku = "BOOK-1984", Quantity = 2, UnitPrice = 12.50m },
        new OrderLine { Sku = "MUG-CLASSIC", Quantity = 1, UnitPrice = 8.00m },
        new OrderLine { Sku = "PEN-BLUE", Quantity = 10, UnitPrice = 1.25m },
    };

    internal static Order Order() => new()
    {
        Id = 1001,
        PlacedAt = new DateTime(2025, 4, 21, 10, 15, 0, DateTimeKind.Utc),
        Customer = Customer(),
        Lines = OrderLines(),
    };
}
