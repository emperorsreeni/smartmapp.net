using FluentAssertions;
using SmartMapp.Net.Tests.Unit.TestTypes;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class EnumTransformerTests
{
    private readonly MappingScope _scope = new();

    // ── EnumToString ─────────────────────────────────────────────────

    [Fact]
    public void EnumToString_Default_ReturnsMemberName()
    {
        var transformer = new EnumToStringTransformer();

        var result = transformer.Transform(OrderStatus.Pending, _scope);

        result.Should().Be("Pending");
    }

    [Fact]
    public void EnumToString_WithDescription_ReturnsDescriptionAttribute()
    {
        var options = new EnumTransformerOptions { UseDescriptionAttribute = true };
        var transformer = new EnumToStringTransformer(options);

        var result = transformer.Transform(OrderStatus.Shipped, _scope);

        result.Should().Be("Order has shipped");
    }

    [Fact]
    public void EnumToString_CanTransform_IdentifiesEnums()
    {
        var transformer = new EnumToStringTransformer();

        transformer.CanTransform(typeof(OrderStatus), typeof(string)).Should().BeTrue();
        transformer.CanTransform(typeof(string), typeof(OrderStatus)).Should().BeFalse();
        transformer.CanTransform(typeof(int), typeof(string)).Should().BeFalse();
    }

    // ── StringToEnum ─────────────────────────────────────────────────

    [Fact]
    public void StringToEnum_CaseInsensitive_Parses()
    {
        var transformer = new StringToEnumTransformer();

        var result = transformer.Transform("pending", typeof(OrderStatusDto), _scope);

        result.Should().Be(OrderStatusDto.Pending);
    }

    [Fact]
    public void StringToEnum_Invalid_NoFallback_Throws()
    {
        var transformer = new StringToEnumTransformer();

        var act = () => transformer.Transform("NonExistent", typeof(OrderStatusDto), _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void StringToEnum_Invalid_WithFallback_ReturnsFallback()
    {
        var options = new EnumTransformerOptions();
        options.FallbackValue(OrderStatusDto.Unknown);
        var transformer = new StringToEnumTransformer(options);

        var result = transformer.Transform("NonExistent", typeof(OrderStatusDto), _scope);

        result.Should().Be(OrderStatusDto.Unknown);
    }

    [Fact]
    public void StringToEnum_Null_WithFallback_ReturnsFallback()
    {
        var options = new EnumTransformerOptions();
        options.FallbackValue(OrderStatusDto.Unknown);
        var transformer = new StringToEnumTransformer(options);

        var result = transformer.Transform(null, typeof(OrderStatusDto), _scope);

        result.Should().Be(OrderStatusDto.Unknown);
    }

    [Fact]
    public void StringToEnum_CaseSensitive_ExactCase_Parses()
    {
        var options = new EnumTransformerOptions { CaseInsensitive = false };
        var transformer = new StringToEnumTransformer(options);

        var result = transformer.Transform("Pending", typeof(OrderStatusDto), _scope);

        result.Should().Be(OrderStatusDto.Pending);
    }

    [Fact]
    public void StringToEnum_CaseSensitive_WrongCase_Throws()
    {
        var options = new EnumTransformerOptions { CaseInsensitive = false };
        var transformer = new StringToEnumTransformer(options);

        var act = () => transformer.Transform("pending", typeof(OrderStatusDto), _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void StringToEnum_CanTransform_IdentifiesStringToEnum()
    {
        var transformer = new StringToEnumTransformer();

        transformer.CanTransform(typeof(string), typeof(OrderStatus)).Should().BeTrue();
        transformer.CanTransform(typeof(int), typeof(OrderStatus)).Should().BeFalse();
    }

    // ── EnumToEnum ───────────────────────────────────────────────────

    [Fact]
    public void EnumToEnum_ByName_MatchingMembers()
    {
        var transformer = new EnumToEnumTransformer();

        var result = transformer.Transform(OrderStatus.Pending, typeof(OrderStatusDto), _scope);

        result.Should().Be(OrderStatusDto.Pending);
    }

    [Fact]
    public void EnumToEnum_ByValue_MatchingValues()
    {
        var options = new EnumTransformerOptions { Strategy = EnumMappingStrategy.ByValue };
        var transformer = new EnumToEnumTransformer(options);

        var result = transformer.Transform(OrderStatus.Shipped, typeof(OrderStatusDto), _scope);

        result.Should().Be(OrderStatusDto.Shipped);
    }

    [Fact]
    public void EnumToEnum_NonOverlapping_Throws()
    {
        var transformer = new EnumToEnumTransformer();

        var act = () => transformer.Transform(OrderStatus.Pending, typeof(PaymentStatus), _scope);

        act.Should().Throw<TransformationException>();
    }

    [Fact]
    public void EnumToEnum_CanTransform_EnumToEnum_DifferentTypes()
    {
        var transformer = new EnumToEnumTransformer();

        transformer.CanTransform(typeof(OrderStatus), typeof(OrderStatusDto)).Should().BeTrue();
        transformer.CanTransform(typeof(OrderStatus), typeof(OrderStatus)).Should().BeFalse();
        transformer.CanTransform(typeof(int), typeof(OrderStatus)).Should().BeFalse();
    }

    [Fact]
    public void EnumToEnum_Flags_ByName_CombinesValues()
    {
        var transformer = new EnumToEnumTransformer();

        var combined = FilePermissions.Read | FilePermissions.Write;
        var result = transformer.Transform(combined, typeof(FilePermissionsDto), _scope);

        result.Should().Be(FilePermissionsDto.Read | FilePermissionsDto.Write);
    }

    [Fact]
    public void EnumToString_Flags_ReturnsCommaSeparated()
    {
        var transformer = new EnumToStringTransformer();

        var combined = FilePermissions.Read | FilePermissions.Write;
        var result = transformer.Transform(combined, _scope);

        result.Should().Be("Read, Write");
    }
}
