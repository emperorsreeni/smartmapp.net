using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T04 — unit tests for <see cref="ITypeTransformer"/> DI resolution.
/// <para>
/// Scope: verifies (a) the scanner auto-registers transformer implementations as Transient,
/// and (b) the <see cref="DependencyInjectionProviderFactory"/> satisfies transformer
/// constructor dependencies via <c>ActivatorUtilities.CreateInstance</c>. Full per-call
/// transformer resolution via <c>p.TransformWith&lt;T&gt;()</c> requires compilation-layer
/// changes deferred to a later sprint (see spec §S8-T04 "Transformers with request-scoped
/// dependencies remain unsupported in Sprint 8").
/// </para>
/// </summary>
public class TransformerInjectionTests
{
    [Fact]
    public void Transformer_AutoRegisteredAsTransient_ByScanner()
    {
        var services = new ServiceCollection();
        services.AddSingleton<S8T04TransformerCallCounter>();
        services.AddSculptor(options =>
        {
            options.ScanAssembliesContaining<TransformerInjectionTests>();
            // No blueprint needed — we're only asserting registration side-effects.
        });

        services.Should().Contain(d =>
            d.ServiceType == typeof(S8T04UpperCaseTransformer)
            && d.Lifetime == ServiceLifetime.Transient,
            "scanned ITypeTransformer implementations must auto-register as Transient per §11.2.");
    }

    [Fact]
    public void Transformer_ResolvedViaFactory_HasCtorDependencyInjected()
    {
        var services = new ServiceCollection();
        var counter = new S8T04TransformerCallCounter();
        services.AddSingleton(counter);
        services.AddTransient<S8T04UpperCaseTransformer>();
        using var sp = services.BuildServiceProvider();

        var factory = new DependencyInjectionProviderFactory(sp);
        var transformer = factory.Resolve(typeof(S8T04UpperCaseTransformer), sp)
            as ITypeTransformer<string, string>;

        transformer.Should().NotBeNull();
        var result = transformer!.Transform("hello", new MappingScope());

        result.Should().Be("HELLO");
        counter.Count.Should().Be(1,
            "the transformer's ctor-injected counter must be the same instance the factory resolved.");
    }

    [Fact]
    public void Transformer_ResolvedViaFactory_UnregisteredType_UsesActivatorUtilities()
    {
        // Only the ctor dependency is registered — the transformer itself is not.
        // ActivatorUtilities.CreateInstance must still construct it.
        var services = new ServiceCollection();
        services.AddSingleton(new S8T04TransformerCallCounter());
        using var sp = services.BuildServiceProvider();

        var factory = new DependencyInjectionProviderFactory(sp);
        var transformer = factory.Resolve(typeof(S8T04UpperCaseTransformer), sp);

        transformer.Should().BeOfType<S8T04UpperCaseTransformer>();
    }

    [Fact]
    public void Transformer_ResolvedViaFactory_WithRootFallback_WorksWhenServiceProviderIsNull()
    {
        // Forge-time transformer resolution path: scope.ServiceProvider is null, resolver
        // falls back to the captured root SP.
        var services = new ServiceCollection();
        services.AddSingleton(new S8T04TransformerCallCounter());
        using var root = services.BuildServiceProvider();

        var factory = new DependencyInjectionProviderFactory(root);

        var transformer = factory.Resolve(typeof(S8T04UpperCaseTransformer), serviceProvider: null);

        transformer.Should().BeOfType<S8T04UpperCaseTransformer>();
    }
}
