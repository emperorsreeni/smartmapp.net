using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T04 — unit tests for <see cref="AmbientServiceProvider"/>, the
/// <c>AsyncLocal&lt;IServiceProvider?&gt;</c> bridge that flows the DI scope from
/// <see cref="Microsoft.Extensions.DependencyInjection.SculptorServiceCollectionExtensions"/>-resolved
/// sculptors into deferred value-provider resolution at mapping time.
/// </summary>
public class AmbientServiceProviderTests
{
    [Fact]
    public void Current_DefaultsToNull()
    {
        AmbientServiceProvider.Current.Should().BeNull();
    }

    [Fact]
    public void Enter_SetsCurrent_RestoresOnDispose()
    {
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();

        AmbientServiceProvider.Current.Should().BeNull();

        using (AmbientServiceProvider.Enter(sp))
        {
            AmbientServiceProvider.Current.Should().BeSameAs(sp);
        }

        AmbientServiceProvider.Current.Should().BeNull("dispose must restore the previous value.");
    }

    [Fact]
    public void Enter_SupportsNestedScopes_RestoresPreviousOnInnerDispose()
    {
        var services = new ServiceCollection();
        using var sp1 = services.BuildServiceProvider();
        using var sp2 = services.BuildServiceProvider();

        using (AmbientServiceProvider.Enter(sp1))
        {
            AmbientServiceProvider.Current.Should().BeSameAs(sp1);
            using (AmbientServiceProvider.Enter(sp2))
            {
                AmbientServiceProvider.Current.Should().BeSameAs(sp2);
            }
            AmbientServiceProvider.Current.Should().BeSameAs(sp1,
                "inner dispose must restore the outer scope, not clear to null.");
        }
        AmbientServiceProvider.Current.Should().BeNull();
    }

    [Fact]
    public void Enter_WithNull_ClearsSlotForDuration()
    {
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();

        using (AmbientServiceProvider.Enter(sp))
        {
            AmbientServiceProvider.Current.Should().BeSameAs(sp);
            using (AmbientServiceProvider.Enter(null))
            {
                AmbientServiceProvider.Current.Should().BeNull(
                    "explicit null push replaces the current slot for the duration of the scope.");
            }
            AmbientServiceProvider.Current.Should().BeSameAs(sp);
        }
    }

    [Fact]
    public async Task Current_FlowsAcrossAwait()
    {
        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();

        using (AmbientServiceProvider.Enter(sp))
        {
            await Task.Yield();
            AmbientServiceProvider.Current.Should().BeSameAs(sp,
                "AsyncLocal must flow across await continuations.");
        }

        await Task.Yield();
        AmbientServiceProvider.Current.Should().BeNull();
    }

    [Fact]
    public void EnterIfUnset_PreservesOuterAmbient_WhenAlreadySet()
    {
        // Spec §S8-T04 review fix (constraint 4): "AsyncLocal<IServiceProvider> must be cleared
        // in finally to prevent leaks across requests." Corollary — the wrapper's push must NOT
        // clobber an ambient SP set by outer middleware or user code. EnterIfUnset only
        // installs the fallback when nothing is currently set.
        var services = new ServiceCollection();
        using var outer = services.BuildServiceProvider();
        using var fallback = services.BuildServiceProvider();

        using (AmbientServiceProvider.Enter(outer))
        {
            using (SmartMapp.Net.DependencyInjection.Internal.ServiceProviderAmbientAccessor.EnterIfUnset(fallback))
            {
                AmbientServiceProvider.Current.Should().BeSameAs(outer,
                    "when an outer ambient is already set, EnterIfUnset must not override it with the fallback.");
            }
            AmbientServiceProvider.Current.Should().BeSameAs(outer, "dispose restores — still the outer scope.");
        }
    }

    [Fact]
    public void EnterIfUnset_InstallsFallback_WhenNoOuterAmbient()
    {
        var services = new ServiceCollection();
        using var fallback = services.BuildServiceProvider();

        AmbientServiceProvider.Current.Should().BeNull();

        using (SmartMapp.Net.DependencyInjection.Internal.ServiceProviderAmbientAccessor.EnterIfUnset(fallback))
        {
            AmbientServiceProvider.Current.Should().BeSameAs(fallback,
                "with no outer ambient, EnterIfUnset installs the fallback provider.");
        }
        AmbientServiceProvider.Current.Should().BeNull();
    }

    [Fact]
    public async Task Current_IsIsolatedAcrossParallelTasks()
    {
        var services = new ServiceCollection();
        using var spA = services.BuildServiceProvider();
        using var spB = services.BuildServiceProvider();

        async Task<IServiceProvider?> ObserveAfterEnter(IServiceProvider sp)
        {
            using (AmbientServiceProvider.Enter(sp))
            {
                await Task.Delay(5);
                return AmbientServiceProvider.Current;
            }
        }

        var taskA = Task.Run(() => ObserveAfterEnter(spA));
        var taskB = Task.Run(() => ObserveAfterEnter(spB));

        var observedA = await taskA;
        var observedB = await taskB;

        observedA.Should().BeSameAs(spA);
        observedB.Should().BeSameAs(spB);
        AmbientServiceProvider.Current.Should().BeNull("outer context remains unaffected.");
    }
}
