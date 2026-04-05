using FluentAssertions;
using NSubstitute;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Caching;
using SmartMapp.Net.Collections;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Diagnostics;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

/// <summary>
/// Cross-feature integration tests for Sprint 6.
/// Tests feature intersections and end-to-end scenarios.
/// </summary>
public class Sprint6IntegrationTests
{
    // --- Cross-feature: Fluent API produces correct Blueprints end-to-end ---

    [Fact]
    public void FluentApi_FullChain_ProducesCorrectBlueprint()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Id, p => p.From(s => s.Id).SetOrder(1))
            .Property(d => d.Name, p => p.From(s => s.Name).FallbackTo("unknown").SetOrder(2))
            .DepthLimit(5)
            .TrackReferences()
            .OnMapping((s, d) => { })
            .OnMapped((s, d) => { });

        var blueprints = builder.Build(validate: false);

        blueprints.Should().HaveCount(1);
        var bp = blueprints[0];
        bp.OriginType.Should().Be(typeof(SimpleClass));
        bp.TargetType.Should().Be(typeof(SimpleDto));
        bp.Links.Should().HaveCount(2);
        bp.Links[0].Order.Should().Be(1);
        bp.Links[1].Order.Should().Be(2);
        bp.Links[1].Fallback.Should().Be("unknown");
        bp.MaxDepth.Should().Be(5);
        bp.TrackReferences.Should().BeTrue();
        bp.OnMapping.Should().NotBeNull();
        bp.OnMapped.Should().NotBeNull();
    }

    [Fact]
    public void FluentApi_BuildWith_SetsTargetFactory()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .BuildWith(s => new SimpleDto { Id = s.Id * 10 });

        var blueprints = builder.Build(validate: false);
        blueprints[0].TargetFactory.Should().NotBeNull();

        var source = new SimpleClass { Id = 5 };
        var result = blueprints[0].TargetFactory!(source);
        result.Should().BeOfType<SimpleDto>();
        ((SimpleDto)result).Id.Should().Be(50);
    }

    [Fact]
    public void FluentApi_TransformWith_SetsTransformerOnLink()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name).TransformWith(v => v.ToUpper()));

        var blueprints = builder.Build(validate: false);
        blueprints[0].Links[0].Transformer.Should().NotBeNull();
        blueprints[0].Links[0].Transformer.Should().BeOfType<InlineTypeTransformer>();
    }

    [Fact]
    public void FluentApi_PostProcess_SetsTransformerOnLink()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name).PostProcess(v => v.Trim()));

        var blueprints = builder.Build(validate: false);
        blueprints[0].Links[0].Transformer.Should().NotBeNull();
    }

    // --- Cross-feature: Bidirectional + Fluent API ---

    [Fact]
    public void Bidirectional_GeneratesInverseBlueprint_DuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<BidiProduct, BidiProductDto>()
            .Property(d => d.Id, p => p.From(s => s.Id))
            .Property(d => d.Name, p => p.From(s => s.Name))
            .Bidirectional();

        var blueprints = builder.Build(validate: false);

        // Should have forward + inverse
        blueprints.Should().HaveCount(2);
        blueprints.Should().Contain(b => b.OriginType == typeof(BidiProduct) && b.TargetType == typeof(BidiProductDto));
        blueprints.Should().Contain(b => b.OriginType == typeof(BidiProductDto) && b.TargetType == typeof(BidiProduct));
    }

    [Fact]
    public void Bidirectional_InverseLinks_AreCorrect()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<BidiProduct, BidiProductDto>()
            .Property(d => d.Id, p => p.From(s => s.Id))
            .Property(d => d.Name, p => p.From(s => s.Name))
            .Bidirectional();

        var blueprints = builder.Build(validate: false);
        var inverse = blueprints.First(b => b.OriginType == typeof(BidiProductDto));

        inverse.Links.Should().HaveCount(2);
        inverse.Links.Should().Contain(l => l.TargetMember.Name == "Id");
        inverse.Links.Should().Contain(l => l.TargetMember.Name == "Name");
    }

    [Fact]
    public void Bidirectional_SkippedProperty_NotInInverse()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<BidiProduct, BidiProductDto>()
            .Property(d => d.Id, p => p.From(s => s.Id))
            .Property(d => d.Name, p => p.Skip())
            .Bidirectional();

        var blueprints = builder.Build(validate: false);
        var inverse = blueprints.First(b => b.OriginType == typeof(BidiProductDto));

        // Skipped property should not appear in inverse
        inverse.Links.Should().HaveCount(1);
        inverse.Links[0].TargetMember.Name.Should().Be("Id");
    }

    // --- Cross-feature: Blueprint Inheritance + Fluent Override ---

    [Fact]
    public void InheritFrom_MergesBaseLinks_DuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make))
            .Property(d => d.Model, p => p.From(s => s.Model));

        builder.Bind<Car, CarDto>()
            .InheritFrom<Vehicle, VehicleDto>()
            .Property(d => d.Doors, p => p.From(s => s.Doors));

        var blueprints = builder.Build(validate: false);
        var carBp = blueprints.First(b => b.OriginType == typeof(Car));

        // Should have inherited Make + Model, plus own Doors
        carBp.Links.Should().HaveCount(3);
        carBp.Links.Should().Contain(l => l.TargetMember.Name == "Make");
        carBp.Links.Should().Contain(l => l.TargetMember.Name == "Model");
        carBp.Links.Should().Contain(l => l.TargetMember.Name == "Doors");
    }

    [Fact]
    public void InheritFrom_DerivedOverridesBase_DuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make))
            .Property(d => d.Model, p => p.From(s => s.Model));

        builder.Bind<Car, CarDto>()
            .InheritFrom<Vehicle, VehicleDto>()
            .Property(d => d.Make, p => p.From(s => s.Make).FallbackTo("UnknownMake"))
            .Property(d => d.Doors, p => p.From(s => s.Doors));

        var blueprints = builder.Build(validate: false);
        var carBp = blueprints.First(b => b.OriginType == typeof(Car));

        // Make should be overridden, Model inherited, Doors added
        carBp.Links.Should().HaveCount(3);
        var makeLink = carBp.Links.First(l => l.TargetMember.Name == "Make");
        makeLink.Fallback.Should().Be("UnknownMake");
    }

    // --- Cross-feature: Validation catches real issues ---

    [Fact]
    public void Validation_DuplicateBindings_ThrowsDuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>();

        var act = () => builder.Bind<SimpleClass, SimpleDto>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate binding*");
    }

    [Fact]
    public void Validation_MissingOtherwise_ThrowsDuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Notification, NotificationDto>()
            .DiscriminateBy(n => n.Type)
            .When<EmailNotificationDto>("email");

        var act = () => builder.Build(validate: true);
        act.Should().Throw<BlueprintValidationException>();
    }

    [Fact]
    public void Validation_InvalidMaterialize_ThrowsDuringBuild()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<PersonSource, IPersonDto>()
            .Materialize<PersonDtoImpl>();

        // This should be valid
        var act = () => builder.Build(validate: true);
        act.Should().NotThrow();
    }

    // --- Cross-feature: Polymorphic dispatch + inheritance ---

    [Fact]
    public void PolymorphicDispatch_WithExtendWith_DispatchesCorrectly()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto),
        });
        var cache = new MappingDelegateCache();
        var dispatchBuilder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        var circlePair = TypePair.Of<Circle, CircleDto>();
        var rectPair = TypePair.Of<Rectangle, RectangleDto>();

        resolver.RegisterExplicitDerivedPair(basePair, circlePair);
        resolver.RegisterExplicitDerivedPair(basePair, rectPair);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        Func<object, MappingScope, object> circleDel = (o, s) => new CircleDto { Id = ((Circle)o).Id, Radius = ((Circle)o).Radius };
        Func<object, MappingScope, object> rectDel = (o, s) => new RectangleDto { Id = ((Rectangle)o).Id, Width = ((Rectangle)o).Width };

        var dispatch = dispatchBuilder.BuildDispatchDelegate(basePair, baseDel, pair =>
        {
            if (pair == circlePair) return circleDel;
            if (pair == rectPair) return rectDel;
            return baseDel;
        })!;

        dispatch(new Circle { Id = 1, Radius = 3 }, new MappingScope()).Should().BeOfType<CircleDto>();
        dispatch(new Rectangle { Id = 2, Width = 5 }, new MappingScope()).Should().BeOfType<RectangleDto>();
        dispatch(new Shape { Id = 3 }, new MappingScope()).Should().BeOfType<ShapeDto>();
    }

    // --- Thread safety ---

    [Fact]
    public async Task ThreadSafety_ConcurrentGetOrCompile_NoCorruption()
    {
        var cache = new MappingDelegateCache();
        var pair = TypePair.Of<Shape, ShapeDto>();
        var callCount = 0;

        Func<TypePair, Func<object, MappingScope, object>> factory = p =>
        {
            Interlocked.Increment(ref callCount);
            Thread.Sleep(10); // Simulate work
            return (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        };

        var tasks = Enumerable.Range(0, 100).Select(_ =>
            Task.Run(() => cache.GetOrCompile(pair, factory))).ToArray();

        await Task.WhenAll(tasks);

        // Factory should have been called exactly once due to Lazy
        callCount.Should().Be(1);

        // All tasks should have gotten the same delegate
        var delegates = tasks.Select(t => t.Result).Distinct().ToList();
        delegates.Should().HaveCount(1);
    }

    [Fact]
    public void ThreadSafety_ConcurrentDispatch_NoCorruption()
    {
        var resolver = new InheritanceResolver(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle),
            typeof(ShapeDto), typeof(CircleDto), typeof(RectangleDto),
        });
        var cache = new MappingDelegateCache();
        var dispatchBuilder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Shape, ShapeDto>();
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Circle, CircleDto>());
        resolver.RegisterExplicitDerivedPair(basePair, TypePair.Of<Rectangle, RectangleDto>());
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new ShapeDto { Id = ((Shape)o).Id };
        Func<object, MappingScope, object> circleDel = (o, s) => new CircleDto { Id = ((Circle)o).Id };
        Func<object, MappingScope, object> rectDel = (o, s) => new RectangleDto { Id = ((Rectangle)o).Id };

        var dispatch = dispatchBuilder.BuildDispatchDelegate(basePair, baseDel, pair =>
        {
            if (pair.OriginType == typeof(Circle)) return circleDel;
            if (pair.OriginType == typeof(Rectangle)) return rectDel;
            return baseDel;
        })!;

        var shapes = new Shape[]
        {
            new Circle { Id = 1 }, new Rectangle { Id = 2 }, new Shape { Id = 3 },
            new Circle { Id = 4 }, new Rectangle { Id = 5 },
        };

        var results = new object[1000];
        Parallel.For(0, 1000, i =>
        {
            var shape = shapes[i % shapes.Length];
            results[i] = dispatch(shape, new MappingScope());
        });

        // Verify no corruption — all results should be of the correct type
        for (var i = 0; i < 1000; i++)
        {
            var shape = shapes[i % shapes.Length];
            if (shape is Circle) results[i].Should().BeOfType<CircleDto>();
            else if (shape is Rectangle) results[i].Should().BeOfType<RectangleDto>();
            else results[i].Should().BeOfType<ShapeDto>();
        }
    }

    // --- Stress test ---

    [Fact]
    public void Stress_PolymorphicCollectionWith100Elements_AllDispatchedCorrectly()
    {
        Func<object, MappingScope, object> elementMapper = (o, scope) =>
        {
            return o switch
            {
                Square sq => new SquareDto { Id = sq.Id, Side = sq.Side },
                Rectangle r => new RectangleDto { Id = r.Id, Width = r.Width, Height = r.Height },
                Circle c => new CircleDto { Id = c.Id, Radius = c.Radius },
                Shape s => new ShapeDto { Id = s.Id },
                _ => throw new InvalidOperationException(),
            };
        };

        var elements = new Shape[100];
        for (var i = 0; i < 100; i++)
        {
            elements[i] = (i % 4) switch
            {
                0 => new Circle { Id = i, Radius = i },
                1 => new Rectangle { Id = i, Width = i, Height = i * 2 },
                2 => new Square { Id = i, Side = i },
                _ => new Shape { Id = i },
            };
        }

        var result = PolymorphicCollectionMapper.MapPolymorphicCollection<ShapeDto>(
            elements, elementMapper, new MappingScope());

        result.Should().HaveCount(100);

        for (var i = 0; i < 100; i++)
        {
            switch (i % 4)
            {
                case 0: result[i].Should().BeOfType<CircleDto>(); break;
                case 1: result[i].Should().BeOfType<RectangleDto>(); break;
                case 2: result[i].Should().BeOfType<SquareDto>(); break;
                case 3: result[i].Should().BeOfType<ShapeDto>(); break;
            }
            result[i].Id.Should().Be(i);
        }
    }

    // --- Combined: Discriminator + Collection ---

    [Fact]
    public void DiscriminatorDispatch_CombinedWithCollection_MapsMixedElements()
    {
        var resolver = new InheritanceResolver();
        var cache = new MappingDelegateCache();
        var dispatchBuilder = new PolymorphicDispatchBuilder(resolver, cache);

        var basePair = TypePair.Of<Notification, NotificationDto>();
        var emailPair = TypePair.Of<Notification, EmailNotificationDto>();
        var smsPair = TypePair.Of<Notification, SmsNotificationDto>();

        var param = System.Linq.Expressions.Expression.Parameter(typeof(Notification), "n");
        var discExpr = System.Linq.Expressions.Expression.Lambda<Func<Notification, string>>(
            System.Linq.Expressions.Expression.Property(param, nameof(Notification.Type)), param);

        var discConfig = new DiscriminatorConfig(discExpr);
        discConfig.AddWhen("email", emailPair);
        discConfig.AddWhen("sms", smsPair);
        discConfig.OtherwisePair = basePair;
        resolver.RegisterDiscriminator(basePair, discConfig);
        resolver.BuildDerivedPairLookup(new[] { basePair });

        Func<object, MappingScope, object> baseDel = (o, s) => new NotificationDto { Title = ((Notification)o).Title };
        Func<object, MappingScope, object> emailDel = (o, s) => new EmailNotificationDto { Title = ((Notification)o).Title };
        Func<object, MappingScope, object> smsDel = (o, s) => new SmsNotificationDto { Title = ((Notification)o).Title };

        var dispatch = dispatchBuilder.BuildDispatchDelegate(basePair, baseDel, pair =>
        {
            if (pair == emailPair) return emailDel;
            if (pair == smsPair) return smsDel;
            return baseDel;
        })!;

        // Map a collection of mixed notifications using the dispatch delegate
        var notifications = new[]
        {
            new Notification { Type = "email", Title = "Email1" },
            new Notification { Type = "sms", Title = "Sms1" },
            new Notification { Type = "push", Title = "Push1" },
            new Notification { Type = "email", Title = "Email2" },
        };

        var results = PolymorphicCollectionMapper.MapPolymorphicCollection<NotificationDto>(
            notifications, dispatch, new MappingScope());

        results.Should().HaveCount(4);
        results[0].Should().BeOfType<EmailNotificationDto>();
        results[1].Should().BeOfType<SmsNotificationDto>();
        results[2].Should().BeOfType<NotificationDto>(); // fallback
        results[3].Should().BeOfType<EmailNotificationDto>();
    }

    // --- MappingBlueprint subclass end-to-end ---

    [Fact]
    public void MappingBlueprint_Subclass_ProducesValidBlueprints()
    {
        var blueprint = new OrderMappingBlueprint();
        var builder = new BlueprintBuilder();
        blueprint.Design(builder);

        var blueprints = builder.Build(validate: false);
        blueprints.Should().HaveCount(1);
        blueprints[0].Links.Should().HaveCount(2);
    }

    private class OrderMappingBlueprint : MappingBlueprint
    {
        public override void Design(IBlueprintBuilder plan)
        {
            plan.Bind<BidiProduct, BidiProductDto>()
                .Property(d => d.Id, p => p.From(s => s.Id))
                .Property(d => d.Name, p => p.From(s => s.Name))
                .DepthLimit(10);
        }
    }

    // --- TypeHierarchyMap ---

    [Fact]
    public void TypeHierarchyMap_3LevelHierarchy_DiscoversMostDerivedFirst()
    {
        var map = new TypeHierarchyMap(new[]
        {
            typeof(Shape), typeof(Circle), typeof(Rectangle), typeof(Square),
        });

        var derivedFromShape = map.GetDerivedTypes(typeof(Shape));
        derivedFromShape.Should().NotBeEmpty();
        // Square (most derived) should appear before Circle/Rectangle
        var squareIndex = derivedFromShape.ToList().FindIndex(t => t == typeof(Square));
        var circleIndex = derivedFromShape.ToList().FindIndex(t => t == typeof(Circle));
        if (squareIndex >= 0 && circleIndex >= 0)
        {
            squareIndex.Should().BeLessThan(circleIndex);
        }
    }

    [Fact]
    public void TypeHierarchyMap_InterfaceHierarchy_DiscoversDerived()
    {
        var map = new TypeHierarchyMap(new[]
        {
            typeof(PersonDtoImpl),
        });

        var derived = map.GetDerivedTypes(typeof(IPersonDto));
        derived.Should().Contain(typeof(PersonDtoImpl));
    }

    // --- Validation result formatting ---

    [Fact]
    public void BlueprintValidationResult_MultipleErrors_FormattedCorrectly()
    {
        var result = new BlueprintValidationResult();
        result.AddError(typeof(Shape), typeof(ShapeDto), "Error 1");
        result.AddError(typeof(Circle), typeof(CircleDto), "Error 2");
        result.AddWarning(typeof(Rectangle), typeof(RectangleDto), "Warning 1");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Warnings.Should().HaveCount(1);
        result.All.Should().HaveCount(3);
        result.ToString().Should().Contain("FAILED");
        result.ToString().Should().Contain("Error 1");
        result.ToString().Should().Contain("Warning 1");
    }
}
