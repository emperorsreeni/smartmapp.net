# SmartMapp.Net.DependencyInjection

`Microsoft.Extensions.DependencyInjection` integration for
[SmartMapp.Net](https://www.nuget.org/packages/SmartMapp.Net) — the
high-performance, zero-configuration object-to-object mapping library for .NET.

## Install

```powershell
dotnet add package SmartMapp.Net.DependencyInjection
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Abstractions;

var services = new ServiceCollection();

services.AddSculptor(options =>
{
    options.ScanAssembliesContaining<Order>();
});

var provider = services.BuildServiceProvider();

var sculptor = provider.GetRequiredService<ISculptor>();
var dto = sculptor.Map<Order, OrderDto>(order);
```

## What this package adds

- `IServiceCollection.AddSculptor()` family — registers `ISculptor`,
  `ISculptorConfiguration`, and `IMapper<TOrigin, TTarget>` for every
  discovered type pair.
- DI-resolved `IValueProvider<,,>` and `ITypeTransformer<,>` — providers can
  depend on scoped services such as `DbContext` or `ILogger<T>`.
- `ValidateOnStartup` hosted service — fails host startup on invalid blueprint
  configuration with structured diagnostics.
- `IQueryable.SelectAs<TTarget>()` — EF Core-translatable projections.
- `MapTo<TTarget>()` object extension — opt-in ambient mapping helper.

## Target Frameworks

`netstandard2.1`, `net8.0`, `net10.0`.

## License

MIT
