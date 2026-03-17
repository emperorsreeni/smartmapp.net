using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class MethodToPropertyConventionTests
{
    private readonly MethodToPropertyConvention _convention = new();

    [Fact]
    public void Priority_Is275()
    {
        _convention.Priority.Should().Be(275);
    }

    [Fact]
    public void TryLink_GetFullName_MatchesFullName()
    {
        var origin = new TypeModel(typeof(MethodSource));
        var target = typeof(MethodTarget).GetProperty(nameof(MethodTarget.FullName))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<MethodAccessProvider>();

        var source = new MethodSource { FirstName = "Alice", LastName = "Smith" };
        var result = provider!.Provide(source, null!, "", new MappingScope());
        result.Should().Be("Alice Smith");
    }

    [Fact]
    public void TryLink_GetTotal_MatchesTotal()
    {
        var origin = new TypeModel(typeof(MethodSource));
        var target = typeof(MethodTarget).GetProperty(nameof(MethodTarget.Total))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<MethodAccessProvider>();

        var result = provider!.Provide(new MethodSource(), null!, "", new MappingScope());
        result.Should().Be(100m);
    }

    [Fact]
    public void TryLink_ExactMethodName_MatchesComputedAge()
    {
        var origin = new TypeModel(typeof(MethodSource));
        var target = typeof(MethodTarget).GetProperty(nameof(MethodTarget.ComputedAge))!;

        _convention.TryLink(target, origin, out var provider).Should().BeTrue();
        provider.Should().BeOfType<MethodAccessProvider>();

        var result = provider!.Provide(new MethodSource(), null!, "", new MappingScope());
        result.Should().Be(25);
    }

    [Fact]
    public void TryLink_NoMatch_ReturnsFalse()
    {
        var origin = new TypeModel(typeof(MethodSource));
        var target = typeof(NonFlatDto).GetProperty(nameof(NonFlatDto.FooBarBaz))!;

        _convention.TryLink(target, origin, out var provider).Should().BeFalse();
        provider.Should().BeNull();
    }

    [Fact]
    public void TryLink_IgnoresVoidMethods()
    {
        // MethodSource.DoSomething() is void — should not appear in ParameterlessValueMethods
        var origin = new TypeModel(typeof(MethodSource));
        origin.ParameterlessValueMethods.Should().NotContain(m => m.Name == "DoSomething");
    }

    [Fact]
    public void TryLink_IgnoresMethodsWithParameters()
    {
        // MethodSource.GetValue(int) has a parameter — should not appear
        var origin = new TypeModel(typeof(MethodSource));
        origin.ParameterlessValueMethods.Should().NotContain(m => m.Name == "GetValue");
    }

    [Fact]
    public void MethodAccessProvider_ToString_ContainsMethodName()
    {
        var method = typeof(MethodSource).GetMethod(nameof(MethodSource.GetFullName))!;
        var provider = new MethodAccessProvider(method);

        provider.ToString().Should().Contain("GetFullName");
    }

    [Fact]
    public void MethodAccessProvider_NullOrigin_ReturnsNull()
    {
        var method = typeof(MethodSource).GetMethod(nameof(MethodSource.GetFullName))!;
        var provider = new MethodAccessProvider(method);

        var result = provider.Provide(null!, null!, "", new MappingScope());

        result.Should().BeNull();
    }
}
