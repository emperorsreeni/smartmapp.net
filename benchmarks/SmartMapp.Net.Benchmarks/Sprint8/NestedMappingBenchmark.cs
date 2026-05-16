// SPDX-License-Identifier: MIT
using BenchmarkDotNet.Attributes;

namespace SmartMapp.Net.Benchmarks.Sprint8;

/// <summary>
/// Sprint 8 · S8-T12 — nested 3-level graph (Order → Customer → Address) flattened into a
/// single flat DTO via the flattening convention. Spec §9.1 warm-latency target is
/// &lt; 500 ns; this benchmark records the Sprint 8 Expression-Compiled baseline for the
/// regression gate.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class NestedMappingBenchmark
{
    private ISculptor _sculptor = null!;
    private NestedOrder _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sculptor = new SculptorBuilder().UseBlueprint<NestedBlueprint>().Forge();
        _source = Fixtures.CreateNestedOrder();
    }

    [Benchmark(Description = "ISculptor.Map<NestedOrder, FlatDto> (3-level flatten)")]
    public NestedOrderFlatDto Map_Nested3Level() => _sculptor.Map<NestedOrder, NestedOrderFlatDto>(_source);
}
