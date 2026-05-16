// SPDX-License-Identifier: MIT
// <copyright file="ExternalCaller.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;

namespace SmartMapp.Net.Tests.ExternalAssemblyFixture;

/// <summary>
/// Trampoline type that invokes <see cref="SculptorServiceCollectionExtensions.AddSculptor(IServiceCollection)"/>
/// from a separate assembly so <see cref="System.Reflection.Assembly.GetCallingAssembly"/> can be
/// exercised against a distinct caller (see
/// <c>CallingAssemblyDetectionTests</c> in <c>SmartMapp.Net.Tests.Unit</c>).
/// </summary>
public static class ExternalCaller
{
    /// <summary>
    /// Invokes <see cref="SculptorServiceCollectionExtensions.AddSculptor(IServiceCollection)"/>
    /// from this assembly's context.
    /// </summary>
    /// <param name="services">The service collection to register the sculptor in.</param>
    /// <returns>The service collection, for fluent chaining.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static IServiceCollection CallAddSculptor(IServiceCollection services)
        => services.AddSculptor();
}
