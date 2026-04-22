// SPDX-License-Identifier: MIT
using Microsoft.EntityFrameworkCore;

namespace SmartMapp.Net.Tests.Integration.Fixtures;

/// <summary>
/// Deterministic EF Core InMemory seeding helper used across the integration suite. Exposes a
/// single entry point (<see cref="CreateSeeded"/>) that any test class can call to spin up an
/// isolated <see cref="AppDbContext"/> pre-loaded with the canonical 3-customer / 3-order /
/// 5-line fixture. Each call uses a fresh <see cref="Guid"/>-keyed InMemory database so
/// concurrent tests never collide.
/// </summary>
public static class OrderSeedBuilder
{
    public static AppDbContext CreateSeeded(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? $"s8t11-{Guid.NewGuid():N}")
            .Options;
        var ctx = new AppDbContext(options);
        Seed(ctx);
        return ctx;
    }

    public static void Seed(AppDbContext db)
    {
        if (db.Orders.Any()) return;

        var alice = new Customer
        {
            Id = 1, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com",
            Address = new Address { Street = "221B Baker Street", City = "London", PostalCode = "NW1 6XE" },
        };
        var bob = new Customer
        {
            Id = 2, FirstName = "Bob", LastName = "Brown", Email = "bob@example.com",
            Address = new Address { Street = "742 Evergreen Terrace", City = "Springfield", PostalCode = "49007" },
        };
        var carol = new Customer
        {
            Id = 3, FirstName = "Carol", LastName = "White", Email = "carol@example.com",
            Address = new Address { Street = "10 Downing Street", City = "London", PostalCode = "SW1A 2AA" },
        };

        db.Customers.AddRange(alice, bob, carol);

        var placedAt = new DateTime(2025, 4, 21, 10, 15, 0, DateTimeKind.Utc);

        db.Orders.AddRange(
            new Order
            {
                Id = 1, PlacedAt = placedAt, CustomerId = alice.Id, Customer = alice,
                Lines =
                {
                    new OrderLine { Id = 1, OrderId = 1, Sku = "BOOK-1984", Quantity = 2, UnitPrice = 12.50m },
                    new OrderLine { Id = 2, OrderId = 1, Sku = "MUG-CLASSIC", Quantity = 1, UnitPrice = 8.00m },
                },
            },
            new Order
            {
                Id = 2, PlacedAt = placedAt.AddHours(1), CustomerId = bob.Id, Customer = bob,
                Lines =
                {
                    new OrderLine { Id = 3, OrderId = 2, Sku = "PEN-BLUE", Quantity = 10, UnitPrice = 1.25m },
                },
            },
            new Order
            {
                Id = 3, PlacedAt = placedAt.AddHours(2), CustomerId = carol.Id, Customer = carol,
                Lines =
                {
                    new OrderLine { Id = 4, OrderId = 3, Sku = "KEYBOARD-MX", Quantity = 1, UnitPrice = 149.99m },
                    new OrderLine { Id = 5, OrderId = 3, Sku = "CABLE-USB-C", Quantity = 2, UnitPrice = 4.50m },
                },
            });

        db.SaveChanges();
    }
}
