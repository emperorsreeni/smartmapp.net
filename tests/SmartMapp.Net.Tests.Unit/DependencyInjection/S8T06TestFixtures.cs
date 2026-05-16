using SmartMapp.Net;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

// S8-T06 fixtures — shapes exercising flat / nested / flattened / nullable projection paths.
// Blueprints are internal so the assembly scanner skips them during default calling-assembly
// scans performed by other Sprint 8 tests.

public class S8T06Address
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Country { get; set; }
}

public class S8T06Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public S8T06Address? Address { get; set; }
}

public class S8T06Order
{
    public int Id { get; set; }
    public decimal Total { get; set; }
    public S8T06Customer Customer { get; set; } = new();
}

public record S8T06OrderFlatDto
{
    public int Id { get; init; }
    public decimal Total { get; init; }
}

public record S8T06OrderNestedDto
{
    public int Id { get; init; }
    public decimal Total { get; init; }
    public S8T06CustomerDto Customer { get; init; } = new();
}

public record S8T06CustomerDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public record S8T06OrderFlattenedDto
{
    public int Id { get; init; }
    public decimal Total { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public string? CustomerAddressCity { get; init; }
    public string? CustomerAddressCountry { get; init; }
}

internal class S8T06FlatBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T06Order, S8T06OrderFlatDto>();
    }
}

internal class S8T06FlattenedBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T06Order, S8T06OrderFlattenedDto>();
    }
}

internal class S8T06NestedBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T06Customer, S8T06CustomerDto>();
        plan.Bind<S8T06Order, S8T06OrderNestedDto>();
    }
}

// Collection projection fixtures — exercise the IEnumerable<TS> -> IEnumerable<TD> path in
// SculptorProjectionBuilder when the element pair has a registered blueprint.

public class S8T06LineItem
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public record S8T06LineItemDto
{
    public int Id { get; init; }
    public string Sku { get; init; } = string.Empty;
    public decimal Price { get; init; }
}

public class S8T06OrderWithItems
{
    public int Id { get; set; }
    public List<S8T06LineItem> Items { get; set; } = new();
}

public record S8T06OrderWithItemsDto
{
    public int Id { get; init; }
    public List<S8T06LineItemDto> Items { get; init; } = new();
}

internal class S8T06CollectionBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T06LineItem, S8T06LineItemDto>();
        plan.Bind<S8T06OrderWithItems, S8T06OrderWithItemsDto>();
    }
}

// Blueprint that binds Total via a lambda expression provider — lambda-based providers are
// wrapped in ExpressionValueProvider, which is not pattern-matched by SculptorProjectionBuilder.
// Drives the unsupported-provider diagnostic test.
internal class S8T06UnsupportedProviderBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T06Order, S8T06OrderFlatDto>()
            .Property(d => d.Total, p => p.From(o => o.Total * 1.1m));
    }
}
