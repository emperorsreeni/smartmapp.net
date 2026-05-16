// SPDX-License-Identifier: MIT
using BenchmarkDotNet.Attributes;

namespace SmartMapp.Net.Benchmarks.Sprint8;

/// <summary>
/// Sprint 8 · S8-T12 — 1K-item flat DTO collection. Spec §9.1 target &lt; 100 μs; Sprint 8
/// Expression-Compiled baseline recorded here drives the CI regression gate. Collection
/// size varies via <see cref="Count"/> params so the baseline includes 100 / 1 000 / 10 000
/// for trend tracking.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class CollectionMappingBenchmark
{
    private ISculptor _sculptor = null!;
    private List<Item> _items = null!;

    [Params(100, 1_000, 10_000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _sculptor = new SculptorBuilder().UseBlueprint<ItemBlueprint>().Forge();
        _items = Fixtures.CreateItems(Count);
    }

    [Benchmark(Description = "MapAll<Item[], ItemDto[]>")]
    public List<ItemDto> MapCollection()
    {
        var result = new List<ItemDto>(_items.Count);
        foreach (var item in _items)
        {
            result.Add(_sculptor.Map<Item, ItemDto>(item));
        }
        return result;
    }
}
