using BenchmarkDotNet.Attributes;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Compilation;

namespace SmartMapp.Net.Benchmarks;

/// <summary>
/// Sprint 5 collection mapping benchmarks — establishes baseline performance metrics
/// for array, list, dictionary, immutable, and nested collection mapping scenarios.
/// §9.1 target: 1K flat DTO collection &lt; 100μs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class CollectionBenchmarks
{
    private Func<object, MappingScope, object> _arrayMapper = null!;
    private Func<object, MappingScope, object> _listMapper = null!;
    private Func<object, MappingScope, object> _dictMapper = null!;
    private Func<object, MappingScope, object> _immutableMapper = null!;
    private Func<object, MappingScope, object> _nestedMapper = null!;

    private ArraySource _array1K = null!;
    private ListSource _list1K = null!;
    private ListSource _list10K = null!;
    private DictSource _dict1K = null!;
    private ImmutableSource _immutable1K = null!;
    private NestedListSource _nested100x100 = null!;

    [GlobalSetup]
    public void Setup()
    {
        var cache = new TypeModelCache();
        var delegateCache = new MappingDelegateCache();
        var compiler = new BlueprintCompiler(cache, delegateCache);

        _arrayMapper = compiler.Compile(AutoBlueprint<ArraySource, ArrayTarget>(cache));
        _listMapper = compiler.Compile(AutoBlueprint<ListSource, ListTarget>(cache));
        _dictMapper = compiler.Compile(AutoBlueprint<DictSource, DictTarget>(cache));
        _immutableMapper = compiler.Compile(AutoBlueprint<ImmutableSource, ImmutableTarget>(cache));
        _nestedMapper = compiler.Compile(AutoBlueprint<NestedListSource, NestedListTarget>(cache));

        _array1K = new ArraySource { Values = Enumerable.Range(0, 1000).ToArray() };
        _list1K = new ListSource { Values = Enumerable.Range(0, 1000).ToList() };
        _list10K = new ListSource { Values = Enumerable.Range(0, 10_000).ToList() };
        _dict1K = new DictSource
        {
            Entries = Enumerable.Range(0, 1000).ToDictionary(i => $"key{i}", i => i)
        };
        _immutable1K = new ImmutableSource { Values = Enumerable.Range(0, 1000).ToArray() };
        _nested100x100 = new NestedListSource
        {
            Matrix = Enumerable.Range(0, 100)
                .Select(i => Enumerable.Range(i * 100, 100).ToList())
                .ToList()
        };
    }

    [Benchmark(Description = "int[1K] → int[1K] (Array.Copy)")]
    public object Array_1K() => _arrayMapper(_array1K, new MappingScope());

    [Benchmark(Description = "List<int>[1K] → List<int>[1K]")]
    public object List_1K() => _listMapper(_list1K, new MappingScope());

    [Benchmark(Description = "List<int>[10K] → List<int>[10K]")]
    public object List_10K() => _listMapper(_list10K, new MappingScope());

    [Benchmark(Description = "Dict<string,int>[1K] → Dict<string,int>[1K]")]
    public object Dictionary_1K() => _dictMapper(_dict1K, new MappingScope());

    [Benchmark(Description = "int[1K] → ImmutableArray<int>[1K]")]
    public object ImmutableArray_1K() => _immutableMapper(_immutable1K, new MappingScope());

    [Benchmark(Description = "List<List<int>>[100×100] nested")]
    public object Nested_100x100() => _nestedMapper(_nested100x100, new MappingScope());

    // ── Helper ──

    private static Blueprint AutoBlueprint<TOrigin, TTarget>(TypeModelCache cache)
    {
        var originModel = cache.GetOrAdd(typeof(TOrigin));
        var targetModel = cache.GetOrAdd(typeof(TTarget));
        var links = new List<PropertyLink>();

        foreach (var targetMember in targetModel.WritableMembers)
        {
            var originMember = originModel.GetMember(targetMember.Name);
            if (originMember is null) continue;

            links.Add(new PropertyLink
            {
                TargetMember = targetMember.MemberInfo,
                Provider = new DirectMemberProvider(originMember.MemberInfo),
                LinkedBy = ConventionMatch.ExactName(originMember.Name),
            });
        }

        return new Blueprint
        {
            OriginType = typeof(TOrigin),
            TargetType = typeof(TTarget),
            Links = links,
        };
    }

    // ── Benchmark Types ──

    public class ArraySource { public int[] Values { get; set; } = Array.Empty<int>(); }
    public class ArrayTarget { public int[] Values { get; set; } = Array.Empty<int>(); }

    public class ListSource { public List<int> Values { get; set; } = new(); }
    public class ListTarget { public List<int> Values { get; set; } = new(); }

    public class DictSource { public Dictionary<string, int> Entries { get; set; } = new(); }
    public class DictTarget { public Dictionary<string, int> Entries { get; set; } = new(); }

    public class ImmutableSource { public int[] Values { get; set; } = Array.Empty<int>(); }
    public class ImmutableTarget { public System.Collections.Immutable.ImmutableArray<int> Values { get; set; } }

    public class NestedListSource { public List<List<int>> Matrix { get; set; } = new(); }
    public class NestedListTarget { public List<List<int>> Matrix { get; set; } = new(); }
}
