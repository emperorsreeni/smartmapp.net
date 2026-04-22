# SmartMapp.Net.Samples.Console

A runnable console walkthrough that exercises every headline v1.0 SmartMapp.Net feature
end-to-end. Each scenario is self-contained — it builds a dedicated sculptor, performs a
mapping, and prints a clearly delimited section header plus before/after output.

## Run

From the repository root:

```shell
dotnet run --project samples/SmartMapp.Net.Samples.Console
```

Exit code is `0` on success and `1` on any scenario exception (wired into CI as the
`Run Console sample (smoke)` step in `.github/workflows/ci.yml` and black-box tested by
`ConsoleSampleSmokeTests` under `tests/SmartMapp.Net.Tests.Unit/Samples/`).

## Scenarios

| # | Scenario | Demonstrates |
|---|---|---|
| 1 | Zero-Config Flat Mapping | `Bind<S,D>(_ => { })` — no per-property config; conventions do the work |
| 2 | Flattening | `Customer.Address.City` → `CustomerDto.AddressCity` via the flattening convention |
| 3 | Collection Mapping | `MapAll<OrderLine, OrderLineDto>` over a `List<T>` |
| 4 | Polymorphic Mapping | `Bind<Shape, ShapeDto>().ExtendWith<Circle, CircleDto>().ExtendWith<Rectangle, RectangleDto>()` — base pair dispatches to the derived DTO at runtime |
| 5 | Inline `options.Bind<S, D>` | Fluent configuration with a computed `.Property(d => d.Total, p => p.From(...))` projection |
| 6 | Blueprint Class | Reusable `OrderBlueprint : MappingBlueprint` with an `OnMapped(origin, target)` post-hook that trims whitespace |
| 7 | Attribute-Based Config | `[MappedBy<Product>]` + `[Unmapped]` discovered by `ScanAssembliesContaining<Product>()` — zero fluent code |
| 8 | `MapTo<T>()` Extension | Ambient `ISculptor` installed by `services.AddSculptor()` powers `product.MapTo<ProductDto>()` without an explicit sculptor argument |
| 9 | Bidirectional Mapping | `.Bidirectional()` auto-generates the inverse `(CustomerDto → Customer)` blueprint from a single declaration |

## Layout

```
samples/SmartMapp.Net.Samples.Console/
├── Blueprints/
│   └── OrderBlueprint.cs        # Reusable MappingBlueprint configuration for scenario 6
├── Fixtures/
│   └── SampleData.cs            # Deterministic fixture factories shared across scenarios
├── Models/                      # Origin + DTO types used by the scenarios
├── Output/
│   └── ConsoleOutput.cs         # Section() / PrintBeforeAfter() presentation helpers
├── Program.cs                   # Top-level statements: one try-block per scenario, exit 0/1
└── SmartMapp.Net.Samples.Console.csproj
```

## Target Frameworks

The sample multi-targets `net8.0` and `net10.0` to match the repo's CI matrix. The smoke test
invokes `dotnet run --framework net10.0`.

## Dependencies

Only what the spec §S8-T09 Constraints bullet 2 permits:

- `SmartMapp.Net` (project reference)
- `SmartMapp.Net.DependencyInjection` (project reference — drives scenario 8's ambient `MapTo<T>()`)
- `Microsoft.Extensions.Hosting` (package reference — available for DI scenarios though the
  current set uses `ServiceCollection` directly)
