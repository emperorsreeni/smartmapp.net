// SPDX-License-Identifier: MIT
// <copyright file="SculptorRegistrationState.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Sentinel singleton registered by
/// <see cref="Microsoft.Extensions.DependencyInjection.SculptorServiceCollectionExtensions.AddSculptor(Microsoft.Extensions.DependencyInjection.IServiceCollection)"/>
/// to detect double registration of the default (un-keyed) sculptor.
/// </summary>
/// <remarks>
/// Keyed sculptor registrations (<c>AddSculptor(string key, ...)</c>) arrive in Sprint 16 and
/// will not collide with this sentinel — each key will have its own distinct state instance.
/// </remarks>
internal sealed class SculptorRegistrationState
{
    /// <summary>
    /// Shared instance — all default-key registrations resolve to the same sentinel.
    /// </summary>
    internal static readonly SculptorRegistrationState Instance = new();

    private SculptorRegistrationState()
    {
    }
}
