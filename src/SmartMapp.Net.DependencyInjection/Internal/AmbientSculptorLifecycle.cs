// SPDX-License-Identifier: MIT
// <copyright file="AmbientSculptorLifecycle.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using SmartMapp.Net.Extensions;

namespace SmartMapp.Net.DependencyInjection.Internal;

/// <summary>
/// Singleton <see cref="IDisposable"/> that installs an ambient <see cref="ISculptor"/> via
/// <see cref="SculptorAmbient.Install"/> on construction and clears the slot on
/// <see cref="Dispose"/>. Registered by <c>AddSculptorCore</c> and resolved inside
/// <c>ResolveSculptor</c> on the first resolve of <see cref="ISculptor"/>; Microsoft DI
/// disposes it automatically when the owning <see cref="IServiceProvider"/> is disposed —
/// preventing cross-container / cross-test ambient bleed per spec §S8-T07 Constraints bullet 1.
/// </summary>
internal sealed class AmbientSculptorLifecycle : IDisposable
{
    private int _disposed;

    internal AmbientSculptorLifecycle(ISculptor sculptor)
    {
        if (sculptor is null) throw new ArgumentNullException(nameof(sculptor));
        SculptorAmbient.Install(sculptor);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            SculptorAmbient.Clear();
        }
    }
}
