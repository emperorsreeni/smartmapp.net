// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T11 — integration coverage for <see cref="IMapper{TOrigin,TTarget}"/> resolution
/// against a real DI container populated via <c>AddSculptor</c>. Focus is end-to-end: the
/// resolved mapper actually produces correct DTOs across every blueprint-declared pair.
/// </summary>
public sealed class MapperResolutionTests
{
    private static Order SampleOrder(int id = 1) => new()
    {
        Id = id,
        PlacedAt = DateTime.UnixEpoch.AddHours(id),
        Customer = new Customer
        {
            Id = id, FirstName = "Alice", LastName = "Smith", Email = "alice@example.com",
            Address = new Address { City = "London" },
        },
        Lines =
        {
            new OrderLine { Sku = "A", Quantity = 2, UnitPrice = 5m },
            new OrderLine { Sku = "B", Quantity = 1, UnitPrice = 10m },
        },
    };

    private static ServiceProvider BuildProvider(
        ServiceLifetime lifetime = ServiceLifetime.Singleton,
        decimal taxRate = 0.10m)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(taxRate));
        services.AddSculptor(lifetime, o => o.UseBlueprint<IntegrationBlueprint>());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void IMapper_FlatListPair_ResolvesAndMaps()
    {
        using var sp = BuildProvider();

        var mapper = sp.GetRequiredService<IMapper<Order, OrderListDto>>();
        var dto = mapper.Map(SampleOrder(42));

        dto.Id.Should().Be(42);
        dto.CustomerFirstName.Should().Be("Alice");
        dto.CustomerAddressCity.Should().Be("London");
    }

    [Fact]
    public void IMapper_DetailPair_UsesDiResolvedProvider()
    {
        using var sp = BuildProvider(taxRate: 0.25m);

        var mapper = sp.GetRequiredService<IMapper<Order, OrderDto>>();
        var dto = mapper.Map(SampleOrder());

        // Subtotal = 2*5 + 1*10 = 20 → Tax@25% = 5.00
        dto.Subtotal.Should().Be(20m);
        dto.Tax.Should().Be(5m,
            "TaxCalculatorProvider must be resolved from DI with the registered 25% ITaxRateSource.");
        dto.Total.Should().Be(20m * 1.10m);
        dto.Lines.Should().HaveCount(2);
        dto.Lines[0].LineTotal.Should().Be(10m);
    }

    [Fact]
    public void IMapper_Singleton_SameInstanceAcrossResolves()
    {
        using var sp = BuildProvider();
        var a = sp.GetRequiredService<IMapper<Order, OrderListDto>>();
        var b = sp.GetRequiredService<IMapper<Order, OrderListDto>>();
        a.Should().BeSameAs(b);
    }

    [Fact]
    public void IMapper_Scoped_SameInScopeDifferentAcrossScopes()
    {
        using var sp = BuildProvider(ServiceLifetime.Scoped);

        IMapper<Order, OrderListDto> a1, a2, b1;
        using (var a = sp.CreateScope())
        {
            a1 = a.ServiceProvider.GetRequiredService<IMapper<Order, OrderListDto>>();
            a2 = a.ServiceProvider.GetRequiredService<IMapper<Order, OrderListDto>>();
        }
        using (var b = sp.CreateScope())
            b1 = b.ServiceProvider.GetRequiredService<IMapper<Order, OrderListDto>>();

        a1.Should().BeSameAs(a2);
        a1.Should().NotBeSameAs(b1);
    }

    [Fact]
    public void IMapper_UnregisteredPair_ThrowsWithActionableMessage()
    {
        using var sp = BuildProvider();

        var act = () => sp.GetRequiredService<IMapper<DateTime, Guid>>();

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        var message = (ex.InnerException?.Message ?? string.Empty) + " " + ex.Message;
        message.Should().Contain("No Blueprint is registered");
    }

    [Fact]
    public void IMapper_MapAll_ReturnsMaterializedList()
    {
        using var sp = BuildProvider();
        var mapper = sp.GetRequiredService<IMapper<Order, OrderListDto>>();

        var origins = new[] { SampleOrder(1), SampleOrder(2), SampleOrder(3) };
        var dtos = mapper.MapAll(origins);

        dtos.Should().HaveCount(3);
        dtos.Select(d => d.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }
}
