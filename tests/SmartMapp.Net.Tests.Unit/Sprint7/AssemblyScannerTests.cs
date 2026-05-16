using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;
using SmartMapp.Net.Discovery;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class AssemblyScannerTests
{
    [Fact]
    public void Scan_Null_ReturnsEmpty()
    {
        var result = new AssemblyScanner().Scan((System.Reflection.Assembly[]?)null);
        result.Should().BeSameAs(AssemblyScanResult.Empty);
    }

    [Fact]
    public void Scan_Empty_ReturnsEmpty()
    {
        var result = new AssemblyScanner().Scan(Array.Empty<System.Reflection.Assembly>());
        result.Should().BeSameAs(AssemblyScanResult.Empty);
    }

    [Fact]
    public void Scan_DedupesAssemblies()
    {
        var asm = typeof(AssemblyScannerTests).Assembly;
        var result = new AssemblyScanner().Scan(asm, asm, asm);
        result.ScannedAssemblies.Should().HaveCount(1);
    }

    [Fact]
    public void Scan_DiscoversPublicBlueprintSubclasses()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(ScannerFixtures.PublicScannerBlueprint));
        result.BlueprintTypes.Should().Contain(typeof(ScannerFixtures.PublicScannerBlueprint));
    }

    [Fact]
    public void Scan_ExcludesAbstractBlueprints()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(ScannerFixtures.AbstractScannerBlueprint));
        result.BlueprintTypes.Should().NotContain(typeof(ScannerFixtures.AbstractScannerBlueprint));
    }

    [Fact]
    public void Scan_ExcludesOpenGenericTypes()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(ScannerFixtures.OpenGenericProvider<>));
        result.BlueprintTypes.Should().NotContain(typeof(ScannerFixtures.OpenGenericProvider<>));
        result.ValueProviders.Should().NotContain(p => p.ImplementationType == typeof(ScannerFixtures.OpenGenericProvider<>));
    }

    [Fact]
    public void Scan_ExcludesPrivateNestedTypes()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(AssemblyScannerTests));
        // ConventionPipelineTests (public sibling) is fine — but private nested test fixtures must NOT surface.
        result.BlueprintTypes.Should().NotContain(t => t.DeclaringType == typeof(MappingBlueprintTests));
    }

    [Fact]
    public void Scan_DiscoversClosedGenericValueProvider()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(ScannerFixtures.PublicFixedProvider));
        result.ValueProviders.Should().Contain(p =>
            p.ImplementationType == typeof(ScannerFixtures.PublicFixedProvider)
            && p.GenericArguments.Count == 3);
    }

    [Fact]
    public void Scan_DiscoversClosedGenericTypeTransformer()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(ScannerFixtures.PublicIntToStringTransformer));
        result.TypeTransformers.Should().Contain(t =>
            t.ImplementationType == typeof(ScannerFixtures.PublicIntToStringTransformer)
            && t.GenericArguments.Count == 2
            && t.GenericArguments[0] == typeof(int)
            && t.GenericArguments[1] == typeof(string));
    }

    [Fact]
    public void Scan_DiscoversAttributedPairs_Both_MappedBy_And_MapsInto()
    {
        var result = new AssemblyScanner().ScanContaining(typeof(ScannerFixtures.PublicMappedByTarget));

        result.AttributedPairs.Should().Contain(p =>
            p.OriginType == typeof(ScannerFixtures.PublicMappedByOrigin)
            && p.TargetType == typeof(ScannerFixtures.PublicMappedByTarget)
            && p.Source == AttributeSource.MappedBy);

        result.AttributedPairs.Should().Contain(p =>
            p.OriginType == typeof(ScannerFixtures.PublicMapsIntoOrigin)
            && p.TargetType == typeof(ScannerFixtures.PublicMapsIntoTarget)
            && p.Source == AttributeSource.MapsInto);
    }

    [Fact]
    public void Scan_DeterministicOrdering_ByFullName()
    {
        var asm = typeof(AssemblyScannerTests).Assembly;
        var first = new AssemblyScanner().Scan(asm);
        var second = new AssemblyScanner().Scan(asm);

        first.BlueprintTypes.Select(t => t.FullName).Should().BeEquivalentTo(
            second.BlueprintTypes.Select(t => t.FullName), o => o.WithStrictOrdering());

        first.AttributedPairs.Select(p => $"{p.OriginType.FullName}->{p.TargetType.FullName}:{p.Source}")
            .Should().BeEquivalentTo(
                second.AttributedPairs.Select(p => $"{p.OriginType.FullName}->{p.TargetType.FullName}:{p.Source}"),
                o => o.WithStrictOrdering());
    }

    [Fact]
    public void ScanContaining_Empty_ReturnsEmpty()
    {
        var result = new AssemblyScanner().ScanContaining();
        result.Should().BeSameAs(AssemblyScanResult.Empty);
    }
}

/// <summary>
/// Public fixture namespace for scanner discovery tests. All types here are intentionally public
/// so the scanner's IsVisible filter lets them through.
/// </summary>
public static class ScannerFixtures
{
    public class PublicScannerOrigin { public int Id { get; set; } }
    public class PublicScannerTarget { public int Id { get; init; } }

    public class PublicScannerBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<PublicScannerOrigin, PublicScannerTarget>();
        }
    }

    public abstract class AbstractScannerBlueprint : MappingBlueprint
    {
    }

    public sealed class OpenGenericProvider<T> : IValueProvider<PublicScannerOrigin, PublicScannerTarget, T>
    {
        public T Provide(PublicScannerOrigin origin, PublicScannerTarget target, string targetMemberName, MappingScope scope)
            => default!;

        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope)
            => default;
    }

    public sealed class PublicFixedProvider : IValueProvider<PublicScannerOrigin, PublicScannerTarget, int>
    {
        public int Provide(PublicScannerOrigin origin, PublicScannerTarget target, string targetMemberName, MappingScope scope) => 7;
        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => 7;
    }

    public sealed class PublicIntToStringTransformer : ITypeTransformer<int, string>
    {
        public bool CanTransform(Type originType, Type targetType) => true;
        public string Transform(int origin, MappingScope scope) => origin.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public class PublicMappedByOrigin { public int Id { get; set; } }

    [MappedBy(typeof(PublicMappedByOrigin))]
    public record PublicMappedByTarget { public int Id { get; init; } }

    public class PublicMapsIntoTarget { public int Id { get; set; } }

    [MapsInto(typeof(PublicMapsIntoTarget))]
    public class PublicMapsIntoOrigin { public int Id { get; set; } }
}
