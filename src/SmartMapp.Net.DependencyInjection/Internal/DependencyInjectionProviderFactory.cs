// SPDX-License-Identifier: MIT
// <copyright file="DependencyInjectionProviderFactory.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// <see cref="IProviderResolver"/> implementation that prefers DI-registered services over
/// <see cref="Activator.CreateInstance(Type)"/> and uses
/// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/> for rich
/// constructor injection when a type is not registered in the container.
/// </summary>
/// <remarks>
/// <para>
/// Installed on <c>SculptorOptions.ProviderResolver</c> by <c>AddSculptorCore</c> so that
/// <c>p.From&lt;TaxCalculatorProvider&gt;()</c> and <c>p.TransformWith&lt;MoneyFormatter&gt;()</c>
/// resolve their concrete instances through the container at mapping time — per spec §11.4 /
/// Sprint 8 · S8-T04.
/// </para>
/// <para>
/// The factory captures the <em>root</em> <see cref="IServiceProvider"/> once at registration
/// so transformer resolution during <see cref="SculptorBuilder.Forge"/> can still satisfy
/// singleton / root-scoped constructor dependencies even though no request scope is active.
/// When <see cref="IProviderResolver.Resolve"/> is called at mapping time the supplied
/// service provider argument overrides the captured root so request-scoped services
/// (<c>DbContext</c>, <c>ILogger&lt;T&gt;</c>, scoped caches) flow correctly into
/// <see cref="IValueProvider"/> instances.
/// </para>
/// </remarks>
internal sealed class DependencyInjectionProviderFactory : IProviderResolver
{
    /// <summary>
    /// <see cref="TraceSource"/> consulted by <see cref="EmitActivatorFallbackWarningOnce"/>
    /// so users can opt-in to diagnostics via standard .NET trace configuration without
    /// forcing a <c>Microsoft.Extensions.Logging</c> dependency on this package (full
    /// <see cref="Microsoft.Extensions.Logging.ILogger"/> integration lands in S8-T05).
    /// </summary>
    internal static readonly TraceSource TraceSource = new("SmartMapp.Net.DependencyInjection");

    private static readonly ConcurrentDictionary<Type, byte> WarnedTypes = new();
    private readonly IServiceProvider _rootServiceProvider;

    /// <summary>
    /// Initializes a new factory bound to the supplied root provider.
    /// </summary>
    /// <param name="rootServiceProvider">
    /// The application's root <see cref="IServiceProvider"/>. Used as a fallback when
    /// <see cref="Resolve"/> is invoked with a <c>null</c> ambient scope (e.g. transformer
    /// resolution during forge).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when the supplied service provider is <c>null</c>.</exception>
    internal DependencyInjectionProviderFactory(IServiceProvider rootServiceProvider)
    {
        _rootServiceProvider = rootServiceProvider ?? throw new ArgumentNullException(nameof(rootServiceProvider));
    }

    /// <inheritdoc />
    public object Resolve(Type type, IServiceProvider? serviceProvider)
    {
        if (type is null) throw new ArgumentNullException(nameof(type));

        // Ambient scope takes priority — carries the request's scoped services (DbContext, …).
        // Fall back to the captured root when the resolver is invoked outside any scope
        // (builder-only / forge-time transformer resolution).
        var sp = serviceProvider ?? _rootServiceProvider;

        // 1. DI-registered hit — respects the container's configured lifetime.
        var registered = sp.GetService(type);
        if (registered is not null) return registered;

        // 2. Unregistered type: use ActivatorUtilities so constructor dependencies can be
        //    satisfied from DI even though the type itself has no registration. This is the
        //    key improvement over DefaultProviderResolver, which only handles parameterless
        //    constructors.
        if (type.IsAbstract || type.IsInterface)
        {
            throw new InvalidOperationException(
                $"Cannot instantiate '{type.FullName}': type is abstract or an interface and no DI registration was found. " +
                $"Register a concrete implementation via services.AddTransient<{type.Name}, MyImpl>().");
        }

        try
        {
            var instance = ActivatorUtilities.CreateInstance(sp, type);
            EmitActivatorFallbackWarningOnce(type, sp);
            return instance;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"ActivatorUtilities.CreateInstance failed for '{type.FullName}'. " +
                "Ensure all constructor dependencies are registered in DI, or register the type itself " +
                $"via services.AddTransient<{type.Name}>(). See inner exception for the underlying failure.",
                ex);
        }
    }

    /// <summary>
    /// Emits a one-shot <see cref="TraceEventType.Warning"/> when a type with at least one
    /// DI-registered constructor dependency is activated via <c>ActivatorUtilities</c> rather
    /// than a direct DI registration — the scenario spec §S8-T04 Technical Considerations
    /// bullet 4 flags as "likely misconfiguration". Silent for types with no registered ctor
    /// deps (purely parameterless or non-DI construction) since those cases are well-defined.
    /// </summary>
    private static void EmitActivatorFallbackWarningOnce(Type type, IServiceProvider sp)
    {
        if (!WarnedTypes.TryAdd(type, 0)) return;

        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
        if (ctor is null) return;

        var parameters = ctor.GetParameters();
        if (parameters.Length == 0) return; // Parameterless ctor — no misconfiguration suspected.

        var hasRegisteredDep = false;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (sp.GetService(parameters[i].ParameterType) is not null)
            {
                hasRegisteredDep = true;
                break;
            }
        }

        if (!hasRegisteredDep) return;

        TraceSource.TraceEvent(
            TraceEventType.Warning,
            id: 1,
            format: "SmartMapp.Net.DependencyInjection: type '{0}' was activated via ActivatorUtilities.CreateInstance " +
                   "because it is not registered in DI, but at least one of its constructor dependencies IS registered. " +
                   "This is likely a misconfiguration — register the type explicitly via services.AddTransient<{1}>() to " +
                   "integrate it with container lifecycle management (spec §S8-T04 Technical Considerations).",
            args: new object[] { type.FullName ?? type.Name, type.Name });
    }
}
