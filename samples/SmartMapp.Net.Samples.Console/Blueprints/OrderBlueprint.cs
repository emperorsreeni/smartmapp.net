using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Samples.Console.Models;

namespace SmartMapp.Net.Samples.Console.Blueprints;

/// <summary>
/// Reusable blueprint-class configuration for <see cref="Order"/> → <see cref="OrderDto"/>.
/// Shown alongside the inline <c>options.Bind&lt;S,D&gt;(...)</c> form in the sample to
/// illustrate both authoring styles side-by-side.
/// </summary>
/// <remarks>
/// Flattened members (<c>CustomerName</c>, <c>CustomerAddressCity</c>) are picked up by the
/// flattening convention without any explicit property rule. Only <c>Total</c> needs an
/// explicit <c>.From(...)</c> because no origin member is named <c>Total</c> — it is computed
/// from the <see cref="Order.Lines"/> collection.
/// </remarks>
public sealed class OrderBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<Order, OrderDto>()
            .Property(d => d.Total,
                      p => p.From(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice)))
            .OnMapped((_, target) =>
            {
                // Post-mapping normalisation — demonstrates the OnMapped hook.
                target.CustomerName = target.CustomerName.Trim();
            });

        // Nested-collection projection: OrderLine -> OrderLineDto.
        plan.Bind<OrderLine, OrderLineDto>();
    }
}
