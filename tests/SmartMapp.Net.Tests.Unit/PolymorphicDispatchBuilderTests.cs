using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class PolymorphicDispatchBuilderTests
{
    [Fact]
    public void BuildDispatchDelegate_NoDerivedPairs_ReturnsNull()
    {
        var resolver = new InheritanceResolver();
        var cache = new MappingDelegateCache();
        var builder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto();

        var result = builder.BuildDispatchDelegate(basePair, baseDelegate, _ => baseDelegate);

        result.Should().BeNull("no derived pairs exist");
    }

    [Fact]
    public void BuildDispatchDelegate_WithDerivedPairs_ReturnsDispatchDelegate()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });
        var cache = new MappingDelegateCache();
        var builder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        Func<object, MappingScope, object> circleDelegate = (o, s) => new CircleDto { Id = ((Circle)o).Id, Radius = ((Circle)o).Radius };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDelegate, pair =>
        {
            if (pair == circlePair) return circleDelegate;
            return baseDelegate;
        });

        dispatch.Should().NotBeNull();
    }

    [Fact]
    public void DispatchDelegate_DispatchesToDerivedType()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });
        var cache = new MappingDelegateCache();
        var builder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        Func<object, MappingScope, object> circleDelegate = (o, s) => new CircleDto { Id = ((Circle)o).Id, Radius = ((Circle)o).Radius };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDelegate, pair =>
        {
            if (pair == circlePair) return circleDelegate;
            return baseDelegate;
        })!;

        // Map a Circle — should dispatch to CircleDto
        var circle = new Circle { Id = 1, Radius = 5.0 };
        var result = dispatch(circle, new MappingScope());

        result.Should().BeOfType<CircleDto>();
        var circleDto = (CircleDto)result;
        circleDto.Id.Should().Be(1);
        circleDto.Radius.Should().Be(5.0);
    }

    [Fact]
    public void DispatchDelegate_FallsBackToBaseForBaseType()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });
        var cache = new MappingDelegateCache();
        var builder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        Func<object, MappingScope, object> circleDelegate = (o, s) => new CircleDto();

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDelegate, pair =>
        {
            if (pair == circlePair) return circleDelegate;
            return baseDelegate;
        })!;

        // Map a base Shape — should use base delegate
        var shape = new Shape { Id = 42 };
        var result = dispatch(shape, new MappingScope());

        result.Should().BeOfType<ShapeDto>();
        ((ShapeDto)result).Id.Should().Be(42);
    }

    [Fact]
    public void DispatchDelegate_NullOrigin_ReturnsNull()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle),
            typeof(ShapeDto), typeof(CircleDto),
        });
        var cache = new MappingDelegateCache();
        var builder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new ShapeDto();

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDelegate, _ => baseDelegate)!;

        var result = dispatch(null!, new MappingScope());
        result.Should().BeNull();
    }

    [Fact]
    public void DiscriminatorDispatch_MatchesCorrectWhenClause()
    {
        var resolver = new InheritanceResolver();
        var cache = new MappingDelegateCache();
        var builder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Notification, NotificationDto>();
        var emailPair = TypePair.Of<Notification, EmailNotificationDto>();
        var smsPair = TypePair.Of<Notification, SmsNotificationDto>();

        // Configure discriminator
        var param = System.Linq.Expressions.Expression.Parameter(typeof(Notification), "n");
        var discExpr = System.Linq.Expressions.Expression.Lambda<Func<Notification, string>>(
            System.Linq.Expressions.Expression.Property(param, nameof(Notification.Type)), param);

        var discConfig = new DiscriminatorConfig(discExpr);
        discConfig.AddWhen("email", emailPair);
        discConfig.AddWhen("sms", smsPair);
        discConfig.OtherwisePair = basePair;

        resolver.RegisterDiscriminator(basePair, discConfig);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDelegate = (o, s) => new NotificationDto { Title = ((Notification)o).Title };
        Func<object, MappingScope, object> emailDelegate = (o, s) => new EmailNotificationDto { Title = ((Notification)o).Title };
        Func<object, MappingScope, object> smsDelegate = (o, s) => new SmsNotificationDto { Title = ((Notification)o).Title };

        var dispatch = builder.BuildDispatchDelegate(basePair, baseDelegate, pair =>
        {
            if (pair == emailPair) return emailDelegate;
            if (pair == smsPair) return smsDelegate;
            return baseDelegate;
        })!;

        // Test email dispatch
        var emailNotification = new Notification { Type = "email", Title = "Hello" };
        var emailResult = dispatch(emailNotification, new MappingScope());
        emailResult.Should().BeOfType<EmailNotificationDto>();

        // Test sms dispatch
        var smsNotification = new Notification { Type = "sms", Title = "Hi" };
        var smsResult = dispatch(smsNotification, new MappingScope());
        smsResult.Should().BeOfType<SmsNotificationDto>();

        // Test fallback
        var otherNotification = new Notification { Type = "push", Title = "Push" };
        var otherResult = dispatch(otherNotification, new MappingScope());
        otherResult.Should().BeOfType<NotificationDto>();
    }
}
