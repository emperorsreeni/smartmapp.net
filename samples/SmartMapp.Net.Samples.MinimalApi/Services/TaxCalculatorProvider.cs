using Microsoft.Extensions.Logging;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Samples.MinimalApi.Models;

namespace SmartMapp.Net.Samples.MinimalApi.Services;

/// <summary>
/// DI-resolved <see cref="IValueProvider{TOrigin, TTarget, TMember}"/> that computes the
/// <see cref="OrderDto.Tax"/> member from the order's line subtotal. Demonstrates spec §11.4 /
/// Sprint 8 · S8-T04 — a provider with constructor-injected dependencies
/// (<see cref="ILogger{T}"/>) is activated by the DI-aware provider factory on every mapping
/// call, falling back to <see cref="System.Activator"/> when no container is wired in.
/// </summary>
/// <remarks>
/// The hard-coded 10% rate keeps the sample deterministic so the integration tests can assert
/// an exact tax value for each seeded order. A production implementation would inject a
/// scoped <c>ITaxRateRepository</c> or similar.
/// </remarks>
public sealed class TaxCalculatorProvider : IValueProvider<Order, OrderDto, decimal>
{
    private const decimal FlatTaxRate = 0.10m;

    private readonly ILogger<TaxCalculatorProvider> _logger;

    public TaxCalculatorProvider(ILogger<TaxCalculatorProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public decimal Provide(Order origin, OrderDto target, string targetMemberName, MappingScope scope)
    {
        var subtotal = origin.Lines.Sum(l => l.Quantity * l.UnitPrice);
        var tax = decimal.Round(subtotal * FlatTaxRate, 2, MidpointRounding.AwayFromZero);

        _logger.LogDebug(
            "TaxCalculatorProvider: order {OrderId} subtotal={Subtotal:F2} tax={Tax:F2}",
            origin.Id, subtotal, tax);

        return tax;
    }

    // Non-generic ITypeTransformer<> / IValueProvider fallback — the runtime always prefers
    // the strongly-typed overload above, so delegating here keeps behaviour consistent when
    // the DI pipeline resolves this provider via its non-generic interface.
    object? IValueProvider.Provide(object origin, object target, string targetMemberName, MappingScope scope)
        => Provide((Order)origin, (OrderDto)target, targetMemberName, scope);
}
