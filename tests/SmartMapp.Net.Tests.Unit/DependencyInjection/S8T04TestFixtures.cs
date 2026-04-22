using SmartMapp.Net;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

// S8-T04 test fixtures — providers and transformers with constructor dependencies, and the
// blueprint wiring that exercises the DeferredValueProvider / AttributeDeferredTypeTransformer
// code paths. Kept internal so scanner-discovered blueprints don't poison default-scan tests.

public class S8T04Order
{
    public int Id { get; set; }
    public decimal Subtotal { get; set; }
}

public record S8T04OrderDto
{
    public int Id { get; init; }
    public decimal Subtotal { get; init; }
    public decimal Tax { get; init; }
    public string Currency { get; init; } = string.Empty;
}

// A service the tax-calculator provider depends on via constructor injection.
public interface IS8T04TaxService
{
    decimal ComputeTax(decimal subtotal);
}

public sealed class S8T04FixedTaxService : IS8T04TaxService
{
    public decimal Rate { get; }
    public S8T04FixedTaxService(decimal rate = 0.2m) { Rate = rate; }
    public decimal ComputeTax(decimal subtotal) => subtotal * Rate;
}

// A scoped service that records its scope id — lets tests assert scope isolation.
public sealed class S8T04ScopeId
{
    public Guid Id { get; } = Guid.NewGuid();
}

// Value provider with a constructor dependency on the tax service. Declared public because
// ActivatorUtilities.CreateInstance reaches it from the DI package's reflection path — the
// class itself is a leaf consumer, it doesn't register via the blueprint scanner.
public sealed class S8T04TaxProvider : IValueProvider<S8T04Order, S8T04OrderDto, decimal>
{
    private readonly IS8T04TaxService _tax;
    public S8T04TaxProvider(IS8T04TaxService tax) { _tax = tax; }

    public decimal Provide(S8T04Order origin, S8T04OrderDto target, string targetMemberName, MappingScope scope)
        => _tax.ComputeTax(origin.Subtotal);

    object? IValueProvider.Provide(object origin, object target, string targetMemberName, MappingScope scope)
        => Provide((S8T04Order)origin, (S8T04OrderDto)target, targetMemberName, scope);
}

// Provider that captures the ambient scope id so tests can prove scope isolation.
public sealed class S8T04ScopeCapturingProvider : IValueProvider<S8T04Order, S8T04OrderDto, string>
{
    private readonly S8T04ScopeId _scope;
    public S8T04ScopeCapturingProvider(S8T04ScopeId scope) { _scope = scope; }

    public string Provide(S8T04Order origin, S8T04OrderDto target, string targetMemberName, MappingScope scope)
        => _scope.Id.ToString();

    object? IValueProvider.Provide(object origin, object target, string targetMemberName, MappingScope scope)
        => Provide((S8T04Order)origin, (S8T04OrderDto)target, targetMemberName, scope);
}

// Provider with a parameterless constructor — exercises the activator-fallback path when DI
// is absent or the type isn't registered.
public sealed class S8T04NoDepsProvider : IValueProvider<S8T04Order, S8T04OrderDto, decimal>
{
    public decimal Provide(S8T04Order origin, S8T04OrderDto target, string targetMemberName, MappingScope scope)
        => origin.Subtotal * 0.1m;

    object? IValueProvider.Provide(object origin, object target, string targetMemberName, MappingScope scope)
        => Provide((S8T04Order)origin, (S8T04OrderDto)target, targetMemberName, scope);
}

// Counter for transformer DI tests.
public sealed class S8T04TransformerCallCounter
{
    public int Count;
}

// Type transformer with a ctor dependency on the counter — exercises DI resolution for
// transformers referenced via p.TransformWith<T>().
public sealed class S8T04UpperCaseTransformer : ITypeTransformer<string, string>
{
    private readonly S8T04TransformerCallCounter _counter;
    public S8T04UpperCaseTransformer(S8T04TransformerCallCounter counter) { _counter = counter; }
    public bool CanTransform(Type originType, Type targetType) => originType == typeof(string) && targetType == typeof(string);
    public string Transform(string origin, MappingScope scope)
    {
        System.Threading.Interlocked.Increment(ref _counter.Count);
        return origin?.ToUpperInvariant() ?? string.Empty;
    }
}

// Blueprints kept internal so the scanner's IsVisible filter excludes them from default-scan
// tests. Explicit UseBlueprint<T>() still works from same-assembly test code.
internal class S8T04TaxBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T04Order, S8T04OrderDto>()
            .Property(d => d.Tax, p => p.From<S8T04TaxProvider>());
    }
}

internal class S8T04ScopeBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T04Order, S8T04OrderDto>()
            .Property(d => d.Currency, p => p.From<S8T04ScopeCapturingProvider>());
    }
}

internal class S8T04NoDepsBlueprint : MappingBlueprint
{
    public override void Design(IBlueprintBuilder plan)
    {
        plan.Bind<S8T04Order, S8T04OrderDto>()
            .Property(d => d.Tax, p => p.From<S8T04NoDepsProvider>());
    }
}
