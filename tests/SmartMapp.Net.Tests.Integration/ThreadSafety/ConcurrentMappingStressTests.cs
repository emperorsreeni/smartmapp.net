// SPDX-License-Identifier: MIT
using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.ThreadSafety;

/// <summary>
/// Sprint 8 · S8-T11 — thread-safety stress suite per spec §17.2.5 (10K parallel iterations)
/// and §S8-T11 Acceptance bullet 9 ("ConcurrentMappingStressTests runs 10K parallel mappings,
/// zero exceptions"). Tests are marked with the <c>Stress</c> trait so CI can opt out under
/// time pressure (spec §S8-T11 Tech-Cons bullet 4).
/// </summary>
[Trait("Category", "Stress")]
public sealed class ConcurrentMappingStressTests
{
    private const int Iterations = 10_000;

    private readonly ITestOutputHelper _output;

    public ConcurrentMappingStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>());
        return services.BuildServiceProvider();
    }

    private static Order SampleOrder(int id) => new()
    {
        Id = id,
        PlacedAt = DateTime.UnixEpoch.AddSeconds(id),
        Customer = new Customer
        {
            Id = id, FirstName = "C" + id, LastName = "L" + id, Email = $"c{id}@x.com",
            Address = new Address { City = "London" },
        },
        Lines =
        {
            new OrderLine { Sku = "A", Quantity = 2, UnitPrice = 5m },
            new OrderLine { Sku = "B", Quantity = 1, UnitPrice = 10m },
        },
    };

    [Fact]
    public void Map_10K_ParallelInvocations_ZeroExceptions_CorrectDtos()
    {
        using var sp = BuildProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var errors = new ConcurrentBag<Exception>();
        var ids = new ConcurrentBag<int>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Parallel.For(0, Iterations, i =>
        {
            try
            {
                var dto = sculptor.Map<Order, OrderDto>(SampleOrder(i));
                ids.Add(dto.Id);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });
        sw.Stop();

        _output.WriteLine($"Map stress: {Iterations} iterations in {sw.ElapsedMilliseconds} ms " +
                          $"({Iterations / sw.Elapsed.TotalSeconds:F0} maps/sec)");

        errors.Should().BeEmpty("spec §17.2.5 demands zero exceptions across 10K parallel mappings.");
        ids.Should().HaveCount(Iterations);
    }

    [Fact]
    public void IMapper_10K_ParallelInvocations_ProducesCorrectDtoIds()
    {
        using var sp = BuildProvider();
        var mapper = sp.GetRequiredService<IMapper<Order, OrderListDto>>();

        var failures = new ConcurrentBag<string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Parallel.For(0, Iterations, i =>
        {
            var dto = mapper.Map(SampleOrder(i));
            if (dto.Id != i) failures.Add($"iteration {i} returned id {dto.Id}");
        });
        sw.Stop();

        _output.WriteLine($"IMapper<,> stress: {Iterations} iterations in {sw.ElapsedMilliseconds} ms");

        failures.Should().BeEmpty("every parallel IMapper<,>.Map call must preserve the origin's Id.");
    }

    [Fact]
    public void Compose_10K_ParallelInvocations_ZeroExceptions()
    {
        var services = new ServiceCollection();
        services.AddSculptor(options =>
        {
            options.Compose<DashboardViewModel>(c => c
                .FromOrigin<UserProfile>()
                .FromOrigin<OrderSummary>()
                .FromOrigin<CompanyInfo>());
        });
        using var sp = services.BuildServiceProvider();
        var sculptor = sp.GetRequiredService<ISculptor>();

        var errors = new ConcurrentBag<Exception>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Parallel.For(0, Iterations, i =>
        {
            try
            {
                var user = new UserProfile { UserId = i, DisplayName = "U" + i, Email = $"u{i}@x.com" };
                var summary = new OrderSummary { OpenOrders = i % 10, LifetimeValue = i * 1m };
                var company = new CompanyInfo { CompanyName = "C" + i, Plan = "P" };
                _ = sculptor.Compose<DashboardViewModel>(user, summary, company);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        });
        sw.Stop();

        _output.WriteLine($"Compose stress: {Iterations} iterations in {sw.ElapsedMilliseconds} ms");

        errors.Should().BeEmpty("Compose dispatch must be fully thread-safe under spec §17.2.5 load.");
    }
}
