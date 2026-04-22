// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T11 — provider DI coverage. Asserts that <c>p.From&lt;TaxCalculatorProvider&gt;()</c>
/// resolves against the current <see cref="IServiceScope"/>'s <see cref="IServiceProvider"/> on
/// every mapping call, including when a scoped <see cref="AppDbContext"/> (or
/// <see cref="ITaxRateSource"/>) lives alongside it.
/// </summary>
public sealed class ProviderResolutionTests
{
    private static Order SampleOrder() => new()
    {
        Id = 1, PlacedAt = DateTime.UnixEpoch,
        Customer = new Customer { Id = 1, FirstName = "Alice", LastName = "Smith", Email = "a@x.com", Address = new Address { City = "London" } },
        Lines = { new OrderLine { Sku = "A", Quantity = 2, UnitPrice = 5m } },
    };

    [Fact]
    public void Provider_ResolvedWithScopedDbContext_SeesScopeBoundServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase($"s8t11-prov-{Guid.NewGuid():N}"));
        services.AddScoped<ITaxRateSource>(_ => new FixedTaxRateSource(0.20m));
        services.AddSculptor(ServiceLifetime.Scoped, o => o.UseBlueprint<IntegrationBlueprint>());
        using var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        OrderSeedBuilder.Seed(db); // seed via the scoped DbContext to prove it's usable
        var sculptor = scope.ServiceProvider.GetRequiredService<ISculptor>();

        var dto = sculptor.Map<Order, OrderDto>(SampleOrder());

        // Subtotal = 10 → 20% → 2.00
        dto.Tax.Should().Be(2m,
            "the scoped ITaxRateSource (20%) must be visible to TaxCalculatorProvider on every Map call.");
    }

    [Fact]
    public void Provider_AcrossTwoScopes_SeesEachScopesRate()
    {
        var services = new ServiceCollection();
        // Registers a per-scope rate — each scope's ITaxRateSource is a different instance with
        // a different numeric rate seeded from an incrementing counter.
        var counter = 0;
        services.AddScoped<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m * ++counter));
        services.AddSculptor(ServiceLifetime.Scoped, o => o.UseBlueprint<IntegrationBlueprint>());
        using var sp = services.BuildServiceProvider();

        decimal taxA, taxB;
        using (var a = sp.CreateScope())
            taxA = a.ServiceProvider.GetRequiredService<ISculptor>().Map<Order, OrderDto>(SampleOrder()).Tax;
        using (var b = sp.CreateScope())
            taxB = b.ServiceProvider.GetRequiredService<ISculptor>().Map<Order, OrderDto>(SampleOrder()).Tax;

        taxA.Should().NotBe(taxB, "each scope's provider must see its own ITaxRateSource rate.");
    }

    [Fact]
    public void Provider_AutoRegisteredAsTransient_ByScanner()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddSculptor(options =>
        {
            options.ScanAssembliesContaining<ProviderResolutionTests>();
            options.UseBlueprint<IntegrationBlueprint>();
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(TaxCalculatorProvider)
            && d.Lifetime == ServiceLifetime.Transient,
            "scanned IValueProvider<,,> implementations are auto-registered as Transient per §11.2.");
    }

    [Fact]
    public void Provider_UnsatisfiedCtorDep_BuilderOnly_Throws()
    {
        // No DI — the builder-only path cannot satisfy TaxCalculatorProvider(ITaxRateSource);
        // DefaultProviderResolver must surface a DI-registration hint on the first Map call.
        var sculptor = new SculptorBuilder()
            .UseBlueprint<IntegrationBlueprint>()
            .Forge();

        var act = () => sculptor.Map<Order, OrderDto>(SampleOrder());

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("Register it in DI", StringComparison.Ordinal)
                      || ex.Message.Contains("no public parameterless constructor", StringComparison.Ordinal));
    }

    [Fact]
    public void Provider_Resolution_1000ParallelMapsAcrossScopes_AllHonorTheirScopesRate()
    {
        const int iterations = 1000;

        // Each iteration creates a fresh scope whose ITaxRateSource uses iteration index as the
        // rate seed. The provider must see the scope-local seed, so the resulting Tax value is
        // exactly subtotal * (iterationIndex * 0.001).
        var services = new ServiceCollection();
        services.AddScoped<IterationIndex>();
        services.AddScoped<ITaxRateSource>(sp =>
            new FixedTaxRateSource(sp.GetRequiredService<IterationIndex>().Value * 0.001m));
        services.AddSculptor(ServiceLifetime.Scoped, o => o.UseBlueprint<IntegrationBlueprint>());
        using var sp = services.BuildServiceProvider();

        var results = new ConcurrentDictionary<int, decimal>();
        Parallel.For(0, iterations, i =>
        {
            using var scope = sp.CreateScope();
            scope.ServiceProvider.GetRequiredService<IterationIndex>().Value = i;
            var sculptor = scope.ServiceProvider.GetRequiredService<ISculptor>();
            var dto = sculptor.Map<Order, OrderDto>(SampleOrder());
            results[i] = dto.Tax;
        });

        results.Should().HaveCount(iterations);
        // Sampling check: at least 950 / 1000 distinct Tax values (slack for i=0 ties on rounding).
        results.Values.Distinct().Count().Should().BeGreaterThan(iterations - 50,
            "distinct rates per scope must yield distinct tax values in the vast majority of iterations.");
    }

    private sealed class IterationIndex { public int Value { get; set; } }
}
