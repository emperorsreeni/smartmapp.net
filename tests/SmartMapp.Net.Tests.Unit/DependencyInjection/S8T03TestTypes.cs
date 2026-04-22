using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

// S8-T03 fixtures — kept local so the mapping pairs asserted in MapperResolutionTests do not
// drift with unrelated Sprint 7 / Sprint 6 changes.

public class S8T03Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public S8T03Customer Customer { get; set; } = new();
}

public class S8T03Customer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public record S8T03OrderDto
{
    public int Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Total { get; init; }
}

public record S8T03OrderDtoFlat
{
    public int Id { get; init; }
    public string CustomerFirstName { get; init; } = string.Empty;
    public string CustomerLastName { get; init; } = string.Empty;
}

[MappedBy(typeof(S8T03Order))]
public record S8T03AttributedOrderDto
{
    public int Id { get; init; }

    [LinkedFrom("Customer.FirstName")]
    public string FirstName { get; init; } = string.Empty;
}

// Intentionally internal: keeps scanner-discovered blueprint registration scoped to the
// tests that explicitly opt-in via UseBlueprint<T>(). The AssemblyScanner's IsConsiderable
// filter excludes non-visible types, so internal blueprints never pollute a default
// calling-assembly scan performed by unrelated tests in this same test assembly.
internal class S8T03OrderBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T03Order, S8T03OrderDto>();
        plan.Bind<S8T03Order, S8T03OrderDtoFlat>();
        plan.Bind<S8T03Customer, S8T03CustomerDto>();
    }
}

public record S8T03CustomerDto
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
}

public record S8T03CustomerDtoBidirectional
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
}

internal class S8T03BidirectionalBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T03Customer, S8T03CustomerDtoBidirectional>().Bidirectional();
    }
}

// Polymorphic hierarchy — demonstrates that IMapper<Shape, ShapeDto> resolves even when
// the runtime dispatch picks a derived pair.
public abstract class S8T03Shape
{
    public string Label { get; set; } = string.Empty;
}

public sealed class S8T03Circle : S8T03Shape
{
    public double Radius { get; set; }
}

public sealed class S8T03Rectangle : S8T03Shape
{
    public double Width { get; set; }
    public double Height { get; set; }
}

// ShapeDto is concrete (not abstract) so the polymorphic Shape -> ShapeDto binding
// compiles without requiring .Materialize<TConcrete>(). The base-only Label property is
// sufficient to demonstrate that an abstract-origin / concrete-target pair resolves.
public record S8T03ShapeDto(string Label);
public sealed record S8T03CircleDto(string Label, double Radius) : S8T03ShapeDto(Label);
public sealed record S8T03RectangleDto(string Label, double Width, double Height) : S8T03ShapeDto(Label);

internal class S8T03PolymorphicBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T03Shape, S8T03ShapeDto>();
        plan.Bind<S8T03Circle, S8T03CircleDto>();
        plan.Bind<S8T03Rectangle, S8T03RectangleDto>();
    }
}
