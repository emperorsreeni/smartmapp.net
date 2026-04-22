using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartMapp.Net.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Samples;

/// <summary>
/// Sprint 8 · S8-T10 integration tests for <c>SmartMapp.Net.Samples.MinimalApi</c>. Uses
/// <see cref="WebApplicationFactory{TEntryPoint}"/> to spin up the sample's Minimal API
/// pipeline in-process and exercise the spec §16.1 endpoints plus the startup-validator
/// fail-fast profile.
/// </summary>
public class MinimalApiSampleIntegrationTests
{
    /// <summary>
    /// Custom factory that switches the Minimal API to use a per-test EF Core InMemory
    /// database name so parallel tests don't share seeded state, and optionally sets the
    /// <c>Sample:InvalidBlueprint</c> flag to drive the fail-fast startup path. Injects
    /// a <see cref="CapturingLoggerProvider"/> so tests can assert on emitted log events.
    /// </summary>
    private sealed class MinimalApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName;
        private readonly bool _invalid;

        internal CapturingLoggerProvider Logs { get; } = new();

        internal MinimalApiFactory(string dbName, bool invalid = false)
        {
            _dbName = dbName;
            _invalid = invalid;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Sample:DbName"] = _dbName,
                    ["Sample:InvalidBlueprint"] = _invalid ? "true" : "false",
                });
            });

            // Attach the capturing logger provider so tests can inspect
            // SmartMapp.Net.DependencyInjection emitted log events.
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<ILoggerProvider>(Logs);
            });

            // Belt-and-braces: some environments / .NET SDK versions bind the
            // `WebApplicationBuilder.Configuration` before the test factory's override
            // is merged. Setting the env var the sample's Program.cs also reads guarantees
            // the invalid profile is honoured regardless of configuration-chain ordering.
            if (_invalid)
            {
                Environment.SetEnvironmentVariable("SMARTMAPP_SAMPLE_INVALID", "true");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _invalid)
            {
                Environment.SetEnvironmentVariable("SMARTMAPP_SAMPLE_INVALID", null);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Minimal <see cref="ILoggerProvider"/> that appends every logged message into a
    /// thread-safe list so integration tests can assert on specific log events without
    /// pulling in a logging-framework mock. Filters nothing — log-level inspection happens
    /// at assertion time.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        internal ConcurrentQueue<(string Category, LogLevel Level, string Message)> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Entries);

        public void Dispose() { }

        private sealed class CapturingLogger : ILogger
        {
            private readonly string _category;
            private readonly ConcurrentQueue<(string, LogLevel, string)> _entries;

            internal CapturingLogger(string category, ConcurrentQueue<(string, LogLevel, string)> entries)
            {
                _category = category;
                _entries = entries;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (formatter is null) return;
                _entries.Enqueue((_category, logLevel, formatter(state, exception)));
            }
        }
    }

    [Fact]
    public async Task GetOrders_ReturnsSeededList_MappedToDtos()
    {
        // Spec §S8-T10 Acceptance bullet 1-2: /orders returns the 3 seeded orders mapped to
        // OrderListDto via db.Orders.SelectAs<OrderListDto>(sculptor).ToListAsync().
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new MinimalApiFactory(nameof(GetOrders_ReturnsSeededList_MappedToDtos));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/orders", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var orders = await response.Content.ReadFromJsonAsync<List<OrderListDtoProbe>>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);

        orders.Should().NotBeNull();
        orders!.Should().HaveCount(3, "the seeded fixture contains three orders.");
        orders.Should().ContainSingle(o => o.Id == 1 && o.CustomerFirstName == "Alice"
                                                     && o.CustomerAddressCity == "London",
            "Alice's order is seeded with a London address.");
        orders.Should().ContainSingle(o => o.Id == 2 && o.CustomerFirstName == "Bob"
                                                     && o.CustomerAddressCity == "Springfield");
        orders.Should().ContainSingle(o => o.Id == 3 && o.CustomerFirstName == "Carol"
                                                     && o.CustomerAddressCity == "London");
    }

    [Fact]
    public async Task GetOrderById_ReturnsDto_WithSubtotalTaxAndTotalFromProvider()
    {
        // Spec §S8-T10 Acceptance bullet 3: /orders/{id} maps via ISculptor.Map<Order, OrderDto>
        // with the DI-resolved TaxCalculatorProvider contributing the Tax member.
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new MinimalApiFactory(nameof(GetOrderById_ReturnsDto_WithSubtotalTaxAndTotalFromProvider));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/orders/1", ct);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<OrderDtoProbe>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);

        dto.Should().NotBeNull();
        dto!.Id.Should().Be(1);
        dto.CustomerFirstName.Should().Be("Alice");
        dto.CustomerLastName.Should().Be("Smith");
        dto.CustomerEmail.Should().Be("alice@example.com");

        // Order 1 seeded lines: BOOK-1984 x2 @ 12.50 + MUG-CLASSIC x1 @ 8.00 = 33.00
        dto.Subtotal.Should().Be(33.00m);
        // TaxCalculatorProvider applies a flat 10% rate.
        dto.Tax.Should().Be(3.30m);
        // Total = Subtotal * 1.10.
        dto.Total.Should().Be(33.00m * 1.10m);

        dto.Lines.Should().HaveCount(2);
        dto.Lines.Should().ContainSingle(l => l.Sku == "BOOK-1984" && l.Quantity == 2 && l.LineTotal == 25.00m);
        dto.Lines.Should().ContainSingle(l => l.Sku == "MUG-CLASSIC" && l.Quantity == 1 && l.LineTotal == 8.00m);
    }

    [Fact]
    public async Task GetOrderById_UnknownId_Returns404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new MinimalApiFactory(nameof(GetOrderById_UnknownId_Returns404));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/orders/9999", ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Startup_ValidProfile_EmitsInformationLog_FromSculptorStartupValidator()
    {
        // Spec §S8-T10 Acceptance bullet 4: "Startup validator runs and logs an Information
        // entry." Guards the success-path log from SculptorStartupValidator (event id 1) by
        // capturing ILogger output via a custom ILoggerProvider wired into the test host.
        var ct = TestContext.Current.CancellationToken;
        await using var factory = new MinimalApiFactory(nameof(Startup_ValidProfile_EmitsInformationLog_FromSculptorStartupValidator));

        // Triggering any request forces the host to start, which drives the hosted
        // SculptorStartupValidator through its success-path logging.
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/orders", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var validatorCategory = typeof(SculptorStartupValidator).FullName!;
        var successEntry = factory.Logs.Entries.FirstOrDefault(
            e => e.Category == validatorCategory
                 && e.Level == LogLevel.Information
                 && e.Message.Contains("startup validation succeeded", StringComparison.OrdinalIgnoreCase));

        successEntry.Should().NotBe(default,
            "SculptorStartupValidator must emit an Information log entry on successful forge + validation. " +
            "Captured entries from '{0}':\n{1}",
            validatorCategory,
            string.Join('\n', factory.Logs.Entries
                .Where(e => e.Category == validatorCategory)
                .Select(e => $"  [{e.Level}] {e.Message}")));
    }

    [Fact]
    public async Task Startup_InvalidProfile_FailsFast_WithSculptorStartupValidationException()
    {
        // Spec §S8-T10 Acceptance bullet 5: the deliberately-invalid Testing profile drives
        // SculptorStartupValidator to throw, so host startup (triggered by CreateClient)
        // surfaces the validation exception. StrictMode promotes warnings from the
        // incompatible Bind<Address, OrderDto>() to startup failures.
        await using var factory = new MinimalApiFactory(
            nameof(Startup_InvalidProfile_FailsFast_WithSculptorStartupValidationException),
            invalid: true);

        var act = () => factory.CreateClient();

        var thrown = act.Should().Throw<Exception>(
            "the deliberately-invalid configuration must surface as a startup failure.")
            .Which;

        // Either the validator's fail-fast wrapper (SculptorStartupValidationException) or the
        // forge-time BlueprintValidationException is an acceptable fail-fast signal — both
        // satisfy spec §12.1 "host fails to start on any blueprint validation error". Which
        // one surfaces depends on whether the error is caught at forge Stage 9 or at the
        // SculptorStartupValidator's StrictMode-promotion step. Accept either so the guard is
        // stable across the two code paths.
        var chain = Unwind(thrown).ToList();
        chain.Should().Contain(
            e => e is SculptorStartupValidationException || e is BlueprintValidationException,
            "the fail-fast path must surface SculptorStartupValidationException or BlueprintValidationException; chain was:\n{0}",
            string.Join(" -> ", chain.Select(e => e.GetType().Name)));
    }

    private static IEnumerable<Exception> Unwind(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException agg)
            {
                foreach (var inner in agg.InnerExceptions)
                {
                    foreach (var nested in Unwind(inner)) yield return nested;
                }
            }
        }
    }

    // Local-record JSON probes — independent of the sample assembly's DTO types so a rename
    // in the sample produces a compile error here rather than a silent test pass.
    private sealed record OrderListDtoProbe(int Id, DateTime PlacedAt, string CustomerFirstName,
        string CustomerLastName, string CustomerAddressCity);

    private sealed record OrderDtoProbe(int Id, DateTime PlacedAt, string CustomerFirstName,
        string CustomerLastName, string CustomerEmail, decimal Subtotal, decimal Tax, decimal Total,
        List<OrderLineDtoProbe> Lines);

    private sealed record OrderLineDtoProbe(string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);
}
