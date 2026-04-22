# SmartMapp.Net.Samples.MinimalApi

Production-shaped ASP.NET Minimal API sample that exercises the Sprint 8 scope end-to-end:
DI registration, startup validation, `IQueryable` projection via `SelectAs`, and a
DI-resolved `IValueProvider` (`TaxCalculatorProvider`). Shape follows spec §16.1.

## Run

From the repository root:

```shell
# http — default profile on port 5080
dotnet run --project samples/SmartMapp.Net.Samples.MinimalApi

# https — optional; adds TLS on port 5443
dotnet run --project samples/SmartMapp.Net.Samples.MinimalApi --launch-profile https

# Testing — deliberately invalid blueprint; host should FAIL to start (fail-fast demo)
dotnet run --project samples/SmartMapp.Net.Samples.MinimalApi --launch-profile Testing
```

Then hit the endpoints:

```shell
curl http://localhost:5080/orders
curl http://localhost:5080/orders/1
```

## Endpoints

| Verb + Path | Mapping path | Demonstrates |
|---|---|---|
| `GET /orders` | `db.Orders.SelectAs<OrderListDto>(sculptor).ToListAsync()` | EF Core `IQueryable` projection translated to a single SELECT (spec §8.10 / §S8-T06) |
| `GET /orders/{id}` | `sculptor.Map<Order, OrderDto>(order)` | Detail mapping with a DI-resolved `IValueProvider` (`TaxCalculatorProvider`) contributing `Tax` per spec §11.4 / §S8-T04 |

## Layout

```
samples/SmartMapp.Net.Samples.MinimalApi/
├── Blueprints/
│   └── OrderBlueprint.cs       # Binds Order → OrderListDto, Order → OrderDto, OrderLine → OrderLineDto
├── Data/
│   ├── AppDbContext.cs         # EF Core InMemory: Orders / OrderLines / Customers
│   └── DbInitializer.cs        # Deterministic seed (3 customers × 3 orders × 5 line items)
├── Models/                     # Entity + DTO types
├── Properties/
│   └── launchSettings.json     # http / https / Testing profiles (Testing = invalid blueprint)
├── Services/
│   └── TaxCalculatorProvider.cs # IValueProvider<Order, OrderDto, decimal> resolved from DI
├── Program.cs                  # Top-level statements + `public partial class Program {}`
└── SmartMapp.Net.Samples.MinimalApi.csproj
```

## Invalid Blueprint Profile

The `Testing` launch profile sets `SMARTMAPP_SAMPLE_INVALID=true`, which layers a
structurally-incompatible `Bind<Address, OrderDto>()` and turns on `StrictMode`. The result
is that `SculptorStartupValidator` promotes the convention engine's unlinked-member warnings
to startup errors and the host **fails to start** with a
`SculptorStartupValidationException` — the fail-fast demo required by spec §12.1 /
§S8-T10 Acceptance bullet 5. The same environment variable is set by
`MinimalApiSampleIntegrationTests.Startup_InvalidProfile_FailsFast` under
`tests/SmartMapp.Net.Tests.Unit/Samples/`.

## Target Frameworks

The sample multi-targets `net8.0` and `net10.0` to match the repo's CI matrix.

## Dependencies

- `SmartMapp.Net` (project reference)
- `SmartMapp.Net.DependencyInjection` (project reference — provides `AddSculptor`,
  `SculptorStartupValidator`, and the `SelectAs<TTarget>(IQueryable, ISculptor)` extension)
- `Microsoft.EntityFrameworkCore.InMemory` (package reference — sample data layer; **not** a
  runtime dependency of SmartMapp.Net itself per spec §S8-T10 Constraints bullet 1)

## Insights Endpoint

A commented-out `app.MapSculptorInsights("/_insights")` line marks the spot reserved for the
Sprint 16 `SmartMapp.Net.AspNetCore` package (spec §16.1 / §S8-T10 Constraints bullet 2).
