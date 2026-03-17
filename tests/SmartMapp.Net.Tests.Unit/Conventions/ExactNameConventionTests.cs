using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class ExactNameConventionTests
{
    private readonly ExactNameConvention _convention = new();

    [Fact]
    public void Priority_Is100()
    {
        _convention.Priority.Should().Be(100);
    }

    [Fact]
    public void TryLink_ExactCaseMatch()
    {
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(ExactTarget).GetProperty(nameof(ExactTarget.Name))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
        provider.Should().BeOfType<PropertyAccessProvider>();

        var result = provider!.Provide(new ExactSource { Name = "Alice" }, null!, "", new MappingScope());
        result.Should().Be("Alice");
    }

    [Fact]
    public void TryLink_CaseInsensitiveMatch()
    {
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(CaseInsensitiveTarget).GetProperty(nameof(CaseInsensitiveTarget.name))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void TryLink_UpperCaseMatch()
    {
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(CaseInsensitiveTarget).GetProperty(nameof(CaseInsensitiveTarget.EMAIL))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void TryLink_NoMatch_ReturnsFalse()
    {
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(NonFlatDto).GetProperty(nameof(NonFlatDto.FooBarBaz))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_PrefersExactCaseOverCaseInsensitive()
    {
        // ExactSource has "Name" (exact case) — should prefer over case-insensitive
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(ExactTarget).GetProperty(nameof(ExactTarget.Name))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        var pap = provider.Should().BeOfType<PropertyAccessProvider>().Subject;
        pap.OriginMember.Name.Should().Be("Name");
    }

    [Fact]
    public void TryLink_PrefersPropertyOverField()
    {
        // FieldAndPropertySource has field "Id" and property "Code"
        var origin = new TypeModel(typeof(FieldAndPropertySource));
        var target = typeof(ExactTarget).GetProperty(nameof(ExactTarget.Name))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        var pap = provider.Should().BeOfType<PropertyAccessProvider>().Subject;
        // Name is a property — should match property
        pap.OriginMember.Name.Should().Be("Name");
    }

    [Fact]
    public void TryLink_MatchesIntProperty()
    {
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(ExactTarget).GetProperty(nameof(ExactTarget.Id))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        var result = provider!.Provide(new ExactSource { Id = 42 }, null!, "", new MappingScope());
        result.Should().Be(42);
    }

    [Fact]
    public void TryLink_MatchesDateTimeProperty()
    {
        var origin = new TypeModel(typeof(ExactSource));
        var target = typeof(DateTimeTarget).GetProperty(nameof(DateTimeTarget.CreatedAt))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var now = DateTime.UtcNow;
        var result = provider!.Provide(new ExactSource { CreatedAt = now }, null!, "", new MappingScope());
        result.Should().Be(now);
    }
}
