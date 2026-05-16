using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Samples.MinimalApi.Models;
using SmartMapp.Net.Samples.MinimalApi.Services;

namespace SmartMapp.Net.Samples.MinimalApi.Blueprints;

/// <summary>
/// Reusable <see cref="MappingBlueprint"/> for the three mapping pairs the Minimal API uses:
/// <list type="bullet">
///   <item><c>Order → OrderListDto</c> — flat, EF-translatable; drives <c>SelectAs</c> on <c>/orders</c>.</item>
///   <item><c>Order → OrderDto</c> — detail, uses the DI-resolved <see cref="TaxCalculatorProvider"/>
///         plus a computed subtotal/total.</item>
///   <item><c>OrderLine → OrderLineDto</c> — nested-collection projection inside <c>OrderDto.Lines</c>.</item>
/// </list>
/// Flattened members (<c>CustomerFirstName</c>, <c>CustomerAddressCity</c>, …) are picked up
/// by the flattening convention automatically — no explicit <c>.Property(...)</c> rule needed.
/// </summary>
public sealed class OrderBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        // Flat list DTO — every member is conventionally linkable and EF-translatable.
        plan.Bind<Order, OrderListDto>();

        // Detail DTO — the DI-resolved TaxCalculatorProvider populates Tax; Subtotal, Total,
        // and OrderLineDto.LineTotal use inline expressions so no extra provider types are
        // needed just for arithmetic.
        plan.Bind<Order, OrderDto>()
            .Property(d => d.Subtotal,
                      p => p.From(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice)))
            .Property(d => d.Tax,
                      p => p.From<TaxCalculatorProvider>())
            .Property(d => d.Total,
                      p => p.From(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice) * 1.10m));

        plan.Bind<OrderLine, OrderLineDto>()
            .Property(d => d.LineTotal, p => p.From(l => l.Quantity * l.UnitPrice));
    }
}
