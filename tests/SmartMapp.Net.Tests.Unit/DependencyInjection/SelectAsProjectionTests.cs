using System.Linq.Expressions;
using FluentAssertions;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection.Extensions;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T06 — unit tests for <see cref="SculptorProjectionBuilder"/>, the
/// strongly-typed <see cref="ISculptor.GetProjection{TOrigin, TTarget}"/>, and the
/// <see cref="SculptorQueryableExtensions.SelectAs{TOrigin, TTarget}"/> in-memory queryable
/// path. End-to-end EF-translation verification lives in the EF Core InMemory integration
/// tests.
/// </summary>
public class SelectAsProjectionTests
{
    private static ISculptor BuildSculptor<TBlueprint>() where TBlueprint : MappingBlueprint, new()
        => new SculptorBuilder().UseBlueprint<TBlueprint>().Forge();

    [Fact]
    public void GetProjection_FlatPair_ReturnsMemberInitLambda()
    {
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();

        var expr = sculptor.GetProjection<S8T06Order, S8T06OrderFlatDto>();

        expr.Should().BeAssignableTo<Expression<Func<S8T06Order, S8T06OrderFlatDto>>>();
        expr.Body.Should().BeAssignableTo<MemberInitExpression>(
            "spec §S8-T06 Tech-Cons. bullet 1: projection body must be an EF-friendly MemberInit node.");

        var init = (MemberInitExpression)expr.Body;
        init.NewExpression.Type.Should().Be(typeof(S8T06OrderFlatDto));
        init.Bindings.Select(b => b.Member.Name).Should().Contain(new[] { nameof(S8T06OrderFlatDto.Id), nameof(S8T06OrderFlatDto.Total) });
    }

    [Fact]
    public void GetProjection_FlatPair_RunsInMemory_ProducesExpectedDto()
    {
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();
        var source = new[]
        {
            new S8T06Order { Id = 1, Total = 10m },
            new S8T06Order { Id = 2, Total = 20m },
        }.AsQueryable();

        var projected = source.SelectAs<S8T06Order, S8T06OrderFlatDto>(sculptor).ToList();

        projected.Should().HaveCount(2);
        projected[0].Should().Be(new S8T06OrderFlatDto { Id = 1, Total = 10m });
        projected[1].Should().Be(new S8T06OrderFlatDto { Id = 2, Total = 20m });
    }

    [Fact]
    public void GetProjection_FlattenedPair_EmitsNullSafeChain()
    {
        var sculptor = BuildSculptor<S8T06FlattenedBlueprint>();

        var expr = sculptor.GetProjection<S8T06Order, S8T06OrderFlattenedDto>();

        // The CustomerAddressCity binding must contain a ConditionalExpression (null-safe ternary)
        // — it crosses a nullable Customer.Address intermediate.
        var init = (MemberInitExpression)expr.Body;
        var cityBinding = init.Bindings.OfType<MemberAssignment>()
            .Single(b => b.Member.Name == nameof(S8T06OrderFlattenedDto.CustomerAddressCity));
        cityBinding.Expression.Should().BeAssignableTo<ConditionalExpression>(
            "chained member access over a nullable intermediate must emit `?:` null-safe ternary (spec §S8-T06 Tech-Cons. bullet 2).");
    }

    [Fact]
    public void GetProjection_FlattenedPair_RunsInMemory_AppliesNullSafety()
    {
        var sculptor = BuildSculptor<S8T06FlattenedBlueprint>();
        var source = new[]
        {
            new S8T06Order
            {
                Id = 1, Total = 10m,
                Customer = new S8T06Customer
                {
                    Name = "Alice",
                    Address = new S8T06Address { City = "Paris", Country = "FR" },
                },
            },
            new S8T06Order
            {
                Id = 2, Total = 20m,
                Customer = new S8T06Customer { Name = "Bob", Address = null },
            },
        }.AsQueryable();

        var projected = source.SelectAs<S8T06Order, S8T06OrderFlattenedDto>(sculptor).ToList();

        projected[0].CustomerName.Should().Be("Alice");
        projected[0].CustomerAddressCity.Should().Be("Paris");
        projected[0].CustomerAddressCountry.Should().Be("FR");
        projected[1].CustomerName.Should().Be("Bob");
        projected[1].CustomerAddressCity.Should().BeNull(
            "null-safe chain returns default when an intermediate (Address) is null.");
        projected[1].CustomerAddressCountry.Should().BeNull();
    }

    [Fact]
    public void GetProjection_UsesMemberInit_NoRuntimeDelegate_NoMappingScopeReference()
    {
        // The projection body must contain no Expression.Invoke (delegate calls) and no
        // Constant of type MappingScope — both would break EF-Core LINQ-to-SQL translation
        // (spec §S8-T06 Constraints bullet 2: "No method calls into SmartMapp.Net runtime types").
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();
        var expr = sculptor.GetProjection<S8T06Order, S8T06OrderFlatDto>();

        var inspector = new ForbiddenNodeInspector();
        inspector.Visit(expr);

        inspector.HasInvoke.Should().BeFalse("Expression.Invoke is not translatable.");
        inspector.HasMappingScopeConstant.Should().BeFalse("MappingScope constants would leak runtime state into SQL.");
    }

    [Fact]
    public void GetProjection_IsCached_SameInstanceOnRepeatedCalls()
    {
        // Spec §S8-T06 Acceptance bullet 6: "Projection cached: repeated call returns same
        // Expression<> instance." Backing store is ForgedSculptorConfiguration.ProjectionCache.
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();

        var a = sculptor.GetProjection<S8T06Order, S8T06OrderFlatDto>();
        var b = sculptor.GetProjection<S8T06Order, S8T06OrderFlatDto>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void SelectAs_WithGenericSource_DelegatesToSculptorGetProjection()
    {
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();
        var source = new[] { new S8T06Order { Id = 7, Total = 3m } }.AsQueryable();

        var result = source.SelectAs<S8T06Order, S8T06OrderFlatDto>(sculptor).ToList();

        result.Should().ContainSingle()
            .Which.Should().Be(new S8T06OrderFlatDto { Id = 7, Total = 3m });
    }

    [Fact]
    public void SelectAs_TargetOnly_InfersOriginFromElementType()
    {
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();
        IQueryable source = new[] { new S8T06Order { Id = 9, Total = 5m } }.AsQueryable();

        var result = source.SelectAs<S8T06OrderFlatDto>(sculptor).ToList();

        result.Should().ContainSingle().Which.Id.Should().Be(9);
    }

    [Fact]
    public void SelectAs_RuntimeTyped_ReturnsIQueryableOfTargetType()
    {
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();
        IQueryable source = new[] { new S8T06Order { Id = 3, Total = 8m } }.AsQueryable();

        IQueryable result = source.SelectAs(sculptor, typeof(S8T06OrderFlatDto));

        result.ElementType.Should().Be(typeof(S8T06OrderFlatDto));
        var materialised = result.Cast<S8T06OrderFlatDto>().ToList();
        materialised.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void GetProjection_NestedPair_RecursivelyBuildsMemberInit()
    {
        // Spec §S8-T06 Unit-Tests bullet 1: "SelectAsProjectionTests — flat, nested, flattened,
        // nullable, collection projection". Nested case: Order.Customer → OrderNestedDto.Customer
        // where Customer has its own registered (Customer, CustomerDto) pair. Builder must
        // recurse into a nested MemberInit so the tree stays EF-translatable (no Expression.Invoke).
        var sculptor = BuildSculptor<S8T06NestedBlueprint>();

        var expr = sculptor.GetProjection<S8T06Order, S8T06OrderNestedDto>();

        var init = (MemberInitExpression)expr.Body;
        var customerBinding = init.Bindings.OfType<MemberAssignment>()
            .Single(b => b.Member.Name == nameof(S8T06OrderNestedDto.Customer));

        // The Customer binding is wrapped in a null-safe ConditionalExpression whose False branch
        // is a nested MemberInit that produces the CustomerDto.
        customerBinding.Expression.Should().BeAssignableTo<ConditionalExpression>();
        var conditional = (ConditionalExpression)customerBinding.Expression;
        conditional.IfFalse.Should().BeAssignableTo<MemberInitExpression>(
            "nested DTOs must be materialised via a recursive MemberInit, not Expression.Convert.");
        ((MemberInitExpression)conditional.IfFalse).NewExpression.Type.Should().Be(typeof(S8T06CustomerDto));
    }

    [Fact]
    public void GetProjection_NestedPair_RunsInMemory_ProducesNestedDto()
    {
        var sculptor = BuildSculptor<S8T06NestedBlueprint>();
        var source = new[]
        {
            new S8T06Order { Id = 1, Total = 10m, Customer = new S8T06Customer { Id = 5, Name = "Alice" } },
        }.AsQueryable();

        var dtos = source.SelectAs<S8T06Order, S8T06OrderNestedDto>(sculptor).ToList();

        dtos.Should().ContainSingle()
            .Which.Customer.Should().BeEquivalentTo(new S8T06CustomerDto { Id = 5, Name = "Alice" });
    }

    [Fact]
    public void GetProjection_CollectionMember_EmitsSelectAndToList()
    {
        // Spec §S8-T06 Unit-Tests bullet 1 (collection projection): List<LineItem> -> List<LineItemDto>.
        // Builder must emit Enumerable.Select + ToList so the generated tree is EF-translatable.
        var sculptor = BuildSculptor<S8T06CollectionBlueprint>();

        var expr = sculptor.GetProjection<S8T06OrderWithItems, S8T06OrderWithItemsDto>();
        var init = (MemberInitExpression)expr.Body;

        var itemsBinding = init.Bindings.OfType<MemberAssignment>()
            .Single(b => b.Member.Name == nameof(S8T06OrderWithItemsDto.Items));

        // Top-level call is ToList; its argument is Enumerable.Select over a MemberInit lambda.
        itemsBinding.Expression.Should().BeAssignableTo<MethodCallExpression>();
        var toListCall = (MethodCallExpression)itemsBinding.Expression;
        toListCall.Method.Name.Should().Be(nameof(Enumerable.ToList));

        var selectCall = toListCall.Arguments[0].Should().BeAssignableTo<MethodCallExpression>().Which;
        selectCall.Method.Name.Should().Be(nameof(Enumerable.Select));

        // The Select lambda body must be a MemberInit (element-level projection) — confirms the
        // element pair was recursively built.
        var selectorLambda = (LambdaExpression)selectCall.Arguments[1];
        selectorLambda.Body.Should().BeAssignableTo<MemberInitExpression>();
    }

    [Fact]
    public void GetProjection_CollectionMember_RunsInMemory_ProducesMappedElements()
    {
        var sculptor = BuildSculptor<S8T06CollectionBlueprint>();
        var source = new[]
        {
            new S8T06OrderWithItems
            {
                Id = 9,
                Items =
                {
                    new S8T06LineItem { Id = 1, Sku = "A", Price = 1m },
                    new S8T06LineItem { Id = 2, Sku = "B", Price = 2m },
                },
            },
        }.AsQueryable();

        var dtos = source.SelectAs<S8T06OrderWithItems, S8T06OrderWithItemsDto>(sculptor).ToList();

        dtos.Should().ContainSingle().Which.Items.Should().BeEquivalentTo(new[]
        {
            new S8T06LineItemDto { Id = 1, Sku = "A", Price = 1m },
            new S8T06LineItemDto { Id = 2, Sku = "B", Price = 2m },
        });
    }

    [Fact]
    public void GetProjection_UnsupportedProvider_EmitsDiagnostic_MemberStaysAtDefault()
    {
        // Spec §S8-T06 Acceptance bullet 5: "Unsupported transformer emits ProjectionWarning
        // through Sprint 17 logging but does not throw." We mint a blueprint with a property
        // whose provider type is something the builder cannot translate (ExpressionValueProvider
        // derived from p.From(x => x.Total * 1.1m)). Projection must:
        //   1) not throw at build time
        //   2) record a ProjectionDiagnostic on ForgedSculptorConfiguration
        //   3) emit the surrounding MemberInit without a binding for the offending member
        var sculptor = new SculptorBuilder()
            .UseBlueprint<S8T06UnsupportedProviderBlueprint>()
            .Forge();

        var expr = sculptor.GetProjection<S8T06Order, S8T06OrderFlatDto>();
        var init = (MemberInitExpression)expr.Body;

        init.Bindings.Should().NotContain(b => b.Member.Name == nameof(S8T06OrderFlatDto.Total),
            "unsupported provider member must be dropped from MemberInit — target stays at default.");

        // Reach into the internal config via reflection to assert the diagnostic was recorded.
        var config = ((SmartMapp.Net.Sculptor)sculptor).ForgedConfiguration;
        config.ProjectionDiagnostics.Should().Contain(d =>
            d.TargetMemberName == nameof(S8T06OrderFlatDto.Total)
            && d.Pair.OriginType == typeof(S8T06Order)
            && d.Pair.TargetType == typeof(S8T06OrderFlatDto));
    }

    [Fact]
    public void SelectAs_NullArgs_Throw()
    {
        var sculptor = BuildSculptor<S8T06FlatBlueprint>();
        IQueryable<S8T06Order> typed = Array.Empty<S8T06Order>().AsQueryable();
        IQueryable untyped = typed;

        ((Action)(() => typed.SelectAs<S8T06Order, S8T06OrderFlatDto>(null!)))
            .Should().Throw<ArgumentNullException>().WithParameterName("sculptor");
        ((Action)(() => ((IQueryable<S8T06Order>)null!).SelectAs<S8T06Order, S8T06OrderFlatDto>(sculptor)))
            .Should().Throw<ArgumentNullException>().WithParameterName("source");
        ((Action)(() => untyped.SelectAs<S8T06OrderFlatDto>(null!)))
            .Should().Throw<ArgumentNullException>().WithParameterName("sculptor");
        ((Action)(() => untyped.SelectAs(sculptor, null!)))
            .Should().Throw<ArgumentNullException>().WithParameterName("targetType");
    }

    private sealed class ForbiddenNodeInspector : ExpressionVisitor
    {
        internal bool HasInvoke { get; private set; }
        internal bool HasMappingScopeConstant { get; private set; }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            HasInvoke = true;
            return base.VisitInvocation(node);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type == typeof(MappingScope) || node.Value is MappingScope)
                HasMappingScopeConstant = true;
            return base.VisitConstant(node);
        }
    }
}
