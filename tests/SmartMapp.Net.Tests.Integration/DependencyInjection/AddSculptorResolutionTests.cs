// SPDX-License-Identifier: MIT
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Tests.Integration.Fixtures;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T11 — end-to-end resolution tests that exercise every public
/// <c>AddSculptor</c> overload through a real <see cref="ServiceProvider"/>. These
/// complement the unit-level registration tests by asserting that the resolved services
/// actually perform a mapping against the <see cref="IntegrationBlueprint"/>.
/// </summary>
public sealed class AddSculptorResolutionTests
{
    private static Order SampleOrder() => new()
    {
        Id = 7,
        PlacedAt = new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        Customer = new Customer
        {
            Id = 10, FirstName = "Ada", LastName = "Lovelace", Email = "ada@example.com",
            Address = new Address { City = "London" },
        },
        Lines =
        {
            new OrderLine { Sku = "X", Quantity = 2, UnitPrice = 5m },
        },
    };

    private static IServiceProvider BuildProvider(Action<IServiceCollection> mutate)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        mutate(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Overload_Default_ResolvesSculptorAndMaps()
    {
        using var sp = (ServiceProvider)BuildProvider(s =>
            s.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>()));

        var sculptor = sp.GetRequiredService<ISculptor>();
        var dto = sculptor.Map<Order, OrderListDto>(SampleOrder());

        dto.CustomerFirstName.Should().Be("Ada");
        dto.CustomerAddressCity.Should().Be("London");
    }

    [Fact]
    public void Overload_OptionsCallback_AppliedLazilyOnFirstResolve()
    {
        var invocations = 0;
        using var sp = (ServiceProvider)BuildProvider(s => s.AddSculptor(options =>
        {
            invocations++;
            options.UseBlueprint<IntegrationBlueprint>();
        }));

        invocations.Should().Be(0, "options callback is deferred until first ISculptor resolve (S8-T02).");
        _ = sp.GetRequiredService<ISculptor>();
        _ = sp.GetRequiredService<ISculptor>();
        invocations.Should().Be(1, "options callback must run exactly once regardless of resolve count.");
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void Overload_LifetimeOptions_AppliesToSculptorHandle(ServiceLifetime lifetime)
    {
        // Spec §S8-T11 Tech-Cons bullet 2: parameterise the lifetime combinations.
        using var sp = (ServiceProvider)BuildProvider(s =>
            s.AddSculptor(lifetime, o => o.UseBlueprint<IntegrationBlueprint>()));

        using var scope = sp.CreateScope();
        var a = scope.ServiceProvider.GetRequiredService<ISculptor>();
        var b = scope.ServiceProvider.GetRequiredService<ISculptor>();

        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
            case ServiceLifetime.Scoped:
                a.Should().BeSameAs(b);
                break;
            case ServiceLifetime.Transient:
                a.Should().NotBeSameAs(b);
                break;
        }

        a.Map<Order, OrderListDto>(SampleOrder()).Id.Should().Be(7);
    }

    [Fact]
    public void Overload_ScopedLifetime_DifferentInstanceAcrossScopes()
    {
        using var sp = (ServiceProvider)BuildProvider(s =>
            s.AddSculptor(ServiceLifetime.Scoped, o => o.UseBlueprint<IntegrationBlueprint>()));

        ISculptor fromA, fromB;
        using (var a = sp.CreateScope()) fromA = a.ServiceProvider.GetRequiredService<ISculptor>();
        using (var b = sp.CreateScope()) fromB = b.ServiceProvider.GetRequiredService<ISculptor>();

        fromA.Should().NotBeSameAs(fromB, "Scoped sculptor yields a fresh wrapper per scope.");
    }

    [Fact]
    public void SculptorConfiguration_ResolvesToSameInstanceAsSculptor()
    {
        using var sp = (ServiceProvider)BuildProvider(s =>
            s.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>()));

        var sculptor = sp.GetRequiredService<ISculptor>();
        var config = sp.GetRequiredService<ISculptorConfiguration>();

        config.Should().BeSameAs(sculptor);
        config.GetAllBlueprints().Should().HaveCountGreaterThanOrEqualTo(4,
            "IntegrationBlueprint declares 4 pairs (Order→OrderListDto, Order→OrderDto, OrderLine→OrderLineDto, Customer→CustomerContact).");
    }

    [Fact]
    public void DuplicateAddSculptor_Throws_WithSprint16Hint()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddSculptor();

        var act = () => services.AddSculptor();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been called*")
            .Where(ex => ex.Message.Contains("Sprint 16", StringComparison.Ordinal));
    }

    [Fact]
    public void AddSculptor_UnregisteredPair_ThrowsOnMapperResolve()
    {
        // Spec §S8-T11 unit-tests bullet 2 — error path: resolving a pair that has no
        // blueprint throws from GetRequiredService<IMapper<,>>, while the sculptor itself
        // resolves fine. Use a pair that's deliberately absent from IntegrationBlueprint so
        // the auto-scan of the test assembly doesn't accidentally satisfy it.
        using var sp = (ServiceProvider)BuildProvider(s =>
            s.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>()));

        var sculptor = sp.GetRequiredService<ISculptor>();
        sculptor.Should().NotBeNull();

        var act = () => sp.GetRequiredService<IMapper<DateTime, Guid>>();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OptionsCallback_Throws_SurfacesAtFirstResolve()
    {
        // Spec §S8-T11 unit-tests bullet 2: "invalid options callback" surfaces on resolve.
        using var sp = (ServiceProvider)BuildProvider(s => s.AddSculptor(_ =>
            throw new InvalidOperationException("boom")));

        var act = () => sp.GetRequiredService<ISculptor>();
        var thrown = act.Should().Throw<Exception>().Which;
        var messages = Unwind(thrown).Select(e => e.Message).ToList();
        messages.Should().Contain(m => m.Contains("boom", StringComparison.Ordinal),
            "the user-supplied options callback exception must surface (possibly wrapped) on first resolve.");
    }

    private static IEnumerable<Exception> Unwind(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
            yield return current;
    }

    [Fact]
    public void AddSculptor_ReturnsSameServiceCollectionForChaining()
    {
        var services = new ServiceCollection();
        var returned = services.AddSculptor();
        returned.Should().BeSameAs(services);
    }
}
