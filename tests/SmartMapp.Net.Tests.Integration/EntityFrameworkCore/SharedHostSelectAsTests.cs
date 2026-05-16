// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.EntityFrameworkCore;

/// <summary>
/// Sprint 8 · S8-T11 — exercises the shared <see cref="AppHostFixture"/> from the EF Core
/// category. Pairs with <see cref="SmartMapp.Net.Tests.Integration.DependencyInjection.SharedHostMapperTests"/>
/// in the same xUnit collection: both resolve services from the same root provider and
/// query the same seeded InMemory database, proving spec §S8-T11 Technical Considerations
/// bullet 1 (single forge + single seed across related test classes).
/// </summary>
[Collection(AppHostCollection.Name)]
public sealed class SharedHostSelectAsTests
{
    private readonly AppHostFixture _host;
    public SharedHostSelectAsTests(AppHostFixture host) { _host = host; }

    [Fact]
    public async Task SharedSeed_SelectAs_ReturnsAllOrders()
    {
        using var scope = _host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ct = TestContext.Current.CancellationToken;

        var list = await db.Orders.OrderBy(o => o.Id)
            .SelectAs<OrderListDto>(_host.Sculptor)
            .ToListAsync(ct);

        list.Should().HaveCount(3,
            "the fixture's OrderSeedBuilder runs once; both this class and the DI sibling see the same 3 orders.");
    }

    [Fact]
    public async Task SharedSeed_AcrossScopes_IsStable()
    {
        // Two sequential scopes off the same shared root SP must see the same seed. Guards
        // against accidental per-test re-seeding (which would violate the shared-seed contract
        // the fixture is supposed to uphold).
        var ct = TestContext.Current.CancellationToken;

        int countA, countB;
        using (var scope = _host.CreateScope())
            countA = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Orders.CountAsync(ct);
        using (var scope = _host.CreateScope())
            countB = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Orders.CountAsync(ct);

        countA.Should().Be(countB);
        countA.Should().Be(3);
    }
}
