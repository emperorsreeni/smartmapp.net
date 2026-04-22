using FluentAssertions;
using SmartMapp.Net;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T08 — runtime dispatch tests for
/// <see cref="ISculptor.Compose{TTarget}(object[])"/>. Covers 2-, 3-, and 4-origin dispatch
/// (spec §S8-T08 Unit-Tests "2, 3, 4 origin dispatch"), null origins, order-independence,
/// ambiguous matches, unregistered target, single-origin parity, options-vs-builder parity,
/// last-origin-wins collision, missing-slot defaults, explicit-property override, and the
/// Sprint 15-reserved .When / .OnlyIf / .TransformWith fail-fast guards.
/// </summary>
public class CompositionDispatcherTests
{
    private static ISculptor BuildDashboardSculptor()
        => new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>()
                    .FromOrigin<S8T08OrderSummary>()
                    .FromOrigin<S8T08Company>());
            })
            .Forge();

    [Fact]
    public void Compose_TwoOrigins_ProducesDtoWithPropertiesFromBoth()
    {
        var sculptor = BuildDashboardSculptor();
        var user = new S8T08User { UserId = 1, DisplayName = "Alice", Email = "alice@example.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 3, LifetimeValue = 500m };

        var dto = sculptor.Compose<S8T08DashboardViewModel>(user, summary);

        dto.UserId.Should().Be(1);
        dto.DisplayName.Should().Be("Alice");
        dto.Email.Should().Be("alice@example.com");
        dto.OpenOrders.Should().Be(3);
        dto.LifetimeValue.Should().Be(500m);
    }

    [Fact]
    public void Compose_ThreeOrigins_ProducesDtoWithPropertiesFromAll()
    {
        var sculptor = BuildDashboardSculptor();
        var user = new S8T08User { UserId = 2, DisplayName = "Bob", Email = "bob@example.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 7, LifetimeValue = 1200m };
        var company = new S8T08Company { CompanyName = "Contoso", Plan = "Enterprise" };

        var dto = sculptor.Compose<S8T08DashboardViewModel>(user, summary, company);

        dto.UserId.Should().Be(2);
        dto.OpenOrders.Should().Be(7);
        dto.LifetimeValue.Should().Be(1200m);
        dto.CompanyName.Should().Be("Contoso");
        dto.Plan.Should().Be("Enterprise");
    }

    [Fact]
    public void Compose_OriginOrderIndependent_SameResult()
    {
        var sculptor = BuildDashboardSculptor();
        var user = new S8T08User { UserId = 3, DisplayName = "Carol", Email = "c@x.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 1, LifetimeValue = 10m };

        var forward = sculptor.Compose<S8T08DashboardViewModel>(user, summary);
        var reverse = sculptor.Compose<S8T08DashboardViewModel>(summary, user);

        reverse.Should().BeEquivalentTo(forward,
            "declaration order (not caller order) drives composition — spec §S8-T08 Acceptance bullet 3.");
    }

    [Fact]
    public void Compose_NullOriginInParamsArray_ContributesNothing_OtherOriginsApplied()
    {
        var sculptor = BuildDashboardSculptor();
        var user = new S8T08User { UserId = 4, DisplayName = "Dave", Email = "d@x.com" };

        var dto = sculptor.Compose<S8T08DashboardViewModel>(user, null!, null!);

        dto.UserId.Should().Be(4);
        dto.DisplayName.Should().Be("Dave");
        dto.OpenOrders.Should().Be(0, "the null OrderSummary slot contributed nothing.");
        dto.CompanyName.Should().BeEmpty("the null Company slot contributed nothing.");
    }

    [Fact]
    public void Compose_TwoInstancesOfSameDeclaredType_ThrowsAmbiguousMatch()
    {
        var sculptor = BuildDashboardSculptor();
        var u1 = new S8T08User { UserId = 1 };
        var u2 = new S8T08User { UserId = 2 };

        var act = () => sculptor.Compose<S8T08DashboardViewModel>(u1, u2);

        act.Should().Throw<SmartMapp.Net.MappingConfigurationException>()
            .WithMessage("*Ambiguous composition origin*two caller instances both match*S8T08User*");
    }

    [Fact]
    public void Compose_UnregisteredTarget_Throws()
    {
        // Use a simple sculptor with NO composition registered for TTarget.
        var sculptor = new SculptorBuilder()
            .Configure(options => options.Bind<S8T08User, S8T08UserDto>(_ => { }))
            .Forge();

        var u1 = new S8T08User { UserId = 1 };
        var u2 = new S8T08User { UserId = 2 };

        var act = () => sculptor.Compose<S8T08DashboardViewModel>(u1, u2);

        act.Should().Throw<SmartMapp.Net.MappingConfigurationException>()
            .WithMessage("*No composition blueprint registered*S8T08DashboardViewModel*");
    }

    [Fact]
    public void Compose_SingleOrigin_IdenticalToMap_WhenNoCompositionRegistered()
    {
        // Spec §S8-T08 Acceptance bullet 6: "Single-origin Compose<T>(x) identical to Map<X,T>(x)."
        var sculptor = new SculptorBuilder()
            .Configure(options => options.Bind<S8T08User, S8T08UserDto>(_ => { }))
            .Forge();

        var user = new S8T08User { UserId = 42, DisplayName = "Sam", Email = "s@x.com" };

        var viaMap = sculptor.Map<S8T08User, S8T08UserDto>(user);
        var viaCompose = sculptor.Compose<S8T08UserDto>(user);

        viaCompose.Should().BeEquivalentTo(viaMap);
    }

    [Fact]
    public void Compose_WithNullOrigins_ParamsArray_Throws()
    {
        var sculptor = BuildDashboardSculptor();

        var actNull = () => sculptor.Compose<S8T08DashboardViewModel>(null!);
        actNull.Should().Throw<ArgumentNullException>();

        var actEmpty = () => sculptor.Compose<S8T08DashboardViewModel>(Array.Empty<object>());
        actEmpty.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Compose_OptionsApi_ProducesSameResultAsBuilderApi()
    {
        // Spec §S8-T08 Acceptance bullet 7: "Works via both SculptorBuilder.Compose<T>() and
        // options.Compose<T>()."
        var builderSculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>()
                    .FromOrigin<S8T08OrderSummary>());
            })
            .Forge();

        var inlineBuilder = new SculptorBuilder();
        inlineBuilder.Compose<S8T08DashboardViewModel>()
            .FromOrigin<S8T08User>()
            .FromOrigin<S8T08OrderSummary>();
        var builderApiSculptor = inlineBuilder.Forge();

        var user = new S8T08User { UserId = 5, DisplayName = "Eve", Email = "e@x.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 9, LifetimeValue = 999m };

        var dtoA = builderSculptor.Compose<S8T08DashboardViewModel>(user, summary);
        var dtoB = builderApiSculptor.Compose<S8T08DashboardViewModel>(user, summary);

        dtoB.Should().BeEquivalentTo(dtoA);
    }

    [Fact]
    public void Compose_LastDeclaredOriginWinsOnCollision()
    {
        // Both S8T08OverrideUser and S8T08OverrideCompany define DisplayName. Declaration order
        // is User first, Company last → Company's DisplayName wins.
        var sculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08OverrideDto>(c => c
                    .FromOrigin<S8T08OverrideUser>()
                    .FromOrigin<S8T08OverrideCompany>());
            })
            .Forge();

        var user = new S8T08OverrideUser { UserId = 1, DisplayName = "FromUser" };
        var company = new S8T08OverrideCompany { CompanyName = "Acme", DisplayName = "FromCompany" };

        var dto = sculptor.Compose<S8T08OverrideDto>(user, company);

        dto.DisplayName.Should().Be("FromCompany",
            "spec §S8-T08 Constraints: last-origin-wins on collisions (Company declared after User).");
        dto.UserId.Should().Be(1, "non-colliding User property still applied.");
        dto.CompanyName.Should().Be("Acme");
    }

    [Fact]
    public void Compose_MissingOriginSlot_StaysAtTargetDefault()
    {
        // Register 3 origins, call with only 2. The missing slot's contributions are skipped;
        // the target's default values survive.
        var sculptor = BuildDashboardSculptor();
        var user = new S8T08User { UserId = 11, DisplayName = "Fred", Email = "f@x.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 4, LifetimeValue = 40m };

        var dto = sculptor.Compose<S8T08DashboardViewModel>(user, summary);

        dto.UserId.Should().Be(11);
        dto.OpenOrders.Should().Be(4);
        dto.CompanyName.Should().BeEmpty("Company origin was not supplied; its slot contributes nothing.");
        dto.Plan.Should().BeEmpty();
    }

    [Fact]
    public void Compose_FourOrigins_ProducesDtoWithPropertiesFromAll()
    {
        // Spec §S8-T08 Unit-Tests: "2, 3, 4 origin dispatch" — rounds out coverage beyond the
        // 2- and 3-origin cases above using the S8T08RichDashboardViewModel fixture.
        var sculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08RichDashboardViewModel>(c => c
                    .FromOrigin<S8T08User>()
                    .FromOrigin<S8T08OrderSummary>()
                    .FromOrigin<S8T08Company>()
                    .FromOrigin<S8T08Preferences>());
            })
            .Forge();

        var user = new S8T08User { UserId = 9, DisplayName = "Nora", Email = "n@x.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 6, LifetimeValue = 250m };
        var company = new S8T08Company { CompanyName = "Northwind", Plan = "Pro" };
        var prefs = new S8T08Preferences { Locale = "en-GB", TimeZone = "Europe/London" };

        var dto = sculptor.Compose<S8T08RichDashboardViewModel>(user, summary, company, prefs);

        dto.UserId.Should().Be(9);
        dto.DisplayName.Should().Be("Nora");
        dto.OpenOrders.Should().Be(6);
        dto.CompanyName.Should().Be("Northwind");
        dto.Locale.Should().Be("en-GB");
        dto.TimeZone.Should().Be("Europe/London");
    }

    [Fact]
    public void Compose_FromOrigin_ExplicitPropertyCallback_RoutesThroughPartialBlueprint()
    {
        // Spec §S8-T08 Acceptance bullet 1: FromOrigin<User>(r => r.Property(...).From(...))
        // must register an explicit per-origin binding on the composition's partial blueprint.
        // Convention alone maps User.DisplayName → Dashboard.DisplayName straight through; the
        // explicit .Property(...).From(u => u.DisplayName.ToUpperInvariant()) override proves
        // the configure callback is actually wired into the partial blueprint (not silently
        // ignored as it was for the first Sprint 8 pass).
        var sculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>(r => r.Property(t => t.DisplayName,
                        p => p.From(u => u.DisplayName.ToUpperInvariant())))
                    .FromOrigin<S8T08OrderSummary>());
            })
            .Forge();

        var user = new S8T08User { UserId = 1, DisplayName = "alice", Email = "a@x.com" };
        var summary = new S8T08OrderSummary { OpenOrders = 2, LifetimeValue = 20m };

        var dto = sculptor.Compose<S8T08DashboardViewModel>(user, summary);

        dto.DisplayName.Should().Be("ALICE",
            "the explicit FromOrigin<User>(r => r.Property(...).From(u => u.DisplayName.ToUpperInvariant())) override must win over convention.");
        // Non-overridden convention-derived bindings still apply.
        dto.UserId.Should().Be(1);
        dto.Email.Should().Be("a@x.com");
        dto.OpenOrders.Should().Be(2);
        dto.LifetimeValue.Should().Be(20m);
    }

    [Fact]
    public void Compose_FromOrigin_TypeLevelWhen_ThrowsAtForge()
    {
        // Spec §S8-T08 Technical Considerations bullet 3: per-origin .When / .OnlyIf /
        // .TransformWith are reserved for Sprint 15 and must throw NotSupportedException with
        // an actionable sprint-deferred message at Forge time.
        var act = () => new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>(r => r.When(u => u.UserId > 0)));
            })
            .Forge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*FromOrigin*S8T08User*.When*Sprint 15*");
    }

    [Fact]
    public void Compose_FromOrigin_PropertyOnlyIf_ThrowsAtForge()
    {
        var act = () => new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>(r => r.Property(t => t.DisplayName,
                        p => p.From(u => u.DisplayName).OnlyIf(u => u.UserId > 0))));
            })
            .Forge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*FromOrigin*S8T08User*.OnlyIf*Sprint 15*");
    }

    [Fact]
    public void Compose_FromOrigin_PropertyTransformWith_ThrowsAtForge()
    {
        var act = () => new SculptorBuilder()
            .Configure(options =>
            {
                options.Compose<S8T08DashboardViewModel>(c => c
                    .FromOrigin<S8T08User>(r => r.Property(t => t.DisplayName,
                        p => p.From(u => u.DisplayName).TransformWith(s => s.ToUpperInvariant()))));
            })
            .Forge();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*FromOrigin*S8T08User*.TransformWith*Sprint 15*");
    }
}
