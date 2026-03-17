using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class PropertyAccessProviderTests
{
    [Fact]
    public void Provide_ReadsPropertyValue()
    {
        var prop = typeof(ExactSource).GetProperty(nameof(ExactSource.Name))!;
        var provider = new PropertyAccessProvider(prop);
        var source = new ExactSource { Name = "Alice" };

        var result = provider.Provide(source, null!, "", new MappingScope());

        result.Should().Be("Alice");
    }

    [Fact]
    public void Provide_ReadsFieldValue()
    {
        var field = typeof(FieldAndPropertySource).GetField(nameof(FieldAndPropertySource.Id))!;
        var provider = new PropertyAccessProvider(field);
        var source = new FieldAndPropertySource { Id = 42 };

        var result = provider.Provide(source, null!, "", new MappingScope());

        result.Should().Be(42);
    }

    [Fact]
    public void MemberPath_DefaultsToMemberName()
    {
        var prop = typeof(ExactSource).GetProperty(nameof(ExactSource.Name))!;
        var provider = new PropertyAccessProvider(prop);

        provider.MemberPath.Should().Be("Name");
    }

    [Fact]
    public void MemberPath_UsesOverrideWhenProvided()
    {
        var prop = typeof(ExactSource).GetProperty(nameof(ExactSource.Name))!;
        var provider = new PropertyAccessProvider(prop, "Custom.Path");

        provider.MemberPath.Should().Be("Custom.Path");
    }

    [Fact]
    public void ToString_ContainsMemberPath()
    {
        var prop = typeof(ExactSource).GetProperty(nameof(ExactSource.Name))!;
        var provider = new PropertyAccessProvider(prop);

        provider.ToString().Should().Contain("Name");
    }

    [Fact]
    public void Provide_NullOrigin_ThrowsNullReference()
    {
        var prop = typeof(ExactSource).GetProperty(nameof(ExactSource.Name))!;
        var provider = new PropertyAccessProvider(prop);

        var act = () => provider.Provide(null!, null!, "", new MappingScope());

        act.Should().Throw<NullReferenceException>();
    }
}
