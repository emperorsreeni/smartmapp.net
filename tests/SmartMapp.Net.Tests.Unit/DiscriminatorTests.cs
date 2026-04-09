using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class DiscriminatorTests
{
    [Fact]
    public void DiscriminateBy_SetsDiscriminatorConfig()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Notification, NotificationDto>()
            .DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email")
            .When<SmsNotificationDto>("sms")
            .Otherwise<NotificationDto>();

        builder.Bindings[0].Discriminator.Should().NotBeNull();
    }

    [Fact]
    public void DiscriminateBy_WhenClauses_AccumulateCorrectly()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Notification, NotificationDto>()
            .DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email")
            .When<SmsNotificationDto>("sms")
            .Otherwise<NotificationDto>();

        var disc = builder.Bindings[0].Discriminator!;
        disc.WhenClauses.Should().HaveCount(2);
        disc.WhenClauses[0].Value.Should().Be("email");
        disc.WhenClauses[1].Value.Should().Be("sms");
    }

    [Fact]
    public void DiscriminateBy_OtherwiseClause_SetCorrectly()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Notification, NotificationDto>()
            .DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email")
            .Otherwise<NotificationDto>();

        var disc = builder.Bindings[0].Discriminator!;
        disc.OtherwisePair.Should().NotBeNull();
        disc.OtherwisePair!.Value.TargetType.Should().Be(typeof(NotificationDto));
    }

    [Fact]
    public void DiscriminateBy_IntDiscriminator_Works()
    {
        var config = new BindingConfiguration(TypePair.Of<Shape, ShapeDto>());
        var rule = new BindingRule<Shape, ShapeDto>(config);

        rule.DiscriminateBy(s => s.Id)
            .When<CircleDto>(1)
            .When<RectangleDto>(2)
            .Otherwise<ShapeDto>();

        config.Discriminator.Should().NotBeNull();
        config.Discriminator!.WhenClauses.Should().HaveCount(2);
        config.Discriminator.WhenClauses[0].Value.Should().Be(1);
    }

    [Fact]
    public void DiscriminateBy_MissingOtherwise_FailsValidation()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Notification, NotificationDto>()
            .DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email");
        // No .Otherwise() call

        var act = () => builder.Build(validate: true);
        act.Should().Throw<Diagnostics.BlueprintValidationException>()
            .Which.Message.Should().Contain("Otherwise()");
    }

    [Fact]
    public void DiscriminateBy_WhenClause_StoresCorrectTargetPair()
    {
        var config = new BindingConfiguration(TypePair.Of<Notification, NotificationDto>());
        var rule = new BindingRule<Notification, NotificationDto>(config);

        rule.DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email")
            .Otherwise<NotificationDto>();

        var clause = config.Discriminator!.WhenClauses[0];
        clause.TargetPair.OriginType.Should().Be(typeof(Notification));
        clause.TargetPair.TargetType.Should().Be(typeof(EmailNotificationDto));
    }
}
