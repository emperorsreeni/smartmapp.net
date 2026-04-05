using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Tests.Unit.TestTypes;
using NSubstitute;

namespace SmartMapp.Net.Tests.Unit;

public class BlueprintValidatorTests
{
    private readonly BlueprintValidator _validator;

    public BlueprintValidatorTests()
    {
        _validator = new BlueprintValidator(TypeModelCache.Default);
    }

    [Fact]
    public void Validate_EmptyBlueprints_IsValid()
    {
        var result = _validator.Validate(Array.Empty<Blueprint>());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DuplicateBindings_ReportsError()
    {
        var bp1 = Blueprint.Empty(TypePair.Of<SimpleClass, SimpleDto>());
        var bp2 = Blueprint.Empty(TypePair.Of<SimpleClass, SimpleDto>());

        var result = _validator.Validate(new[] { bp1, bp2 });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("Duplicate binding");
    }

    [Fact]
    public void Validate_UnlinkedRequiredMember_StrictMode_ReportsError()
    {
        var bp = new Blueprint
        {
            OriginType = typeof(SimpleClass),
            TargetType = typeof(RequiredClass),
            Links = Array.Empty<PropertyLink>(),
            StrictRequiredMembers = true,
        };

        var result = _validator.Validate(new[] { bp }, strictMode: true);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Required target member"));
    }

    [Fact]
    public void Validate_AllMembersLinked_IsValid()
    {
        var link1 = new PropertyLink
        {
            TargetMember = typeof(SimpleDto).GetProperty("Id")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Id"),
        };
        var link2 = new PropertyLink
        {
            TargetMember = typeof(SimpleDto).GetProperty("Name")!,
            Provider = Substitute.For<IValueProvider>(),
            LinkedBy = ConventionMatch.ExactName("Name"),
        };

        var bp = new Blueprint
        {
            OriginType = typeof(SimpleClass),
            TargetType = typeof(SimpleDto),
            Links = new[] { link1, link2 },
        };

        var result = _validator.Validate(new[] { bp });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_DiscriminatorWithoutOtherwise_ReportsError()
    {
        var discConfig = new DiscriminatorConfig(
            System.Linq.Expressions.Expression.Lambda(
                System.Linq.Expressions.Expression.Constant("test")));
        // No Otherwise set

        var config = new BindingConfiguration(TypePair.Of<Shape, ShapeDto>())
        {
            Discriminator = discConfig,
        };

        var result = _validator.Validate(
            Array.Empty<Blueprint>(),
            new[] { config });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("Otherwise()");
    }

    [Fact]
    public void Validate_DiscriminatorWithOtherwise_IsValid()
    {
        var discConfig = new DiscriminatorConfig(
            System.Linq.Expressions.Expression.Lambda(
                System.Linq.Expressions.Expression.Constant("test")));
        discConfig.OtherwisePair = TypePair.Of<Shape, ShapeDto>();

        var config = new BindingConfiguration(TypePair.Of<Shape, ShapeDto>())
        {
            Discriminator = discConfig,
        };

        var result = _validator.Validate(
            Array.Empty<Blueprint>(),
            new[] { config });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidMaterializeType_ReportsError()
    {
        var config = new BindingConfiguration(TypePair.Of<PersonSource, IPersonDto>())
        {
            MaterializeType = typeof(SimpleClass), // Does not implement IPersonDto
        };

        var result = _validator.Validate(
            Array.Empty<Blueprint>(),
            new[] { config });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("does not implement");
    }

    [Fact]
    public void Validate_ValidMaterializeType_IsValid()
    {
        var config = new BindingConfiguration(TypePair.Of<PersonSource, IPersonDto>())
        {
            MaterializeType = typeof(PersonDtoImpl),
        };

        var result = _validator.Validate(
            Array.Empty<Blueprint>(),
            new[] { config });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AbstractMaterializeType_ReportsError()
    {
        var config = new BindingConfiguration(TypePair.Of<AnimalSource, AbstractAnimal>())
        {
            MaterializeType = typeof(AbstractAnimal), // Still abstract
        };

        var result = _validator.Validate(
            Array.Empty<Blueprint>(),
            new[] { config });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Message.Should().Contain("concrete");
    }

    [Fact]
    public void Validate_CircularInheritance_ReportsError()
    {
        var configA = new BindingConfiguration(TypePair.Of<SimpleClass, SimpleDto>())
        {
            InheritFromPair = TypePair.Of<Order, OrderDto>(),
        };
        var configB = new BindingConfiguration(TypePair.Of<Order, OrderDto>())
        {
            InheritFromPair = TypePair.Of<SimpleClass, SimpleDto>(),
        };

        var result = _validator.Validate(
            Array.Empty<Blueprint>(),
            new[] { configA, configB });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("Circular"));
    }

    [Fact]
    public void ValidationResult_ToString_ContainsAllFindings()
    {
        var result = new BlueprintValidationResult();
        result.AddError(typeof(SimpleClass), typeof(SimpleDto), "Test error");
        result.AddWarning(typeof(Order), typeof(OrderDto), "Test warning");

        var str = result.ToString();
        str.Should().Contain("Test error");
        str.Should().Contain("Test warning");
        str.Should().Contain("FAILED");
    }

    [Fact]
    public void ValidationResult_NoFindings_ShowsPassedMessage()
    {
        var result = new BlueprintValidationResult();
        result.ToString().Should().Contain("passed");
    }
}
