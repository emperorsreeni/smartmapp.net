// SPDX-License-Identifier: MIT
using Microsoft.EntityFrameworkCore;

namespace SmartMapp.Net.Tests.Integration.Fixtures;

/// <summary>
/// EF Core InMemory <see cref="DbContext"/> for the integration test suite. A unique database
/// name is created per test class via <see cref="OrderSeedBuilder"/> so xUnit's per-class
/// parallelism doesn't bleed state.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Customer>().OwnsOne(c => c.Address);
        modelBuilder.Entity<Order>()
            .HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId);
    }
}
