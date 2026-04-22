# Sprint 8 Retrospective — DI Integration & v1.0 RC

**Sprint Duration:** Day 1 – Day 10
**Version at Close:** `1.0.0-rc.1`
**Tasks Delivered:** S8-T00 through S8-T12 (12 tasks, ~134 planned points)
**Test Count at Close:** 1171 unit + 56 integration = **1227 cumulative green**
**Packages Packed:** `SmartMapp.Net.1.0.0-rc.1` + `SmartMapp.Net.DependencyInjection.1.0.0-rc.1` (both with `.snupkg` symbols)

---

## What Worked

- _(fill during sprint review)_ — examples:
  - Lazy-forge invariant from S8-T02 held cleanly across every subsequent task (T03 / T04 / T05 / T11), no configure-callback-timing regressions surfaced.
  - Fixture reuse strategy (`AppHostFixture` + `[CollectionDefinition]`) scaled the integration suite to 47 → 56 tests in under 2 s wall time.
  - Sample-first documentation (MinimalApi + Console) meant the integration suite had an existing `public partial class Program` entry point for `WebApplicationFactory<Program>` out of the gate.

## What Slipped

- _(fill during sprint review)_ — candidates:
  - Benchmarks: spec called for dual-TFM tracking via `[SimpleJob(RuntimeMoniker.Net80)]` + `Net90`. BenchmarkDotNet 0.14 caps at `Net90`, and the dev environment currently only has the .NET 10 SDK installed, so the baseline ran under `net10.0` with an unpinned `[SimpleJob]`. Sprint 9 should attach Net80 / Net90 jobs once those runtimes are on the CI runner.
  - EF Core projection benchmark originally used InMemory; owned-entity key collisions pushed the final version to an in-process `AsQueryable()` pipeline. A SQL-flavoured benchmark needs an ephemeral SQLite container (already added for the S8-T11 SQL-shape assertion).
  - Stryker.NET mutation target (≥ 80%) is configured in CI but the first run's actual score is recorded as a stretch — an explicit floor lands in Sprint 9 once the baseline stabilises.

## Carry-Over Into Sprint 9

- Pin benchmarks to `[SimpleJob(RuntimeMoniker.Net80)]` + `Net90` once those SDKs are on CI runners.
- Add an **SQLite-backed** `SelectAsProjectionBenchmark` variant alongside the in-process one for a real SQL-translation cost number.
- Expand `Directory.Packages.props` version-catalog to include an upgraded AutoMapper build once a non-vulnerable 14.x lands (current benchmark baseline uses `13.0.1` with `NoWarn NU1903`).
- Finalise Stryker mutation floor (≥ 80%) based on the Sprint 8 baseline run.
- Sprint 9 **IL Emit** pass targets the §9.1 `< 100 ns` flat-mapping goal (Sprint 8 Expression-Compiled baseline sits at ~63 ns already — margin for regression-proof IL gains).

## IL Emit Readiness Assessment (Sprint 9 Gate)

- Forge pipeline layered behind `SculptorBuildPipeline` stages — IL Emit slots in as an alternate Stage 7 backend without touching convention / binding / validation code.
- Sprint 8 baseline numbers (committed at `benchmarks/results/sprint-8-baseline.json`) plus the CI regression gate at +10% mean Sprint 9's IL Emit PRs get automatic regression visibility from the first green build.
- Public API surface for `1.0.0-rc.1` is locked; IL Emit is purely internal. No breaking changes expected.

## Sprint 8 Baseline (Expression-Compiled, net10.0, RELEASE)

| Benchmark | Mean | Alloc |
|---|---:|---:|
| `FlatMappingBenchmark.Map_Warm` | 63.27 ns | 248 B |
| `NestedMappingBenchmark.Map_Nested3Level` | 66.45 ns | 224 B |
| `AutoMapperComparisonBenchmark.SmartMapp` | 64.20 ns | 248 B |
| `AutoMapperComparisonBenchmark.AutoMapper` | 74.20 ns | 104 B |
| `CollectionMappingBenchmark.MapCollection(100)` | 5.99 μs | 20 KB |
| `CollectionMappingBenchmark.MapCollection(1 000)` | 61.27 μs | 200 KB |
| `CollectionMappingBenchmark.MapCollection(10 000)` | 706.34 μs | 2 MB |
| `SelectAsProjectionBenchmark.Projection_Cached` | 13.56 ns | 0 B |
| `SelectAsProjectionBenchmark.SelectAs_InMemory100` | 907.06 μs | 26.9 KB |
| `ForgeBenchmark.Forge_Bundle` | 6.05 ms | 254.6 KB |

Full entries + stddev / median in [`benchmarks/results/sprint-8-baseline.json`](../../benchmarks/results/sprint-8-baseline.json).

---

## Action Items

- [ ] _(fill during sprint review)_
- [ ] _(fill during sprint review)_
