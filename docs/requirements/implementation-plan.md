# SmartMapp.Net — Implementation Plan

> **Spec Reference:** [spec.md](./spec.md)  
> **Version:** 1.0.0  
> **Methodology:** 2-week sprints, test-first development  
> **Estimated Total Duration:** ~26 weeks (5 phases)  
> **Team Assumption:** 1–3 developers

---

## Table of Contents

1. [Project Bootstrap](#1-project-bootstrap)
2. [Phase 1 — Core (v1.0)](#2-phase-1--core-v10) — Sprints 1–8
3. [Phase 2 — Performance (v1.1)](#3-phase-2--performance-v11) — Sprints 9–11
4. [Phase 3 — Advanced (v1.2)](#4-phase-3--advanced-v12) — Sprints 12–15
5. [Phase 4 — Ecosystem (v1.3)](#5-phase-4--ecosystem-v13) — Sprints 16–18
6. [Phase 5 — Intelligence (v2.0)](#6-phase-5--intelligence-v20) — Sprints 19–21+
7. [Cross-Cutting Concerns](#7-cross-cutting-concerns)
8. [Dependency Graph](#8-dependency-graph)
9. [Risk Register](#9-risk-register)
10. [Definition of Done](#10-definition-of-done)

---

## 1. Project Bootstrap

**Duration:** ~2 days (pre-Sprint 1)

### 1.1 Repository Scaffolding

```
smartmapp.net/
  src/
    SmartMapp.Net/                            # Core library (netstandard2.1;net8.0;net9.0)
    SmartMapp.Net.Codegen/                    # Source generator (netstandard2.0 — analyzer constraint)
    SmartMapp.Net.DependencyInjection/        # DI extensions (netstandard2.1;net8.0)
    SmartMapp.Net.AspNetCore/                 # ASP.NET integration (net8.0;net9.0)
    SmartMapp.Net.Insights/                   # Telemetry & diagnostics (net8.0;net9.0)
    SmartMapp.Net.Validation/                 # FluentValidation bridge (netstandard2.1;net8.0)
  tests/
    SmartMapp.Net.Tests.Unit/                 # xUnit, FluentAssertions, NSubstitute
    SmartMapp.Net.Tests.Integration/          # EF Core in-memory, TestServer
    SmartMapp.Net.Tests.Performance/          # BenchmarkDotNet
    SmartMapp.Net.Tests.Codegen/              # Source generator snapshot tests (Verify)
  samples/
    SmartMapp.Net.Samples.MinimalApi/
    SmartMapp.Net.Samples.Console/
  benchmarks/
    SmartMapp.Net.Benchmarks/                 # Standalone BenchmarkDotNet runner
  docs/
    api/
    guides/
  .github/
    workflows/
      ci.yml
      release.yml
  Directory.Build.props                    # Central <LangVersion>, <Nullable>, <ImplicitUsings>
  Directory.Packages.props                 # Central Package Management
  global.json                              # SDK pinning
  SmartMapp.Net.sln
  README.md
  LICENSE
  .editorconfig
  .gitignore
```

### 1.2 Build Infrastructure

| Item | Details |
|---|---|
| **SDK** | .NET 9 SDK (supports building net8.0/netstandard2.1 targets) |
| **Lang** | C# 13, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` |
| **Packages** | Central Package Management via `Directory.Packages.props` |
| **Analyzers** | `Microsoft.CodeAnalysis.NetAnalyzers`, `StyleCop.Analyzers`, `SonarAnalyzer.CSharp` |
| **Testing** | xUnit 2.9+, FluentAssertions 7+, NSubstitute 5+, Verify 26+ (codegen snapshots) |
| **Benchmarking** | BenchmarkDotNet 0.14+ |
| **CI** | GitHub Actions — build, test, benchmark, pack, publish |
| **Coverage** | Coverlet → Codecov, target ≥ 90% |
| **Mutation** | Stryker.NET (gate on mutation score ≥ 80%) |

### 1.3 CI Pipeline (`ci.yml`)

```
trigger: push/PR to main
steps:
  1. dotnet restore
  2. dotnet build --configuration Release
  3. dotnet test --configuration Release --collect:"XPlat Code Coverage"
  4. Upload coverage to Codecov
  5. dotnet run (benchmarks — store artifact, fail on >10% regression)
  6. dotnet pack --configuration Release
  7. Upload NuGet artifacts
```

### 1.4 Release Pipeline (`release.yml`)

```
trigger: tag v*
steps:
  1. Full CI pipeline
  2. Stryker.NET mutation testing
  3. dotnet nuget push to NuGet.org
  4. GitHub Release with changelog
```

---

## 2. Phase 1 — Core (v1.0)

**Goal:** A fully functional mapping library that handles 95% of real-world scenarios with zero configuration, a fluent API for the remaining 5%, attribute-based config, DI integration, and a comprehensive test suite.

**Duration:** Sprints 1–8 (16 weeks)

---

### Sprint 1 — Foundation Types & Type Metadata (Weeks 1–2)

> Lay the groundwork: core abstractions, type reflection cache, and the metadata model that every other component depends on.

#### Deliverables

| # | Task | Namespace / Location | Spec Ref |
|---|---|---|---|
| 1.1 | Define `TypePair` readonly struct | `SmartMapp.Net` | §4.1 |
| 1.2 | Define `Blueprint` sealed record | `SmartMapp.Net` | §4.1 |
| 1.3 | Define `PropertyLink` sealed record | `SmartMapp.Net` | §4.1 |
| 1.4 | Define `MappingStrategy` enum (`Emit`, `Compiled`, `Interpreted`, `SourceGenerated`) | `SmartMapp.Net` | §4.2 |
| 1.5 | Define `ConventionMatch` enum/record (traceability of how a link was made) | `SmartMapp.Net` | §4.1 |
| 1.6 | Define `MappingScope` class (depth, visited refs, service provider, items dict) | `SmartMapp.Net` | §13, §20 |
| 1.7 | Build `TypeModel` — cached reflection wrapper over `Type` (properties, fields, methods, constructors, attributes, inheritance chain) | `SmartMapp.Net.Discovery` | §5 |
| 1.8 | Build `TypeModelCache` — `ConcurrentDictionary<Type, TypeModel>` | `SmartMapp.Net.Caching` | §9.8 |
| 1.9 | Define core interfaces: `IValueProvider`, `ITypeTransformer` | `SmartMapp.Net` | §7.5, §7.6 |
| 1.10 | Define `ISculptor`, `IMapper<TOrigin, TTarget>` interfaces (signatures only) | `SmartMapp.Net` | §14.1 |

#### Tests (~40)

- `TypeModel` correctly reflects: public props, init-only props, required members, record ctors, fields, methods, nested types, generic types, inheritance chains.
- `TypePair` equality, hashing.
- `Blueprint` immutability (cannot mutate after creation).
- `MappingScope` depth tracking, reference tracking.

#### Exit Criteria

- All foundation types compile and pass unit tests.
- No runtime dependencies outside BCL.

---

### Sprint 2 — Convention Engine (Weeks 3–4)

> The intelligent auto-linking brain. This is the core differentiator — make property linking "just work."

#### Deliverables

| # | Task | Namespace / Location | Spec Ref |
|---|---|---|---|
| 2.1 | Define `IPropertyConvention` interface | `SmartMapp.Net.Conventions` | §10.4 |
| 2.2 | `ExactNameConvention` — case-insensitive exact property name match | `SmartMapp.Net.Conventions` | §5.2 |
| 2.3 | `FlatteningConvention` — `CustomerAddressCity` → `Customer.Address.City` | `SmartMapp.Net.Conventions` | §5.3, §8.4 |
| 2.4 | `UnflatteningConvention` — reverse of flattening | `SmartMapp.Net.Conventions` | §8.4 |
| 2.5 | `PrefixDroppingConvention` — strip `Get`, `m_`, etc. | `SmartMapp.Net.Conventions` | §5.5 |
| 2.6 | `CaseConvention` — snake_case, camelCase, PascalCase interop | `SmartMapp.Net.Conventions` | §5.4 |
| 2.7 | `MethodToPropertyConvention` — `GetFullName()` → `FullName` | `SmartMapp.Net.Conventions` | §5.2 |
| 2.8 | `AbbreviationConvention` — `Addr` → `Address` via alias dict | `SmartMapp.Net.Conventions` | §6.4 |
| 2.9 | `StructuralSimilarityScorer` — scores how well two types match (0.0–1.0) | `SmartMapp.Net.Conventions` | §5.2 |
| 2.10 | `ConventionPipeline` — ordered execution of conventions, returns `List<PropertyLink>` | `SmartMapp.Net.Conventions` | §10.5 |
| 2.11 | Define `ITypeConvention` interface + default `NameSuffixTypeConvention` (Order → OrderDto/OrderViewModel) | `SmartMapp.Net.Conventions` | §10.4 |

#### Tests (~80)

- Exact name: `Id` → `Id`, `Name` → `Name`, case-insensitive.
- Flattening: `Customer.Address.City` → `CustomerAddressCity` (1, 2, 3 levels deep).
- Unflattening: reverse of above.
- Prefix dropping: `GetName()` → `Name`, `m_id` → `Id`.
- Case conversion: `first_name` → `FirstName`, `firstName` → `FirstName`.
- Method linking: `GetTotal()` → `Total`.
- Abbreviations: `Addr` → `Address`, `Qty` → `Quantity`.
- Structural similarity scoring: exact match = 1.0, partial = 0.x, none = 0.0.
- Convention pipeline ordering and short-circuit behavior.
- Type conventions: `Order` pairs with `OrderDto`, `OrderViewModel`, etc.

#### Exit Criteria

- Convention pipeline can produce a `List<PropertyLink>` for any two types with no user config.
- ≥ 90% of same-assembly DTO pairs link correctly with zero configuration.

---

### Sprint 3 — Built-in Type Transformers (Weeks 5–6)

> Type coercion layer — every transformer is a standalone, testable unit.

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 3.1 | `ITypeTransformer<TOrigin, TTarget>` generic interface (already defined in S1) | §7.5 |
| 3.2 | `ParsableTransformer` — `string` → `IParsable<T>` (generic math) | §7.1 |
| 3.3 | `ToStringTransformer` — any `T` → `string` via `ToString()` with culture | §7.1 |
| 3.4 | `DateTimeTransformers` — `DateTime` ↔ `DateTimeOffset`, `DateOnly`, `TimeOnly` | §7.1 |
| 3.5 | `EnumTransformers` — enum ↔ string, enum ↔ enum (by name, by value) | §7.1, §7.3 |
| 3.6 | `GuidTransformer` — `Guid` ↔ `string` | §7.1 |
| 3.7 | `UriTransformer` — `string` → `Uri` | §7.1 |
| 3.8 | `BoolIntTransformer` — `bool` ↔ `int` | §7.1 |
| 3.9 | `NullableTransformer` — `T` ↔ `Nullable<T>` | §7.1 |
| 3.10 | `Base64Transformer` — `byte[]` ↔ `string` | §7.1 |
| 3.11 | `JsonElementTransformer` — `JsonElement` ↔ `T` | §7.1 |
| 3.12 | `ImplicitExplicitOperatorTransformer` — detect and use cast operators | §7.2 |
| 3.13 | `TypeTransformerRegistry` — stores and looks up transformers by `(Type, Type)` | `SmartMapp.Net.Transformers` |
| 3.14 | String transformation options: `TrimAll`, `NullToEmpty`, custom `Apply` | §7.4 |

#### Tests (~60)

- Each transformer: valid input, null input, edge cases (empty string, min/max values, invalid parse).
- Enum: by name (case-insensitive), by value, with fallback.
- Nullable wrap/unwrap with null handling.
- Operator detection and invocation.
- Registry lookup: exact match, inheritance match, no match.

#### Exit Criteria

- All 18+ built-in conversions from §7.1 table are implemented and tested.
- `TypeTransformerRegistry` resolves the correct transformer for any `(Type, Type)` pair.

---

### Sprint 4 — Mapping Engine: Expression Compiler (Weeks 7–8)

> The first functional mapping engine — expression tree compilation. This is the "cold path" that every mapping starts with before potential IL promotion.

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 4.1 | `ExpressionMappingCompiler` — builds `Expression<Func<TOrigin, MappingScope, TTarget>>` from a `Blueprint` | §4.2, §9 |
| 4.2 | Parameterless constructor support | §4.4 |
| 4.3 | Best-match constructor support (match origin props to ctor params by name + type) | §4.4 |
| 4.4 | Record / primary constructor support | §4.4 |
| 4.5 | Init-only property assignment via expression tree | §8.8 |
| 4.6 | Required member enforcement | §8.8 |
| 4.7 | Null-safe navigation in origin access (`origin.Customer?.Name`) | §4.3 |
| 4.8 | Nested object recursive mapping (detect complex type → call mapper) | §8.2 |
| 4.9 | Circular reference tracking via `MappingScope.VisitedObjects` identity map | §8.5 |
| 4.10 | Depth limit enforcement | §8.5 |
| 4.11 | Type transformer integration (insert conversion call when types differ) | §4.3 |
| 4.12 | `BlueprintCompiler` — orchestrates: take `Blueprint` → produce compiled `Delegate` | §4.2 |
| 4.13 | `MappingDelegateCache` — `ConcurrentDictionary<TypePair, Delegate>` | §9.8 |

#### Tests (~50)

- Flat DTO mapping (10 simple properties).
- Constructor matching (parameterless, best-match, record positional).
- Init-only and required members.
- Nested 1-level, 2-level, 3-level objects.
- Null origin, null nested properties.
- Circular reference: self-referencing type, mutual reference, deep cycle.
- Depth limit hit → stop recursion, no exception.
- Type transformation in compiled expression (e.g., `DateTime` → `DateTimeOffset`).

#### Exit Criteria

- `sculptor.Map<Order, OrderDto>(order)` works end-to-end for flat, nested, and recursive types using expression compilation.
- All mappings return correct results.

---

### Sprint 5 — Collections & Flattening (Weeks 9–10)

> Map every collection type .NET offers, and make flattening/unflattening seamless.

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 5.1 | `CollectionMapper` — detect source/target collection type and dispatch | §8.3 |
| 5.2 | Array mapping (`T[]` → `T[]`) with `Array.Copy` fast-path for same type | §8.3 |
| 5.3 | `List<T>` mapping with pre-sized allocation | §8.3 |
| 5.4 | `IEnumerable<T>` → materialized `List<T>` / `T[]` | §8.3 |
| 5.5 | `ICollection<T>`, `IReadOnlyList<T>`, `IReadOnlyCollection<T>` | §8.3 |
| 5.6 | `HashSet<T>` (set semantics preserved) | §8.3 |
| 5.7 | `Dictionary<K,V>` (key + value mapping) | §8.3, §8.6 |
| 5.8 | Immutable collections: `ImmutableList<T>`, `ImmutableArray<T>` (builder pattern) | §8.3 |
| 5.9 | `ObservableCollection<T>`, `ReadOnlyCollection<T>` | §8.3 |
| 5.10 | `Dictionary<string, object>` ↔ object mapping | §8.6 |
| 5.11 | ValueTuple mapping: `(int Id, string Name)` ↔ object | §8.7 |
| 5.12 | Integrate flattening into the mapping expression (flatten/unflatten during compilation) | §8.4 |

#### Tests (~40)

- Every collection type in §8.3 table: empty, single, many items.
- Element mapping within collections (complex types).
- Dictionary: string keys, int keys, value mapping.
- Dictionary ↔ object round-trip.
- Tuple ↔ object round-trip.
- Flattening 3 levels deep.
- Unflattening 3 levels deep.
- Collection of collections (nested lists).

#### Exit Criteria

- All 12 collection types from §8.3 table map correctly.
- Flattening/unflattening works for 1–3 levels without configuration.

---

### Sprint 6 — Inheritance, Polymorphism & Fluent API (Weeks 11–12)

> Two critical features in one sprint: polymorphic dispatch and the user-facing configuration API.

#### Deliverables — Inheritance (§8.1)

| # | Task | Spec Ref |
|---|---|---|
| 6.1 | `InheritanceResolver` — walks type hierarchy, finds best matching derived Blueprint | §8.1 |
| 6.2 | Automatic polymorphic mapping: `Map<Shape, ShapeDto>(circle)` → `CircleDto` | §8.1 |
| 6.3 | `ExtendWith<TDerivedOrigin, TDerivedTarget>()` explicit config | §8.1 |
| 6.4 | `DiscriminateBy()` with `When()` / `Otherwise()` | §8.1 |
| 6.5 | `InheritFrom<TBase>()` — inherit links from base Blueprint | §6.5 |
| 6.6 | Interface/abstract type mapping: `Materialize<T>()` or `DispatchProxy` generation | §8.9 |

#### Deliverables — Fluent API (§6)

| # | Task | Spec Ref |
|---|---|---|
| 6.7 | `MappingBlueprint` abstract base class with `Design(IBlueprintBuilder)` | §6.1 |
| 6.8 | `IBlueprintBuilder` with `Bind<S,D>()` returning `IBindingRule<S,D>` | §6.1, §14.3 |
| 6.9 | `IBindingRule<S,D>` — full fluent chain: `.Property()`, `.BuildWith()`, `.When()`, `.OnMapping()`, `.OnMapped()`, `.Bidirectional()`, `.DepthLimit()`, `.TrackReferences()`, etc. | §6.5 |
| 6.10 | `IPropertyRule<S,D,M>` — `.From()`, `.Skip()`, `.FallbackTo()`, `.When()`, `.OnlyIf()`, `.TransformWith()`, `.SetOrder()`, `.PostProcess()` | §6.5 |
| 6.11 | Bidirectional mapping: `.Bidirectional()` auto-generates inverse Blueprint | §8.12 |
| 6.12 | Blueprint validation: detect duplicate bindings, missing required links in strict mode | §12.1 |

#### Tests (~50)

- Polymorphic: base → correct derived type for 2-level, 3-level hierarchies.
- Polymorphic collection: `List<Shape>` → mixed `[CircleDto, RectangleDto]`.
- Discriminator: dispatch by property value.
- Interface proxy: map to interface target.
- Fluent API: every method on `IBindingRule` and `IPropertyRule` modifies the Blueprint correctly.
- Bidirectional: map A→B then B→A.
- Blueprint validation: strict mode catches unlinked required members.

#### Exit Criteria

- Polymorphic mapping works for inheritance hierarchies without configuration.
- Full fluent API is functional and produces valid Blueprints.
- Bidirectional mapping round-trips correctly.

---

### Sprint 7 — Attribute Config, Builder, Sculptor (Weeks 13–14)

> Wire everything together: attributes, the builder pattern, and the main `Sculptor` class.

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 7.1 | All attribute definitions: `[MappedBy<T>]`, `[MapsInto<T>]`, `[LinkedFrom]`, `[LinksTo]`, `[Unmapped]`, `[TransformWith<T>]`, `[ProvideWith<T>]`, `[MapsIntoEnum]` | §14.4 |
| 7.2 | `AttributeConvention` — reads attributes during convention pipeline | §6.3 |
| 7.3 | `AssemblyScanner` — discovers blueprints, providers, transformers, attributed types | §5.1 |
| 7.4 | `SculptorBuilder` — accumulates config, calls scanner, builds blueprints, caches delegates | §14.3 |
| 7.5 | `SculptorBuilder.Forge()` — freezes configuration, returns `Sculptor` | §13.2 |
| 7.6 | `Sculptor` class (implements `ISculptor`) — Map, MapAll, MapToArray, MapLazy, Compose, Inspect, GetMappingAtlas | §14.1 |
| 7.7 | `Mapper<TOrigin, TTarget>` class (implements `IMapper<S,D>`) — strongly-typed fast-path | §14.1 |
| 7.8 | `SculptorOptions` — global config: conventions, nulls, throughput, max recursion, strict mode, validation | §6.4 |
| 7.9 | Inline configuration: `options.Bind<S,D>(rule => ...)` without Blueprint class | §6.2 |
| 7.10 | Configuration validation: `ISculptorConfiguration.Validate()` | §12.1 |
| 7.11 | `MappingInspection` — traces every link decision for `Inspect<S,D>()` | §12.2 |
| 7.12 | `MappingAtlas` — graph of all registered type pairs, DOT format export | §12.3 |

#### Tests (~50)

- Attribute-driven mapping: `[MappedBy<Order>]` on `OrderDto` auto-links.
- `[Unmapped]` skips property, `[LinkedFrom]` overrides convention.
- Assembly scanner finds all blueprints, providers, transformers in test assembly.
- `SculptorBuilder.Forge()` freezes config — post-forge modification throws.
- `Sculptor.Map<S,D>()` end-to-end for every scenario tested so far.
- `IMapper<S,D>` injection and usage.
- `Inspect<S,D>()` returns correct link trace.
- `GetMappingAtlas()` contains all registered pairs, DOT output is valid.
- Global options: null handling, max recursion, strict mode.

#### Exit Criteria

- Full end-to-end mapping works: `SculptorBuilder().Forge().Map<Order, OrderDto>(order)`.
- Attributes, fluent API, and auto-discovery all produce correct Blueprints.
- The library is **functionally complete** for v1.0 at this point.

---

### Sprint 8 — DI Integration, Validation, Samples & Test Hardening (Weeks 15–16)

> Production-ready: DI registration, startup validation, documentation, samples, test coverage push.

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 8.1 | `SmartMapp.Net.DependencyInjection` package | §11 |
| 8.2 | `AddSculptor()` — scans calling assembly, registers ISculptor (Singleton) | §11.1 |
| 8.3 | `AddSculptor(Action<SculptorOptions>)` — with configuration callback | §11.1 |
| 8.4 | `AddSculptor(ServiceLifetime)` — configurable lifetime | §11.1 |
| 8.5 | Register `IMapper<S,D>` for every discovered type pair | §11.2 |
| 8.6 | `ValidateOnStartup` — hosted service validates all blueprints on app start | §12.1 |
| 8.7 | DI-resolved value providers: `IValueProvider<S,D,M>` created via DI | §11.4 |
| 8.8 | Extension methods: `MapTo<T>()` on objects, `SelectAs<T>()` on IQueryable | §14.5 |
| 8.9 | IQueryable projection: `SelectAs<D>()` builds `Expression<Func<S,D>>` for EF Core | §8.10 |
| 8.10 | Multi-origin `Compose<T>()` | §8.11 |
| 8.11 | Sample: `SmartMapp.Net.Samples.Console` — basic mapping scenarios | §16 |
| 8.12 | Sample: `SmartMapp.Net.Samples.MinimalApi` — ASP.NET Minimal API with EF Core | §16.1 |
| 8.13 | README.md with quick-start, feature list, comparison table | — |
| 8.14 | Integration tests: DI resolution, EF Core SelectAs, startup validation | §17.1 |
| 8.15 | Coverage push to ≥ 90% | — |
| 8.16 | BenchmarkDotNet suite: flat, nested, collection, vs AutoMapper baseline | §9.9, §17.2.6 |

#### Tests (~60+)

- DI: `AddSculptor()` registers `ISculptor`, `IMapper<,>`, `ISculptorConfiguration`.
- DI: provider resolved from DI container.
- DI: startup validation catches invalid config.
- IQueryable: `SelectAs<OrderDto>()` generates valid expression tree.
- Compose: multi-origin composition.
- Edge cases: null origins, empty collections, max depth, all-null nested objects, types with no matching members.
- Thread safety: concurrent `Map` calls from 10K parallel tasks.

#### Exit Criteria

- **v1.0 release candidate.**
- All 400+ tests pass.
- ≥ 90% code coverage.
- Benchmarks run and produce baseline numbers.
- Samples compile and run.
- NuGet packages `SmartMapp.Net` and `SmartMapp.Net.DependencyInjection` are packable.

---

## 3. Phase 2 — Performance (v1.1)

**Goal:** Achieve sub-100ns flat mapping, near-zero allocations, and automatic parallel collection mapping.

**Duration:** Sprints 9–11 (6 weeks)

---

### Sprint 9 — IL Emit Engine & Adaptive Promotion (Weeks 17–18)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 9.1 | `ILEmitMappingCompiler` — emits `DynamicMethod` from `Blueprint` | §9.2 |
| 9.2 | Emit: parameterless ctor, property getters/setters, null checks | §9.2 |
| 9.3 | Emit: nested object mapping (call delegate recursively) | §9.2 |
| 9.4 | Emit: type transformer invocation | §9.2 |
| 9.5 | `AdaptivePromotionManager` — tracks invocation counts per `TypePair` | §9.2 |
| 9.6 | Atomic delegate swap via `Interlocked.CompareExchange` | §9.2 |
| 9.7 | Background thread promotion (does not block mapping) | §9.2 |
| 9.8 | Configurable promotion threshold (default: 10) | §9.2 |
| 9.9 | `MappingStrategy` selection: Expression → IL Emit after threshold | §4.2 |

#### Tests (~25)

- IL-emitted mapper produces identical results to expression-compiled mapper for all existing test scenarios.
- Promotion occurs after threshold invocations.
- Atomic swap is lock-free and thread-safe under concurrent load.
- Background promotion does not cause mapping failures during transition.
- Benchmark: IL Emit ≤ 100ns for flat 10-prop DTO.

#### Exit Criteria

- Flat mapping benchmark ≤ 100ns (warm).
- Adaptive promotion transparent to caller.

---

### Sprint 10 — Object Pooling & Cache Optimization (Weeks 19–20)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 10.1 | `MappingScopePool` — `ObjectPool<MappingScope>` | §9.4 |
| 10.2 | `ArrayPool<T>` integration for collection buffers | §9.4 |
| 10.3 | `StringBuilderPool` for string concatenation in transformers | §9.4 |
| 10.4 | Migrate Blueprint cache to `FrozenDictionary<TypePair, Blueprint>` (.NET 8+) | §9.8 |
| 10.5 | Migrate delegate cache to `FrozenDictionary` | §9.8 |
| 10.6 | `#if NET8_0_OR_GREATER` conditional compilation for frozen collections | §9.8 |
| 10.7 | Memory allocation benchmarks — verify zero-alloc hot path | §9.1 |

#### Tests (~15)

- MappingScope reuse: pool returns clean instance, reset works correctly.
- ArrayPool: buffer returned after use, no leaks.
- FrozenDictionary: faster lookups than ConcurrentDictionary (benchmark).
- Memory benchmark: 0 bytes allocated for flat mapping (pooled).

#### Exit Criteria

- Memory per flat mapping: 0 bytes (pooled) verified by BenchmarkDotNet `[MemoryDiagnoser]`.

---

### Sprint 11 — Parallel Collections & SIMD (Weeks 21–22)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 11.1 | `ParallelCollectionMapper` — `Parallel.ForEachAsync` with partitioning | §9.6 |
| 11.2 | Configurable threshold, max parallelism, partition size | §9.6 |
| 11.3 | Pre-allocated results array (order-preserving) | §9.6 |
| 11.4 | Per-partition `MappingScope` (no sharing across threads) | §13.3 |
| 11.5 | `ForceParallelCollections()` / `DisableParallelCollections()` per binding | §8.3 |
| 11.6 | SIMD primitive copy: `Buffer.BlockCopy` for same-type primitives | §9.5 |
| 11.7 | SIMD widening: `Vector.Widen` for `int[]` → `long[]` etc. | §9.5 |
| 11.8 | Updated benchmarks: parallel collection vs sequential | §9.9 |

#### Tests (~20)

- Parallel: 10K items, results match sequential in order and value.
- Parallel: exception in one partition aggregates correctly.
- Parallel: below threshold stays sequential.
- SIMD: `int[]` → `int[]` uses `BlockCopy`.
- SIMD: `int[]` → `long[]` uses widening.
- Benchmark: 10K collection < 800us (parallel) vs ~3000us (AutoMapper).

#### Exit Criteria

- All §9.1 performance targets met or exceeded.
- Parallel mapping is order-preserving and thread-safe.

---

## 4. Phase 3 — Advanced (v1.2)

**Goal:** Source generators, mapping pipeline, streaming, composition, and collection merging.

**Duration:** Sprints 12–15 (8 weeks)

---

### Sprint 12 — Source Generator: SmartMapp.Net.Codegen (Weeks 23–24)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 12.1 | Roslyn incremental generator project setup (`IIncrementalGenerator`) | §9.3 |
| 12.2 | Detect `[MappedBy<T>]` on `partial` classes/records | §9.3 |
| 12.3 | Emit `MapFromXxx()` static method as partial class member | §9.3 |
| 12.4 | Support: simple properties, flattened properties, `[Unmapped]` skip | §9.3 |
| 12.5 | Support: record constructors, init-only setters | §9.3 |
| 12.6 | Emit nullable reference type annotations | §9.3 |
| 12.7 | Diagnostic: warning for unlinked required members | §9.3 |
| 12.8 | `GeneratedMapperRegistry` — auto-register generated mappers at startup | §9.3 |
| 12.9 | Integration: `SculptorBuilder` prefers generated mapper over expression/IL | §9.3 |

#### Tests (~20, using Verify snapshot testing)

- Generated code snapshot: flat DTO, nested DTO, record DTO.
- Generated mapper produces identical results to runtime mapper.
- `[Unmapped]` property excluded from generated code.
- Warning emitted for unlinked required member.
- Generated mapper registered and used by `ISculptor`.

#### Exit Criteria

- `[MappedBy<Order>] public partial record OrderDto` generates compile-time mapping code.
- Generated code is AOT-compatible and trimmer-safe.

---

### Sprint 13 — Mapping Filter Pipeline & Hooks (Weeks 25–26)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 13.1 | `IMappingFilter` interface | §10.2 |
| 13.2 | `MappingContext` record (origin, target, types, scope, items) | §10.2 |
| 13.3 | `MappingDelegate` — next in chain | §10.2 |
| 13.4 | `MappingFilterPipeline` — builds and executes chain-of-responsibility | §10.2 |
| 13.5 | Global filter registration: `options.Filters.Add<T>()` | §10.2 |
| 13.6 | Per-binding filter registration: `.UseFilters(f => ...)` | §10.2 |
| 13.7 | `IMappingHook<TOrigin, TTarget>` interface | §10.3 |
| 13.8 | Hook auto-discovery: scan for implementations, match by `TTarget` interface/type | §10.3 |
| 13.9 | Hook execution: `OnMapping` before, `OnMapped` after | §10.3 |
| 13.10 | DI-resolved filters and hooks (Scoped lifetime) | §11.2 |

#### Tests (~25)

- Filter: logging filter captures timing.
- Filter: caching filter short-circuits on cache hit.
- Filter: chain order matters (first registered = outermost).
- Hook: `OnMapped` modifies target after mapping.
- Hook: auto-discovered for interface-typed targets.
- Filter + hook combined: both execute in correct order.

#### Exit Criteria

- Filter pipeline wraps every mapping call.
- Hooks fire for type-matching mappings.

---

### Sprint 14 — Streaming, Lazy Mapping & Collection Merging (Weeks 27–28)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 14.1 | `MapLazy<S,D>()` — returns `IEnumerable<D>` that maps on iteration | §9.7 |
| 14.2 | `MapStream<S,D>()` — returns `IAsyncEnumerable<D>` with cancellation | §9.7 |
| 14.3 | Collection merge: `.Merge(matchBy, onAdd, onRemove, onUpdate)` | §8.3 |
| 14.4 | Existing-target mapping: `sculptor.Map(origin, existingTarget)` | §14.1 |
| 14.5 | Map-onto for nested objects (update in place) | §16.8 |

#### Tests (~20)

- Lazy: deferred execution verified (side-effect counter).
- Lazy: partial enumeration works.
- Stream: `IAsyncEnumerable` cancellation respected.
- Stream: large async enumerable (10K items) maps correctly.
- Merge: add new, update matching, remove missing.
- Merge: empty collections, all-new, all-removed.
- Existing target: properties overwritten, nulls handled per config.

#### Exit Criteria

- Streaming mapping works with EF Core `AsAsyncEnumerable()`.
- Collection merging handles EF Core update pattern.

---

### Sprint 15 — Multi-Origin Composition & Addon System (Weeks 29–30)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 15.1 | `Compose<TTarget>(params object[] origins)` | §8.11 |
| 15.2 | `ICompositionRule<TTarget>` with `.FromOrigin<T>(rule => ...)` | §8.11, §14.3 |
| 15.3 | Composition Blueprint: multiple origin types → single target | §8.11 |
| 15.4 | `ISculptorAddon` interface | §10.1 |
| 15.5 | `IAddonContext` — provides access to conventions, transformers, filters, options | §10.1 |
| 15.6 | `options.Addons.Install<T>()` | §10.1 |
| 15.7 | Mapping events: `OnBlueprintCreated`, `OnMappingError`, `OnPropertyLinked` | §10.6 |
| 15.8 | `IMappingStrategyProvider` — custom strategy hook | §10.7 |

#### Tests (~20)

- Compose: 2, 3, 4 origins into single target.
- Compose: overlapping properties from different origins (last wins or configurable).
- Addon: install addon, verify it adds conventions/transformers.
- Events: `OnBlueprintCreated` fires for every blueprint.
- Events: `OnMappingError` suppresses exception when `Handled = true`.

#### Exit Criteria

- Multi-origin composition works.
- Addon system extensible.
- All v1.2 features complete.

---

## 5. Phase 4 — Ecosystem (v1.3)

**Goal:** ASP.NET Core integration, observability, health checks, and validation.

**Duration:** Sprints 16–18 (6 weeks)

---

### Sprint 16 — ASP.NET Core & Health Checks (Weeks 31–32)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 16.1 | `SmartMapp.Net.AspNetCore` package | §3.2 |
| 16.2 | `MapSculptorInsights(prefix)` — diagnostic endpoints | §12.3 |
| 16.3 | `GET /insights/mappings` — JSON list | §12.3 |
| 16.4 | `GET /insights/mappings/atlas` — DOT/SVG graph | §12.3 |
| 16.5 | `GET /insights/mappings/{type}` — details for type | §12.3 |
| 16.6 | `AddSculptorHealthCheck()` — validates blueprints | §11.6 |
| 16.7 | Keyed services: `AddSculptor("key", options => ...)` | §11.5 |
| 16.8 | Sample: `SmartMapp.Net.Samples.MinimalApi` updated with insights endpoints | §16.1 |

#### Tests (~15)

- Health check: healthy when all blueprints valid, unhealthy when invalid.
- Insights endpoints: return JSON, DOT format.
- Keyed services: two sculptor instances with different configs.

---

### Sprint 17 — OpenTelemetry & Logging (Weeks 33–34)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 17.1 | `SmartMapp.Net.Insights` package | §3.2 |
| 17.2 | `AddSculptorInstrumentation()` for tracing | §12.4 |
| 17.3 | `AddSculptorInstrumentation()` for metrics | §12.4 |
| 17.4 | Metrics: `smartmappnet.mappings.total`, `.duration`, `.errors`, `.collections.parallel`, `.cache.hits`, `.cache.promotions` | §12.4 |
| 17.5 | Traces: span per mapping with origin_type, target_type, strategy, link_count | §12.4 |
| 17.6 | Structured logging via `ILogger<SmartMapp.Net>` | §12.5 |
| 17.7 | `DebuggerDisplay` on `Blueprint` | §12.6 |

#### Tests (~15)

- Metrics: counters increment on mapping.
- Traces: span created with correct attributes.
- Logging: debug-level logs for property links.

---

### Sprint 18 — FluentValidation Integration & Polish (Weeks 35–36)

#### Deliverables

| # | Task | Spec Ref |
|---|---|---|
| 18.1 | `SmartMapp.Net.Validation` package | §3.2 |
| 18.2 | `.ValidateAfterMapping(validator)` on binding rule | §6.5 |
| 18.3 | Auto-discover `AbstractValidator<T>` and apply post-map | §18.1 |
| 18.4 | `MappingValidationException` with structured errors | §16.4 |
| 18.5 | Final documentation pass: API docs, migration guide, README | §15.4 |
| 18.6 | Blazor sample: `SmartMapp.Net.Samples.Blazor` | §18.3 |
| 18.7 | Full regression test run across all packages | — |
| 18.8 | NuGet package metadata, icons, descriptions for all 6 packages | — |

#### Tests (~10)

- Validation: valid DTO passes, invalid throws `MappingValidationException`.
- Validation: auto-discovered validator applied.

#### Exit Criteria

- All 6 NuGet packages ready for release.
- v1.3 feature-complete.

---

## 6. Phase 5 — Intelligence (v2.0)

**Goal:** Dynamic/expando mapping, enhanced Roslyn analyzers, IDE code fixes.

**Duration:** Sprints 19–21+ (6+ weeks, scope TBD)

---

### Sprint 19 — Dynamic & Dictionary Deep Mapping (Weeks 37–38)

| # | Task | Spec Ref |
|---|---|---|
| 19.1 | `ExpandoObject` mapping (as `IDictionary<string, object>`) | §19 |
| 19.2 | Deep dictionary mapping (nested `Dictionary<string, object>` → nested objects) | §8.6, §19 |
| 19.3 | Tuple mapping enhancements (unnamed tuples, Item1/Item2 matching) | §19 |

### Sprint 20 — Roslyn Analyzers & Code Fixes (Weeks 39–40)

| # | Task | Spec Ref |
|---|---|---|
| 20.1 | Analyzer: warn when `[MappedBy<T>]` target has unlinked required members | §19 |
| 20.2 | Analyzer: warn when `Map<S,D>()` called with no discoverable binding | §19 |
| 20.3 | Code fix: generate `[LinkedFrom]` attribute for ambiguous links | §19 |
| 20.4 | Code fix: generate `MappingBlueprint` stub from type pair | §19 |

### Sprint 21 — Expression Visitors & Profiler Integration (Weeks 41–42)

| # | Task | Spec Ref |
|---|---|---|
| 21.1 | Custom `ExpressionVisitor` for `SelectAs<D>()` optimization | §19 |
| 21.2 | dotTrace / PerfView annotation support | §19 |

---

## 7. Cross-Cutting Concerns

### 7.1 Testing Strategy

| Layer | Framework | Approach |
|---|---|---|
| **Unit** | xUnit + FluentAssertions | One test class per component, AAA pattern |
| **Integration** | xUnit + TestServer + EF Core InMemory | Service registration, HTTP endpoints, DB projection |
| **Codegen** | Verify (snapshot) | Compare generated source to golden files |
| **Performance** | BenchmarkDotNet | Regression tracked per PR, fail on >10% regression |
| **Mutation** | Stryker.NET | Gate on ≥ 80% mutation score |
| **Thread Safety** | `Parallel.For` stress tests | 10K concurrent maps, zero exceptions |

### 7.2 Code Quality Gates (CI)

- Build: zero warnings (TreatWarningsAsErrors)
- Tests: 100% pass
- Coverage: ≥ 90% line coverage
- Analyzers: zero diagnostics (StyleCop, .NET analyzers, SonarAnalyzer)
- Benchmarks: no regression > 10%
- Package: valid NuGet package with symbols

### 7.3 Documentation

| Artifact | When | Tool |
|---|---|---|
| XML doc comments | Every public API member | C# `///` |
| README.md | Sprint 8 (initial), updated each phase | Manual |
| API reference | Generated each release | DocFX or xmldoc2md |
| Migration guide | Sprint 8 | Manual |
| Architecture guide | Sprint 8 | Manual, diagrams in Mermaid |
| Samples | Sprint 8, updated each phase | Runnable projects |

### 7.4 Multi-Targeting

```xml
<!-- SmartMapp.Net (core) -->
<TargetFrameworks>netstandard2.1;net8.0;net9.0</TargetFrameworks>

<!-- Conditional features -->
#if NET8_0_OR_GREATER
  FrozenDictionary, keyed services, generic math IParsable<T>
#endif
```

---

## 8. Dependency Graph

Sprint execution order with dependencies:

```
S1 (Foundation) ──────────────────────────────────────────┐
  │                                                        │
  v                                                        │
S2 (Conventions) ─────────────────────┐                    │
  │                                    │                   │
  v                                    v                   v
S3 (Transformers) ──────> S4 (Expression Compiler) ──> S5 (Collections)
                                      │
                                      v
                          S6 (Inheritance + Fluent API)
                                      │
                                      v
                          S7 (Attributes + Builder + Sculptor)
                                      │
                                      v
                          S8 (DI + Samples + Tests) ────> v1.0 RELEASE
                                      │
                          ┌───────────┼───────────────┐
                          v           v               v
                    S9 (IL Emit) S10 (Pooling)  S11 (Parallel+SIMD)
                          │           │               │
                          └───────────┴───────────────┘
                                      │
                                      v ──────────────> v1.1 RELEASE
                          ┌───────────┼───────────┐
                          v           v           v
                    S12 (Codegen) S13 (Filters) S14 (Streaming)
                          │           │           │
                          └───────────┴───────────┘
                                      │
                                      v
                              S15 (Compose + Addons) ──> v1.2 RELEASE
                                      │
                          ┌───────────┼───────────┐
                          v           v           v
                    S16 (ASP.NET) S17 (OTel)  S18 (Validation)
                          │           │           │
                          └───────────┴───────────┘
                                      │
                                      v ──────────────> v1.3 RELEASE
                                      │
                          ┌───────────┼───────────┐
                          v           v           v
                    S19 (Dynamic) S20 (Analyzers) S21 (Visitors)
                          │           │               │
                          └───────────┴───────────────┘
                                      │
                                      v ──────────────> v2.0 RELEASE
```

---

## 9. Risk Register

| # | Risk | Impact | Probability | Mitigation |
|---|---|---|---|---|
| R1 | IL Emit complexity causes hard-to-debug runtime errors | High | Medium | Comprehensive IL verification tests; fallback to expression compiler on error; interpreted mode for debugging |
| R2 | Source generator incremental caching invalidation bugs | Medium | Medium | Snapshot tests via Verify; test with large solutions; follow Roslyn best practices |
| R3 | Parallel collection mapping causes race conditions | High | Low | Per-partition MappingScope; pre-allocated results array; extensive stress tests |
| R4 | Expression tree complexity for `SelectAs<D>()` with deep graphs | Medium | Medium | Limit projection depth; provide escape hatch with raw `Expression<Func<S,D>>`; EF Core integration tests |
| R5 | Performance regression between sprints | Medium | Medium | BenchmarkDotNet in CI; fail build on >10% regression; track baselines in artifacts |
| R6 | .NET Standard 2.1 compatibility friction | Low | Medium | Conditional compilation; feature flags; test on all target frameworks in CI |
| R7 | Circular reference tracking has performance overhead on hot paths | Medium | Low | Opt-in via `.TrackReferences()`; disabled by default when no circular types detected |
| R8 | Startup scan time grows with large assemblies | Medium | Low | Parallel scanning; lazy blueprint compilation; configurable scan scope |
| R9 | AutoMapper behavioral compatibility gaps hurt adoption | Medium | Medium | Migration guide with side-by-side examples; comprehensive test suite mirroring AutoMapper scenarios |

---

## 10. Definition of Done

A sprint is **done** when:

- [ ] All deliverables implemented and code reviewed
- [ ] All new code has XML doc comments on public members
- [ ] Unit tests pass (100%)
- [ ] Integration tests pass (if applicable)
- [ ] Code coverage ≥ 90%
- [ ] No new analyzer warnings
- [ ] BenchmarkDotNet results recorded (if applicable)
- [ ] No performance regression > 10%
- [ ] CHANGELOG.md updated
- [ ] Sprint demo / review completed

A **release** is done when:

- [ ] All sprint DoDs met for the phase
- [ ] Mutation testing score ≥ 80%
- [ ] README and docs updated
- [ ] Samples compile and run
- [ ] NuGet packages built with correct metadata
- [ ] Release notes written
- [ ] Tagged in Git

---

## Summary

| Phase | Version | Sprints | Weeks | Key Deliverable |
|---|---|---|---|---|
| **Core** | v1.0 | 1–8 | 1–16 | Fully functional library, DI, tests, samples |
| **Performance** | v1.1 | 9–11 | 17–22 | IL Emit, SIMD, parallel, object pooling |
| **Advanced** | v1.2 | 12–15 | 23–30 | Source gen, filters, hooks, streaming, addons |
| **Ecosystem** | v1.3 | 16–18 | 31–36 | ASP.NET, OpenTelemetry, health, validation |
| **Intelligence** | v2.0 | 19–21+ | 37–42+ | Dynamic, analyzers, code fixes |

**Total: ~465+ tests, 6 NuGet packages, 21 sprints, 42 weeks.**

---

*SmartMapp.Net: Less code. More features. Better performance.*
