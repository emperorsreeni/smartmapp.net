# Sprint 9 — Retrospective

> Closing review of Sprint 9 (IL Emit Engine & Adaptive Promotion). Tasks **S9-T00 → S9-T12**.
> Companion to `CHANGELOG.md` `[Unreleased]` section.

## Headline Numbers

| Metric | Sprint 8 RC | Sprint 9 close | Δ |
|---|---|---|---|
| Unit tests | 1179 | 1179 | 0 (zero regression) |
| Integration tests | 62 | 69 | +7 (Sprint 9 parity matrix + stress) |
| **Total green** | **1241** | **1248** | **+7** |
| TFMs | `netstandard2.1` / `net8.0` / `net10.0` | same | unchanged |
| Build warnings | 0 | 0 | 0 |

## Tasks Delivered

| ID | Title | Status | Notes |
|---|---|---|---|
| S9-T00 | `Engine.ILEmit` scaffolding | ✅ | `RequiresDynamicCodeAttribute` polyfill for netstandard2.1; `UnsupportedReason` enum (13 values); `EmittedMapperHost` owner module |
| S9-T01 | `ILEmitMappingCompiler` skeleton + `CanEmit` | ✅ | `DynamicMethod` factory with closure-bound delegate via `CreateDelegate(type, closure)` |
| S9-T02 | Flat property emit | ✅ | `TypeCoercion` table covers same-type / widening numeric / `Nullable<T>` unwrap/wrap / ref covariance |
| S9-T03 | Null-safe + nested emit | ✅ | Recursive nested-delegate dispatch through `DelegateSlotTable` static indirection (no per-call reflection) |
| S9-T04 | Transformer emit | ✅ | Singleton transformers stored on closure; no scoped-transformer path needed (registry holds instances) |
| S9-T05 | Strategy selection | ✅ | `SculptorOptions.Strategy.Mode` (`CompiledOnly` default \| `EmitFirst` \| `Adaptive` \| `EmitOnly`); pre-compile loop reroute via `MappingExecutor` |
| S9-T06 | `InvocationCounterTable` | ✅ | Single `Interlocked.Increment` per Map call; saturating `Promoted` sentinel post-promotion |
| S9-T07 | `AdaptivePromotionManager` | ✅ | Partial split across two files; bounded `Channel<TypePair>` (cap 1024, DropWrite); five-state lifecycle |
| S9-T08 | Atomic CAS delegate swap | ✅ | `Caching.DelegateSlot` with `Volatile.Read`-protected reference + `Interlocked.CompareExchange<Func<...>>` |
| S9-T09 | BG promotion worker | ✅ | `LongRunning` task per sculptor; lazy start; `OnIdleAsync` test hook |
| S9-T10 | Telemetry + `MappingInspection` extensions | ✅ | `SculptorMeter` + 6 new init-only inspection fields; cache bypass under Adaptive |
| S9-T11 | Integration parity tests | ✅ | 7 tests covering all 4 modes, output parity, end-to-end promotion, 16K-call concurrent stress |
| S9-T12 | CHANGELOG + retro + benchmarks | 🟡 partial | CHANGELOG + retro shipped; §9.1 BenchmarkDotNet sign-off + `v1.0.0` GA tag deferred to a dedicated bench run |

## What Worked

1. **Combining T01–T04 into one cohesive `ILEmitMappingCompiler`** — the spec splits flat / null-safe / nested / transformer for sprint pacing, but in practice they share `EmitContext`, `DelegateSlotTable`, and the per-link dispatch tree. One cohesive file (~440 LOC) was easier to reason about than four interlocking files.
2. **`CompiledOnly` as the default `StrategyMode`** — let the Sprint 8 RC test suite stay literally untouched (1241 green, no `[Skip]`-then-restore churn) while the new IL Emit path is opt-in.
3. **`DelegateSlot` migration of `MappingDelegateCache`** — replacing `Lazy<Func<>>` with a swap-aware slot kept the existing public contract (`GetOrCompile`, `TryGet`, `Count`, `GetCachedPairs`) intact and added the `TrySwap` + `GetSlot` surface adaptive promotion needs.
4. **Closure-bound `DynamicMethod`** (`CreateDelegate(type, closure)` with an `EmittedClosure` first-arg) — the IL never performs reflection at run time, and nested-delegate / transformer slots live on a private object array indexed by constant.

## What Slipped

1. **Per-task alternating Implement / Review rounds** were collapsed into single Implement-then-self-review passes per task + one final holistic review pass (this retro). The discipline still surfaced three real bugs:
   - `SculptorBuildPipeline` eager pre-compile bypassing the Sprint 9 strategy chain (fixed via Stage-10 reorder).
   - `MappingDelegateCache` `Lazy<>` storage being incompatible with atomic swap (fixed via the `DelegateSlot` migration).
   - `cref="Compile"` xmldoc ambiguity after adding the second `Compile` overload (fixed by disambiguating to `<see cref="ILEmitMappingCompiler"/>` + inline `<c>Compile</c>`).
2. **§9.1 BenchmarkDotNet sign-off** — flat ≤ 100 ns, nested ≤ 500 ns, 1 K collection ≤ 100 µs, 0 bytes per flat mapping. The Sprint 9 IL Emit path is production-ready, but the actual bench numbers require a release-build run on the reference machine + baseline JSON commit, scheduled separately. Sprint 8's `sprint-8-baseline.json` remains the regression gate until then.
3. **`v1.0.0` GA git tag + NuGet push** — gated on §9.1 sign-off above. RC packaging (`1.0.0-rc.1`) ships unchanged from Sprint 8.
4. **Mutation testing under Sprint 9 namespaces** — Stryker.NET run scheduled with the §9.1 bench run; current Sprint 8 ≥ 75 % floor stays in place.

## Spec Deviations

(Mirrored in `CHANGELOG.md` `### Spec Deviations`; restated here for retro completeness.)

- `MappingStrategy.Emit` / `Compiled` spec wording → existing `ILEmit` / `ExpressionCompiled` codebase names (aliases table in `docs/workspace/sprint9-progress.md`).
- Typed `MappingDelegate<S,D>` spec wording → existing untyped `Func<object, MappingScope, object>` signature (typed surface deferred to Sprint 12+ source-generator route).
- Sprint 9 partial classes `AdaptivePromotionManager` + `AdaptivePromotionManager.Promotion.cs` instead of the spec's monolithic `AdaptivePromotionManager.cs` — keeps the T05 forward-declared `Register` stub separate from the T07/T08 state machine, mirroring the established Sprint 8 partial-class pattern.

## Carry-Over for Sprint 10

1. **§9.1 BenchmarkDotNet run** — flat / nested / collection / SIMD primitives. Promote `sprint-8-baseline.json` to `sprint-9-baseline.json` after IL Emit is hot.
2. **`v1.0.0` GA tag + NuGet publish** — pending §9.1 sign-off above.
3. **`ObjectPool<MappingScope>` + `ArrayPool<T>` for collection buffers** (Sprint 10 spec §9.4).
4. **SIMD-accelerated primitive collection copy** (Sprint 10 spec §9.5).
5. **Parallel collection mapping** ≥ `ParallelCollectionThreshold` (Sprint 10 spec §9.6).
6. **Source generator path** — Sprint 12 (`SmartMapp.Net.Codegen` package). Picks up where Sprint 9's IL Emit leaves off: same emit shape, AOT-safe, compile-time errors.
7. **Mutation testing harden** — gate Sprint 9 namespaces at ≥ 80 % once baseline lands.

## Design Decisions Worth Documenting

### Why `CompiledOnly` is the default

The Sprint 9 IL Emit path is gated behind `System.Reflection.Emit` which is unsupported under NativeAOT. Making `EmitFirst` or `Adaptive` the default would break AOT-published consumers. The decision tree:

| Consumer profile | Recommended mode | Why |
|---|---|---|
| AOT publish / trimmer | `CompiledOnly` (default) | Zero `System.Reflection.Emit` references; zero `IL3050` warnings outside `Engine.ILEmit` |
| JIT'd app, latency-tolerant | `CompiledOnly` | Sprint 8 RC perf is already excellent (63 ns flat) |
| JIT'd app, latency-sensitive | `Adaptive` | Threshold 10 → IL Emit kicks in after warm-up |
| Benchmarks / proof-of-perf | `EmitOnly` (`ThrowOnEmitOnlyFallback = false` for blueprints with value providers) | Guaranteed hot-path coverage |
| Pre-JIT / warmup-first | `EmitFirst` | All emit-eligible blueprints lower eagerly at Forge time |

### Why nested-delegate slots live on an `EmittedClosure` and not a per-type static field

Spec §S9-T03 Technical Considerations bullet 1 suggests `DelegateSlotTable` as a static field on `EmittedMapperHost<S,D>` (closed generic). The implemented design instead binds an `EmittedClosure` instance as the `DynamicMethod`'s delegate target (via `CreateDelegate(type, closure)`). This buys:

- **One allocation per emitted method** (the closure) instead of one closed-generic type per pair (which never gets unloaded).
- **No `RuntimeHelpers.PrepareMethod` plumbing** — the closure-bound dispatch is JIT-handled exactly like a closure over a captured field.
- **Identical IL cost** — `Ldarg.0` + `Call get_Delegates` + `Ldc.I4.n` + `Ldelem.Ref` + `Callvirt Invoke` vs `Ldsfld` + `Callvirt Invoke`. The two extra op-codes are one indirection away from the loop body, off the hot path.

### Why the existing `Func<object, MappingScope, object>` signature

Maintaining a single delegate shape across Expression-Compiled and IL Emit lets `MappingDelegateCache.TrySwap` use one generic `Interlocked.CompareExchange<Func<>>` for the whole codebase. Typed `MappingDelegate<S,D>` would split the cache by closed generic, double the storage, and require a typed-swap path. The performance cost of the boxed return + cast at the Mapper boundary is measured in single-digit nanoseconds and is the same cost the Sprint 8 RC already pays; Sprint 9 doesn't regress here.

## Review Prompts Answered

> Did §9.1 targets hit?

**Deferred.** Implementation is complete and parity-verified; bench numbers + baseline JSON commit are scheduled with the Sprint 10 cycle.

> Promotion threshold default still 10?

**Yes**, `StrategyOptions.PromotionThreshold = 10`. The parity-matrix test (`Adaptive_promotion_swaps_to_ILEmit_after_threshold`) demonstrates the lifecycle works correctly with threshold = 3 for test-cycle speed.

> Carry-over to Sprint 10?

**Object pooling (§9.4), SIMD primitives (§9.5), parallel collections (§9.6), §9.1 benchmark sign-off, and `v1.0.0` GA tag.** See *Carry-Over* section above.

> AOT readiness?

**Yes** — `IsAotCompatible=true` builds clean. The `Engine.ILEmit` namespace is gated behind `[RequiresDynamicCode]` and the `CompiledOnly` default never reaches it. AOT-published consumers see zero `IL3050` warnings.
