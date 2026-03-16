# SmartMapp.Net — Executive Summary

> **Version:** 1.0.0 | **License:** MIT | **Target:** .NET 8+ | **Status:** In Development

---

## What Is SmartMapp.Net?

SmartMapp.Net is a high-performance, open-source object-to-object mapping library for .NET. It transforms data between domain models, DTOs, view models, and API contracts — a task required in virtually every .NET application — with **zero configuration** for the vast majority of use cases.

**Tagline:** *Less code. More features. Better performance.*

---

## The Problem

Every .NET application that follows clean architecture must translate objects between layers (database entities → domain models → API responses → view models). Existing mapping libraries in the .NET ecosystem share common limitations:

- **Extensive ceremony** — developers must write explicit registration and per-property configuration for every type pair.
- **Performance overhead** — reliance on reflection and expression tree compilation leads to unnecessary allocations on every call.
- **Limited modern .NET support** — most solutions lack AOT/trimming safety, `IAsyncEnumerable` support, and parallel collection processing.
- **Opaque diagnostics** — debugging why a property received an unexpected value is difficult and time-consuming.

---

## The Solution

SmartMapp.Net eliminates these pain points through four core innovations:

### 1. Zero-Configuration Auto-Discovery
SmartMapp.Net scans assemblies at startup and automatically links types by name, structure, and conventions. For 95% of mappings, developers write **zero configuration code** — just register and use.

### 2. Superior Performance
A multi-tier execution engine delivers near-manual-code speed:

| Scenario | Target |
|---|---|
| Flat 10-property DTO (warm) | < 100 ns |
| Nested 3-level object (warm) | < 500 ns |
| 1,000-item collection | < 100 μs |
| Memory per flat mapping | **0 bytes** (pooled) |
| Startup scan (100 types) | < 50 ms |

Performance is achieved through IL Emit code generation, adaptive hot-path promotion, SIMD-accelerated primitive copies, object pooling, and automatic parallel collection processing.

### 3. Compile-Time Safety (Source Generator)
An optional Roslyn source generator emits mapping code at build time, providing:
- **Zero runtime overhead** — no reflection, no compilation.
- **Full AOT and trimmer compatibility** — critical for cloud-native and mobile deployments.
- **Compile-time error detection** — unlinked required members surface as compiler warnings.

### 4. Enterprise-Grade Observability
Built-in diagnostics beyond what existing solutions offer:
- **Mapping inspection** — ask "why did property X get value Y?" and get a traced answer.
- **OpenTelemetry integration** — metrics and distributed traces out of the box.
- **Mapping atlas visualizer** — DOT/SVG graph of all registered type transformations.
- **ASP.NET Core diagnostic endpoints** — `/insights/mappings` for runtime introspection.

---

## Key Capabilities

| Capability | Description |
|---|---|
| **Auto-Discovery** | Assembly scanning links types by name conventions, suffixes, structural similarity |
| **Fluent API** | `Bind<S,D>().Property(d => ..., p => p.From(...))` — concise and chainable |
| **Attribute-Based Config** | `[MappedBy<T>]`, `[Unmapped]`, `[LinkedFrom]` — zero boilerplate |
| **Polymorphic Mapping** | Automatic inheritance hierarchy detection — no manual configuration |
| **Deep Object Graphs** | Recursive nested mapping with circular reference tracking |
| **Collection Support** | All .NET collection types including immutable, observable, and read-only |
| **Flattening / Unflattening** | `Customer.Address.City` ↔ `CustomerAddressCity` — bidirectional, automatic |
| **Record & Init Support** | Full support for C# records, `init`-only properties, `required` members |
| **Parallel Processing** | Collections above a configurable threshold mapped in parallel automatically |
| **Streaming** | `IAsyncEnumerable` support for large dataset streaming (`MapStream`) |
| **Multi-Origin Composition** | Combine N source objects into a single target (`Compose`) |
| **IQueryable Projection** | `SelectAs<D>()` generates SQL-translatable expressions for EF Core |
| **Bidirectional Mapping** | Single `.Bidirectional()` call creates the inverse mapping |
| **Extensibility** | Addon system, mapping filters (middleware), hooks, custom conventions |
| **DI Integration** | Single `services.AddSculptor()` call; supports keyed services (.NET 8+) |
| **Validation** | FluentValidation integration; startup and runtime configuration checks |

---

## Architecture at a Glance

```
┌──────────────────────────────────────────────────────────────┐
│                      Public API Layer                         │
│   ISculptor · IMapper<S,D> · Fluent Bindings · Extensions    │
├──────────────────────────────────────────────────────────────┤
│                   Mapping Filter Pipeline                     │
│   Pre-Map → Convention Linking → Execution → Post-Map        │
├──────────────────────────────────────────────────────────────┤
│                     Mapping Engine                            │
│   IL Emitter (hot)  ·  Source Gen (build)  ·  Expression     │
├──────────────────────────────────────────────────────────────┤
│                   Convention Engine                           │
│   Name Link · Flattening · Unflattening · Type Coercion      │
├──────────────────────────────────────────────────────────────┤
│               Discovery & Metadata Layer                     │
│   Assembly Scanner · Type Analyzer · Blueprint Builder        │
├──────────────────────────────────────────────────────────────┤
│                    Infrastructure                            │
│   Object Pool · Parallel Scheduler · Cache · Diagnostics     │
└──────────────────────────────────────────────────────────────┘
```

The system is organized into **six NuGet packages**, each independently versionable:

| Package | Purpose |
|---|---|
| **SmartMapp.Net** | Core library — zero external dependencies, < 150 KB |
| **SmartMapp.Net.Codegen** | Roslyn source generator for compile-time code emission |
| **SmartMapp.Net.DependencyInjection** | `IServiceCollection` extensions |
| **SmartMapp.Net.AspNetCore** | Model binding, request/response filters, diagnostic endpoints |
| **SmartMapp.Net.Insights** | OpenTelemetry instrumentation, atlas visualizer, health checks |
| **SmartMapp.Net.Validation** | FluentValidation integration for pre/post-map validation |

---

## What Sets SmartMapp.Net Apart

| Dimension | Traditional Mapping Libraries | SmartMapp.Net |
|---|---|---|
| **Setup** | Explicit registration per type pair | Zero-config auto-discovery; opt-in overrides |
| **Performance** | Reflection + expression trees | IL Emit + source gen + SIMD |
| **Memory** | Allocations on every call | Near-zero allocation (pooled) |
| **AOT / Trimming** | Generally unsupported | Fully supported via source gen |
| **Parallelism** | Manual or unsupported | Automatic parallel collections |
| **Diagnostics** | Basic configuration validation | Full telemetry, atlas visualizer, mapping inspection |
| **Streaming** | Generally unsupported | `IAsyncEnumerable` native |
| **Modern C#** | Partial record/init support | Full record, `init`, `required` member support |
| **Extensibility** | Limited plugin models | Addon system, filter pipeline, hooks, events |

---

## Quality & Testing Strategy

| Metric | Target |
|---|---|
| **Test count** | 465+ tests across unit, integration, performance, and codegen |
| **Code coverage** | > 90% |
| **Mutation testing** | Stryker.NET integrated into CI |
| **Performance regression gate** | > 10% regression blocks release |
| **Supported frameworks** | .NET 8, .NET 9 (full); .NET Standard 2.1 (core) |
| **Versioning** | Semantic Versioning 2.0 |

---

## Delivery Roadmap

| Phase | Version | Scope | Key Deliverables |
|---|---|---|---|
| **Phase 1 — Core** | v1.0 | Foundation | Convention engine, expression compiler, fluent API, blueprints, attributes, all type transformers, collection mapping, polymorphism, circular refs, records, DI, validation, 400+ tests, benchmarks, docs |
| **Phase 2 — Performance** | v1.1 | Speed | IL Emit with adaptive promotion, SIMD collection copy, object pooling, FrozenDictionary cache, parallel collections |
| **Phase 3 — Advanced** | v1.2 | Features | Source generator, filter pipeline, hooks, multi-origin composition, collection merging, streaming/lazy mapping |
| **Phase 4 — Ecosystem** | v1.3 | Integration | ASP.NET Core package, OpenTelemetry, atlas visualizer, health checks, FluentValidation, diagnostic endpoints |
| **Phase 5 — Intelligence** | v2.0 | Next-gen | `dynamic`/`ExpandoObject` mapping, Roslyn analyzers, IDE quick-fixes, performance profiler integration |

---

## Core Terminology

| Term | Meaning |
|---|---|
| **Sculptor** | The runtime engine — primary interface for all mapping operations |
| **Blueprint** | Immutable instruction set describing how a type pair is mapped |
| **Property Link** | A single origin → target member instruction |
| **Forge** | The act of freezing configuration into an immutable Sculptor |
| **Mapping** | The act of transforming one object into another |
| **Convention** | An auto-linking rule (name match, flattening, type coercion, etc.) |

---

## Why SmartMapp.Net?

1. **Developer Productivity** — Eliminates 80–95% of mapping boilerplate. Teams spend time on business logic, not plumbing.
2. **Runtime Performance** — Near-manual-code speed with zero allocations on hot paths. Measurable reduction in API latency and cloud compute costs.
3. **Cloud-Native Ready** — AOT and trimmer safe via source generation. Critical for serverless, container, and mobile targets where startup time and binary size matter.
4. **Operational Visibility** — Built-in OpenTelemetry, diagnostic endpoints, and mapping inspection. Production issues are diagnosed in minutes, not hours.
5. **Low Adoption Risk** — MIT licensed, zero dependencies in the core package, includes migration guides from popular libraries, and supports incremental adoption alongside existing solutions.

---

*SmartMapp.Net: Less code. More features. Better performance.*
