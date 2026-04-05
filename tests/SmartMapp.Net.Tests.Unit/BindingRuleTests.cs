using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class BindingRuleTests
{
    private readonly BindingConfiguration _config;
    private readonly BindingRule<SimpleClass, SimpleDto> _rule;

    public BindingRuleTests()
    {
        _config = new BindingConfiguration(TypePair.Of<SimpleClass, SimpleDto>());
        _rule = new BindingRule<SimpleClass, SimpleDto>(_config);
    }

    [Fact]
    public void Property_AddsPropertyConfig()
    {
        _rule.Property(d => d.Name, p => p.From(s => s.Name));

        _config.PropertyConfigs.Should().HaveCount(1);
        _config.PropertyConfigs[0].TargetMember.Name.Should().Be("Name");
    }

    [Fact]
    public void Property_MultipleProperties_AllAdded()
    {
        _rule
            .Property(d => d.Id, p => p.From(s => s.Id))
            .Property(d => d.Name, p => p.From(s => s.Name));

        _config.PropertyConfigs.Should().HaveCount(2);
    }

    [Fact]
    public void BuildWith_SetsFactoryExpression()
    {
        _rule.BuildWith(s => new SimpleDto { Id = s.Id });

        _config.FactoryExpression.Should().NotBeNull();
    }

    [Fact]
    public void When_SetsCondition()
    {
        _rule.When(s => s.Id > 0);

        _config.Condition.Should().NotBeNull();
    }

    [Fact]
    public void OnMapping_SetsHook()
    {
        _rule.OnMapping((s, d) => { });

        _config.OnMappingHook.Should().NotBeNull();
    }

    [Fact]
    public void OnMapped_SetsHook()
    {
        _rule.OnMapped((s, d) => { });

        _config.OnMappedHook.Should().NotBeNull();
    }

    [Fact]
    public void Bidirectional_SetsFlag()
    {
        _rule.Bidirectional();

        _config.IsBidirectional.Should().BeTrue();
    }

    [Fact]
    public void DepthLimit_SetsMaxDepth()
    {
        _rule.DepthLimit(5);

        _config.MaxDepth.Should().Be(5);
    }

    [Fact]
    public void TrackReferences_SetsFlag()
    {
        _rule.TrackReferences();

        _config.TrackReferences.Should().BeTrue();
    }

    [Fact]
    public void ExtendWith_AddsExplicitDerivedPair()
    {
        var config = new BindingConfiguration(TypePair.Of<Shape, ShapeDto>());
        var rule = new BindingRule<Shape, ShapeDto>(config);

        rule.ExtendWith<Circle, CircleDto>();

        config.ExplicitDerivedPairs.Should().HaveCount(1);
        config.ExplicitDerivedPairs[0].Should().Be(TypePair.Of<Circle, CircleDto>());
    }

    [Fact]
    public void ExtendWith_NoDuplicates()
    {
        var config = new BindingConfiguration(TypePair.Of<Shape, ShapeDto>());
        var rule = new BindingRule<Shape, ShapeDto>(config);

        rule.ExtendWith<Circle, CircleDto>();
        rule.ExtendWith<Circle, CircleDto>();

        config.ExplicitDerivedPairs.Should().HaveCount(1);
    }

    [Fact]
    public void InheritFrom_SetsBasePair()
    {
        var config = new BindingConfiguration(TypePair.Of<Car, CarDto>());
        var rule = new BindingRule<Car, CarDto>(config);

        rule.InheritFrom<Vehicle, VehicleDto>();

        config.InheritFromPair.Should().Be(TypePair.Of<Vehicle, VehicleDto>());
    }

    [Fact]
    public void FallbackTo_SetsFallback()
    {
        var fallback = new SimpleDto { Id = 999 };
        _rule.FallbackTo(fallback);

        _config.HasFallback.Should().BeTrue();
        _config.FallbackValue.Should().Be(fallback);
    }

    [Fact]
    public void StrictMode_SetsFlag()
    {
        _rule.StrictMode();

        _config.StrictMode.Should().BeTrue();
    }

    [Fact]
    public void DiscriminateBy_SetsDiscriminator()
    {
        var config = new BindingConfiguration(TypePair.Of<Notification, NotificationDto>());
        var rule = new BindingRule<Notification, NotificationDto>(config);

        rule.DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email")
            .When<SmsNotificationDto>("sms")
            .Otherwise<NotificationDto>();

        config.Discriminator.Should().NotBeNull();
        config.Discriminator!.WhenClauses.Should().HaveCount(2);
        config.Discriminator.OtherwisePair.Should().NotBeNull();
    }

    [Fact]
    public void Materialize_SetsMaterializeType()
    {
        var config = new BindingConfiguration(TypePair.Of<PersonSource, IPersonDto>());
        var rule = new BindingRule<PersonSource, IPersonDto>(config);

        rule.Materialize<PersonDtoImpl>();

        config.MaterializeType.Should().Be(typeof(PersonDtoImpl));
    }

    [Fact]
    public void FluentChaining_AllMethodsReturnSameRule()
    {
        var result = _rule
            .Property(d => d.Id, p => p.From(s => s.Id))
            .Property(d => d.Name, p => p.From(s => s.Name))
            .DepthLimit(10)
            .TrackReferences()
            .StrictMode()
            .Bidirectional();

        result.Should().BeSameAs(_rule);
    }
}
