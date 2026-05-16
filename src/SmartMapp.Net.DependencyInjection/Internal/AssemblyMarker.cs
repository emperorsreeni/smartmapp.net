// SPDX-License-Identifier: MIT
// <copyright file="AssemblyMarker.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net.Abstractions;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Internal marker type used to obtain a reference to the
/// <c>SmartMapp.Net.DependencyInjection</c> assembly without forcing a
/// public surface during package scaffolding.
/// </summary>
/// <remarks>
/// The type also anchors references to the two assemblies this package
/// layers on top of — <c>SmartMapp.Net</c> and
/// <c>Microsoft.Extensions.DependencyInjection.Abstractions</c> — so the
/// compiler retains them in the assembly manifest even while Sprint 8 is
/// still scaffolding (no public API yet). This type will be removed or
/// replaced once <c>AddSculptor()</c> lands in S8-T01; consumers must not
/// depend on it.
/// </remarks>
internal static class AssemblyMarker
{
    /// <summary>
    /// Anchors a reference to <see cref="ISculptor"/> from the
    /// <c>SmartMapp.Net</c> core assembly.
    /// </summary>
    internal static readonly Type CoreMarkerType = typeof(ISculptor);

    /// <summary>
    /// Anchors a reference to <see cref="IServiceCollection"/> from
    /// <c>Microsoft.Extensions.DependencyInjection.Abstractions</c>.
    /// </summary>
    internal static readonly Type ServiceCollectionType = typeof(IServiceCollection);
}
