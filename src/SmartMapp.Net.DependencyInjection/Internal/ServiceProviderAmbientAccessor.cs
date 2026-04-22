// SPDX-License-Identifier: MIT
// <copyright file="ServiceProviderAmbientAccessor.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using SmartMapp.Net.Runtime;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Thin DI-package facade over <see cref="AmbientServiceProvider"/>. Callers push the current
/// request's <see cref="IServiceProvider"/> around every <c>Map</c> invocation using a
/// <c>using</c> statement — the core runtime then picks it up via
/// <see cref="MappingExecutor.CreateScope"/> when the caller did not supply an explicit
/// provider.
/// </summary>
/// <remarks>
/// <para>
/// Spec §S8-T04 Constraints bullet 4: "<c>IMappingScopeFactory</c> acquires the current
/// <see cref="IServiceProvider"/> via a cached <c>AsyncLocal&lt;IServiceProvider?&gt;</c> set
/// inside the sculptor's DI wrapper (cleared in <c>finally</c>)." This accessor is that
/// bridge — implemented once here so <see cref="DependencyInjectionMapper{TOrigin, TTarget}"/>,
/// <see cref="DependencyInjectionSculptor"/>, and any future DI-resolved entry points share
/// the same push/pop semantics.
/// </para>
/// </remarks>
internal static class ServiceProviderAmbientAccessor
{
    /// <summary>
    /// Pushes <paramref name="serviceProvider"/> as the ambient
    /// <see cref="AmbientServiceProvider.Current"/> and returns a token that restores the
    /// previous value on disposal. Safe to nest.
    /// </summary>
    /// <param name="serviceProvider">
    /// The <see cref="IServiceProvider"/> to install for the duration of the mapping call.
    /// When <c>null</c>, the accessor leaves the ambient slot untouched so callers that run
    /// outside DI do not accidentally clobber an already-installed ambient provider set by
    /// an enclosing scope.
    /// </param>
    internal static AmbientServiceProvider.Scope Enter(IServiceProvider? serviceProvider)
        => AmbientServiceProvider.Enter(serviceProvider);

    /// <summary>
    /// Pushes <paramref name="serviceProvider"/> only when no ambient is currently installed,
    /// preserving any outer scope set by middleware or user code (spec §S8-T04 constraint 4).
    /// Returns a token whose <see cref="IDisposable.Dispose"/> is a no-op when the push was
    /// skipped.
    /// </summary>
    /// <param name="serviceProvider">The fallback <see cref="IServiceProvider"/>.</param>
    internal static AmbientServiceProvider.Scope EnterIfUnset(IServiceProvider? serviceProvider)
    {
        if (AmbientServiceProvider.Current is not null)
        {
            // Outer code (e.g. ASP.NET middleware pushing HttpContext.RequestServices) already
            // established a scope — do not clobber it. Return a no-op token whose Dispose
            // restores the same value.
            return AmbientServiceProvider.Enter(AmbientServiceProvider.Current);
        }
        return AmbientServiceProvider.Enter(serviceProvider);
    }
}
