// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.EntityFrameworkCore;

/// <summary>
/// Sprint 8 · S8-T11 Acceptance bullet 5 — "verifies SQL shape via <c>ToQueryString()</c>".
/// The <see cref="SelectAsEfCoreTests"/> class covers <c>ToListAsync</c> round-trips against
/// EF InMemory (which returns a placeholder for <c>ToQueryString</c> — no SQL language), so
/// this companion class uses an in-process SQLite database to produce real SQL and asserts
/// the rewritten query is a single server-side <c>SELECT</c> that projects the DTO columns
/// directly (no entity-graph materialisation, no N+1).
/// </summary>
public sealed class SelectAsSqliteSqlShapeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ISculptor _sculptor;

    public SelectAsSqliteSqlShapeTests()
    {
        // `Data Source=:memory:` lives only while the connection stays open — keeping it on the
        // fixture means the schema + seed survive across every test method call. No file I/O,
        // no cross-process contention.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        OrderSeedBuilder.Seed(_db);

        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>());
        _sculptor = services.BuildServiceProvider().GetRequiredService<ISculptor>();
    }

    [Fact]
    public void SelectAs_ToQueryString_EmitsSingleServerSideSelect()
    {
        var query = _db.Orders.OrderBy(o => o.Id).SelectAs<OrderListDto>(_sculptor);

        var sql = query.ToQueryString();

        // Sanity — SQLite provider returned something non-placeholder.
        sql.Should().NotBeNullOrWhiteSpace();
        sql.Should().StartWith("SELECT", "the rewritten query must be a server-side projection.");

        // Flat DTO columns must appear in the SELECT list — proves the projection rewrites
        // client-side field-by-field rather than pulling the whole entity graph and mapping
        // in-process. Column names use the EF Core owned-entity convention: Address.City →
        // "Address_City" (not a nested join — owned types flatten into the parent table).
        sql.Should().Contain("\"Id\"");
        sql.Should().Contain("\"PlacedAt\"");
        sql.Should().Contain("\"FirstName\"", "flattened CustomerFirstName source column must be part of the SELECT list.");
        sql.Should().Contain("\"LastName\"");
        sql.Should().Contain("\"Address_City\"",
            "flattened CustomerAddressCity projects into the owned-entity's Address_City column under EF Core's default convention.");

        // AS-aliases prove the projection is rewriting per-column into the DTO shape (rather
        // than materialising Customer and mapping in-process).
        sql.Should().Contain("AS \"CustomerFirstName\"");
        sql.Should().Contain("AS \"CustomerAddressCity\"");

        // Joins must be inlined — one SELECT on Orders INNER JOIN Customers, no separate
        // round-trip for the navigation property.
        sql.Should().Contain("INNER JOIN \"Customers\"",
            "the server-side SELECT must join Customer inline rather than fetch it separately.");

        // Only one SELECT statement emitted — i.e. no N+1 subqueries.
        var selectOccurrences = sql.Split("SELECT", StringSplitOptions.None).Length - 1;
        selectOccurrences.Should().Be(1,
            $"expected exactly 1 top-level SELECT; saw {selectOccurrences}. SQL:\n{sql}");
    }

    [Fact]
    public async Task SelectAs_RoundTripsViaSqlite_WithCorrectFlattenedValues()
    {
        var ct = TestContext.Current.CancellationToken;

        var dtos = await _db.Orders.OrderBy(o => o.Id)
            .SelectAs<OrderListDto>(_sculptor)
            .ToListAsync(ct);

        dtos.Should().HaveCount(3);
        dtos[0].CustomerFirstName.Should().Be("Alice");
        dtos[0].CustomerAddressCity.Should().Be("London");
        dtos[1].CustomerFirstName.Should().Be("Bob");
        dtos[2].CustomerAddressCity.Should().Be("London");
    }

    [Fact]
    public void SelectAs_QueryableElementType_IsTargetDto_UnderSqlite()
    {
        var query = _db.Orders.SelectAs<Order, OrderListDto>(_sculptor);
        query.ElementType.Should().Be(typeof(OrderListDto),
            "the structural invariant from S8-T06 still holds for a relational provider.");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
