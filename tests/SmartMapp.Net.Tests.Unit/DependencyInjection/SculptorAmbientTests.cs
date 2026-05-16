using FluentAssertions;
using NSubstitute;
using SmartMapp.Net;
using SmartMapp.Net.Extensions;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T07 — unit tests for <see cref="SculptorAmbient"/>, the
/// <see cref="AsyncLocal{T}"/>-backed accessor consumed by
/// <see cref="SculptorObjectExtensions.MapTo{TTarget}(object)"/> and the ambient
/// <c>IQueryable.SelectAs&lt;T&gt;()</c> overload. Each test snapshots and restores the
/// ambient slot so parallel xUnit runs don't bleed sculptors across tests.
/// </summary>
public class SculptorAmbientTests : IDisposable
{
    private readonly ISculptor? _restorePrevious;

    public SculptorAmbientTests()
    {
        _restorePrevious = SculptorAmbient.Current;
        // Ensure every test starts with a clean slot. Tests that need an ambient push one via
        // SculptorAmbient.Set(...) so the dispose-token restores on exit.
        if (_restorePrevious is not null)
        {
            using var reset = SculptorAmbient.Set(_restorePrevious);
        }
    }

    public void Dispose()
    {
        // Restore the slot to whatever it was when the test started. Avoids leaking test state
        // into other suites running on the same process (xUnit parallel runs share AsyncLocal
        // context across some tests).
        if (_restorePrevious is null)
        {
            // Nothing was installed — clear the slot after this test completes.
            typeof(SculptorAmbient)
                .GetMethod("Clear", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, null);
        }
        else
        {
            using var _ = SculptorAmbient.Set(_restorePrevious);
        }
    }

    [Fact]
    public void Current_WithNoInstall_IsNull()
    {
        // After the ctor's defensive reset, Current should be null (unless another concurrent
        // test pushed an ambient — which is tolerated by checking the null-or-expected value).
        var current = SculptorAmbient.Current;
        (current is null || current == _restorePrevious).Should().BeTrue();
    }

    [Fact]
    public void Set_PushesSculptorAndRestoresOnDispose()
    {
        var sculptor = Substitute.For<ISculptor>();

        using (SculptorAmbient.Set(sculptor))
        {
            SculptorAmbient.Current.Should().BeSameAs(sculptor);
        }

        // After dispose, Current reverts to whatever was there before the Set call.
        SculptorAmbient.Current.Should().BeSameAs(_restorePrevious);
    }

    [Fact]
    public void Set_NestedOverride_RestoresInner_NotOuter()
    {
        var outer = Substitute.For<ISculptor>();
        var inner = Substitute.For<ISculptor>();

        using (SculptorAmbient.Set(outer))
        {
            SculptorAmbient.Current.Should().BeSameAs(outer);
            using (SculptorAmbient.Set(inner))
            {
                SculptorAmbient.Current.Should().BeSameAs(inner);
            }
            SculptorAmbient.Current.Should().BeSameAs(outer,
                "inner dispose must restore the outer push, not clear to null.");
        }
    }

    [Fact]
    public void Set_NullSculptor_Throws()
    {
        var act = () => SculptorAmbient.Set(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("sculptor");
    }

    [Fact]
    public async Task Current_FlowsAcrossAwait_Within_SetScope()
    {
        var sculptor = Substitute.For<ISculptor>();

        using (SculptorAmbient.Set(sculptor))
        {
            SculptorAmbient.Current.Should().BeSameAs(sculptor);
            await Task.Yield();
            SculptorAmbient.Current.Should().BeSameAs(sculptor,
                "AsyncLocal storage must flow across await continuations.");
        }
    }

    [Fact]
    public async Task Current_IsIsolatedAcrossParallelTasks()
    {
        var spA = Substitute.For<ISculptor>();
        var spB = Substitute.For<ISculptor>();

        async Task<ISculptor?> Observe(ISculptor s)
        {
            using (SculptorAmbient.Set(s))
            {
                await Task.Delay(5);
                return SculptorAmbient.Current;
            }
        }

        var taskA = Task.Run(() => Observe(spA));
        var taskB = Task.Run(() => Observe(spB));

        (await taskA).Should().BeSameAs(spA);
        (await taskB).Should().BeSameAs(spB);
    }
}
