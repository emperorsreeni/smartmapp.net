// SPDX-License-Identifier: MIT
// <copyright file="SculptorBuilderFactory.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Reflection;
using SmartMapp.Net.Configuration;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Composes a <see cref="SculptorBuilder"/> from a pre-captured assembly list and an optional
/// <see cref="SculptorOptions"/> configuration callback. Invoked by
/// <see cref="ForgedSculptorHost"/> on the first resolve of <see cref="ISculptor"/>.
/// </summary>
/// <remarks>
/// <para>
/// The factory is deliberately stateless with respect to the produced builder — every call to
/// <see cref="Build"/> yields a fresh <see cref="SculptorBuilder"/> / <see cref="SculptorOptions"/>
/// pair. <see cref="ForgedSculptorHost"/> guarantees that <see cref="Build"/> is invoked at most
/// once per host instance by wrapping it in a <see cref="Lazy{T}"/>.
/// </para>
/// <para>
/// Sprint 8 S8-T01 exposes only the assemblies surface. Sprint 8 S8-T02 adds the
/// <see cref="Action{T}"/> configuration callback used by the <c>AddSculptor(options =&gt; ...)</c>
/// overload. The factory already accepts the callback parameter so T02 can wire through without
/// further plumbing.
/// </para>
/// </remarks>
internal sealed class SculptorBuilderFactory
{
    private readonly IReadOnlyList<Assembly> _assemblies;
    private readonly Action<SculptorOptions>? _configure;

    /// <summary>
    /// Initializes a new <see cref="SculptorBuilderFactory"/>.
    /// </summary>
    /// <param name="assemblies">
    /// Assemblies to scan for blueprints, providers, and transformers. Must not be <c>null</c>.
    /// An empty list is permitted — the forged sculptor will contain zero blueprints, useful
    /// for tests and bootstrap scenarios.
    /// </param>
    /// <param name="configure">
    /// Optional options-customisation callback invoked during <see cref="Build"/> after the
    /// assemblies have been queued. Reserved for S8-T02.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="assemblies"/> is <c>null</c>.
    /// </exception>
    internal SculptorBuilderFactory(
        IReadOnlyList<Assembly> assemblies,
        Action<SculptorOptions>? configure = null)
    {
        _assemblies = assemblies ?? throw new ArgumentNullException(nameof(assemblies));
        _configure = configure;
    }

    /// <summary>
    /// Gets the assemblies the factory will queue on every <see cref="Build"/> call.
    /// </summary>
    internal IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>
    /// Gets a value indicating whether a configuration callback is attached.
    /// </summary>
    internal bool HasConfigure => _configure is not null;

    /// <summary>
    /// Produces a fresh <see cref="SculptorBuilder"/> pre-populated with the optional options
    /// callback and — when the callback did not scan any assemblies — the captured default
    /// assembly set (typically the calling assembly of <c>AddSculptor()</c>).
    /// </summary>
    /// <remarks>
    /// Per spec §11.1 / S8-T02 acceptance: "Combining an options callback with
    /// <c>ScanAssembliesContaining&lt;T&gt;()</c> overrides calling-assembly default." The
    /// configure callback therefore runs <b>before</b> the default fallback so that any explicit
    /// <c>options.ScanAssemblies</c> / <c>options.ScanAssembliesContaining&lt;T&gt;()</c> call
    /// wins and the calling-assembly default is only applied when the user supplied none.
    /// </remarks>
    /// <returns>A ready-to-<c>Forge()</c> builder.</returns>
    internal SculptorBuilder Build()
    {
        var options = new SculptorOptions();

        if (_configure is not null)
        {
            // Per spec §S8-T02 unit-tests: "Options callback exceptions surface at resolve time
            // wrapped in InvalidOperationException". The wrapper keeps the public exception
            // type stable for callers regardless of what the user callback throws, while the
            // original exception is preserved in InnerException for diagnostics.
            try
            {
                _configure(options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The SculptorOptions configuration callback supplied to AddSculptor(...) " +
                    "threw an exception. See the inner exception for the underlying cause.",
                    ex);
            }
        }

        // Fallback: if the configure callback did not scan any assemblies explicitly, apply the
        // captured default set. This preserves the calling-assembly scan for the
        // zero-configuration overloads while honouring explicit scans in the configure form.
        if (options.Assemblies.Count == 0 && _assemblies.Count > 0)
        {
            var array = new Assembly[_assemblies.Count];
            for (var i = 0; i < _assemblies.Count; i++) array[i] = _assemblies[i];
            options.ScanAssemblies(array);
        }

        return new SculptorBuilder(options);
    }
}
