using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Diagnostics;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class ForgePipelineTests
{
    [Fact]
    public void Forge_ReturnsNonNullSculptor()
    {
        var builder = new SculptorBuilder();
        builder.Bind<ForgeOrigin, ForgeTarget>();
        var sculptor = builder.Forge();
        sculptor.Should().NotBeNull();
    }

    [Fact]
    public void Forge_SecondCall_ThrowsInvalidOperation()
    {
        var builder = new SculptorBuilder();
        builder.Bind<ForgeOrigin, ForgeTarget>();
        _ = builder.Forge();

        var act = () => builder.Forge();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Forge_FreezesOptions()
    {
        var builder = new SculptorBuilder();
        builder.Bind<ForgeOrigin, ForgeTarget>();
        var options = builder.Options;
        _ = builder.Forge();

        options.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void Forge_EveryBlueprint_HasCompiledDelegate()
    {
        var builder = new SculptorBuilder();
        builder.Bind<ForgeOrigin, ForgeTarget>();
        builder.Bind<ForgeOrigin2, ForgeTarget2>();

        var sculptor = builder.Forge();

        // Hot path — both compile without throwing
        var r1 = sculptor.Map<ForgeOrigin, ForgeTarget>(new ForgeOrigin { Id = 1 });
        var r2 = sculptor.Map<ForgeOrigin2, ForgeTarget2>(new ForgeOrigin2 { Id = 2 });

        r1.Id.Should().Be(1);
        r2.Id.Should().Be(2);
    }

    [Fact]
    public void Forge_InlineAndBlueprint_ForSamePair_Throws()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.Bind<ForgeOrigin, ForgeTarget>(_ => { }));
        builder.UseBlueprint<ForgeInlineClashBlueprint>();

        var act = () => builder.Forge();
        // The duplicate is detected during Design() execution — surfaces as InvalidOperationException
        // from BlueprintBuilder.Bind (which the pipeline does NOT wrap).
        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("Duplicate"));
    }

    [Fact]
    public void Forge_StrictMode_UnlinkedRequiredMember_Throws()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.StrictMode = true);
        builder.Bind<ForgeSmallOrigin, ForgeStrictTarget>();

        var act = () => builder.Forge();
        act.Should().Throw<BlueprintValidationException>();
    }

    [Fact]
    public void Forge_ValidateOnStartupFalse_DoesNotThrow_OnStrictFailure()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o =>
        {
            o.StrictMode = true;
            o.ValidateOnStartup = false;
        });
        builder.Bind<ForgeSmallOrigin, ForgeStrictTarget>();

        var act = () => builder.Forge();
        act.Should().NotThrow();
    }

    [Fact]
    public void Forge_Attribute_Source_RegistersPair()
    {
        var builder = new SculptorBuilder();
        builder.ScanAssembliesContaining<ForgeAttributedTarget>();
        var sculptor = builder.Forge();

        ((ISculptorConfiguration)sculptor).HasBinding<ForgeAttributedOrigin, ForgeAttributedTarget>()
            .Should().BeTrue();
    }

    [Fact]
    public void Forge_MixedSources_Coexist_WhenDifferentPairs()
    {
        var builder = new SculptorBuilder();

        // Source 1: inline binding
        builder.Configure(o => o.Bind<ForgeOrigin, ForgeTarget>(_ => { }));

        // Source 2: blueprint class
        builder.UseBlueprint<ForgeExtraBlueprint>(); // binds (ForgeOrigin2, ForgeTarget2)

        // Source 3: attribute
        builder.ScanAssembliesContaining<ForgeAttributedTarget>();

        var sculptor = builder.Forge();
        var config = (ISculptorConfiguration)sculptor;

        config.HasBinding<ForgeOrigin, ForgeTarget>().Should().BeTrue();
        config.HasBinding<ForgeOrigin2, ForgeTarget2>().Should().BeTrue();
        config.HasBinding<ForgeAttributedOrigin, ForgeAttributedTarget>().Should().BeTrue();
    }

    [Fact]
    public void Forge_Transformer_Registration_ViaAddTransformer_Succeeds()
    {
        var builder = new SculptorBuilder();
        builder.AddTransformer<ForgeCustomTransformer>();
        builder.Bind<ForgeOrigin, ForgeTarget>();

        var sculptor = builder.Forge();
        sculptor.Should().NotBeNull();
    }
}

// ---- Fixtures (public so scanner + convention can see them) ----

public class ForgeOrigin { public int Id { get; set; } }
public class ForgeTarget { public int Id { get; init; } }
public class ForgeOrigin2 { public int Id { get; set; } }
public class ForgeTarget2 { public int Id { get; init; } }

public class ForgeSmallOrigin { public int Id { get; set; } }

public class ForgeStrictTarget
{
    public int Id { get; set; }
    // MissingRequired won't be linked from ForgeSmallOrigin -> must trigger strict-mode error.
    public required string MissingRequired { get; set; }
}

public class ForgeInlineClashBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<ForgeOrigin, ForgeTarget>();
    }
}

public class ForgeExtraBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<ForgeOrigin2, ForgeTarget2>();
    }
}

public class ForgeAttributedOrigin { public int Id { get; set; } }

[SmartMapp.Net.Attributes.MappedBy(typeof(ForgeAttributedOrigin))]
public record ForgeAttributedTarget { public int Id { get; init; } }

public sealed class ForgeCustomTransformer : ITypeTransformer<int, string>
{
    public bool CanTransform(Type originType, Type targetType) => true;
    public string Transform(int origin, MappingScope scope) => origin.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
