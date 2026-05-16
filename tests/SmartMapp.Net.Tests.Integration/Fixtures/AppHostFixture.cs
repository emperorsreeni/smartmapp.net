// SPDX-License-Identifier: MIT
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.Fixtures;

/// <summary>
/// Shared <see cref="IServiceProvider"/> + seeded <see cref="AppDbContext"/> for every
/// integration test class in the <see cref="AppHostCollection"/> collection. Matches spec
/// §S8-T11 Technical Considerations bullet 1: "Use <c>CollectionDefinition</c> to share
/// <c>AppHostFixture</c> across related test classes without re-seeding." The fixture is
/// instantiated once by xUnit, so the sculptor is forged, the DI container is built, and
/// <see cref="OrderSeedBuilder"/> runs exactly once — every test class in the collection
/// resolves the same <see cref="ISculptor"/> and sees the same seed shape.
/// </summary>
/// <remarks>
/// Tests that need their own isolated container (e.g. to exercise a distinct
/// <see cref="ServiceLifetime"/> or a custom blueprint) build their own provider via
/// <see cref="ServiceCollection"/> directly — the fixture is opt-in.
/// </remarks>
public sealed class AppHostFixture : IDisposable
{
    public ServiceProvider RootServices { get; }
    public ISculptor Sculptor { get; }
    public string SeededDbName { get; }

    public AppHostFixture()
    {
        SeededDbName = $"s8t11-apphost-{Guid.NewGuid():N}";

        var services = new ServiceCollection();
        services.AddSingleton<ITaxRateSource>(_ => new FixedTaxRateSource(0.10m));
        services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(SeededDbName));
        services.AddSculptor(o => o.UseBlueprint<IntegrationBlueprint>());

        RootServices = services.BuildServiceProvider();

        // Eagerly resolve the sculptor so the forge + ambient installation happens up-front
        // rather than on the first test that happens to run. Keeps per-test timings clean.
        Sculptor = RootServices.GetRequiredService<ISculptor>();

        // Seed the shared database exactly once.
        using var scope = RootServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        OrderSeedBuilder.Seed(db);
    }

    /// <summary>
    /// Creates a fresh DI scope off the shared root provider. Tests that need a scoped
    /// <see cref="AppDbContext"/> or scoped sculptor dependencies call this instead of
    /// building their own container — that way the sculptor / blueprint / forge cache is
    /// reused across tests in the collection.
    /// </summary>
    public IServiceScope CreateScope() => RootServices.CreateScope();

    public void Dispose() => RootServices.Dispose();
}

/// <summary>
/// xUnit collection definition that binds tests opting in via <c>[Collection(AppHostCollection.Name)]</c>
/// to a single shared <see cref="AppHostFixture"/> instance. Serialises the collection's tests
/// only implicitly (xUnit runs members of the same collection sequentially unless opted in to
/// parallel collection execution, which we don't), matching the spec's "shared seed across tests
/// within a class" constraint.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AppHostCollection : ICollectionFixture<AppHostFixture>
{
    public const string Name = "AppHost (shared seed)";
}
