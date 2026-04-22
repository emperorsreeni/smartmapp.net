using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T04 — unit tests for <see cref="DefaultProviderResolver"/>, the core
/// <see cref="SmartMapp.Net.Abstractions.IProviderResolver"/> implementation used when no DI
/// container is wired into the sculptor.
/// </summary>
public class DefaultProviderResolverTests
{
    [Fact]
    public void Resolve_NullType_Throws()
    {
        var act = () => DefaultProviderResolver.Instance.Resolve(null!, serviceProvider: null);

        act.Should().Throw<ArgumentNullException>().WithParameterName("type");
    }

    [Fact]
    public void Resolve_WithDiRegistration_ReturnsServiceProviderInstance()
    {
        var services = new ServiceCollection();
        var known = new S8T04FixedTaxService(0.1m);
        services.AddSingleton<S8T04FixedTaxService>(known);
        using var sp = services.BuildServiceProvider();

        var resolved = DefaultProviderResolver.Instance.Resolve(typeof(S8T04FixedTaxService), sp);

        resolved.Should().BeSameAs(known);
    }

    [Fact]
    public void Resolve_WithNullServiceProvider_ActivatesParameterlessType()
    {
        var resolved = DefaultProviderResolver.Instance.Resolve(typeof(S8T04NoDepsProvider), serviceProvider: null);

        resolved.Should().BeOfType<S8T04NoDepsProvider>();
    }

    [Fact]
    public void Resolve_UnregisteredTypeWithCtorDeps_ThrowsWithDiagnosticHint()
    {
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();

        // S8T04TaxProvider has ctor(IS8T04TaxService) which isn't registered; default resolver
        // cannot satisfy this via Activator.CreateInstance(Type.EmptyTypes).
        var act = () => DefaultProviderResolver.Instance.Resolve(typeof(S8T04TaxProvider), sp);

        act.Should().Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("no public parameterless constructor")
                      || ex.Message.Contains("Register it in DI"),
                "message must point the user at DI registration as the fix.");
    }

    [Fact]
    public void Resolve_AbstractType_Throws()
    {
        var act = () => DefaultProviderResolver.Instance.Resolve(typeof(System.IO.Stream), serviceProvider: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*abstract or an interface*");
    }

    [Fact]
    public void Resolve_Interface_Throws()
    {
        var act = () => DefaultProviderResolver.Instance.Resolve(typeof(IS8T04TaxService), serviceProvider: null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*abstract or an interface*");
    }

    [Fact]
    public void Instance_IsSingleton()
    {
        var a = DefaultProviderResolver.Instance;
        var b = DefaultProviderResolver.Instance;

        a.Should().BeSameAs(b);
    }
}
