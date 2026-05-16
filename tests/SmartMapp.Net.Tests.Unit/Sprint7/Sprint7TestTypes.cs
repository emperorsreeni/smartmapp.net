using SmartMapp.Net;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Attributes;

namespace SmartMapp.Net.Tests.Unit.Sprint7;

public class Sprint7Order
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Sprint7Customer Customer { get; set; } = new();
    public decimal Total { get; set; }
    public string InternalCode { get; set; } = string.Empty;
}

public class Sprint7Customer
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public record Sprint7OrderDto
{
    public int Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Total { get; init; }
}

[MappedBy(typeof(Sprint7Order))]
public record Sprint7AttributedOrderDto
{
    public int Id { get; init; }

    [LinkedFrom("Customer.FirstName")]
    public string FirstName { get; init; } = string.Empty;

    [Unmapped]
    public string? InternalCode { get; init; }
}

public class Sprint7SimpleBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<Sprint7Order, Sprint7OrderDto>();
    }
}
