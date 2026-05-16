// SPDX-License-Identifier: MIT
using AutoMapper;
using BenchmarkDotNet.Attributes;

namespace SmartMapp.Net.Benchmarks.Sprint8;

/// <summary>
/// Sprint 8 · S8-T12 — SmartMapp.Net vs AutoMapper comparison over the same flat 10-property
/// DTO shape. Spec §S8-T12 Acceptance bullet 2: "vs AutoMapper comparison ratio" — the ratio
/// landing in <c>sprint-8-baseline.json</c> drives the CI regression gate and the README
/// comparison table once the Sprint 9 IL Emit pass lands.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class AutoMapperComparisonBenchmark
{
    private ISculptor _sculptor = null!;
    private IMapper _autoMapper = null!;
    private FlatSource _source = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sculptor = new SculptorBuilder().UseBlueprint<FlatBlueprint>().Forge();

        var config = new MapperConfiguration(cfg =>
        {
            cfg.CreateMap<FlatSource, FlatTarget>();
        });
        _autoMapper = config.CreateMapper();

        _source = Fixtures.CreateFlatSource();
    }

    [Benchmark(Baseline = true, Description = "SmartMapp.Net — ISculptor.Map")]
    public FlatTarget SmartMapp() => _sculptor.Map<FlatSource, FlatTarget>(_source);

    [Benchmark(Description = "AutoMapper — IMapper.Map<,>")]
    public FlatTarget AutoMapper() => _autoMapper.Map<FlatSource, FlatTarget>(_source);
}
