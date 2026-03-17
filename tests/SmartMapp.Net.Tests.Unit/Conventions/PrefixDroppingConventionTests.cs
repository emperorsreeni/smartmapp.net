using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class PrefixDroppingConventionTests
{
    private readonly PrefixDroppingConvention _convention = new();

    [Fact]
    public void Priority_Is250()
    {
        _convention.Priority.Should().Be(250);
    }

    [Fact]
    public void TryLink_StripsGetPrefix()
    {
        var origin = new TypeModel(typeof(PrefixSource));
        var target = typeof(PrefixTarget).GetProperty(nameof(PrefixTarget.Name))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new PrefixSource { GetName = "Alice" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Alice");
    }

    [Fact]
    public void TryLink_StripsUnderscorePrefix()
    {
        var origin = new TypeModel(typeof(PrefixSource));
        var target = typeof(PrefixTarget).GetProperty(nameof(PrefixTarget.Description))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void TryLink_StripsM_Prefix()
    {
        var origin = new TypeModel(typeof(PrefixSource));
        var target = typeof(PrefixTarget).GetProperty(nameof(PrefixTarget.Id))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new PrefixSource { m_id = 42 };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be(42);
    }

    [Fact]
    public void TryLink_StripsStrPrefix()
    {
        var origin = new TypeModel(typeof(PrefixSource));
        var target = typeof(PrefixTarget).GetProperty(nameof(PrefixTarget.CustomerName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void TryLink_StripsTargetSuffix()
    {
        var origin = new TypeModel(typeof(SuffixSource));
        var target = typeof(SuffixTarget).GetProperty(nameof(SuffixTarget.NameField))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new SuffixSource { Name = "Alice" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Alice");
    }

    [Fact]
    public void TryLink_StripsPropertySuffix()
    {
        var origin = new TypeModel(typeof(SuffixSource));
        var target = typeof(SuffixTarget).GetProperty(nameof(SuffixTarget.DescriptionProperty))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new SuffixSource { Description = "Hello" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Hello");
    }

    [Fact]
    public void TryLink_StripsPropSuffix()
    {
        var origin = new TypeModel(typeof(SuffixSource));
        var target = typeof(SuffixTarget).GetProperty(nameof(SuffixTarget.StatusProp))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new SuffixSource { Status = "Active" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Active");
    }

    [Fact]
    public void TryLink_CustomPrefixList()
    {
        var custom = new PrefixDroppingConvention(
            originPrefixes: new[] { "my_", "the_" });

        var origin = new TypeModel(typeof(PrefixSource));
        var target = typeof(PrefixTarget).GetProperty(nameof(PrefixTarget.Name))!;

        // Default "Get" prefix not in custom list — should not match
        custom.TryLink(target, origin, out _).Should().BeFalse();
    }
}
