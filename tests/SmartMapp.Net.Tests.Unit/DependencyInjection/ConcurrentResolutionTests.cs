using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.DependencyInjection.Internal;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 • S8-T01 — concurrency guarantees for the lazy forge pipeline.
/// Verifies that concurrent first-time resolves collapse to a single <c>Forge()</c>
/// execution and yield identical <see cref="ISculptor"/> references.
/// </summary>
public class ConcurrentResolutionTests
{
    [Fact]
    public void ConcurrentFirstTimeResolves_ReturnSameSculptorInstance()
    {
        var services = new ServiceCollection();
        services.AddSculptor();
        using var provider = services.BuildServiceProvider();

        var results = new ConcurrentBag<ISculptor>();
        var ready = new ManualResetEventSlim(false);

        var threads = Enumerable.Range(0, 100)
            .Select(_ => new Thread(() =>
            {
                ready.Wait();
                results.Add(provider.GetRequiredService<ISculptor>());
            }))
            .ToArray();

        foreach (var t in threads) t.Start();
        ready.Set();
        foreach (var t in threads) t.Join();

        results.Should().HaveCount(100);
        results.Distinct().Should().HaveCount(1,
            "all concurrent resolves must receive the same ISculptor instance.");
    }

    [Fact]
    public void ConcurrentFirstTimeResolves_TriggerForgeExactlyOnce()
    {
        var services = new ServiceCollection();
        services.AddSculptor();
        using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<ForgedSculptorHost>();

        host.IsForged.Should().BeFalse();

        Parallel.For(0, 100, i =>
        {
            _ = i;
            _ = provider.GetRequiredService<ISculptor>();
        });

        host.IsForged.Should().BeTrue();

        // Second resolve round: wrapper identity may differ across resolves (DI decorator),
        // but the INNER global sculptor must be the one produced on first resolve. T04 introduces
        // the DependencyInjectionSculptor wrapper for ambient-SP propagation (§11.4).
        var canonical = host.Sculptor;
        Parallel.For(0, 100, i =>
        {
            _ = i;
            var resolved = provider.GetRequiredService<ISculptor>();
            var inner = resolved is SmartMapp.Net.DependencyInjection.Internal.DependencyInjectionSculptor w
                ? w.Inner
                : resolved;
            inner.Should().BeSameAs(canonical);
        });
    }

    [Fact]
    public void ConcurrentResolvesOfConfigurationAndSculptor_ReturnIdenticalInstance()
    {
        var services = new ServiceCollection();
        services.AddSculptor();
        using var provider = services.BuildServiceProvider();

        var sculptors = new ConcurrentBag<ISculptor>();
        var configs = new ConcurrentBag<ISculptorConfiguration>();

        Parallel.For(0, 100, i =>
        {
            if (i % 2 == 0)
                sculptors.Add(provider.GetRequiredService<ISculptor>());
            else
                configs.Add(provider.GetRequiredService<ISculptorConfiguration>());
        });

        sculptors.Distinct().Should().HaveCount(1);
        configs.Distinct().Should().HaveCount(1);
        ((ISculptorConfiguration)sculptors.First()).Should().BeSameAs(configs.First());
    }
}
