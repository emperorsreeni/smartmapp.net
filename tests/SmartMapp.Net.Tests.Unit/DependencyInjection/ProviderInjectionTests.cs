using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T04 — end-to-end tests for DI-resolved <c>IValueProvider</c> instances
/// referenced via <c>p.From&lt;T&gt;()</c>. Exercises the full DeferredValueProvider →
/// IProviderResolver → ambient-SP path through <see cref="ISculptor"/> and
/// <see cref="IMapper{TOrigin, TTarget}"/>.
/// </summary>
public class ProviderInjectionTests
{
    [Fact]
    public void Provider_WithCtorDependency_ResolvesFromDi()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IS8T04TaxService>(new S8T04FixedTaxService(0.25m));
        services.AddSculptor(options => options.UseBlueprint<S8T04TaxBlueprint>());
        using var provider = services.BuildServiceProvider();

        var sculptor = provider.GetRequiredService<ISculptor>();
        var dto = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = 1, Subtotal = 100m });

        dto.Tax.Should().Be(25m,
            "provider's constructor must receive the DI-registered IS8T04TaxService (25% rate).");
    }

    [Fact]
    public void Provider_ResolvedByIMapper_UsesSameScopedService()
    {
        var services = new ServiceCollection();
        services.AddScoped<IS8T04TaxService>(_ => new S8T04FixedTaxService(0.10m));
        services.AddSculptor(options => options.UseBlueprint<S8T04TaxBlueprint>());
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper<S8T04Order, S8T04OrderDto>>();
        var dto = mapper.Map(new S8T04Order { Id = 2, Subtotal = 200m });

        dto.Tax.Should().Be(20m,
            "IMapper<,>.Map must push the scope's IServiceProvider as ambient so the provider's ctor sees the scoped rate.");
    }

    [Fact]
    public void Provider_AcrossTwoScopes_ResolvesAgainstEachScopesServices()
    {
        // Scoped ISculptor lifetime is required so the DependencyInjectionSculptor wrapper
        // captures each scope's IServiceProvider (a Singleton wrapper would pin the root SP).
        var services = new ServiceCollection();
        services.AddScoped<S8T04ScopeId>();
        services.AddSculptor(ServiceLifetime.Scoped, options => options.UseBlueprint<S8T04ScopeBlueprint>());
        using var provider = services.BuildServiceProvider();

        string currencyA, currencyB;
        Guid scopeIdA, scopeIdB;

        using (var scopeA = provider.CreateScope())
        {
            scopeIdA = scopeA.ServiceProvider.GetRequiredService<S8T04ScopeId>().Id;
            var sculptor = scopeA.ServiceProvider.GetRequiredService<ISculptor>();
            currencyA = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = 1 }).Currency;
        }

        using (var scopeB = provider.CreateScope())
        {
            scopeIdB = scopeB.ServiceProvider.GetRequiredService<S8T04ScopeId>().Id;
            var sculptor = scopeB.ServiceProvider.GetRequiredService<ISculptor>();
            currencyB = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = 1 }).Currency;
        }

        scopeIdA.Should().NotBe(scopeIdB, "sanity: two scopes produce distinct ScopeId singletons.");
        currencyA.Should().Be(scopeIdA.ToString(), "scope A's provider must see scope A's ScopeId.");
        currencyB.Should().Be(scopeIdB.ToString(), "scope B's provider must see scope B's ScopeId.");
        currencyA.Should().NotBe(currencyB, "no provider instance leaked across scopes.");
    }

    [Fact]
    public void Provider_BuilderOnlyUsage_WithNoDeps_WorksViaActivatorFallback()
    {
        // No DI — use the raw SculptorBuilder path. The blueprint references a parameterless
        // provider so DefaultProviderResolver's Activator.CreateInstance fallback kicks in.
        var sculptor = new SculptorBuilder()
            .UseBlueprint<S8T04NoDepsBlueprint>()
            .Forge();

        var dto = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = 3, Subtotal = 50m });

        dto.Tax.Should().Be(5m,
            "provider with a parameterless ctor must activate without DI.");
    }

    [Fact]
    public void Provider_CtorDepsWithoutDi_Throws()
    {
        // Builder-only usage with a provider that REQUIRES ctor deps and no DI to satisfy them.
        // DefaultProviderResolver should throw a diagnostic when the mapping runs.
        var sculptor = new SculptorBuilder()
            .UseBlueprint<S8T04TaxBlueprint>()
            .Forge();

        var act = () => sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = 4, Subtotal = 10m });

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("no public parameterless constructor")
                      || ex.Message.Contains("Register it in DI"),
                "the resolver must surface a clear DI-registration hint.");
    }

    [Fact]
    public void Provider_Resolution_1000ParallelMapsWithScopedDependency_AllUseOwnScope()
    {
        const int iterations = 1000;
        var services = new ServiceCollection();
        services.AddScoped<S8T04ScopeId>();
        // Scoped ISculptor lifetime per the same rationale as Provider_AcrossTwoScopes test.
        services.AddSculptor(ServiceLifetime.Scoped, options => options.UseBlueprint<S8T04ScopeBlueprint>());
        using var provider = services.BuildServiceProvider();

        var perIterationScopeIds = new ConcurrentDictionary<int, (Guid Expected, string Actual)>();

        Parallel.For(0, iterations, i =>
        {
            using var scope = provider.CreateScope();
            var expectedId = scope.ServiceProvider.GetRequiredService<S8T04ScopeId>().Id;
            var sculptor = scope.ServiceProvider.GetRequiredService<ISculptor>();
            var dto = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = i });
            perIterationScopeIds[i] = (expectedId, dto.Currency);
        });

        perIterationScopeIds.Should().HaveCount(iterations);
        foreach (var kv in perIterationScopeIds)
        {
            kv.Value.Actual.Should().Be(kv.Value.Expected.ToString(),
                $"iteration {kv.Key} must resolve its provider against its own scope's ScopeId.");
        }

        var distinctObserved = perIterationScopeIds.Values.Select(v => v.Actual).Distinct().Count();
        distinctObserved.Should().Be(iterations,
            "every scope must produce a distinct ScopeId — no cross-scope leak.");
    }

    [Fact]
    public void Provider_AutoRegisteredAsTransient_ByScanner()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IS8T04TaxService>(new S8T04FixedTaxService(0.1m));
        services.AddSculptor(options =>
        {
            options.ScanAssembliesContaining<ProviderInjectionTests>();
            options.UseBlueprint<S8T04TaxBlueprint>();
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(S8T04TaxProvider)
            && d.Lifetime == ServiceLifetime.Transient,
            "scanned value-provider implementations are auto-registered as Transient per §11.2.");
    }

    [Fact]
    public void Sculptor_PreservesOuterSetAmbient_InsteadOfClobberingWithCapturedSp()
    {
        // Spec §S8-T04 review fix — the wrapper's push must respect an ambient SP that was
        // already set by outer middleware (simulating HttpContext.RequestServices propagation).
        // We prove this by having the outer ambient point at a scope with a DIFFERENT
        // IS8T04TaxService than the one the Singleton wrapper captured at root.
        var services = new ServiceCollection();
        services.AddScoped<IS8T04TaxService>(_ => new S8T04FixedTaxService(0.99m));
        services.AddSculptor(options => options.UseBlueprint<S8T04TaxBlueprint>());
        using var root = services.BuildServiceProvider();
        using var outerScope = root.CreateScope();

        // ISculptor is Singleton and resolved from root: its wrapper captures the root SP.
        var sculptor = root.GetRequiredService<ISculptor>();

        // Simulate outer code (middleware) pushing the scope's SP as ambient before invoking Map.
        using var _ = SmartMapp.Net.Runtime.AmbientServiceProvider.Enter(outerScope.ServiceProvider);

        var dto = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Id = 1, Subtotal = 100m });

        dto.Tax.Should().Be(99m,
            "the wrapper must respect the outer-set ambient (scope's 99% rate) and NOT clobber " +
            "it with its captured root SP. If the scope's tax service were ignored, the provider " +
            "would have failed to resolve (scoped service in root scope) or returned a default rate.");
    }

    [Fact]
    public void AmbientServiceProvider_ClearedAfterMapReturns()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IS8T04TaxService>(new S8T04FixedTaxService(0.1m));
        services.AddSculptor(options => options.UseBlueprint<S8T04TaxBlueprint>());
        using var provider = services.BuildServiceProvider();

        var sculptor = provider.GetRequiredService<ISculptor>();
        _ = sculptor.Map<S8T04Order, S8T04OrderDto>(new S8T04Order { Subtotal = 10m });

        SmartMapp.Net.Runtime.AmbientServiceProvider.Current.Should().BeNull(
            "the DI sculptor wrapper must restore the ambient slot after Map returns, regardless of outcome.");
    }
}
