using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T02 — scope-lifetime semantics for <c>AddSculptor</c>. Validates the
/// default shared-singleton behaviour and the opt-in per-scope rebuild path gated by
/// <see cref="SculptorOptions.AllowPerScopeRebuild"/>.
/// </summary>
public class ScopedLifetimeTests
{
    [Fact]
    public void Scoped_DefaultRebuildFalse_SameInstanceWithinScope()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var s1 = scope.ServiceProvider.GetRequiredService<ISculptor>();
        var s2 = scope.ServiceProvider.GetRequiredService<ISculptor>();

        s1.Should().BeSameAs(s2);
    }

    [Fact]
    public void Scoped_DefaultRebuildFalse_SameInstanceAcrossScopes()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();

        ISculptor a;
        ISculptor b;
        using (var scope1 = provider.CreateScope())
            a = scope1.ServiceProvider.GetRequiredService<ISculptor>();
        using (var scope2 = provider.CreateScope())
            b = scope2.ServiceProvider.GetRequiredService<ISculptor>();

        // Each scope gets a fresh DependencyInjectionSculptor wrapper (it's scoped, and each
        // scope captures its own IServiceProvider), but the INNER sculptor is the shared
        // global one because AllowPerScopeRebuild defaults to false.
        var innerA = ((SmartMapp.Net.DependencyInjection.Internal.DependencyInjectionSculptor)a).Inner;
        var innerB = ((SmartMapp.Net.DependencyInjection.Internal.DependencyInjectionSculptor)b).Inner;
        innerA.Should().BeSameAs(innerB,
            "AllowPerScopeRebuild defaults to false — the global singleton sculptor is shared across scopes.");
    }

    [Fact]
    public void Scoped_RebuildTrue_FreshInstancePerScope()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        ISculptor a;
        ISculptor b;
        using (var scope1 = provider.CreateScope())
            a = scope1.ServiceProvider.GetRequiredService<ISculptor>();
        using (var scope2 = provider.CreateScope())
            b = scope2.ServiceProvider.GetRequiredService<ISculptor>();

        a.Should().NotBeSameAs(b,
            "AllowPerScopeRebuild=true means a fresh sculptor is forged for each scope.");
    }

    [Fact]
    public void Scoped_RebuildTrue_SameInstanceWithinOneScope()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var s1 = scope.ServiceProvider.GetRequiredService<ISculptor>();
        var s2 = scope.ServiceProvider.GetRequiredService<ISculptor>();

        s1.Should().BeSameAs(s2,
            "within a single scope, DI caches the scoped instance — rebuild only happens at scope boundaries.");
    }

    [Fact]
    public void Scoped_RebuildTrue_ConfigurationTracksSculptor()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sculptor = scope.ServiceProvider.GetRequiredService<ISculptor>();
        var config = scope.ServiceProvider.GetRequiredService<ISculptorConfiguration>();

        config.Should().BeSameAs(sculptor,
            "ISculptorConfiguration must resolve to the same per-scope sculptor instance.");
    }

    [Fact]
    public void Transient_DefaultRebuildFalse_SameGlobalSculptorAcrossResolves()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Transient);
        using var provider = services.BuildServiceProvider();

        var s1 = provider.GetRequiredService<ISculptor>();
        var s2 = provider.GetRequiredService<ISculptor>();

        var inner1 = ((SmartMapp.Net.DependencyInjection.Internal.DependencyInjectionSculptor)s1).Inner;
        var inner2 = ((SmartMapp.Net.DependencyInjection.Internal.DependencyInjectionSculptor)s2).Inner;
        inner1.Should().BeSameAs(inner2,
            "Default AllowPerScopeRebuild=false returns the same global inner sculptor even for Transient handle.");
    }

    [Fact]
    public void Transient_RebuildTrue_FreshInstancePerResolve()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Transient, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        var s1 = provider.GetRequiredService<ISculptor>();
        var s2 = provider.GetRequiredService<ISculptor>();

        s1.Should().NotBeSameAs(s2,
            "Transient + AllowPerScopeRebuild=true forges per resolve.");
    }

    [Fact]
    public void Singleton_RebuildTrue_IsNoop_StillGlobalInstance()
    {
        // With ServiceLifetime.Singleton, DI caches the factory result after the first resolve.
        // AllowPerScopeRebuild has no effect — the factory only runs once ever.
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Singleton, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        var s1 = provider.GetRequiredService<ISculptor>();
        var s2 = provider.GetRequiredService<ISculptor>();

        s1.Should().BeSameAs(s2);
    }

    [Fact]
    public void Scoped_RebuildTrue_GlobalHostStaysSingleton_FactoryRunsOncePerScope()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        var globalHost1 = provider.GetRequiredService<ForgedSculptorHost>();
        var globalHost2 = provider.GetRequiredService<ForgedSculptorHost>();
        globalHost1.Should().BeSameAs(globalHost2, "ForgedSculptorHost is always Singleton.");

        using var scope = provider.CreateScope();
        var scopedHost = scope.ServiceProvider.GetRequiredService<ForgedSculptorHost>();
        scopedHost.Should().BeSameAs(globalHost1);
    }

    [Fact]
    public void Scoped_RebuildTrue_ConcurrentScopes_AllDistinct()
    {
        var services = new ServiceCollection();
        services.AddSculptor(ServiceLifetime.Scoped, options =>
        {
            options.AllowPerScopeRebuild = true;
        });
        using var provider = services.BuildServiceProvider();

        var results = new ConcurrentBag<ISculptor>();
        Parallel.For(0, 50, i =>
        {
            _ = i;
            using var scope = provider.CreateScope();
            results.Add(scope.ServiceProvider.GetRequiredService<ISculptor>());
        });

        results.Should().HaveCount(50);
        results.Distinct().Should().HaveCount(50,
            "every scope rebuild must produce a distinct sculptor instance.");
    }
}
