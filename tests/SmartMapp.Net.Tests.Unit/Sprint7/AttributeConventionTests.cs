using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Conventions;
using Xunit;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class AttributeConventionTests
{
    private static readonly TypeModelCache Cache = TypeModelCache.Default;

    private static ConventionPipeline BuildPipeline() =>
        ConventionPipeline.CreateDefault(Cache);

    [Fact]
    public void Priority_IsLowestValue_SoItRunsFirst()
    {
        var convention = new AttributeConvention(Cache);
        convention.Priority.Should().Be(50);
    }

    [Fact]
    public void Unmapped_ProducesSkipLink()
    {
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(UnmappedOrigin), typeof(UnmappedTarget));

        var secret = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(UnmappedTarget.Secret)).Subject;
        secret.IsSkipped.Should().BeTrue();
        secret.LinkedBy.ConventionName.Should().Be("Attribute:Unmapped");
    }

    [Fact]
    public void LinkedFrom_Flat_Links_To_NamedOrigin()
    {
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(FlatOrigin), typeof(FlatLinkedFromTarget));

        var renamed = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(FlatLinkedFromTarget.Renamed)).Subject;
        renamed.IsSkipped.Should().BeFalse();
        renamed.LinkedBy.ConventionName.Should().StartWith("Attribute:LinkedFrom");
        renamed.LinkedBy.OriginMemberPath.Should().Be("Name");
    }

    [Fact]
    public void LinkedFrom_Dotted_Path_UsesChainedProvider()
    {
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(NestedOrigin), typeof(DottedLinkedFromTarget));

        var firstName = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(DottedLinkedFromTarget.FirstName)).Subject;
        firstName.IsSkipped.Should().BeFalse();
        firstName.LinkedBy.OriginMemberPath.Should().Be("Customer.FirstName");
        firstName.Provider.Should().BeOfType<ChainedPropertyAccessProvider>();
    }

    [Fact]
    public void AttributeConvention_Wins_Over_ExactNameConvention()
    {
        // Target has `Name` but [LinkedFrom("FullName")] — attribute must win.
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(NameShadowingOrigin), typeof(NameShadowingTarget));

        var name = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(NameShadowingTarget.Name)).Subject;
        name.LinkedBy.ConventionName.Should().StartWith("Attribute:LinkedFrom");
        name.LinkedBy.OriginMemberPath.Should().Be("FullName");
    }

    [Fact]
    public void TransformWith_AttachesDeferredTransformer()
    {
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(FlatOrigin), typeof(TransformTarget));

        var name = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(TransformTarget.Name)).Subject;
        name.Transformer.Should().NotBeNull();
        name.Transformer!.GetType().Name.Should().Be("AttributeDeferredTypeTransformer");
    }

    [Fact]
    public void ProvideWith_ProducesDeferredProvider()
    {
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(FlatOrigin), typeof(ProvideWithTarget));

        var computed = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(ProvideWithTarget.Computed)).Subject;
        computed.Provider.GetType().Name.Should().Be("AttributeDeferredValueProvider");
    }

    [Fact]
    public void LinksTo_OnOrigin_LinksToTargetWithMatchingName()
    {
        var pipeline = BuildPipeline();
        var links = pipeline.BuildLinks(typeof(LinksToOrigin), typeof(LinksToTarget));

        var renamed = links.Should().ContainSingle(l => l.TargetMember.Name == nameof(LinksToTarget.RenamedTarget)).Subject;
        renamed.IsSkipped.Should().BeFalse();
        renamed.Provider.Should().BeOfType<PropertyAccessProvider>();
    }

    [Fact]
    public void LinkedFrom_MissingMember_Throws_InvalidOperationException()
    {
        var convention = new AttributeConvention(Cache);
        var originModel = Cache.GetOrAdd(typeof(FlatOrigin));
        var target = typeof(BrokenLinkedFromTarget).GetProperty(nameof(BrokenLinkedFromTarget.Value))!;

        var act = () => convention.TryLink(target, originModel, out _);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*NoSuchMember*could not be resolved*");
    }

    [Fact]
    public void Unmapped_Plus_ProvideWith_ConflictDetected()
    {
        var convention = new AttributeConvention(Cache);
        var originModel = Cache.GetOrAdd(typeof(FlatOrigin));
        var target = typeof(UnmappedProvideConflict).GetProperty(nameof(UnmappedProvideConflict.Value))!;

        var act = () => convention.TryLink(target, originModel, out _);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unmapped*ProvideWith*");
    }

    // ---- Fixtures ----

    public class UnmappedOrigin
    {
        public int Id { get; set; }
        public string Secret { get; set; } = string.Empty;
    }

    public class UnmappedTarget
    {
        public int Id { get; set; }

        [Unmapped]
        public string? Secret { get; set; }
    }

    public class FlatOrigin
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class FlatLinkedFromTarget
    {
        public int Id { get; set; }

        [LinkedFrom("Name")]
        public string Renamed { get; set; } = string.Empty;
    }

    public class NestedOrigin
    {
        public int Id { get; set; }
        public NestedCustomer Customer { get; set; } = new();
    }

    public class NestedCustomer
    {
        public string FirstName { get; set; } = string.Empty;
    }

    public class DottedLinkedFromTarget
    {
        public int Id { get; set; }

        [LinkedFrom("Customer.FirstName")]
        public string FirstName { get; set; } = string.Empty;
    }

    public class NameShadowingOrigin
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
    }

    public class NameShadowingTarget
    {
        [LinkedFrom("FullName")]
        public string Name { get; set; } = string.Empty;
    }

    public class TransformTarget
    {
        [TransformWith(typeof(PassthroughStringTransformer))]
        public string Name { get; set; } = string.Empty;
    }

    public sealed class PassthroughStringTransformer : ITypeTransformer<string, string>
    {
        public bool CanTransform(Type originType, Type targetType) => true;
        public string Transform(string origin, MappingScope scope) => origin;
    }

    public class ProvideWithTarget
    {
        [ProvideWith(typeof(FixedComputedProvider))]
        public int Computed { get; set; }
    }

    public sealed class FixedComputedProvider : IValueProvider
    {
        public object? Provide(object origin, object target, string targetMemberName, MappingScope scope) => 42;
    }

    public class LinksToOrigin
    {
        public int Id { get; set; }

        [LinksTo("RenamedTarget")]
        public string OriginName { get; set; } = string.Empty;
    }

    public class LinksToTarget
    {
        public int Id { get; set; }
        public string RenamedTarget { get; set; } = string.Empty;
    }

    public class BrokenLinkedFromTarget
    {
        [LinkedFrom("NoSuchMember")]
        public string Value { get; set; } = string.Empty;
    }

    public class UnmappedProvideConflict
    {
        [Unmapped]
        [ProvideWith(typeof(FixedComputedProvider))]
        public string Value { get; set; } = string.Empty;
    }
}
