// SPDX-License-Identifier: MIT
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.AspNetCore;

/// <summary>
/// Sprint 8 · S8-T11 — end-to-end integration tests that hit the MinimalApi sample's HTTP
/// pipeline via <see cref="WebApplicationFactory{TEntryPoint}"/>. Focus is on the full stack
/// (Kestrel pipeline → EF → sculptor → DTO) rather than the sample-specific tests already
/// locked down in <c>MinimalApiSampleIntegrationTests</c>.
/// </summary>
[Collection(AspNetCoreCollection.Name)]
public sealed class MinimalApiEndpointTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName;
        public Factory(string dbName) { _dbName = dbName; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Sample:DbName"] = _dbName }));
        }
    }

    [Fact]
    public async Task GetOrders_ReturnsAllSeededOrders_WithFlattenedCustomerProperties()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new Factory($"s8t11-list-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/orders", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await response.Content.ReadFromJsonAsync<List<OrderListProbe>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);

        list.Should().NotBeNull();
        list!.Should().HaveCount(3);
        list.Select(o => o.CustomerFirstName).Should().BeEquivalentTo(new[] { "Alice", "Bob", "Carol" });
        list.Should().AllSatisfy(o => o.CustomerAddressCity.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetOrderById_KnownId_ReturnsDetailDtoWithProvidedTax()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new Factory($"s8t11-detail-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/orders/1", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<OrderDetailProbe>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(1);
        dto.Subtotal.Should().Be(33.00m);
        dto.Tax.Should().Be(3.30m, "the DI-resolved TaxCalculatorProvider applies a flat 10% rate.");
        dto.Total.Should().Be(33.00m * 1.10m);
        dto.Lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetOrderById_UnknownId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new Factory($"s8t11-404-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/orders/9999", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrderById_50ConcurrentRequests_AllSucceedWithConsistentDto()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new Factory($"s8t11-concur-{Guid.NewGuid():N}");
        using var client = factory.CreateClient();

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => client.GetFromJsonAsync<OrderDetailProbe>("/orders/1",
                new JsonSerializerOptions(JsonSerializerDefaults.Web), ct))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().HaveCount(50);
        results.Should().AllSatisfy(dto =>
        {
            dto.Should().NotBeNull();
            dto!.Id.Should().Be(1);
            dto.Tax.Should().Be(3.30m);
            dto.Subtotal.Should().Be(33.00m);
        });
    }

    private sealed record OrderListProbe(int Id, DateTime PlacedAt, string CustomerFirstName,
        string CustomerLastName, string CustomerAddressCity);

    private sealed record OrderDetailProbe(int Id, DateTime PlacedAt, string CustomerFirstName,
        string CustomerLastName, string CustomerEmail, decimal Subtotal, decimal Tax,
        decimal Total, List<OrderLineProbe> Lines);

    private sealed record OrderLineProbe(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);
}
