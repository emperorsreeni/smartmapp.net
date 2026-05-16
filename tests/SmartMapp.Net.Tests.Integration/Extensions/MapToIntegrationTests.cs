// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Extensions;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.Extensions;

/// <summary>
/// Sprint 8 · S8-T11 — integration coverage for the <c>MapTo&lt;T&gt;()</c> / ambient
/// <c>SelectAs&lt;T&gt;()</c> extension methods when the ambient sculptor is installed by
/// <c>AddSculptor</c> rather than a raw <c>SculptorAmbient.Set</c> call.
/// </summary>
public sealed class MapToIntegrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>());
        return services.BuildServiceProvider();
    }

    private static Order SampleOrder() => new()
    {
        Id = 99,
        Customer = new Customer { Id = 1, FirstName = "Ada", LastName = "Lovelace", Address = new Address { City = "London" } },
        Lines = { new OrderLine { Sku = "A", Quantity = 2, UnitPrice = 5m } },
    };

    [Fact]
    public void MapTo_AmbientSculptor_InstalledByAddSculptor_Works()
    {
        using var sp = BuildProvider();
        _ = sp.GetRequiredService<ISculptor>(); // triggers ambient install

        var dto = SampleOrder().MapTo<OrderListDto>();

        dto.Id.Should().Be(99);
        dto.CustomerAddressCity.Should().Be("London");
    }

    [Fact]
    public void MapTo_ExplicitSculptor_FromContainer_Works()
    {
        using var sp = BuildProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var dto = SampleOrder().MapTo<OrderDto>(sculptor);

        dto.Subtotal.Should().Be(10m);
        dto.Tax.Should().Be(1m);
    }

    [Fact]
    public async Task AmbientSelectAs_OverEfQueryable_TranslatesAndRoundTrips()
    {
        using var sp = BuildProvider();
        _ = sp.GetRequiredService<ISculptor>(); // install ambient
        using var db = OrderSeedBuilder.CreateSeeded();
        var ct = TestContext.Current.CancellationToken;

        var dtos = await db.Orders
            .OrderBy(o => o.Id)
            .SelectAs<OrderListDto>()
            .ToListAsync(ct);

        dtos.Should().HaveCount(3);
        dtos[0].CustomerFirstName.Should().Be("Alice");
    }

    [Fact]
    public void MapTo_AcrossConcurrentScopes_NoAmbientLeakage()
    {
        // Spec §S8-T07 parallel guard at the integration level: 50 concurrent scopes each resolve
        // their own sculptor, then call .MapTo<T>() — every call must see a non-null ambient and
        // produce a correct DTO, with no interference from neighbouring tasks.
        using var sp = BuildProvider();
        _ = sp.GetRequiredService<ISculptor>();

        Parallel.For(0, 50, i =>
        {
            using var scope = sp.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<ISculptor>();
            var order = SampleOrder();
            order.Id = i;
            var dto = order.MapTo<OrderListDto>();
            dto.Id.Should().Be(i);
        });
    }
}
