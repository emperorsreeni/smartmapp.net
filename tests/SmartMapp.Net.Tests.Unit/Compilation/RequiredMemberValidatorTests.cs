using FluentAssertions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

/// <summary>
/// Direct unit tests for <see cref="RequiredMemberValidator"/>.
/// Verifies strict and non-strict required member validation behavior.
/// </summary>
public class RequiredMemberValidatorTests
{
    private readonly TypeModelCache _cache = new();

    private static PropertyLink MakeLink(System.Reflection.MemberInfo target, string name, bool isSkipped = false)
    {
        return new PropertyLink
        {
            TargetMember = target,
            Provider = new DirectMemberProvider(target),
            LinkedBy = ConventionMatch.ExactName(name),
            IsSkipped = isSkipped,
        };
    }

#if NET7_0_OR_GREATER
    [Fact]
    public void Validate_AllRequiredMapped_ReturnsEmpty()
    {
        var targetModel = _cache.GetOrAdd<RequiredTarget>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var links = new List<PropertyLink>
        {
            MakeLink(typeof(RequiredTarget).GetProperty("Id")!, "Id"),
            MakeLink(typeof(RequiredTarget).GetProperty("Name")!, "Name"),
        };

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(RequiredTarget),
            Links = links,
        };

        var missing = RequiredMemberValidator.Validate(targetModel, blueprint, consumed, strict: false);
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MissingRequired_NonStrict_ReturnsMissingList()
    {
        var targetModel = _cache.GetOrAdd<RequiredTargetWithUnmapped>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Only Id and Name linked — MandatoryField missing
        var links = new List<PropertyLink>
        {
            MakeLink(typeof(RequiredTargetWithUnmapped).GetProperty("Id")!, "Id"),
            MakeLink(typeof(RequiredTargetWithUnmapped).GetProperty("Name")!, "Name"),
        };

        var blueprint = new Blueprint
        {
            OriginType = typeof(SimpleDto),
            TargetType = typeof(RequiredTargetWithUnmapped),
            Links = links,
        };

        var missing = RequiredMemberValidator.Validate(targetModel, blueprint, consumed, strict: false);
        missing.Should().Contain("MandatoryField");
    }

    [Fact]
    public void Validate_MissingRequired_Strict_Throws()
    {
        var targetModel = _cache.GetOrAdd<RequiredTargetWithUnmapped>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var links = new List<PropertyLink>
        {
            MakeLink(typeof(RequiredTargetWithUnmapped).GetProperty("Id")!, "Id"),
        };

        var blueprint = new Blueprint
        {
            OriginType = typeof(SimpleDto),
            TargetType = typeof(RequiredTargetWithUnmapped),
            Links = links,
        };

        var act = () => RequiredMemberValidator.Validate(targetModel, blueprint, consumed, strict: true);
        act.Should().Throw<MappingCompilationException>()
            .WithMessage("*MandatoryField*");
    }

    [Fact]
    public void Validate_RequiredCoveredByCtor_NotReported()
    {
        var targetModel = _cache.GetOrAdd<RequiredTarget>();
        // Id and Name consumed by constructor
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id", "Name" };

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(RequiredTarget),
            Links = new List<PropertyLink>(), // no links — all via ctor
        };

        var missing = RequiredMemberValidator.Validate(targetModel, blueprint, consumed, strict: true);
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SkippedLink_StillReportedAsMissing()
    {
        var targetModel = _cache.GetOrAdd<RequiredTargetWithUnmapped>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var links = new List<PropertyLink>
        {
            MakeLink(typeof(RequiredTargetWithUnmapped).GetProperty("Id")!, "Id"),
            MakeLink(typeof(RequiredTargetWithUnmapped).GetProperty("Name")!, "Name"),
            MakeLink(typeof(RequiredTargetWithUnmapped).GetProperty("MandatoryField")!, "MandatoryField", isSkipped: true),
        };

        var blueprint = new Blueprint
        {
            OriginType = typeof(SimpleDto),
            TargetType = typeof(RequiredTargetWithUnmapped),
            Links = links,
        };

        var missing = RequiredMemberValidator.Validate(targetModel, blueprint, consumed, strict: false);
        missing.Should().Contain("MandatoryField");
    }
#endif

    [Fact]
    public void Validate_OnNetStandard_ReturnsEmpty()
    {
        // On netstandard2.1 / net6.0 builds, required keyword isn't supported
        // The validator should be a no-op
        var targetModel = _cache.GetOrAdd<FlatOrderDto>();
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var blueprint = new Blueprint
        {
            OriginType = typeof(FlatOrder),
            TargetType = typeof(FlatOrderDto),
            Links = new List<PropertyLink>(),
        };

        var missing = RequiredMemberValidator.Validate(targetModel, blueprint, consumed, strict: true);
        missing.Should().BeEmpty();
    }
}
