using System.Reflection;
using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

/// <summary>
/// Third-pass gap-fill tests: ConventionOptions actually wired into Forge,
/// AssemblyScanner caches per-assembly scan results, and unknown-pair errors
/// include fuzzy-match suggestions.
/// </summary>
public class Sprint7ThirdPassGapTests
{
    // ============================ ConventionOptions wiring ============================

    [Fact]
    public void Forge_UsesCustomPrefix_WhenConfigured()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.Conventions.OriginPrefixesAdd("Dst"));
        builder.Bind<CustomPrefixOrigin, CustomPrefixTarget>();

        var sculptor = builder.Forge();
        var origin = new CustomPrefixOrigin { DstName = "Ada" };
        var dto = sculptor.Map<CustomPrefixOrigin, CustomPrefixTarget>(origin);

        dto.Name.Should().Be("Ada");
    }

    [Fact]
    public void Forge_UsesCustomSuffix_WhenConfigured()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.Conventions.TargetSuffixesAdd("Dto2"));
        builder.Bind<SuffixOrigin, SuffixTargetDto2>();

        var sculptor = builder.Forge();
        var dto = sculptor.Map<SuffixOrigin, SuffixTargetDto2>(new SuffixOrigin { Name = "x" });

        dto.NameDto2.Should().Be("x");
    }

    [Fact]
    public void Forge_UsesCustomAbbreviations_WhenEnabled()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.Conventions.EnableAbbreviationExpansion(d => d["Cust"] = "Customer"));
        builder.Bind<AbbrevOrigin, AbbrevTarget>();

        var sculptor = builder.Forge();
        var dto = sculptor.Map<AbbrevOrigin, AbbrevTarget>(new AbbrevOrigin { Customer = "Ada" });

        dto.Cust.Should().Be("Ada");
    }

    [Fact]
    public void Forge_UsesCustomConvention_WhenRegistered()
    {
        var builder = new SculptorBuilder();
        builder.Configure(o => o.Conventions.Add<FixedValueConvention>());
        builder.Bind<CustomConvOrigin, CustomConvTarget>();

        var sculptor = builder.Forge();
        var dto = sculptor.Map<CustomConvOrigin, CustomConvTarget>(new CustomConvOrigin());

        dto.Injected.Should().Be("injected-by-custom-convention");
    }

    // ============================ AssemblyScanner caching ============================

    [Fact]
    public void AssemblyScanner_CachesPerAssembly_AcrossScannerInstances()
    {
        var asm = typeof(Sprint7ThirdPassGapTests).Assembly;
        var first = new AssemblyScanner().Scan(asm);
        var second = new AssemblyScanner().Scan(asm);

        // Same cached singleton instance returned for repeat single-assembly scans.
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AssemblyScanner_MultiAssembly_UsesCachedSingleAssemblyResults()
    {
        var asm1 = typeof(Sprint7ThirdPassGapTests).Assembly;
        var asm2 = typeof(FluentAssertions.AssertionExtensions).Assembly;

        var a = new AssemblyScanner().Scan(asm1, asm2);
        var b = new AssemblyScanner().Scan(asm1, asm2);

        // Multi-assembly produces a new merged result object — but the underlying per-assembly
        // data is cached, so content is deterministic.
        a.BlueprintTypes.Select(t => t.FullName)
            .Should().BeEquivalentTo(b.BlueprintTypes.Select(t => t.FullName),
                o => o.WithStrictOrdering());
    }

    // ============================ Unknown-pair fuzzy-match ============================

    [Fact]
    public void UnknownPair_ErrorMessage_SuggestsNearestRegisteredPair()
    {
        var builder = new SculptorBuilder();
        builder.Bind<Sprint7Order, Sprint7OrderDto>();
        var sculptor = builder.Forge();

        // Request a pair whose origin type shares most characters with Sprint7Order.
        var act = () => sculptor.Map<Sprint7OrderLookalike, Sprint7OrderDto>(new Sprint7OrderLookalike());

        act.Should().Throw<MappingConfigurationException>()
            .Where(ex => ex.Message.Contains("Did you mean"))
            .Where(ex => ex.Message.Contains("Sprint7Order"));
    }

    [Fact]
    public void UnknownPair_NoSuggestions_WhenNoBlueprintsRegistered()
    {
        var sculptor = new SculptorBuilder().Forge();

        var act = () => sculptor.Map<Sprint7Order, Sprint7OrderDto>(new Sprint7Order());

        act.Should().Throw<MappingConfigurationException>()
            .Where(ex => !ex.Message.Contains("Did you mean"));
    }

    // ============================ Fixtures ============================

    public class CustomPrefixOrigin
    {
        public string DstName { get; set; } = string.Empty;
    }

    public class CustomPrefixTarget
    {
        public string Name { get; set; } = string.Empty;
    }

    public class SuffixOrigin
    {
        public string Name { get; set; } = string.Empty;
    }

    public class SuffixTargetDto2
    {
        public string NameDto2 { get; set; } = string.Empty;
    }

    public class AbbrevOrigin
    {
        public string Customer { get; set; } = string.Empty;
    }

    public class AbbrevTarget
    {
        public string Cust { get; set; } = string.Empty;
    }

    public class CustomConvOrigin { }

    public class CustomConvTarget
    {
        public string Injected { get; set; } = string.Empty;
    }

    /// <summary>
    /// Simple custom convention that always links any target property to a constant string.
    /// Priority ordered after built-ins so it only fills gaps.
    /// </summary>
    public sealed class FixedValueConvention : IPropertyConvention
    {
        public int Priority => 900;

        public bool TryLink(MemberInfo targetMember, Discovery.TypeModel originModel, out IValueProvider? provider)
        {
            provider = new ConstantProvider("injected-by-custom-convention");
            return true;
        }

        private sealed class ConstantProvider : IValueProvider
        {
            private readonly string _value;
            public ConstantProvider(string value) => _value = value;
            public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => _value;
        }
    }

    public class Sprint7OrderLookalike
    {
        public int Id { get; set; }
    }
}
