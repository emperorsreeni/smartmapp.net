using System.Reflection;
using FluentAssertions;
using SmartMapp.Net.Attributes;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class AttributeDefinitionTests
{
    [Theory]
    [InlineData(typeof(MappedByAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false)]
    [InlineData(typeof(MapsIntoAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false)]
    [InlineData(typeof(LinkedFromAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false)]
    [InlineData(typeof(LinksToAttribute), AttributeTargets.Property | AttributeTargets.Field, true, false)]
    [InlineData(typeof(UnmappedAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false)]
    [InlineData(typeof(TransformWithAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false)]
    [InlineData(typeof(ProvideWithAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false)]
    [InlineData(typeof(MapsIntoEnumAttribute), AttributeTargets.Field, false, false)]
    public void Attribute_AttributeUsage_Matches_Spec(
        Type attributeType,
        AttributeTargets expectedTargets,
        bool expectedAllowMultiple,
        bool expectedInherited)
    {
        var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();
        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(expectedTargets);
        usage.AllowMultiple.Should().Be(expectedAllowMultiple);
        usage.Inherited.Should().Be(expectedInherited);
    }

    [Theory]
    [InlineData(typeof(MappedByAttribute))]
    [InlineData(typeof(MapsIntoAttribute))]
    [InlineData(typeof(LinkedFromAttribute))]
    [InlineData(typeof(LinksToAttribute))]
    [InlineData(typeof(UnmappedAttribute))]
    [InlineData(typeof(TransformWithAttribute))]
    [InlineData(typeof(ProvideWithAttribute))]
    [InlineData(typeof(MapsIntoEnumAttribute))]
    public void Attribute_IsSealed(Type attributeType)
    {
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void LinkedFromAttribute_PreservesPayload_Including_Transform()
    {
        var prop = typeof(LinkedFromTarget).GetProperty(nameof(LinkedFromTarget.Total))!;
        var attr = prop.GetCustomAttribute<LinkedFromAttribute>();

        attr.Should().NotBeNull();
        attr!.OriginMemberName.Should().Be("Lines");
        attr.Transform.Should().Be("Sum(Quantity * UnitPrice)");
    }

    [Fact]
    public void LinkedFromAttribute_Rejects_NullOrWhitespace_OriginMember()
    {
        var actNull = () => new LinkedFromAttribute(null!);
        var actEmpty = () => new LinkedFromAttribute("");
        var actWhitespace = () => new LinkedFromAttribute("   ");

        actNull.Should().Throw<ArgumentException>();
        actEmpty.Should().Throw<ArgumentException>();
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MappedByAttribute_Reflection_RoundTrip_NonGeneric()
    {
        var origins = AttributeReader.GetMappedByOriginTypes(typeof(TargetA));
        origins.Should().ContainSingle().Which.Should().Be(typeof(OriginA));
    }

    [Fact]
    public void MapsIntoAttribute_Reflection_RoundTrip_NonGeneric()
    {
        var targets = AttributeReader.GetMapsIntoTargetTypes(typeof(OriginB_Source));
        targets.Should().ContainSingle().Which.Should().Be(typeof(TargetB));
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void MappedByAttribute_Generic_ReadableViaAttributeReader()
    {
        var origins = AttributeReader.GetMappedByOriginTypes(typeof(TargetC));
        origins.Should().ContainSingle().Which.Should().Be(typeof(OriginC));
    }

    [Fact]
    public void MappedByAttribute_Generic_DirectReflection_AlsoWorks()
    {
        var attr = typeof(TargetC).GetCustomAttribute<MappedByAttribute<OriginC>>();
        attr.Should().NotBeNull();
        attr!.OriginType.Should().Be(typeof(OriginC));
    }
#endif

    [Fact]
    public void TransformWithAttribute_NonGeneric_CarriesType()
    {
        var prop = typeof(LinkedFromTarget).GetProperty(nameof(LinkedFromTarget.Total))!;
        var transformerType = AttributeReader.GetTransformerType(prop);
        transformerType.Should().BeNull(); // no transformer on Total

        var otherProp = typeof(LinkedFromTarget).GetProperty(nameof(LinkedFromTarget.Name))!;
        var otherType = AttributeReader.GetTransformerType(otherProp);
        otherType.Should().Be(typeof(NoOpTransformer));
    }

    [Fact]
    public void ProvideWithAttribute_NonGeneric_CarriesType()
    {
        var prop = typeof(LinkedFromTarget).GetProperty(nameof(LinkedFromTarget.Computed))!;
        var providerType = AttributeReader.GetProviderType(prop);
        providerType.Should().Be(typeof(NoOpProvider));
    }

    [Fact]
    public void MapsIntoEnumAttribute_CarriesTargetValue()
    {
        var field = typeof(Color).GetField(nameof(Color.Red))!;
        var attr = field.GetCustomAttribute<MapsIntoEnumAttribute>();
        attr.Should().NotBeNull();
        attr!.TargetValue.Should().Be(1);
    }

    // --- Fixtures ---

    private class OriginA { public int Id { get; set; } }

    [MappedBy(typeof(OriginA))]
    private class TargetA { public int Id { get; init; } }

    private class OriginB { public int Id { get; set; } }

    private class TargetB { public int Id { get; init; } }

#pragma warning disable CS9113 // Parameter unread
    [MapsInto(typeof(TargetB))]
    private class OriginB_Source
    {
        public int Id { get; set; }
    }
#pragma warning restore CS9113

    // Attribute declared on OriginB directly via nested helper — keep OriginB clean, use alias below:
    static AttributeDefinitionTests()
    {
        _ = typeof(OriginB_Source);
    }

#if NET7_0_OR_GREATER
    private class OriginC { public int Id { get; set; } }

    [MappedBy<OriginC>]
    private class TargetC { public int Id { get; init; } }
#endif

    private class LinkedFromTarget
    {
        [LinkedFrom("Lines", Transform = "Sum(Quantity * UnitPrice)")]
        public decimal Total { get; init; }

        [TransformWith(typeof(NoOpTransformer))]
        public string Name { get; init; } = string.Empty;

        [ProvideWith(typeof(NoOpProvider))]
        public int Computed { get; init; }
    }

    private sealed class NoOpTransformer : SmartMapp.Net.Abstractions.ITypeTransformer
    {
        public bool CanTransform(Type originType, Type targetType) => true;
    }

    private sealed class NoOpProvider : SmartMapp.Net.Abstractions.IValueProvider
    {
        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => null;
    }

    private enum Color
    {
        [MapsIntoEnum(1)]
        Red = 0,
    }
}
