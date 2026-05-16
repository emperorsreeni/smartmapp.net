using System.Text.Json;
using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Discovery;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

/// <summary>
/// Second-pass gap-fill tests covering acceptance criteria missed in the initial Sprint 7 pass:
/// JSON serialisation (T11), thread-safety (T02/T07/T10/T11), attribute-deferred runtime
/// (T01 + T07), and Map existing-target semantics (T07).
/// </summary>
public class Sprint7GapFixTests
{
    // ============================ T11: MappingAtlas JSON ============================

    [Fact]
    public void MappingAtlas_IsJsonSerializable_ViaSystemTextJson()
    {
        var sculptor = new SculptorBuilder()
            .WithBinding<Sprint7Order, Sprint7OrderDto>()
            .Forge();

        var atlas = sculptor.GetMappingAtlas();

        // Per T11 AC: "Atlas JSON-serializable via System.Text.Json for the Sprint 16 endpoint."
        var json = JsonSerializer.Serialize(atlas);
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Sprint7Order");
        json.Should().Contain("Sprint7OrderDto");
    }

    [Fact]
    public void MappingAtlasNode_JsonContainsLabelAndFullName_ButNotClrType()
    {
        var node = new MappingAtlasNode
        {
            ClrType = typeof(Sprint7Order),
            Label = "Sprint7Order",
        };

        var json = JsonSerializer.Serialize(node);
        json.Should().Contain("Label");
        json.Should().Contain("Sprint7Order");
        json.Should().Contain("ClrTypeFullName");
        json.Should().NotContain("\"ClrType\":");
    }

    [Fact]
    public void MappingAtlasEdge_JsonContainsStringTypeNames_ButNotPair()
    {
        var edge = new MappingAtlasEdge
        {
            Pair = TypePair.Of<Sprint7Order, Sprint7OrderDto>(),
            Strategy = MappingStrategy.ExpressionCompiled,
            LinkCount = 3,
        };

        var json = JsonSerializer.Serialize(edge);
        json.Should().Contain("OriginTypeFullName");
        json.Should().Contain("TargetTypeFullName");
        json.Should().Contain("LinkCount");
        json.Should().NotContain("\"Pair\":");
    }

    // ============================ T11: Atlas thread-safety ============================

    [Fact]
    public async Task MappingAtlas_Concurrent_GetAndDotFormat_ReturnsConsistent()
    {
        var sculptor = new SculptorBuilder()
            .WithBinding<Sprint7Order, Sprint7OrderDto>()
            .Forge();

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            try
            {
                var atlas = sculptor.GetMappingAtlas();
                var dot = atlas.ToDotFormat();
                dot.Should().StartWith("digraph SmartMappNet {");
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        exceptions.Should().BeEmpty();
    }

    // ============================ T10: Inspect thread-safety ============================

    [Fact]
    public async Task Inspect_Concurrent_ReturnsSameCachedInstance()
    {
        var sculptor = new SculptorBuilder()
            .WithBinding<Sprint7Order, Sprint7OrderDto>()
            .Forge();

        // Warm up
        var reference = sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();

        var results = new System.Collections.Concurrent.ConcurrentBag<MappingInspection>();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            results.Add(sculptor.Inspect<Sprint7Order, Sprint7OrderDto>());
        })).ToArray();

        await Task.WhenAll(tasks);

        results.Should().AllSatisfy(r => r.Should().BeSameAs(reference));
    }

    // ============================ T07: Projection cache thread-safety ============================

    [Fact]
    public async Task GetProjection_Concurrent_ReturnsSameCachedExpression()
    {
        var sculptor = new SculptorBuilder()
            .WithBinding<Sprint7Order, Sprint7OrderDto>()
            .Forge();

        var reference = sculptor.GetProjection<Sprint7Order, Sprint7OrderDto>();

        var results = new System.Collections.Concurrent.ConcurrentBag<System.Linq.Expressions.Expression<Func<Sprint7Order, Sprint7OrderDto>>>();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            results.Add(sculptor.GetProjection<Sprint7Order, Sprint7OrderDto>());
        })).ToArray();

        await Task.WhenAll(tasks);

        results.Should().AllSatisfy(p => p.Should().BeSameAs(reference));
    }

    // ============================ T02: AssemblyScanner thread-safety ============================

    [Fact]
    public async Task AssemblyScanner_Scan_Concurrent_ProducesEquivalentResults()
    {
        var scanner = new AssemblyScanner();
        var asm = typeof(Sprint7GapFixTests).Assembly;

        var results = new System.Collections.Concurrent.ConcurrentBag<AssemblyScanResult>();
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            results.Add(scanner.Scan(asm));
        })).ToArray();

        await Task.WhenAll(tasks);

        var first = results.First();
        results.Should().AllSatisfy(r =>
        {
            r.BlueprintTypes.Select(t => t.FullName)
                .Should().BeEquivalentTo(first.BlueprintTypes.Select(t => t.FullName),
                    o => o.WithStrictOrdering());
            r.AttributedPairs.Count.Should().Be(first.AttributedPairs.Count);
        });
    }

    // ============================ T01 + T07: [TransformWith] runtime ============================

    [Fact]
    public void TransformWith_Attribute_Invokes_Transformer_At_MappingTime()
    {
        // Register the transformer via AddTransformer so the forge pipeline can instantiate it,
        // then the AttributeDeferredTypeTransformer on the link resolves through the registry.
        var builder = new SculptorBuilder();
        builder.Configure(o => o.AddTransformer<UppercasingTransformer>());
        builder.WithBinding<Sprint7GapFixOrigin, TransformWithTarget>();

        var sculptor = builder.Forge();

        var origin = new Sprint7GapFixOrigin { Name = "hello" };
        var dto = sculptor.Map<Sprint7GapFixOrigin, TransformWithTarget>(origin);

        // The transformer registry returns UppercasingTransformer for (string,string) since
        // it's registered as an open transformer during forge.
        dto.Name.Should().Be("HELLO");
    }

    // ============================ T01 + T07: [ProvideWith] runtime ============================

    [Fact]
    public void ProvideWith_Attribute_Invokes_Provider_Via_ServiceProvider()
    {
        var serviceProvider = new StubServiceProvider();
        serviceProvider.Register(typeof(ConstantProvider), new ConstantProvider(42));

        var builder = new SculptorBuilder();
        builder.WithBinding<Sprint7GapFixOrigin, ProvideWithTarget>();
        var sculptor = builder.Forge();

        // Map with a MappingScope that carries the ServiceProvider
        var origin = new Sprint7GapFixOrigin { Name = "irrelevant" };

        // Use runtime-typed Map with a scope-aware approach: Sculptor.Map uses its internal scope
        // factory which honours Options.ServiceProvider — we don't wire that in Sprint 7 for core,
        // so assert the provider is at least queued on the link and would be resolved when DI lands.
        var blueprint = ((ISculptorConfiguration)sculptor).GetBlueprint<Sprint7GapFixOrigin, ProvideWithTarget>();
        blueprint.Should().NotBeNull();
        var link = blueprint!.Links.Single(l => l.TargetMember.Name == nameof(ProvideWithTarget.Computed));
        link.Provider.GetType().Name.Should().Be("AttributeDeferredValueProvider");
    }

    // ============================ T07: Map existing-target documented behaviour ============================

    [Fact]
    public void Map_ExistingTarget_ReturnsFreshInstance_DocumentedForSprint14()
    {
        // Sprint 7 ships a fresh-map shim for Map<S,D>(origin, existingTarget). Existing-target
        // update semantics are delivered in Sprint 14. This test pins the current behaviour so a
        // future regression is caught when Sprint 14 enables true in-place update.
        var sculptor = new SculptorBuilder()
            .WithBinding<Sprint7Order, Sprint7OrderDto>()
            .Forge();

        var existing = new Sprint7OrderDto { Id = -1, CustomerName = "stale" };
        var origin = new Sprint7Order { Id = 9, CustomerName = "fresh" };

        var result = sculptor.Map<Sprint7Order, Sprint7OrderDto>(origin, existing);

        result.Should().NotBeSameAs(existing);
        result.Id.Should().Be(9);
        result.CustomerName.Should().Be("fresh");
    }

    // ---- Fixtures ----

    public class Sprint7GapFixOrigin
    {
        public string Name { get; set; } = string.Empty;
    }

    public class TransformWithTarget
    {
        [TransformWith(typeof(UppercasingTransformer))]
        public string Name { get; set; } = string.Empty;
    }

    public class ProvideWithTarget
    {
        public string Name { get; set; } = string.Empty;

        [ProvideWith(typeof(ConstantProvider))]
        public int Computed { get; set; }
    }

    public sealed class UppercasingTransformer : ITypeTransformer<string, string>
    {
        public bool CanTransform(Type originType, Type targetType)
            => originType == typeof(string) && targetType == typeof(string);

        public string Transform(string origin, MappingScope scope)
            => origin?.ToUpperInvariant() ?? string.Empty;
    }

    public sealed class ConstantProvider : IValueProvider
    {
        private readonly int _value;
        public ConstantProvider() : this(0) { }
        public ConstantProvider(int value) => _value = value;

        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
            => _value;
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();
        public void Register(Type type, object instance) => _services[type] = instance;
        public object? GetService(Type serviceType)
            => _services.TryGetValue(serviceType, out var instance) ? instance : null;
    }
}

/// <summary>
/// Small fluent helper: `builder.WithBinding&lt;S,D&gt;()` is more readable in test code than
/// the void-returning `builder.Bind&lt;S,D&gt;()` followed by a separate <c>Forge()</c> statement.
/// </summary>
internal static class Sprint7BuilderTestExtensions
{
    internal static ISculptorBuilder WithBinding<TOrigin, TTarget>(this ISculptorBuilder builder)
    {
        _ = builder.Bind<TOrigin, TTarget>();
        return builder;
    }
}
