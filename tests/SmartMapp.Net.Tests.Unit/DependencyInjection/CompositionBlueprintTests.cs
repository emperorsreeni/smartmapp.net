using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Composition;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T08 — accumulator / immutability / duplicate-rejection tests for the
/// composition rule builder and the forged <see cref="CompositionBlueprint"/> it produces.
/// </summary>
public class CompositionBlueprintTests
{
    [Fact]
    public void CompositionRule_FromOrigin_AccumulatesOriginsInDeclarationOrder()
    {
        var rule = new CompositionRuleBuilder<S8T08DashboardViewModel>();

        rule.FromOrigin<S8T08User>()
            .FromOrigin<S8T08OrderSummary>()
            .FromOrigin<S8T08Company>();

        rule.Origins.Select(o => o.OriginType).Should().Equal(
            typeof(S8T08User),
            typeof(S8T08OrderSummary),
            typeof(S8T08Company));
    }

    [Fact]
    public void CompositionRule_DuplicateFromOrigin_Throws()
    {
        var rule = new CompositionRuleBuilder<S8T08DashboardViewModel>();
        rule.FromOrigin<S8T08User>();

        var act = () => rule.FromOrigin<S8T08User>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate composition origin*S8T08User*");
    }

    [Fact]
    public void ForgedConfig_CompositionBlueprints_HaveDeclarationOrderOrigins()
    {
        var sculptor = (Sculptor)new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>()
                    .FromOrigin<S8T08OrderSummary>());
            })
            .Forge();

        var cb = sculptor.ForgedConfiguration.CompositionBlueprints.Single();
        cb.TargetType.Should().Be(typeof(S8T08DashboardViewModel));
        cb.Origins.Select(o => o.OriginType).Should().Equal(
            new[] { typeof(S8T08User), typeof(S8T08OrderSummary) },
            because: "declaration order is preserved from FromOrigin<...> calls.");
    }

    [Fact]
    public void ForgedConfig_PartialBlueprint_PicksUpConventionLinks()
    {
        // FromOrigin<User>() with no explicit .Property(...) calls — convention matching must
        // still discover User.UserId → Dashboard.UserId / User.DisplayName → Dashboard.DisplayName
        // / User.Email → Dashboard.Email.
        var sculptor = (Sculptor)new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c.FromOrigin<S8T08User>());
            })
            .Forge();

        var cb = sculptor.ForgedConfiguration.CompositionBlueprints.Single();
        var partial = cb.Origins[0].PartialBlueprint;

        partial.Links.Select(l => l.TargetMember.Name)
            .Should().Contain(new[] { "UserId", "DisplayName", "Email" });
    }

    [Fact]
    public void ForgedConfig_DuplicateTargetAcrossTwoComposeCalls_Throws()
    {
        // Spec §S8-T08 Constraints: exactly one composition per target type.
        var act = () => new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c.FromOrigin<S8T08User>());
                options.Compose<S8T08DashboardViewModel>(c => c.FromOrigin<S8T08Company>());
            })
            .Forge();

        act.Should().Throw<SmartMapp.Net.Diagnostics.BlueprintValidationException>()
            .WithMessage("*Multiple composition rules*S8T08DashboardViewModel*");
    }
}
