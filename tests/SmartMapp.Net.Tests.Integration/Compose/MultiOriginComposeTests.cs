// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.Compose;

/// <summary>
/// Sprint 8 · S8-T11 — integration coverage for <see cref="ISculptor.Compose{TTarget}(object[])"/>
/// routed through a DI-registered sculptor. Focus is end-to-end dispatch against a realistic
/// 3-origin dashboard composition (spec §8.11 / §S8-T08 + §S8-T11 Acceptance bullet 8).
/// </summary>
public sealed class MultiOriginComposeTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSculptor(options =>
        {
            options.Compose<DashboardViewModel>(c => c
                .FromOrigin<UserProfile>()
                .FromOrigin<OrderSummary>()
                .FromOrigin<CompanyInfo>());
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Compose_TwoOrigins_MergesPropertiesFromBoth()
    {
        using var sp = BuildProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var user = new UserProfile { UserId = 1, DisplayName = "Alice", Email = "alice@example.com" };
        var summary = new OrderSummary { OpenOrders = 3, LifetimeValue = 500m };

        var dto = sculptor.Compose<DashboardViewModel>(user, summary);

        dto.UserId.Should().Be(1);
        dto.DisplayName.Should().Be("Alice");
        dto.OpenOrders.Should().Be(3);
        dto.LifetimeValue.Should().Be(500m);
        dto.CompanyName.Should().BeEmpty("the Company slot was not supplied.");
    }

    [Fact]
    public void Compose_ThreeOrigins_MergesPropertiesFromAll()
    {
        using var sp = BuildProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var user = new UserProfile { UserId = 2, DisplayName = "Bob", Email = "bob@example.com" };
        var summary = new OrderSummary { OpenOrders = 7, LifetimeValue = 1200m };
        var company = new CompanyInfo { CompanyName = "Contoso", Plan = "Enterprise" };

        var dto = sculptor.Compose<DashboardViewModel>(user, summary, company);

        dto.CompanyName.Should().Be("Contoso");
        dto.Plan.Should().Be("Enterprise");
        dto.UserId.Should().Be(2);
        dto.OpenOrders.Should().Be(7);
    }

    [Fact]
    public void Compose_OriginOrderIndependent_ProducesSameDto()
    {
        using var sp = BuildProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var user = new UserProfile { UserId = 3, DisplayName = "Carol", Email = "c@x.com" };
        var summary = new OrderSummary { OpenOrders = 1, LifetimeValue = 10m };
        var company = new CompanyInfo { CompanyName = "Acme", Plan = "Basic" };

        var forward = sculptor.Compose<DashboardViewModel>(user, summary, company);
        var reverse = sculptor.Compose<DashboardViewModel>(company, summary, user);

        reverse.Should().BeEquivalentTo(forward,
            "declaration order (not caller order) drives Compose — spec §S8-T08 Acceptance bullet 3.");
    }

    [Fact]
    public void Compose_SingleOrigin_ParityWithMap_WhenMapperIsRegistered()
    {
        // Spec §S8-T08 Acceptance bullet 6: "Single-origin Compose<T>(x) identical to Map<X, T>(x)"
        // when no composition blueprint is registered for TTarget. Here we register OrderListDto
        // via the normal Bind<,> path and verify Compose<T>(order) === Map<Order, T>(order).
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>());
        using var sp = services.BuildServiceProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var order = new Order
        {
            Id = 1, PlacedAt = DateTime.UnixEpoch,
            Customer = new Customer
            {
                FirstName = "Alice", LastName = "Smith",
                Address = new Address { City = "London" },
            },
        };

        var viaMap = sculptor.Map<Order, OrderListDto>(order);
        var viaCompose = sculptor.Compose<OrderListDto>(order);

        viaCompose.Should().BeEquivalentTo(viaMap);
    }

    [Fact]
    public void Compose_ViaDi_HostedSculptor_RespectsSingletonIdentity()
    {
        using var sp = BuildProvider();
        var a = sp.GetRequiredService<ISculptor>();
        var b = sp.GetRequiredService<ISculptor>();

        a.Should().BeSameAs(b, "Singleton sculptor lifetime is preserved across Compose dispatches.");

        var user = new UserProfile { UserId = 42, DisplayName = "Dave", Email = "d@x.com" };
        var summary = new OrderSummary { OpenOrders = 2, LifetimeValue = 99m };

        var dtoA = a.Compose<DashboardViewModel>(user, summary);
        var dtoB = b.Compose<DashboardViewModel>(user, summary);

        dtoB.Should().BeEquivalentTo(dtoA, "the same sculptor instance produces equivalent composed DTOs.");
    }
}
