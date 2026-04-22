# SmartMapp.Net

**High-performance, zero-configuration object-to-object mapping library for .NET.**

[![NuGet](https://img.shields.io/nuget/v/SmartMapp.Net.svg)](https://www.nuget.org/packages/SmartMapp.Net)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/emperorsreeni/smartmapp.net/blob/main/LICENSE)

## Install

```powershell
dotnet add package SmartMapp.Net --prerelease
# With DI integration:
dotnet add package SmartMapp.Net.DependencyInjection --prerelease
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;

var services = new ServiceCollection();
services.AddSculptor();                        // auto-discovers type pairs in the calling assembly
using var provider = services.BuildServiceProvider();

var sculptor = provider.GetRequiredService<ISculptor>();
var dto = sculptor.Map<Order, OrderDto>(order); // convention-based, zero config
```

## Highlights

- **Zero-config auto-discovery** — scans assemblies, links types by name / suffix / structural similarity.
- **Flattening / unflattening** — `Customer.Address.City` ↔ `CustomerAddressCity` automatic.
- **Polymorphism** — inheritance hierarchies recognised without manual `[MappedBy]`.
- **Collections** — arrays, `List<>`, dictionaries, `ImmutableArray<>`, observable, read-only.
- **Records / init-only / required** — full C# 11+ record support.
- **IQueryable projection** — `db.Orders.SelectAs<OrderDto>(sculptor)` → single SQL `SELECT` via EF Core.
- **Multi-origin composition** — `sculptor.Compose<Dashboard>(user, summary, company)`.
- **Bidirectional** — one `.Bidirectional()` call wires the inverse mapping.
- **DI-friendly** — `SmartMapp.Net.DependencyInjection` adds `IMapper<,>` open-generic registration, scoped-provider injection, and startup validation.

## Target Frameworks

`netstandard2.1`, `net8.0`, `net10.0`.

## Documentation

Full README, samples, and the Sprint 8 CHANGELOG live at
<https://github.com/emperorsreeni/smartmapp.net>.

## License

MIT
