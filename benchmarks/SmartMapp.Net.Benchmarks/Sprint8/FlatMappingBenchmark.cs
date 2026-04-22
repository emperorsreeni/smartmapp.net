// SPDX-License-Identifier: MIT
using BenchmarkDotNet.Attributes;

namespace SmartMapp.Net.Benchmarks.Sprint8;

/// <summary>
/// Sprint 8 · S8-T12 — flat 10-property DTO warm-path latency. Baseline spec §9.1 target is
/// &lt; 100 ns per mapping on net9.0 once IL Emit lands in Sprint 9; the Sprint 8 number
/// reflects the Expression-Compiled baseline and is recorded for regression gating.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class FlatMappingBenchmark
{
    private ISculptor _sculptor = null!;
    private FlatSource _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sculptor = new SculptorBuilder().UseBlueprint<FlatBlueprint>().Forge();
        _source = Fixtures.CreateFlatSource();
    }

    [Benchmark(Baseline = true, Description = "ISculptor.Map<Flat, FlatDto> — warm")]
    public FlatTarget Map_Warm() => _sculptor.Map<FlatSource, FlatTarget>(_source);
}
