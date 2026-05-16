// SPDX-License-Identifier: MIT
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Tests.Integration.Fixtures;

/// <summary>
/// DI-resolved value provider used by the integration suite. Constructor accepts a scoped
/// <see cref="ITaxRateSource"/> so ProviderResolutionTests can prove the provider's ctor is
/// satisfied against the mapping-scope's <see cref="IServiceProvider"/> on every call.
/// </summary>
public sealed class TaxCalculatorProvider : IValueProvider<Order, OrderDto, decimal>
{
    private readonly ITaxRateSource _rateSource;

    public TaxCalculatorProvider(ITaxRateSource rateSource)
    {
        _rateSource = rateSource ?? throw new ArgumentNullException(nameof(rateSource));
    }

    public decimal Provide(Order origin, OrderDto target, string targetMemberName, MappingScope scope)
    {
        var subtotal = origin.Lines.Sum(l => l.Quantity * l.UnitPrice);
        return decimal.Round(subtotal * _rateSource.Rate, 2, MidpointRounding.AwayFromZero);
    }

    object? IValueProvider.Provide(object origin, object target, string targetMemberName, MappingScope scope)
        => Provide((Order)origin, (OrderDto)target, targetMemberName, scope);
}

/// <summary>
/// Scoped service contract so tests can register a different rate per service scope
/// and prove the DI-resolved provider picks up the scope-bound instance on each call.
/// </summary>
public interface ITaxRateSource
{
    decimal Rate { get; }
}

public sealed class FixedTaxRateSource : ITaxRateSource
{
    public FixedTaxRateSource(decimal rate) { Rate = rate; }
    public decimal Rate { get; }
}
