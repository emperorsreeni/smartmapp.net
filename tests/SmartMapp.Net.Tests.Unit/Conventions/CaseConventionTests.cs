using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class CaseConventionTests
{
    private readonly CaseConvention _convention = new();

    [Fact]
    public void Priority_Is200()
    {
        _convention.Priority.Should().Be(200);
    }

    [Fact]
    public void TryLink_SnakeCaseToPascalCase()
    {
        var origin = new TypeModel(typeof(SnakeCaseSource));
        var target = typeof(PascalCaseTarget).GetProperty(nameof(PascalCaseTarget.FirstName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new SnakeCaseSource { first_name = "Alice" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Alice");
    }

    [Fact]
    public void TryLink_ScreamingSnakeToPascalCase()
    {
        var origin = new TypeModel(typeof(ScreamingSnakeSource));
        var target = typeof(PascalCaseTarget).GetProperty(nameof(PascalCaseTarget.FirstName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();
    }

    [Fact]
    public void TryLink_CamelCaseToPascalCase_SkipsExactMatch()
    {
        // camelCase "firstName" vs PascalCase "FirstName" — 
        // These match case-insensitively so ExactNameConvention handles them.
        // CaseConvention skips exact case-insensitive matches.
        var origin = new TypeModel(typeof(CamelCaseSource));
        var target = typeof(PascalCaseTarget).GetProperty(nameof(PascalCaseTarget.FirstName))!;

        // CaseConvention should skip because "firstName" matches "FirstName" case-insensitively
        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
    }

    [Fact]
    public void TryLink_NoMatch_ReturnsFalse()
    {
        var origin = new TypeModel(typeof(SnakeCaseSource));
        var target = typeof(NonFlatDto).GetProperty(nameof(NonFlatDto.FooBarBaz))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_MultiSegmentSnakeCase()
    {
        var origin = new TypeModel(typeof(SnakeCaseSource));
        var target = typeof(PascalCaseTarget).GetProperty(nameof(PascalCaseTarget.UserAge))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().NotBeNull();

        var source = new SnakeCaseSource { user_age = 30 };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be(30);
    }
}
