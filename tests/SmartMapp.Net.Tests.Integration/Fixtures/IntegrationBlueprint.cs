// SPDX-License-Identifier: MIT
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Tests.Integration.Fixtures;

/// <summary>
/// Shared <see cref="MappingBlueprint"/> used across the integration suite. Binds the four
/// pairs the suite needs:
/// <list type="bullet">
///   <item><c>Order → OrderListDto</c> — flat, EF-translatable. Drives SelectAs / MinimalApi /list.</item>
///   <item><c>Order → OrderDto</c> — detail with DI-resolved <see cref="TaxCalculatorProvider"/>.</item>
///   <item><c>OrderLine → OrderLineDto</c> — nested collection projection.</item>
///   <item><c>Customer → CustomerContact</c> — spare flattening pair so the suite's blueprint
///         count exceeds 3 (spec §S8-T11 context: "realistic 20-pair blueprint sets").</item>
/// </list>
/// </summary>
public sealed class IntegrationBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<Order, OrderListDto>();

        plan.Bind<Order, OrderDto>()
            .Property(d => d.Subtotal,
                      p => p.From(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice)))
            .Property(d => d.Tax,
                      p => p.From<TaxCalculatorProvider>())
            .Property(d => d.Total,
                      p => p.From(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice) * 1.10m));

        plan.Bind<OrderLine, OrderLineDto>()
            .Property(d => d.LineTotal, p => p.From(l => l.Quantity * l.UnitPrice));

        plan.Bind<Customer, CustomerContact>();
    }
}

/// <summary>Spare flattening target — drives the fourth pair in the shared blueprint.</summary>
public sealed class CustomerContact
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AddressCity { get; set; } = string.Empty;
}

/// <summary>
/// DTO with an optional member (<see cref="UnmappedNote"/>) that has no matching source
/// property on <see cref="Customer"/>. With <c>StrictMode=true</c>, the
/// <c>BlueprintValidator</c> emits a warning for this member which the
/// <c>SculptorStartupValidator</c> promotes to a startup failure (spec §6.4 / §12.1).
/// With <c>StrictMode=false</c>, no warnings are emitted and the host starts cleanly.
/// Drives the <c>StartupValidationTests</c> warnings scenarios.
/// </summary>
public sealed class CustomerContactWithNote
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? UnmappedNote { get; set; }
}

public sealed class WarningsBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<Customer, CustomerContactWithNote>();
    }
}
