// SPDX-License-Identifier: MIT
using BenchmarkDotNet.Attributes;
using SmartMapp.Net.DependencyInjection.Extensions;

namespace SmartMapp.Net.Benchmarks.Sprint8;

/// <summary>
/// Sprint 8 · S8-T12 — <c>SelectAs&lt;TTarget&gt;</c> projection cost. Measures the cached
/// projection-expression fetch plus the in-process <c>List&lt;T&gt;.AsQueryable()</c> + Select
/// materialisation path, which exercises the same runtime-compiled lambda EF Core would
/// translate to SQL without pulling EF Core into the benchmark process. (Using EF InMemory
/// here introduces model-setup noise — nested entities with their own keys complicate the
/// OwnsOne story — and EF InMemory does not translate `ToQueryString()` anyway; the actual
/// EF round-trip shape is locked down by the S8-T11 SQLite integration test instead.)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class SelectAsProjectionBenchmark
{
    private ISculptor _sculptor = null!;
    private List<NestedOrder> _orders = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sculptor = new SculptorBuilder().UseBlueprint<NestedBlueprint>().Forge();

        _orders = Enumerable.Range(1, 100)
            .Select(i => new NestedOrder
            {
                Id = i, Total = i * 1.50m,
                Customer = new NestedCustomer
                {
                    Id = i, FirstName = "F" + i, LastName = "L" + i,
                    Address = new NestedAddress { City = "City" + i, Country = "US", PostalCode = "00000" },
                },
            }).ToList();

        // Warm the projection cache so the cached benchmark isolates the lookup cost.
        _ = _sculptor.GetProjection<NestedOrder, NestedOrderFlatDto>();
    }

    [Benchmark(Baseline = true, Description = "GetProjection<NestedOrder, FlatDto> — cached")]
    public System.Linq.Expressions.Expression Projection_Cached() =>
        _sculptor.GetProjection<NestedOrder, NestedOrderFlatDto>();

    [Benchmark(Description = "SelectAs<FlatDto>().ToList() — 100 rows (in-memory)")]
    public List<NestedOrderFlatDto> SelectAs_InMemory100() =>
        _orders.AsQueryable().SelectAs<NestedOrder, NestedOrderFlatDto>(_sculptor).ToList();
}
