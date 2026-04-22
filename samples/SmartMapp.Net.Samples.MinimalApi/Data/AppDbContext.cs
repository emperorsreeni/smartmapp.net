using Microsoft.EntityFrameworkCore;
using SmartMapp.Net.Samples.MinimalApi.Models;

namespace SmartMapp.Net.Samples.MinimalApi.Data;

/// <summary>
/// EF Core InMemory <see cref="DbContext"/> hosting the three sample entities used by
/// the Minimal API scenarios. InMemory is a sample-only dependency (spec §S8-T10 Constraints
/// bullet 1) and is NOT a runtime dependency of SmartMapp.Net itself.
/// </summary>
public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

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
