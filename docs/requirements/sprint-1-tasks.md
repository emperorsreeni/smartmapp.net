# Sprint 1 — Foundation Types & Type Metadata

> **Sprint Duration:** Weeks 1–2 (10 working days)  
> **Sprint Goal:** Establish the foundational type system, metadata model, and core abstractions that every subsequent sprint depends on.  
> **Total Estimate:** 68 story points (~68 hours)  
> **Parent Document:** [implementation-plan.md](./implementation-plan.md)  
> **Spec Reference:** [spec.md](./spec.md) — Sections 4, 5, 7, 9, 13, 14, 20

---

## Table of Contents

1. [Task Pipeline Overview](#1-task-pipeline-overview)
2. [Execution Order & Dependency Graph](#2-execution-order--dependency-graph)
3. [Detailed Task Breakdown](#3-detailed-task-breakdown)
4. [Test Inventory](#4-test-inventory)
5. [Sprint Definition of Done](#5-sprint-definition-of-done)

---

## 1. Task Pipeline Overview

```
DAY 1-2          DAY 2-3          DAY 3-4          DAY 4-5          DAY 5-7         DAY 7-9          DAY 9-10
───────          ──────           ──────           ──────           ──────          ──────           ───────
S1-T00           S1-T01 ──┐
(Bootstrap)  ──> (TypePair) │
                            ├──> S1-T04 ──────────────────────> S1-T07
                 S1-T02 ──┤  (ConventionMatch)                (TypeModel)──> S1-T08 ──> S1-T10
                 (Strategy) │                                                (Cache)    (Tests+Polish)
                            ├──> S1-T05
                 S1-T03 ──┘  (IValueProvider
                 (ITypeTransformer)  + IMappingFilter)
                                     │
                                     ├──> S1-T06 ──────────────────────────> S1-T09
                                     │   (PropertyLink + Blueprint)          (ISculptor
                                     │                                        + IMapper)
                                     └──> S1-T06b
                                         (MappingScope)
```

### Execution Lanes

| Lane | Tasks | Focus |
|---|---|---|
| **Lane A — Primitives** | T00 → T01 → T02 → T04 | Enums, structs, value types |
| **Lane B — Interfaces** | T03 → T05 | Core abstractions (providers, transformers, filters) |
| **Lane C — Composites** | T06, T06b | Records that depend on A + B |
| **Lane D — Metadata** | T07 → T08 | Reflection model and cache |
| **Lane E — Public API** | T09 | ISculptor / IMapper interface signatures |
| **Lane F — Verification** | T10 | Full test suite, polish, documentation |

---

## 2. Execution Order & Dependency Graph

```
S1-T00 ─────────> S1-T01 ─────> S1-T04 ─────────────> S1-T07 ──────> S1-T08
(Bootstrap)       (TypePair)     (ConventionMatch)      (TypeModel)    (TypeModelCache)
                                                                              │
                  S1-T02                                                      v
                  (MappingStrategy)──────────────────┐                  S1-T10
                                                     │                  (Tests+Polish)
                  S1-T03 ──────> S1-T05 ────────> S1-T06 ─────────────> S1-T09
                  (ITypeTransformer)(IValueProvider  (Blueprint +        (ISculptor
                                    + IMappingFilter) PropertyLink)      + IMapper)
                                         │
                                         └──────> S1-T06b
                                                  (MappingScope)
```

### Critical Path

**T00 → T01 → T04 → T07 → T08 → T10** (repo bootstrap through type metadata cache — longest sequential chain)

---

## 3. Detailed Task Breakdown

---

### S1-T00 — Project Bootstrap & Repository Scaffolding

| Field | Detail |
|---|---|
| **ID** | S1-T00 |
| **Title** | Project Bootstrap & Repository Scaffolding |
| **Estimate** | 5 pts (5 hours) |
| **Day** | Day 1 |
| **Predecessors** | None (first task) |
| **Dependents** | All other tasks (T01–T10) |

#### Description

Create the solution structure, project files, build infrastructure, and CI pipeline skeleton. This is the foundation on which all code will be written.

**Subtasks:**

1. Create `SmartMapp.Net.sln` with the `src/SmartMapp.Net/` project targeting `netstandard2.1;net8.0;net9.0`.
2. Create `tests/SmartMapp.Net.Tests.Unit/` project targeting `net9.0`, referencing xUnit, FluentAssertions, NSubstitute.
3. Create `Directory.Build.props` with shared settings: `<LangVersion>13</LangVersion>`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
4. Create `Directory.Packages.props` for Central Package Management.
5. Create `global.json` pinning SDK to 9.0.x.
6. Create `.editorconfig` with C# coding style rules.
7. Create `.gitignore` for .NET projects.
8. Create `.github/workflows/ci.yml` skeleton (restore → build → test → pack).
9. Verify `dotnet build` and `dotnet test` succeed with zero warnings.

#### Acceptance Criteria

- [ ] `dotnet build` succeeds for `SmartMapp.Net` on all three TFMs with zero warnings.
- [ ] `dotnet test` succeeds for `SmartMapp.Net.Tests.Unit` (empty test placeholder passes).
- [ ] `Directory.Build.props` enables nullable, C# 13, implicit usings, and warnings-as-errors.
- [ ] Central Package Management configured in `Directory.Packages.props`.
- [ ] CI pipeline skeleton committed (can be triggered manually).

#### Technical Considerations

- `SmartMapp.Net.Codegen` (source gen) targets `netstandard2.0` — **do not** create it in this sprint, just reserve the folder.
- `netstandard2.1` target requires polyfills for some .NET 8 APIs; use `#if NET8_0_OR_GREATER` guards from the start.
- Choose `<RootNamespace>SmartMapp.Net</RootNamespace>` so sub-namespaces like `SmartMapp.Net.Discovery` are created via folders.
- Use `<IsPackable>true</IsPackable>` on `src/` projects, `<IsPackable>false</IsPackable>` on test projects.

---

### S1-T01 — `TypePair` Readonly Struct

| Field | Detail |
|---|---|
| **ID** | S1-T01 |
| **Title** | Define `TypePair` readonly struct |
| **Estimate** | 3 pts (3 hours) |
| **Day** | Day 2 |
| **Predecessors** | S1-T00 (project must exist) |
| **Dependents** | S1-T04, S1-T06, S1-T07, S1-T08, S1-T09 |

#### Description

`TypePair` is the universal key used across all caches (blueprint cache, delegate cache, convention link cache). It uniquely identifies an `(OriginType, TargetType)` combination.

**Subtasks:**

1. Create `src/SmartMapp.Net/TypePair.cs`.
2. Implement as `readonly record struct` with `Type OriginType` and `Type TargetType` properties.
3. Override `GetHashCode()` with an optimized hash combine (`HashCode.Combine`).
4. Implement `IEquatable<TypePair>`.
5. Override `ToString()` → `"Order -> OrderDto"` format.
6. Add static factory: `TypePair.Of<TOrigin, TTarget>()`.

```csharp
public readonly record struct TypePair(Type OriginType, Type TargetType)
{
    public static TypePair Of<TOrigin, TTarget>() => new(typeof(TOrigin), typeof(TTarget));
    public override string ToString() => $"{OriginType.Name} -> {TargetType.Name}";
}
```

#### Acceptance Criteria

- [ ] `TypePair` is a `readonly record struct`.
- [ ] Two `TypePair` instances with the same `(OriginType, TargetType)` are equal.
- [ ] Two `TypePair` instances with swapped types are NOT equal.
- [ ] `GetHashCode()` is consistent with `Equals()`.
- [ ] `TypePair.Of<Order, OrderDto>()` returns correct pair.
- [ ] `ToString()` returns `"Order -> OrderDto"`.
- [ ] Can be used as a `Dictionary<TypePair, T>` key with correct behavior.

#### Technical Considerations

- `readonly record struct` gives value equality, copy semantics, and zero-alloc for stack locals.
- `HashCode.Combine` is available in `netstandard2.1` — no polyfill needed.
- This struct will be used as a dictionary key millions of times — hash quality matters. `HashCode.Combine(OriginType, TargetType)` is sufficient.
- Do NOT store `Type` references weakly — blueprints live for app lifetime.

---

### S1-T02 — `MappingStrategy` Enum

| Field | Detail |
|---|---|
| **ID** | S1-T02 |
| **Title** | Define `MappingStrategy` enum |
| **Estimate** | 2 pts (2 hours) |
| **Day** | Day 2 |
| **Predecessors** | S1-T00 |
| **Dependents** | S1-T06 (Blueprint references this enum) |

#### Description

Enumerate the four execution strategies that govern how a mapping delegate is generated.

**Subtasks:**

1. Create `src/SmartMapp.Net/MappingStrategy.cs`.
2. Define enum members: `ExpressionCompiled`, `ILEmit`, `SourceGenerated`, `Interpreted`.
3. Add `[Description]` attributes for diagnostic display.
4. XML-doc every member explaining when it is used.

```csharp
public enum MappingStrategy
{
    /// <summary>Default cold-path: compile Expression<Func<>> at first use.</summary>
    ExpressionCompiled = 0,

    /// <summary>Hot-path: DynamicMethod emitted after adaptive promotion threshold.</summary>
    ILEmit = 1,

    /// <summary>Build-time: code emitted by Roslyn source generator. AOT-safe.</summary>
    SourceGenerated = 2,

    /// <summary>Debug mode: step-through traceability, slowest execution.</summary>
    Interpreted = 3,
}
```

#### Acceptance Criteria

- [ ] Enum has exactly 4 members: `ExpressionCompiled`, `ILEmit`, `SourceGenerated`, `Interpreted`.
- [ ] Default value (0) is `ExpressionCompiled` (first-call fallback strategy per §4.2).
- [ ] Each member has XML documentation.
- [ ] Enum can be serialized/deserialized by name (for diagnostics JSON output).

#### Technical Considerations

- Explicitly assign `= 0, 1, 2, 3` — never rely on implicit ordering for an enum that may be persisted in telemetry.
- `ExpressionCompiled` is the default (value 0) because every mapping starts there before potential promotion.

---

### S1-T03 — `ITypeTransformer` Interface

| Field | Detail |
|---|---|
| **ID** | S1-T03 |
| **Title** | Define `ITypeTransformer` interface |
| **Estimate** | 3 pts (3 hours) |
| **Day** | Day 2–3 |
| **Predecessors** | S1-T00 |
| **Dependents** | S1-T05, S1-T06 (PropertyLink references it) |

#### Description

Core abstraction for converting a value from one type to another. Used both in built-in transformers (e.g., `DateTime` → `DateTimeOffset`) and user-defined custom transformers.

**Subtasks:**

1. Create `src/SmartMapp.Net/Abstractions/ITypeTransformer.cs`.
2. Define non-generic marker: `ITypeTransformer`.
3. Define generic: `ITypeTransformer<TOrigin, TTarget>` inheriting the marker.
4. Method: `TTarget Transform(TOrigin origin, MappingScope scope)`.
5. Add `bool CanTransform(Type originType, Type targetType)` on the marker for runtime discovery.

```csharp
public interface ITypeTransformer
{
    bool CanTransform(Type originType, Type targetType);
}

public interface ITypeTransformer<in TOrigin, out TTarget> : ITypeTransformer
{
    TTarget Transform(TOrigin origin, MappingScope scope);
}
```

#### Acceptance Criteria

- [ ] `ITypeTransformer` (non-generic marker) has `CanTransform(Type, Type)`.
- [ ] `ITypeTransformer<TOrigin, TTarget>` inherits `ITypeTransformer`.
- [ ] `Transform` method accepts `MappingScope` for context access.
- [ ] Compiles on all three TFMs.
- [ ] XML documentation on interface and method.

#### Technical Considerations

- `MappingScope` is defined in S1-T06b — use a forward reference / define scope first if needed, or define a minimal placeholder in this task and flesh it out in T06b.
- `in TOrigin, out TTarget` variance enables covariant/contravariant usage in collections of transformers.
- Non-generic marker is essential for `TypeTransformerRegistry` lookups in Sprint 3.

---

### S1-T04 — `ConventionMatch` Record

| Field | Detail |
|---|---|
| **ID** | S1-T04 |
| **Title** | Define `ConventionMatch` record for link traceability |
| **Estimate** | 3 pts (3 hours) |
| **Day** | Day 3 |
| **Predecessors** | S1-T01 (TypePair — for ToString context) |
| **Dependents** | S1-T06 (PropertyLink references it) |

#### Description

Every `PropertyLink` records **how** it was linked — enabling the `Inspect<S,D>()` diagnostic. `ConventionMatch` captures the convention name, confidence, and the origin member path.

**Subtasks:**

1. Create `src/SmartMapp.Net/ConventionMatch.cs`.
2. Define as sealed record.
3. Properties: `string ConventionName`, `string OriginMemberPath`, `double Confidence` (0.0–1.0), `bool IsExplicit` (true if user-configured, false if auto-discovered).
4. Static factories for common matches: `ConventionMatch.ExactName(path)`, `ConventionMatch.Flattened(path)`, `ConventionMatch.Explicit(path)`, `ConventionMatch.CustomProvider(providerType)`.
5. Override `ToString()` for diagnostic display.

```csharp
public sealed record ConventionMatch
{
    public required string ConventionName { get; init; }
    public required string OriginMemberPath { get; init; }
    public double Confidence { get; init; } = 1.0;
    public bool IsExplicit { get; init; }

    public static ConventionMatch ExactName(string path)
        => new() { ConventionName = "ExactName", OriginMemberPath = path, Confidence = 1.0 };

    public static ConventionMatch Flattened(string path)
        => new() { ConventionName = "Flattening", OriginMemberPath = path, Confidence = 1.0 };

    public static ConventionMatch Explicit(string path)
        => new() { ConventionName = "ExplicitBinding", OriginMemberPath = path, IsExplicit = true, Confidence = 1.0 };

    public static ConventionMatch CustomProvider(Type providerType)
        => new() { ConventionName = $"CustomProvider:{providerType.Name}", OriginMemberPath = "", IsExplicit = true, Confidence = 1.0 };

    public override string ToString() => IsExplicit
        ? $"{ConventionName}"
        : $"{ConventionName} ({OriginMemberPath}, {Confidence:P0})";
}
```

#### Acceptance Criteria

- [ ] `ConventionMatch` is a sealed record with `ConventionName`, `OriginMemberPath`, `Confidence`, `IsExplicit`.
- [ ] Static factories produce correct instances for each match type.
- [ ] `ToString()` renders human-readable diagnostic string.
- [ ] `Confidence` defaults to `1.0`.
- [ ] Immutable — no public setters beyond `init`.

#### Technical Considerations

- `required` keyword is C# 11+ / .NET 7+ — use `#if NET7_0_OR_GREATER` or avoid `required` for `netstandard2.1` compatibility. **Recommendation:** use `required` and add `RequiredMemberAttribute` polyfill for netstandard2.1.
- This record is part of every `PropertyLink` — keep it lightweight (no reference to reflection `MemberInfo`).
- Confidence < 1.0 is used by `StructuralSimilarityScorer` in Sprint 2.

---

### S1-T05 — `IValueProvider` and `IMappingFilter` Interfaces

| Field | Detail |
|---|---|
| **ID** | S1-T05 |
| **Title** | Define `IValueProvider` and `IMappingFilter` interfaces |
| **Estimate** | 5 pts (5 hours) |
| **Day** | Day 3–4 |
| **Predecessors** | S1-T03 (ITypeTransformer — for consistency in abstractions) |
| **Dependents** | S1-T06 (PropertyLink uses IValueProvider; Blueprint uses IMappingFilter), S1-T06b (MappingScope), S1-T09 |

#### Description

Define the two other core extensibility interfaces: value providers (extract values from origin) and mapping filters (middleware pipeline).

**Subtasks:**

1. Create `src/SmartMapp.Net/Abstractions/IValueProvider.cs`.
2. Define non-generic marker: `IValueProvider` with `object? Provide(object origin, object target, string targetMemberName, MappingScope scope)`.
3. Define generic: `IValueProvider<in TOrigin, in TTarget, out TMember>` with `TMember Provide(TOrigin origin, TTarget target, string targetMemberName, MappingScope scope)`.
4. Create `src/SmartMapp.Net/Abstractions/IMappingFilter.cs`.
5. Define `MappingDelegate` delegate: `delegate Task<object?> MappingDelegate(MappingContext context)`.
6. Define `MappingContext` record: `OriginType`, `TargetType`, `Origin` (object), `Target` (object?), `MappingScope`, `Items` (dictionary).
7. Define `IMappingFilter` with `Task<object?> ApplyAsync(MappingContext context, MappingDelegate next)`.

```csharp
// IValueProvider
public interface IValueProvider
{
    object? Provide(object origin, object target, string targetMemberName, MappingScope scope);
}

public interface IValueProvider<in TOrigin, in TTarget, out TMember> : IValueProvider
{
    TMember Provide(TOrigin origin, TTarget target, string targetMemberName, MappingScope scope);
}

// MappingContext
public sealed record MappingContext
{
    public required Type OriginType { get; init; }
    public required Type TargetType { get; init; }
    public required object Origin { get; init; }
    public object? Target { get; init; }
    public required MappingScope Scope { get; init; }
    public IDictionary<string, object?> Items { get; init; } = new Dictionary<string, object?>();
}

// MappingDelegate + IMappingFilter
public delegate Task<object?> MappingDelegate(MappingContext context);

public interface IMappingFilter
{
    Task<object?> ApplyAsync(MappingContext context, MappingDelegate next);
}
```

#### Acceptance Criteria

- [ ] `IValueProvider` non-generic has `Provide(object, object, string, MappingScope)`.
- [ ] `IValueProvider<TOrigin, TTarget, TMember>` inherits the marker.
- [ ] `MappingContext` is a sealed record with all required properties.
- [ ] `MappingDelegate` is a named delegate.
- [ ] `IMappingFilter.ApplyAsync` follows chain-of-responsibility pattern (accepts `MappingDelegate next`).
- [ ] All types compile on all three TFMs.
- [ ] XML documentation on all public members.

#### Technical Considerations

- `IMappingFilter` is async (`Task<object?>`) to support async filters (e.g., caching filter). Synchronous filters can return `Task.FromResult`.
- `MappingContext.Items` allows filters to pass data down the pipeline (similar to `HttpContext.Items`).
- `in TOrigin, in TTarget, out TMember` variance maximizes flexibility.
- `MappingScope` is used here — if T06b hasn't been started, create a minimal placeholder class first.

---

### S1-T06 — `PropertyLink` and `Blueprint` Records

| Field | Detail |
|---|---|
| **ID** | S1-T06 |
| **Title** | Define `PropertyLink` and `Blueprint` sealed records |
| **Estimate** | 8 pts (8 hours) |
| **Day** | Day 4–5 |
| **Predecessors** | S1-T01 (TypePair), S1-T02 (MappingStrategy), S1-T04 (ConventionMatch), S1-T05 (IValueProvider, IMappingFilter, ITypeTransformer) |
| **Dependents** | S1-T07, S1-T08, S1-T09, S1-T10 |

#### Description

The two most important data structures in the entire library. `Blueprint` is the immutable instruction set for a type pair; `PropertyLink` is a single instruction within it.

**Subtasks:**

1. Create `src/SmartMapp.Net/PropertyLink.cs`.
2. Implement `PropertyLink` as a sealed record with:
   - `MemberInfo TargetMember` — the target property/field being set.
   - `IValueProvider Provider` — how the value is obtained.
   - `ITypeTransformer? Transformer` — optional type conversion.
   - `ConventionMatch LinkedBy` — traceability.
   - `bool IsSkipped` — true if explicitly skipped via `.Skip()` or `[Unmapped]`.
   - `object? Fallback` — value to use when origin is null (`.FallbackTo()`).
   - `int Order` — execution order (`.SetOrder()`).
   - `Func<object, bool>? Condition` — `.When()` predicate.
   - `Func<object, bool>? PreCondition` — `.OnlyIf()` predicate.
3. Create `src/SmartMapp.Net/Blueprint.cs`.
4. Implement `Blueprint` as a sealed record with:
   - `Type OriginType`
   - `Type TargetType`
   - `TypePair TypePair` — computed from the above
   - `IReadOnlyList<PropertyLink> Links`
   - `MappingStrategy Strategy`
   - `bool IsParallelEligible`
   - `IReadOnlyList<IMappingFilter> Filters`
   - `int MaxDepth` — from `.DepthLimit()`, default `int.MaxValue`
   - `bool TrackReferences` — from `.TrackReferences()`
   - `Func<object, object>? TargetFactory` — from `.BuildWith()`
   - `Action<object, object>? OnMapping` — pre-map hook
   - `Action<object, object>? OnMapped` — post-map hook
5. Add `DebuggerDisplay` attribute per §12.6.
6. Add `Blueprint.Empty(TypePair)` static factory for test scaffolding.
7. Ensure both types are fully immutable — all collections are `IReadOnlyList`, no public mutable state.

#### Acceptance Criteria

- [ ] `PropertyLink` is a sealed record with all 9 properties.
- [ ] `Blueprint` is a sealed record with all 12 properties.
- [ ] Both are fully immutable — no mutable state after construction.
- [ ] `Blueprint.TypePair` is computed from `OriginType` and `TargetType`.
- [ ] `Blueprint` has `[DebuggerDisplay("{DebugView}")]` with format `"Order -> OrderDto [ExpressionCompiled] (8 links)"`.
- [ ] `Blueprint.Empty(pair)` creates a valid empty blueprint.
- [ ] `PropertyLink.IsSkipped == true` means the link is a no-op placeholder.
- [ ] XML documentation on all public members.

#### Technical Considerations

- These records are stored in `FrozenDictionary` (Phase 2) — keep them lightweight. Avoid storing expression trees or large delegates directly on Blueprint; those go in the delegate cache.
- `Condition` and `PreCondition` are `Func<object, bool>?` (non-generic) to allow Blueprint to be non-generic. The typed lambda is compiled during blueprint building.
- `Links` should be sorted by `Order` property at blueprint construction time.
- Thread safety: Blueprint is immutable, so it is inherently thread-safe (§13.1).
- Use `IReadOnlyList<T>` not `ImmutableList<T>` to avoid the `System.Collections.Immutable` dependency for `netstandard2.1`.

---

### S1-T06b — `MappingScope` Class

| Field | Detail |
|---|---|
| **ID** | S1-T06b |
| **Title** | Define `MappingScope` class |
| **Estimate** | 6 pts (6 hours) |
| **Day** | Day 4–5 |
| **Predecessors** | S1-T05 (IValueProvider, IMappingFilter — scope provides services to these) |
| **Dependents** | S1-T07, S1-T09, S1-T10 |

#### Description

Per-mapping context object. Created for each top-level `Map()` call, passed down through nested mappings and into providers/transformers. **Not thread-safe** — each thread gets its own scope.

**Subtasks:**

1. Create `src/SmartMapp.Net/MappingScope.cs`.
2. Properties:
   - `int CurrentDepth` — current recursion depth (incremented on nested map).
   - `int MaxDepth` — from Blueprint.
   - `Dictionary<object, object> VisitedObjects` — identity map for circular ref tracking (keyed by ReferenceEquals).
   - `IServiceProvider? ServiceProvider` — optional DI container.
   - `IDictionary<string, object?> Items` — arbitrary state bag.
   - `CancellationToken CancellationToken` — propagated to async operations.
3. Methods:
   - `MappingScope CreateChild()` — returns a new scope with `CurrentDepth + 1`, shared `VisitedObjects`, shared `ServiceProvider`.
   - `bool TryGetVisited(object origin, out object? target)` — check identity map.
   - `void TrackVisited(object origin, object target)` — add to identity map.
   - `bool IsMaxDepthReached => CurrentDepth >= MaxDepth`.
   - `T GetService<T>()` — resolve from `ServiceProvider`, throw if null.
   - `T? TryGetService<T>()` — resolve from `ServiceProvider`, return null if not found.
   - `void Reset()` — clear all state for object pool reuse.

```csharp
public sealed class MappingScope
{
    public int CurrentDepth { get; private set; }
    public int MaxDepth { get; init; } = int.MaxValue;
    public IServiceProvider? ServiceProvider { get; init; }
    public CancellationToken CancellationToken { get; init; }
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

    public bool IsMaxDepthReached => CurrentDepth >= MaxDepth;

    private Dictionary<object, object>? _visited;

    public MappingScope CreateChild() => new()
    {
        CurrentDepth = CurrentDepth + 1,
        MaxDepth = MaxDepth,
        ServiceProvider = ServiceProvider,
        CancellationToken = CancellationToken,
        _visited = _visited,
    };

    public bool TryGetVisited(object origin, [NotNullWhen(true)] out object? target)
    {
        if (_visited is not null)
            return _visited.TryGetValue(origin, out target);
        target = null;
        return false;
    }

    public void TrackVisited(object origin, object target)
    {
        _visited ??= new Dictionary<object, object>(ReferenceEqualityComparer.Instance);
        _visited[origin] = target;
    }

    public void Reset()
    {
        CurrentDepth = 0;
        _visited?.Clear();
        Items.Clear();
    }
}
```

#### Acceptance Criteria

- [ ] `MappingScope` is a sealed class (not a record — mutable state for depth and visited tracking).
- [ ] `CreateChild()` increments depth and shares the visited map.
- [ ] `TryGetVisited` / `TrackVisited` use `ReferenceEqualityComparer` for identity (not structural equality).
- [ ] `IsMaxDepthReached` returns true when `CurrentDepth >= MaxDepth`.
- [ ] `Reset()` clears all state for pool reuse.
- [ ] `GetService<T>()` throws if service not found; `TryGetService<T>()` returns null.
- [ ] **Not thread-safe** by design — documented in XML comments.

#### Technical Considerations

- `ReferenceEqualityComparer.Instance` is .NET 5+. For `netstandard2.1`, provide a polyfill: `class ReferenceEqualityComparer : IEqualityComparer<object>` using `RuntimeHelpers.GetHashCode` and `ReferenceEquals`.
- Lazy `_visited` dictionary — most mappings have no circular refs, so avoid allocating it unless `TrackVisited` is called.
- This class will be pooled in Phase 2 (Sprint 10) via `ObjectPool<MappingScope>`. Design `Reset()` now to enable that.
- `Items` dictionary is useful for filters to pass data down the pipeline.
- `[NotNullWhen(true)]` attribute on `out` parameter for null-safety.

---

### S1-T07 — `TypeModel` Cached Reflection Wrapper

| Field | Detail |
|---|---|
| **ID** | S1-T07 |
| **Title** | Build `TypeModel` — cached reflection wrapper |
| **Estimate** | 13 pts (13 hours) |
| **Day** | Day 5–7 |
| **Predecessors** | S1-T01 (TypePair), S1-T04 (ConventionMatch — for return types) |
| **Dependents** | S1-T08 (TypeModelCache), S1-T10 |

#### Description

`TypeModel` is the single most performance-critical metadata type. It wraps `System.Type` with pre-computed, cached reflection data used by conventions, expression compiler, and IL emitter. Every property, method, constructor, and field is analyzed once and cached.

**Subtasks:**

1. Create `src/SmartMapp.Net/Discovery/TypeModel.cs`.
2. Create `src/SmartMapp.Net/Discovery/MemberModel.cs` — wrapper for `PropertyInfo` / `FieldInfo`.
3. Create `src/SmartMapp.Net/Discovery/ConstructorModel.cs` — wrapper for `ConstructorInfo`.
4. Create `src/SmartMapp.Net/Discovery/MethodModel.cs` — wrapper for parameterless methods that look like property getters (e.g., `GetFullName()`).
5. `TypeModel` properties:
   - `Type ClrType`
   - `IReadOnlyList<MemberModel> ReadableMembers` — properties with getters + public fields.
   - `IReadOnlyList<MemberModel> WritableMembers` — properties with setters (including init-only) + public fields.
   - `IReadOnlyList<ConstructorModel> Constructors` — sorted by parameter count (descending).
   - `ConstructorModel? PrimaryConstructor` — the record positional ctor or best-match ctor.
   - `IReadOnlyList<MethodModel> ParameterlessValueMethods` — `Get*()` methods with no parameters and non-void return.
   - `bool IsRecord` — detected via `<Clone>$` method.
   - `bool HasParameterlessConstructor`
   - `bool IsAbstract`
   - `bool IsInterface`
   - `bool IsGenericType`
   - `bool IsCollection` — implements `IEnumerable` (not string).
   - `bool IsDictionary` — implements `IDictionary<,>`.
   - `bool IsNullable` — `Nullable<T>`.
   - `Type? UnderlyingNullableType` — `T` from `Nullable<T>`.
   - `Type? CollectionElementType` — `T` from `IEnumerable<T>`.
   - `IReadOnlyList<Type> InheritanceChain` — from type up to (excluding) `object`.
   - `IReadOnlyList<Type> ImplementedInterfaces`
6. `MemberModel` properties:
   - `MemberInfo MemberInfo` — underlying PropertyInfo or FieldInfo.
   - `string Name`
   - `Type MemberType` — return type.
   - `bool IsReadable`
   - `bool IsWritable`
   - `bool IsInitOnly` — `init` setter.
   - `bool IsRequired` — `required` keyword.
   - `bool IsField`
   - `IReadOnlyList<Attribute> CustomAttributes`
7. `ConstructorModel` properties:
   - `ConstructorInfo ConstructorInfo`
   - `IReadOnlyList<ParameterInfo> Parameters`
   - `int ParameterCount`
   - `bool IsPrimary` — only ctor, or decorated with `[PrimaryConstructor]`, or record ctor.
8. Helper: `TypeModel.GetMember(string name)` — case-insensitive lookup by name.
9. Helper: `TypeModel.GetMemberPath(string compoundName)` — resolve `"CustomerAddressCity"` to chain of members (for Sprint 2 flattening).

#### Acceptance Criteria

- [ ] `TypeModel` correctly identifies all public readable properties of a POCO class.
- [ ] `TypeModel` correctly identifies init-only properties.
- [ ] `TypeModel` correctly identifies `required` properties.
- [ ] `TypeModel` correctly identifies record types (via `<Clone>$`).
- [ ] `TypeModel` detects the primary constructor for records with positional params.
- [ ] `TypeModel` sorts constructors by parameter count descending.
- [ ] `TypeModel` identifies collection types (`List<T>`, `T[]`, `IEnumerable<T>`) and extracts element type.
- [ ] `TypeModel` identifies dictionary types and extracts key/value types.
- [ ] `TypeModel` identifies `Nullable<T>` and extracts underlying type.
- [ ] `TypeModel` builds full inheritance chain.
- [ ] `TypeModel` detects parameterless value methods (`GetXxx()`).
- [ ] `GetMember(name)` performs case-insensitive lookup.
- [ ] All reflection is performed once during construction — no lazy evaluation of member lists.

#### Technical Considerations

- **Performance:** `TypeModel` construction is the hot startup path. Use `type.GetProperties(BindingFlags.Public | BindingFlags.Instance)` not `GetMembers()`. Pre-filter and cache.
- **Record detection:** Records have a compiler-generated `<Clone>$` method. Check `type.GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance)`.
- **Init-only detection:** Check for `IsExternalInit` type on the setter's return parameter modifiers. Polyfill needed for netstandard2.1.
- **Required detection:** Check for `RequiredMemberAttribute` on the type. C# 11+ emits this.
- **Collection detection:** Use `type.GetInterfaces()` and look for `IEnumerable<T>`. Special-case `string` (it implements `IEnumerable<char>` but is not a collection).
- **Generic math:** `IParsable<T>` detection is needed for Sprint 3 transformers — not needed here yet.
- Thread-safe: `TypeModel` is immutable after construction — safe to cache and share.

---

### S1-T08 — `TypeModelCache`

| Field | Detail |
|---|---|
| **ID** | S1-T08 |
| **Title** | Build `TypeModelCache` |
| **Estimate** | 4 pts (4 hours) |
| **Day** | Day 7–8 |
| **Predecessors** | S1-T07 (TypeModel must exist) |
| **Dependents** | S1-T10 |

#### Description

Thread-safe, process-lifetime cache of `TypeModel` instances. Used by every component that needs type metadata.

**Subtasks:**

1. Create `src/SmartMapp.Net/Caching/TypeModelCache.cs`.
2. Internal `ConcurrentDictionary<Type, TypeModel>` store.
3. `TypeModel GetOrAdd(Type type)` — returns cached model or creates and caches a new one.
4. `TypeModel GetOrAdd<T>()` — generic convenience.
5. Static singleton: `TypeModelCache.Default` for global use.
6. `void Clear()` for testing scenarios.
7. Expose `int Count` for diagnostics.

```csharp
public sealed class TypeModelCache
{
    public static TypeModelCache Default { get; } = new();

    private readonly ConcurrentDictionary<Type, TypeModel> _cache = new();

    public TypeModel GetOrAdd(Type type) => _cache.GetOrAdd(type, static t => new TypeModel(t));
    public TypeModel GetOrAdd<T>() => GetOrAdd(typeof(T));
    public int Count => _cache.Count;
    public void Clear() => _cache.Clear();
}
```

#### Acceptance Criteria

- [ ] `GetOrAdd` returns the same `TypeModel` instance for the same `Type`.
- [ ] `GetOrAdd` is thread-safe — concurrent calls for the same type do not crash or produce different instances.
- [ ] `TypeModelCache.Default` is a global singleton.
- [ ] `Clear()` empties the cache.
- [ ] `Count` returns the number of cached types.
- [ ] `TypeModel` constructor is called exactly once per `Type` (verified via test counter).

#### Technical Considerations

- `ConcurrentDictionary.GetOrAdd` with `static` lambda avoids closure allocation.
- In Phase 2 (Sprint 10), this will be migrated to `FrozenDictionary` after startup scan completes. Design the interface so the switch is transparent.
- Do not use `Lazy<TypeModel>` inside the dictionary — `GetOrAdd` with a factory is sufficient and simpler.

---

### S1-T09 — `ISculptor` and `IMapper<S,D>` Interface Definitions

| Field | Detail |
|---|---|
| **ID** | S1-T09 |
| **Title** | Define `ISculptor` and `IMapper<TOrigin, TTarget>` interface signatures |
| **Estimate** | 8 pts (8 hours) |
| **Day** | Day 8–9 |
| **Predecessors** | S1-T06 (Blueprint, PropertyLink — for return types in Inspect), S1-T06b (MappingScope), S1-T05 (MappingContext) |
| **Dependents** | S1-T10 |

#### Description

Define the public API surface for the mapping engine. **Signatures only** in this sprint — implementations come in Sprint 4 (expression compiler) and Sprint 7 (Sculptor class).

**Subtasks:**

1. Create `src/SmartMapp.Net/ISculptor.cs`.
2. Define all method signatures per §14.1:
   - `TTarget Map<TOrigin, TTarget>(TOrigin origin)`
   - `TTarget Map<TOrigin, TTarget>(TOrigin origin, TTarget existingTarget)`
   - `object Map(object origin, Type originType, Type targetType)`
   - `object Map(object origin, object target, Type originType, Type targetType)`
   - `IReadOnlyList<TTarget> MapAll<TOrigin, TTarget>(IEnumerable<TOrigin> origins)`
   - `TTarget[] MapToArray<TOrigin, TTarget>(IEnumerable<TOrigin> origins)`
   - `IEnumerable<TTarget> MapLazy<TOrigin, TTarget>(IEnumerable<TOrigin> origins)`
   - `IAsyncEnumerable<TTarget> MapStream<TOrigin, TTarget>(IAsyncEnumerable<TOrigin> origins, CancellationToken ct = default)`
   - `TTarget Compose<TTarget>(params object[] origins)`
   - `IQueryable<TTarget> SelectAs<TTarget>(IQueryable source)`
   - `Expression<Func<TOrigin, TTarget>> GetProjection<TOrigin, TTarget>()`
   - `MappingInspection Inspect<TOrigin, TTarget>()`
   - `MappingAtlas GetMappingAtlas()`
3. Create `src/SmartMapp.Net/IMapper.cs`.
4. Define `IMapper<TOrigin, TTarget>`:
   - `TTarget Map(TOrigin origin)`
   - `TTarget Map(TOrigin origin, TTarget existingTarget)`
   - `IReadOnlyList<TTarget> MapAll(IEnumerable<TOrigin> origins)`
5. Create `src/SmartMapp.Net/Diagnostics/MappingInspection.cs` — placeholder record.
6. Create `src/SmartMapp.Net/Diagnostics/MappingAtlas.cs` — placeholder record.
7. Create `src/SmartMapp.Net/ISculptorConfiguration.cs` — per §14.2.

```csharp
// Placeholder diagnostics records
public sealed record MappingInspection
{
    public TypePair TypePair { get; init; }
    public Blueprint? Blueprint { get; init; }
    public IReadOnlyList<string> LinkTrace { get; init; } = [];
}

public sealed record MappingAtlas
{
    public IReadOnlyList<Blueprint> Blueprints { get; init; } = [];
    public string ToDotFormat() => ""; // placeholder
}
```

#### Acceptance Criteria

- [ ] `ISculptor` has all 13 method signatures from §14.1.
- [ ] `IMapper<TOrigin, TTarget>` has 3 method signatures.
- [ ] `ISculptorConfiguration` has methods: `GetAllBlueprints`, `GetBlueprint<S,D>`, `Validate`, `HasBinding<S,D>`.
- [ ] `MappingInspection` and `MappingAtlas` are placeholder records (fleshed out in Sprint 7).
- [ ] All interfaces compile on all three TFMs.
- [ ] `IAsyncEnumerable` method is behind `#if NET8_0_OR_GREATER` or available via `Microsoft.Bcl.AsyncInterfaces` for netstandard2.1.
- [ ] XML documentation on every method.

#### Technical Considerations

- `IAsyncEnumerable<T>` is in `System.Runtime` for .NET Core but requires `Microsoft.Bcl.AsyncInterfaces` for netstandard2.1. Add conditional `PackageReference`.
- `Expression<Func<TOrigin, TTarget>>` requires `System.Linq.Expressions` — available in all TFMs.
- `IQueryable` requires `System.Linq.Queryable` — available in all TFMs.
- These interfaces are the **public contract** — they must remain stable after v1.0. Design carefully; changes require a major version bump.
- Use `params object[]` for `Compose` — in .NET 9+, consider `params ReadOnlySpan<object>` behind a TFM guard.

---

### S1-T10 — Comprehensive Test Suite & Sprint Polish

| Field | Detail |
|---|---|
| **ID** | S1-T10 |
| **Title** | Comprehensive test suite, XML docs, and sprint polish |
| **Estimate** | 8 pts (8 hours) |
| **Day** | Day 9–10 |
| **Predecessors** | S1-T01 through S1-T09 (all tasks) |
| **Dependents** | Sprint 2 (all tasks depend on Sprint 1 being solid) |

#### Description

Final validation pass: write any remaining tests, ensure 100% coverage of foundation types, verify XML docs, run analyzers clean.

**Subtasks:**

1. **TypePair tests** (~5):
   - Equality: same types → equal.
   - Inequality: swapped types → not equal.
   - Inequality: different types → not equal.
   - Hash code consistency.
   - Dictionary key usage.
   - `Of<S,D>()` factory.
   - `ToString()` format.

2. **MappingStrategy tests** (~3):
   - Default value is `ExpressionCompiled`.
   - All 4 values exist.
   - `Enum.Parse` round-trip.

3. **ConventionMatch tests** (~6):
   - `ExactName` factory.
   - `Flattened` factory.
   - `Explicit` factory.
   - `CustomProvider` factory.
   - `ToString()` formatting.
   - Immutability.

4. **ITypeTransformer / IValueProvider / IMappingFilter tests** (~4):
   - Can implement and mock each interface.
   - Generic and non-generic markers coexist.
   - Verify `MappingContext` record construction.

5. **PropertyLink tests** (~5):
   - Construction with all properties.
   - Immutability (record semantics).
   - `IsSkipped` behavior.
   - `Fallback` value.
   - Default `Order = 0`.

6. **Blueprint tests** (~6):
   - Construction with all properties.
   - Immutability.
   - `TypePair` derived from `OriginType` + `TargetType`.
   - `DebuggerDisplay` output.
   - `Blueprint.Empty()` factory.
   - Links sorted by `Order`.

7. **MappingScope tests** (~8):
   - `CreateChild()` increments depth.
   - `CreateChild()` shares visited map.
   - `IsMaxDepthReached` at boundary.
   - `TryGetVisited` / `TrackVisited` round-trip.
   - Identity tracking uses reference equality (not structural).
   - `GetService<T>()` throws when not found.
   - `TryGetService<T>()` returns null when not found.
   - `Reset()` clears all state.

8. **TypeModel tests** (~15):
   - Public properties of POCO class.
   - Init-only properties.
   - Required members.
   - Record type detection.
   - Primary constructor detection (record positional params).
   - Multiple constructors sorted by param count.
   - Collection detection: `List<T>`, `T[]`, `IEnumerable<T>`, `HashSet<T>`.
   - NOT collection: `string`.
   - Dictionary detection: `Dictionary<K,V>`.
   - Nullable detection and underlying type.
   - Inheritance chain.
   - Parameterless value methods (`GetFullName()`).
   - Fields (public).
   - Generic types.
   - Abstract / interface detection.

9. **TypeModelCache tests** (~4):
   - `GetOrAdd` returns same instance.
   - Thread-safety (concurrent `GetOrAdd` for same type).
   - `Clear()` empties cache.
   - Constructor called once (factory invocation count).

10. **Interface compilation tests** (~2):
    - `ISculptor` compiles and has all expected methods (reflection-based test).
    - `IMapper<S,D>` compiles.

11. **Cross-TFM build verification** — ensure `dotnet build` succeeds on all targets.
12. **XML documentation audit** — every public type and member has `///` docs.
13. **Analyzer clean** — zero warnings with `TreatWarningsAsErrors`.

#### Acceptance Criteria

- [ ] ≥ 40 unit tests pass.
- [ ] 100% statement coverage on all Sprint 1 types.
- [ ] Zero analyzer warnings.
- [ ] XML docs on all public types and members.
- [ ] `dotnet build` succeeds on `netstandard2.1`, `net8.0`, `net9.0` with zero warnings.
- [ ] `dotnet test` passes on all test TFMs.

#### Technical Considerations

- Use `FluentAssertions` for readable assertions (`result.Should().Be(expected)`).
- Use `NSubstitute` to mock `IServiceProvider` in MappingScope tests.
- Create a `tests/SmartMapp.Net.Tests.Unit/TestTypes/` folder with sample POCOs, records, DTOs, and collections for reuse across all Sprint 1 tests and future sprints.
- Tests should be organized into folders mirroring the source: `TypePairTests.cs`, `BlueprintTests.cs`, `TypeModelTests.cs`, etc.

---

## 4. Test Inventory

| Test Group | File | Count |
|---|---|---|
| TypePair | `TypePairTests.cs` | 7 |
| MappingStrategy | `MappingStrategyTests.cs` | 3 |
| ConventionMatch | `ConventionMatchTests.cs` | 6 |
| ITypeTransformer / IValueProvider / IMappingFilter | `AbstractionTests.cs` | 4 |
| PropertyLink | `PropertyLinkTests.cs` | 5 |
| Blueprint | `BlueprintTests.cs` | 6 |
| MappingScope | `MappingScopeTests.cs` | 8 |
| TypeModel | `TypeModelTests.cs` | 15 |
| TypeModelCache | `TypeModelCacheTests.cs` | 4 |
| Interface contracts | `InterfaceContractTests.cs` | 2 |
| **Total** | | **~60** |

---

## 5. Sprint Definition of Done

### Task-Level DoD

A task is **done** when:

- [ ] Code is written and compiles on all TFMs (`netstandard2.1`, `net8.0`, `net9.0`) with zero warnings.
- [ ] All `public` and `protected` members have XML documentation comments (`///`).
- [ ] Unit tests for the task are written, passing, and achieve 100% statement coverage of the new code.
- [ ] Code follows `.editorconfig` style rules — no analyzer warnings.
- [ ] Code has been self-reviewed (or peer-reviewed if team > 1).
- [ ] No `TODO` or `HACK` comments left without a linked issue.

### Sprint-Level DoD

Sprint 1 is **done** when:

- [ ] **All 12 tasks** (T00 through T10, including T06b) are complete per task-level DoD.
- [ ] **≥ 60 unit tests** pass across all test groups.
- [ ] **100% statement coverage** on all Sprint 1 source files.
- [ ] **Zero analyzer warnings** (`TreatWarningsAsErrors` enabled).
- [ ] **Zero runtime dependencies** outside the BCL (no third-party NuGet packages in `SmartMapp.Net` core).
- [ ] **Multi-TFM build** — `dotnet build` succeeds for `netstandard2.1`, `net8.0`, `net9.0`.
- [ ] **CI pipeline** — GitHub Actions workflow runs green (build + test + pack).
- [ ] **Polyfills** — `ReferenceEqualityComparer`, `IsExternalInit`, `RequiredMemberAttribute` are provided for `netstandard2.1`.
- [ ] **Test types** — reusable POCO/record/DTO test fixtures exist in `TestTypes/` folder.
- [ ] **CHANGELOG.md** — entry added for Sprint 1 work.
- [ ] **Sprint retrospective notes** — brief notes on what went well, what to improve.

### Quality Gates

| Gate | Target | Tool |
|---|---|---|
| Build | Zero warnings | `dotnet build -warnaserror` |
| Tests | 100% pass | `dotnet test` |
| Coverage | 100% of Sprint 1 types | Coverlet |
| Style | Zero violations | StyleCop.Analyzers |
| Docs | All public members | Manual audit |
| TFM | 3 targets succeed | `dotnet build` |
| Dependencies | Zero (core package) | Manual audit of `.csproj` |

---

## Appendix A — Task Summary Table

| ID | Title | Est. | Day | Predecessors | Dependents | Critical Path? |
|---|---|---|---|---|---|---|
| S1-T00 | Project Bootstrap | 5 pts | 1 | — | All | Yes |
| S1-T01 | TypePair | 3 pts | 2 | T00 | T04, T06, T07, T08, T09 | Yes |
| S1-T02 | MappingStrategy | 2 pts | 2 | T00 | T06 | No |
| S1-T03 | ITypeTransformer | 3 pts | 2–3 | T00 | T05, T06 | No |
| S1-T04 | ConventionMatch | 3 pts | 3 | T01 | T06 | Yes |
| S1-T05 | IValueProvider + IMappingFilter | 5 pts | 3–4 | T03 | T06, T06b, T09 | No |
| S1-T06 | PropertyLink + Blueprint | 8 pts | 4–5 | T01, T02, T04, T05 | T07, T08, T09, T10 | Yes |
| S1-T06b | MappingScope | 6 pts | 4–5 | T05 | T07, T09, T10 | No |
| S1-T07 | TypeModel | 13 pts | 5–7 | T01, T04 | T08, T10 | Yes |
| S1-T08 | TypeModelCache | 4 pts | 7–8 | T07 | T10 | Yes |
| S1-T09 | ISculptor + IMapper | 8 pts | 8–9 | T06, T06b, T05 | T10 | No |
| S1-T10 | Test Suite + Polish | 8 pts | 9–10 | All | Sprint 2 | Yes |
| | **TOTAL** | **68 pts** | | | | |

---

## Appendix B — Parallel Execution Schedule

For a team of **2 developers**:

| Day | Dev 1 | Dev 2 |
|---|---|---|
| 1 | **T00** (Bootstrap) | — (waiting / assist T00) |
| 2 | **T01** (TypePair) | **T02** (MappingStrategy) + **T03** (ITypeTransformer) |
| 3 | **T04** (ConventionMatch) | **T05** (IValueProvider + IMappingFilter) |
| 4 | **T06** (PropertyLink + Blueprint) — start | **T06b** (MappingScope) |
| 5 | **T06** (PropertyLink + Blueprint) — finish | **T07** (TypeModel) — start |
| 6 | **T07** (TypeModel) — continue | **T07** (TypeModel) — pair programming on complex reflection |
| 7 | **T07** (TypeModel) — finish | **T08** (TypeModelCache) |
| 8 | **T09** (ISculptor + IMapper) — start | **T10** (Test Suite) — start TypePair/Strategy/ConventionMatch tests |
| 9 | **T09** (ISculptor + IMapper) — finish | **T10** (Test Suite) — TypeModel tests, Blueprint tests |
| 10 | **T10** (Test Suite) — MappingScope tests, polish | **T10** (Test Suite) — cross-TFM verification, docs audit, CI green |

For a **solo developer**, the sprint is achievable in 10 days following the dependency graph top-to-bottom.

---

*Sprint 1 delivers the bedrock on which the entire SmartMapp.Net library is built. Every type defined here is referenced by every subsequent sprint.*
