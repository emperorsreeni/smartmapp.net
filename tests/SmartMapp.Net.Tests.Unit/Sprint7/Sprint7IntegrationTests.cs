using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;
using SmartMapp.Net.Diagnostics;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class Sprint7IntegrationTests
{
    [Fact]
    public void EndToEnd_Forge_Map_Inspect_Atlas_AllSucceed()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();

        var sculptor = builder.Forge();
        var origin = new Sprint7Order { Id = 7, CustomerName = "Alice", Total = 42m };

        var dto = sculptor.Map<Sprint7Order, Sprint7OrderDto>(origin);
        dto.Id.Should().Be(7);

        var inspection = sculptor.Inspect<Sprint7Order, Sprint7OrderDto>();
        inspection.LinkCount.Should().BeGreaterThan(0);

        var atlas = sculptor.GetMappingAtlas();
        atlas.Edges.Should().Contain(e => e.Pair == TypePair.Of<Sprint7Order, Sprint7OrderDto>());

        atlas.ToDotFormat().Should().StartWith("digraph SmartMappNet {");
    }

    [Fact]
    public void InlineBinding_ProducesEquivalentBlueprint_ToSubclass()
    {
        // Subclass-based
        var subclassBuilder = new SculptorBuilder();
        subclassBuilder.UseBlueprint<IntegrationSubclassBlueprint>();
        var subclassSculptor = subclassBuilder.Forge();

        // Inline-based
        var inlineBuilder = new SculptorBuilder();
        inlineBuilder.Configure(o => o.Bind<Sprint7Order, Sprint7OrderDto>(rule =>
            rule.Property(d => d.Id, p => p.From(s => s.Id))));
        var inlineSculptor = inlineBuilder.Forge();

        var origin = new Sprint7Order { Id = 77, CustomerName = "X", Total = 1m };
        var viaSubclass = subclassSculptor.Map<Sprint7Order, Sprint7OrderDto>(origin);
        var viaInline = inlineSculptor.Map<Sprint7Order, Sprint7OrderDto>(origin);

        viaInline.Should().BeEquivalentTo(viaSubclass);
    }

    [Fact]
    public void MixedSources_Attribute_Inline_Blueprint_OnDifferentPairs_Coexist()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.Bind<IntegrationInlineOrigin, IntegrationInlineTarget>(_ => { }));
        builder.UseBlueprint<IntegrationBlueprintOnly>();
        builder.ScanAssembliesContaining<IntegrationAttributedTarget>();

        var sculptor = builder.Forge();
        var config = (ISculptorConfiguration)sculptor;

        config.HasBinding<IntegrationInlineOrigin, IntegrationInlineTarget>().Should().BeTrue();
        config.HasBinding<IntegrationBlueprintOrigin, IntegrationBlueprintTarget>().Should().BeTrue();
        config.HasBinding<IntegrationAttributedOrigin, IntegrationAttributedTarget>().Should().BeTrue();
    }

    [Fact]
    public async Task Stress_1000_Concurrent_Maps_OnSameSculptor_NoFailures()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        var sculptor = builder.Forge();

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
        var tasks = Enumerable.Range(0, 1000).Select(i => Task.Run(() =>
        {
            try
            {
                var origin = new Sprint7Order { Id = i, CustomerName = $"C{i}", Total = i };
                var dto = sculptor.Map<Sprint7Order, Sprint7OrderDto>(origin);
                dto.Id.Should().Be(i);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        exceptions.Should().BeEmpty();
    }

    [Fact]
    public void UnknownPair_ErrorMessage_Contains_TypeNames_And_HowToFix()
    {
        var sculptor = new SculptorBuilder().Forge();
        var act = () => sculptor.Map<Sprint7Order, Sprint7OrderDto>(new Sprint7Order());

        act.Should().Throw<MappingConfigurationException>()
            .Where(ex => ex.Message.Contains("No blueprint registered"))
            .Where(ex => ex.Message.Contains("Sprint7Order"))
            .Where(ex => ex.Message.Contains("Sprint7OrderDto"))
            .Where(ex => ex.Message.Contains("Bind<TOrigin, TTarget>"));
    }

    [Fact]
    public void StrictMode_UnlinkedRequired_Surfaces_BlueprintValidationException()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.StrictMode = true);
        builder.Bind<ForgeSmallOrigin, ForgeStrictTarget>();

        var act = () => builder.Forge();
        act.Should().Throw<BlueprintValidationException>()
            .Where(ex => ex.ValidationResult.Errors.Any(e =>
                e.TargetType == typeof(ForgeStrictTarget)
                && e.Message.Contains("MissingRequired")));
    }

    [Fact]
    public void Sculptor_Implements_ISculptorConfiguration_For_Introspection()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        var sculptor = builder.Forge();

        sculptor.Should().BeAssignableTo<ISculptorConfiguration>();
        var config = (ISculptorConfiguration)sculptor;
        config.GetAllBlueprints().Should().NotBeEmpty();
        config.GetAllBlueprintsByPair().Should().ContainKey(TypePair.Of<Sprint7Order, Sprint7OrderDto>());
    }

    [Fact]
    public void ValidateConfiguration_AfterSuccessfulForge_IsValid()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        var sculptor = builder.Forge();
        var config = (ISculptorConfiguration)sculptor;

        var result = config.ValidateConfiguration();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}

// ---- Public fixtures for mixed-source coexistence ----

public class IntegrationInlineOrigin { public int Id { get; set; } }
public class IntegrationInlineTarget { public int Id { get; init; } }

public class IntegrationBlueprintOrigin { public int Id { get; set; } }
public class IntegrationBlueprintTarget { public int Id { get; init; } }

public class IntegrationBlueprintOnly : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<IntegrationBlueprintOrigin, IntegrationBlueprintTarget>();
    }
}

public class IntegrationAttributedOrigin { public int Id { get; set; } }

[MappedBy(typeof(IntegrationAttributedOrigin))]
public record IntegrationAttributedTarget { public int Id { get; init; } }

public class IntegrationSubclassBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<Sprint7Order, Sprint7OrderDto>()
            .Property(d => d.Id, p => p.From(s => s.Id));
    }
}
