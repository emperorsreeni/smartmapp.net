// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T11 — exercises the shared <see cref="AppHostFixture"/> from the DI category.
/// Pairs with <see cref="SmartMapp.Net.Tests.Integration.EntityFrameworkCore.SharedHostSelectAsTests"/>
/// in the same xUnit collection so the spec §S8-T11 Technical Considerations bullet 1
/// ("share <c>AppHostFixture</c> across related test classes without re-seeding") is exercised —
/// both classes consume the same forged sculptor + seeded database, and the fixture is
/// instantiated exactly once for the whole collection run.
/// </summary>
[Collection(AppHostCollection.Name)]
public sealed class SharedHostMapperTests
{
    private readonly AppHostFixture _host;
    public SharedHostMapperTests(AppHostFixture host) { _host = host; }

    [Fact]
    public void SharedSculptor_Resolves_AndMapsSeededOrder()
    {
        using var scope = _host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = db.Orders.Include(o => o.Customer).Include(o => o.Lines).First(o => o.Id == 1);

        var dto = _host.Sculptor.Map<Order, OrderDto>(order);

        dto.CustomerFirstName.Should().Be("Alice");
        dto.Subtotal.Should().Be(33.00m);
        dto.Tax.Should().Be(3.30m, "the shared ITaxRateSource (10%) is visible to TaxCalculatorProvider.");
    }

    [Fact]
    public void SharedProvider_IMapper_Resolves_SameInstanceAsSibling()
    {
        // Proves the collection's fixture-scoped ServiceProvider is the same one the sibling
        // class resolves from — the IMapper<,> instance pulled here is identical to the one
        // pulled in SharedHostSelectAsTests (both are singletons on the shared root).
        var mapper1 = _host.RootServices.GetRequiredService<IMapper<Order, OrderListDto>>();
        var mapper2 = _host.RootServices.GetRequiredService<IMapper<Order, OrderListDto>>();
        mapper1.Should().BeSameAs(mapper2,
            "singleton IMapper<,> across the shared fixture's root provider — no re-resolve per test.");
    }
}
