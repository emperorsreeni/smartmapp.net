// SPDX-License-Identifier: MIT
using BenchmarkDotNet.Attributes;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Benchmarks.Sprint8;

/// <summary>
/// Sprint 8 · S8-T12 — <c>Forge()</c> startup cost. Spec §9.1 "Startup scan (100 types) &lt;
/// 50 ms" target; Acceptance bullet 2 lists "Forge() for 100 pairs" as one of the baseline
/// numbers the Sprint 8 JSON must record.
/// </summary>
/// <remarks>
/// Each invocation builds a fresh <see cref="SculptorBuilder"/> so the measurement covers the
/// full cold-forge pipeline (blueprint collection + convention pass + compilation). Keeping
/// iteration / warmup counts low keeps wall-clock runtime practical — Forge is an O(pairs)
/// operation, not a hot-loop primitive.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 3)]
public class ForgeBenchmark
{
    [Benchmark(Description = "Forge() — 1 blueprint (4 pairs via BundleBlueprint)")]
    public ISculptor Forge_Bundle() =>
        new SculptorBuilder().UseBlueprint<BundleBlueprint>().Forge();

    [Benchmark(Description = "Forge() — 100 unique Bind<,>() pairs via HundredPairsBlueprint")]
    public ISculptor Forge_100Pairs() =>
        // Spec §9.1 / Acceptance bullet 2 — "Forge() for 100 pairs". The pipeline's
        // duplicate-binding invariant (pair-keyed on the TOrigin/TTarget generic arguments) means
        // 100 unique *pairs* requires 100 distinct closed generic types. `HundredPairsBlueprint`
        // binds 100 `ForgeSource<F###> -> ForgeTarget<F###>` pairs (F001 through F100) in one
        // Design pass, exercising the full convention + compilation pipeline at the target scale.
        new SculptorBuilder().UseBlueprint<HundredPairsBlueprint>().Forge();
}

/// <summary>Bundle blueprint that registers the Sprint 8 benchmark's four pairs in one go.</summary>
public sealed class BundleBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<FlatSource, FlatTarget>();
        plan.Bind<NestedOrder, NestedOrderFlatDto>();
        plan.Bind<Item, ItemDto>();
        plan.Bind<ForgeSourceA, ForgeTargetA>();
    }
}
