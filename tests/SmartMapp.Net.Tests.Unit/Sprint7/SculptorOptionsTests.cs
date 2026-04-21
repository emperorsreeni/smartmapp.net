using FluentAssertions;
using SmartMapp.Net.Configuration;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class SculptorOptionsTests
{
    [Fact]
    public void Defaults_Match_Spec()
    {
        var options = new SculptorOptions();

        options.MaxRecursionDepth.Should().Be(10);
        options.ValidateOnStartup.Should().BeTrue();
        options.StrictMode.Should().BeFalse();
        options.ThrowOnUnlinkedMembers.Should().BeFalse();
        options.IsFrozen.Should().BeFalse();

        options.Nulls.FallbackForStrings.Should().BeNull();
        options.Nulls.ThrowOnNullOrigin.Should().BeFalse();
        options.Nulls.UseDefaultForNullTarget.Should().BeTrue();

        options.Throughput.ParallelCollectionThreshold.Should().Be(1000);
        options.Throughput.MaxDegreeOfParallelism.Should().Be(Environment.ProcessorCount);
        options.Throughput.EnableILEmit.Should().BeTrue();
        options.Throughput.EnableAdaptivePromotion.Should().BeTrue();
        options.Throughput.AdaptivePromotionThreshold.Should().Be(10);
        options.Throughput.LazyBlueprintCompilation.Should().BeFalse();

        options.Logging.MinimumLevel.Should().Be(SculptorLogLevel.Warning);
        options.Logging.LogBlueprints.Should().BeFalse();
    }

    [Fact]
    public void Freeze_SetsIsFrozen_AndBlocksMutation()
    {
        var options = new SculptorOptions();
        options.Freeze();

        options.IsFrozen.Should().BeTrue();

        var act = () => options.MaxRecursionDepth = 5;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Freeze_BlocksNullOptions_Mutation()
    {
        var options = new SculptorOptions();
        options.Freeze();

        var act = () => options.Nulls.ThrowOnNullOrigin = true;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Freeze_BlocksThroughputOptions_Mutation()
    {
        var options = new SculptorOptions();
        options.Freeze();

        var act = () => options.Throughput.ParallelCollectionThreshold = 500;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Freeze_BlocksConventionOptions_Mutation()
    {
        var options = new SculptorOptions();
        options.Freeze();

        var act = () => options.Conventions.OriginPrefixesAdd("Get");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Freeze_BlocksScanAssemblies()
    {
        var options = new SculptorOptions();
        options.Freeze();

        var act = () => options.ScanAssembliesContaining<SculptorOptionsTests>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Freeze_BlocksInlineBind()
    {
        var options = new SculptorOptions();
        options.Freeze();

        var act = () => options.Bind<object, object>(_ => { });
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MaxRecursionDepth_ZeroOrNegative_Throws()
    {
        var options = new SculptorOptions();
        var act0 = () => options.MaxRecursionDepth = 0;
        var actNeg = () => options.MaxRecursionDepth = -3;

        act0.Should().Throw<ArgumentOutOfRangeException>();
        actNeg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ParallelCollectionThreshold_ZeroOrNegative_Throws()
    {
        var options = new SculptorOptions();
        var act = () => options.Throughput.ParallelCollectionThreshold = 0;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MaxDegreeOfParallelism_ZeroOrNegative_Throws()
    {
        var options = new SculptorOptions();
        var act = () => options.Throughput.MaxDegreeOfParallelism = 0;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AdaptivePromotionThreshold_ZeroOrNegative_Throws()
    {
        var options = new SculptorOptions();
        var act = () => options.Throughput.AdaptivePromotionThreshold = 0;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ConventionOptions_OriginPrefixesAdd_AccumulatesAndDeduplicates()
    {
        var options = new SculptorOptions();
        options.Conventions.OriginPrefixesAdd("Get", "Str", "Get"); // duplicate "Get"
        options.Conventions.OriginPrefixes.Should().BeEquivalentTo(new[] { "Get", "Str" });
    }

    [Fact]
    public void ConventionOptions_TargetSuffixesAdd_Accumulates()
    {
        var options = new SculptorOptions();
        options.Conventions.TargetSuffixesAdd("Field", "Property");
        options.Conventions.TargetSuffixes.Should().BeEquivalentTo(new[] { "Field", "Property" });
    }

    [Fact]
    public void ConventionOptions_EnableAbbreviationExpansion_Populates()
    {
        var options = new SculptorOptions();
        options.Conventions.EnableAbbreviationExpansion(d =>
        {
            d.Add("Addr", "Address");
            d.Add("Qty", "Quantity");
        });

        options.Conventions.AbbreviationExpansionEnabled.Should().BeTrue();
        options.Conventions.Abbreviations.Should().ContainKey("Addr").WhoseValue.Should().Be("Address");
        options.Conventions.Abbreviations.Should().ContainKey("Qty");
    }

    [Fact]
    public void ScanAssemblies_DuplicatesDeduped()
    {
        var options = new SculptorOptions();
        var asm = typeof(SculptorOptionsTests).Assembly;
        options.ScanAssemblies(asm, asm, asm);
        options.Assemblies.Should().HaveCount(1);
    }

    [Fact]
    public void AddTransformer_InvalidType_Throws()
    {
        var options = new SculptorOptions();
        var act = () => options.AddTransformer(typeof(string));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Bind_Inline_Queues_Registration()
    {
        var options = new SculptorOptions();
        options.Bind<int, string>(_ => { });

        options.InlineBindings.Should().HaveCount(1);
        options.InlineBindings[0].TypePair.OriginType.Should().Be(typeof(int));
        options.InlineBindings[0].TypePair.TargetType.Should().Be(typeof(string));
    }

    [Fact]
    public void Bind_Inline_NullCallback_Throws()
    {
        var options = new SculptorOptions();
        var act = () => options.Bind<int, string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
