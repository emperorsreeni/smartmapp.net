// Sprint 8 · S8-T10 — ASP.NET Minimal API sample exercising the Sprint 8 scope end-to-end:
// DI registration, startup validation, IQueryable projection via SelectAs, and a
// DI-resolved IValueProvider (TaxCalculatorProvider). Shape follows spec §16.1.
//
// The `Program` class is declared `public partial` at the bottom of the file so the Sprint 8
// WebApplicationFactory<Program>-based integration tests can compose a live HTTP client.

using Microsoft.EntityFrameworkCore;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Samples.MinimalApi.Blueprints;
using SmartMapp.Net.Samples.MinimalApi.Data;
using SmartMapp.Net.Samples.MinimalApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------------------
// Configuration: invalid-blueprint test profile (spec §S8-T10 Acceptance bullet 5)
// Enabled via either appsettings `"Sample:InvalidBlueprint": true` or the
// `SMARTMAPP_SAMPLE_INVALID=true` environment variable. The invalid profile is what the
// StartupValidation smoke test asserts against — the host must fail to start.
// ---------------------------------------------------------------------------------------
var useInvalid =
    builder.Configuration.GetValue<bool>("Sample:InvalidBlueprint")
    || string.Equals(
           Environment.GetEnvironmentVariable("SMARTMAPP_SAMPLE_INVALID"),
           "true",
           StringComparison.OrdinalIgnoreCase);

// ---------------------------------------------------------------------------------------
// EF Core InMemory — unique database name per application so parallel tests don't bleed.
// Sample-only dependency (spec §S8-T10 Constraints bullet 1).
// ---------------------------------------------------------------------------------------
var dbName = builder.Configuration["Sample:DbName"] ?? "SmartMapp.Net.Samples.MinimalApi";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(dbName));

// ---------------------------------------------------------------------------------------
// SmartMapp.Net registration (spec §11.1 / §S8-T01). The options callback:
//  • applies OrderBlueprint (Order → OrderListDto / OrderDto + OrderLine → OrderLineDto)
//  • when invalid-mode is enabled, layers a structurally incompatible
//    Bind<Address, OrderDto>() and enables StrictMode so the convention engine's warnings
//    for unlinked target members are promoted to startup errors by SculptorStartupValidator
//    (spec §S8-T05 / §12.1).
// The assembly scanner auto-discovers TaxCalculatorProvider (scanned as IValueProvider
// implementation) and registers it as Transient per spec §S8-T04.
// ---------------------------------------------------------------------------------------
builder.Services.AddSculptor(options =>
{
    options.UseBlueprint<OrderBlueprint>();

    // Spec §S8-T05 review: set `ValidateOnStartup` explicitly so the sample's validator
    // demo fires regardless of `IHostEnvironment.EnvironmentName`. Without this, the
    // post-review environment-aware default would skip validation under the `Testing`
    // launch profile (and under the `Production` default of `Host.CreateDefaultBuilder`)
    // — the sample's whole point is to *demonstrate* the validator, so we always opt in.
    options.ValidateOnStartup = true;

    if (useInvalid)
    {
        // Deliberately-broken binding: DiscriminateBy(...) with no .Otherwise<T>() clause.
        // BlueprintValidator flags this as an ERROR (see spec §12.1), so forge's Stage 9
        // throws BlueprintValidationException which SculptorStartupValidator surfaces as a
        // startup failure — the fail-fast demo required by spec §S8-T10 Acceptance bullet 5.
        options.Bind<Customer, Address>(rule => rule.DiscriminateBy(c => c.Id));
    }
});

var app = builder.Build();

// ---------------------------------------------------------------------------------------
// Seed the InMemory database with deterministic fixture data. Kept in DbInitializer so
// integration tests can seed the same shape (spec §S8-T10 Technical Considerations bullet 2).
// ---------------------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Seed(db);
}

// ---------------------------------------------------------------------------------------
// GET /orders — list view via IQueryable projection. Spec §16.1 / §S8-T06 / §S8-T10
// Acceptance bullet 2: translates to a single EF SELECT (no N+1 lazy loads).
// ---------------------------------------------------------------------------------------
app.MapGet("/orders", async (AppDbContext db, ISculptor sculptor) =>
{
    var list = await db.Orders
        .OrderBy(o => o.Id)
        .SelectAs<OrderListDto>(sculptor)
        .ToListAsync();

    return Results.Ok(list);
});

// ---------------------------------------------------------------------------------------
// GET /orders/{id} — detail view via ISculptor.Map with DI-resolved TaxCalculatorProvider.
// Spec §16.1 / §S8-T10 Acceptance bullet 3.
// ---------------------------------------------------------------------------------------
app.MapGet("/orders/{id:int}", async (int id, AppDbContext db, ISculptor sculptor) =>
{
    var order = await db.Orders
        .Include(o => o.Customer)
        .Include(o => o.Lines)
        .FirstOrDefaultAsync(o => o.Id == id);

    return order is null
        ? Results.NotFound()
        : Results.Ok(sculptor.Map<Order, OrderDto>(order));
});

// ---------------------------------------------------------------------------------------
// Placeholder: spec §16.1 mentions `app.MapSculptorInsights("/_insights")`. The full
// implementation ships in Sprint 16 (`SmartMapp.Net.AspNetCore`) — left here as a marker
// per spec §S8-T10 Constraints bullet 2.
// ---------------------------------------------------------------------------------------
// app.MapSculptorInsights("/_insights");

app.Run();

/// <summary>
/// Public partial declaration so <c>WebApplicationFactory&lt;Program&gt;</c> can compose a
/// live in-process HTTP pipeline from the integration test project. The top-level-statements
/// compiler generates the other part automatically.
/// </summary>
public partial class Program { }
