using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit;

public class BlueprintBuilderTests
{
    [Fact]
    public void Bind_RegistersTypePair()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Order, OrderDto>();

        builder.Bindings.Should().HaveCount(1);
        builder.Bindings[0].TypePair.Should().Be(TypePair.Of<Order, OrderDto>());
    }

    [Fact]
    public void Bind_DuplicatePair_Throws()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Order, OrderDto>();

        var act = () => builder.Bind<Order, OrderDto>();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate binding*");
    }

    [Fact]
    public void Bind_MultiplePairs_AllRegistered()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Order, OrderDto>();
        builder.Bind<Customer, SimpleDto>();

        builder.Bindings.Should().HaveCount(2);
    }

    [Fact]
    public void Build_CreatesBlueprints()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>();

        var blueprints = builder.Build();
        blueprints.Should().HaveCount(1);
        blueprints[0].OriginType.Should().Be(typeof(SimpleClass));
        blueprints[0].TargetType.Should().Be(typeof(SimpleDto));
    }

    [Fact]
    public void Build_WithPropertyConfig_CreatesLinks()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name));

        var blueprints = builder.Build();
        blueprints[0].Links.Should().HaveCount(1);
    }

    [Fact]
    public void Build_WithSkippedProperty_MarksLinkAsSkipped()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.Skip());

        var blueprints = builder.Build();
        blueprints[0].Links.Should().HaveCount(1);
        blueprints[0].Links[0].IsSkipped.Should().BeTrue();
    }

    [Fact]
    public void Build_WithFallback_SetsFallbackOnLink()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name).FallbackTo("default"));

        var blueprints = builder.Build();
        blueprints[0].Links[0].Fallback.Should().Be("default");
    }

    [Fact]
    public void Build_WithMaxDepth_SetsOnBlueprint()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Order, OrderDto>()
            .DepthLimit(5);

        var blueprints = builder.Build();
        blueprints[0].MaxDepth.Should().Be(5);
    }

    [Fact]
    public void Build_WithTrackReferences_SetsOnBlueprint()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Order, OrderDto>()
            .TrackReferences();

        var blueprints = builder.Build();
        blueprints[0].TrackReferences.Should().BeTrue();
    }

    [Fact]
    public void Build_WithStrictMode_SetsOnBlueprint()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<Order, OrderDto>()
            .StrictMode();

        var blueprints = builder.Build();
        blueprints[0].StrictRequiredMembers.Should().BeTrue();
    }

    [Fact]
    public void Build_WithOnMapping_SetsHook()
    {
        var called = false;
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .OnMapping((s, d) => called = true);

        var blueprints = builder.Build();
        blueprints[0].OnMapping.Should().NotBeNull();
        blueprints[0].OnMapping!(new SimpleClass(), new SimpleDto());
        called.Should().BeTrue();
    }

    [Fact]
    public void Build_WithOnMapped_SetsHook()
    {
        var called = false;
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .OnMapped((s, d) => called = true);

        var blueprints = builder.Build();
        blueprints[0].OnMapped.Should().NotBeNull();
        blueprints[0].OnMapped!(new SimpleClass(), new SimpleDto());
        called.Should().BeTrue();
    }

    [Fact]
    public void Build_WithPropertyOrder_SortsLinks()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name).SetOrder(2))
            .Property(d => d.Id, p => p.From(s => s.Id).SetOrder(1));

        var blueprints = builder.Build();
        blueprints[0].Links[0].Order.Should().Be(1);
        blueprints[0].Links[1].Order.Should().Be(2);
    }

    [Fact]
    public void Build_WithCondition_SetsConditionOnLink()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name).When(s => s.Id > 0));

        var blueprints = builder.Build();
        blueprints[0].Links[0].Condition.Should().NotBeNull();
    }

    [Fact]
    public void Build_WithPreCondition_SetsPreConditionOnLink()
    {
        var builder = new BlueprintBuilder();
        builder.Bind<SimpleClass, SimpleDto>()
            .Property(d => d.Name, p => p.From(s => s.Name).OnlyIf(s => s.Id > 0));

        var blueprints = builder.Build();
        blueprints[0].Links[0].PreCondition.Should().NotBeNull();
    }

    [Fact]
    public void Compose_ReturnsNonNullStub()
    {
        // Sprint 7 T04 AC: Compose<T>() must return a non-null stub; full execution lands in Sprint 15.
        var builder = new BlueprintBuilder();
        var rule = builder.Compose<OrderDto>();
        rule.Should().NotBeNull();
    }

    [Fact]
    public void ExpressionValueProvider_Provides_CorrectValue()
    {
        var param = System.Linq.Expressions.Expression.Parameter(typeof(SimpleClass), "s");
        var expr = System.Linq.Expressions.Expression.Lambda<Func<SimpleClass, string>>(
            System.Linq.Expressions.Expression.Property(param, nameof(SimpleClass.Name)),
            param);

        var provider = new ExpressionValueProvider(expr);
        var source = new SimpleClass { Name = "test" };

        var result = provider.Provide(source, new SimpleDto(), "Name", new MappingScope());
        result.Should().Be("test");
    }

    [Fact]
    public void NullValueProvider_ReturnsNull()
    {
        var provider = new NullValueProvider();
        var result = provider.Provide(new object(), new object(), "Test", new MappingScope());
        result.Should().BeNull();
    }

    [Fact]
    public void DeferredValueProvider_ThrowsWithoutServiceProvider()
    {
        // Post-S8-T04: resolution routes through IProviderResolver. IValueProvider is an
        // interface → DefaultProviderResolver throws "abstract or an interface" with a hint
        // pointing at DI registration as the fix (spec §11.4).
        var provider = new DeferredValueProvider(typeof(IValueProvider));
        var scope = new MappingScope();

        var act = () => provider.Provide(new object(), new object(), "Test", scope);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*abstract or an interface*");
    }
}
