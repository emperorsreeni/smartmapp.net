using System.Reflection;
using FluentAssertions;

namespace SmartMapp.Net.Tests.Unit;

public class InterfaceContractTests
{
    [Fact]
    public void ISculptor_HasAllExpectedMethods()
    {
        var methods = typeof(ISculptor).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var methodNames = methods.Select(m => m.Name).ToHashSet();

        methodNames.Should().Contain("Map");
        methodNames.Should().Contain("MapAll");
        methodNames.Should().Contain("MapToArray");
        methodNames.Should().Contain("MapLazy");
        methodNames.Should().Contain("MapStream");
        methodNames.Should().Contain("Compose");
        methodNames.Should().Contain("SelectAs");
        methodNames.Should().Contain("GetProjection");
        methodNames.Should().Contain("Inspect");
        methodNames.Should().Contain("GetMappingAtlas");

        // §14.1: ISculptor has 13 method signatures (some names have overloads)
        methods.Should().HaveCount(13);
    }

    [Fact]
    public void IMapper_HasExpectedMethods()
    {
        var type = typeof(IMapper<,>);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var methodNames = methods.Select(m => m.Name).ToHashSet();

        methodNames.Should().Contain("Map");
        methodNames.Should().Contain("MapAll");
    }

    [Fact]
    public void ISculptorConfiguration_HasExpectedMethods()
    {
        var methods = typeof(ISculptorConfiguration).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var methodNames = methods.Select(m => m.Name).ToHashSet();

        methodNames.Should().Contain("GetAllBlueprints");
        methodNames.Should().Contain("GetBlueprint");
        methodNames.Should().Contain("Validate");
        methodNames.Should().Contain("HasBinding");
    }
}
