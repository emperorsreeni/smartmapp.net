// SPDX-License-Identifier: MIT
using Xunit;

namespace SmartMapp.Net.Tests.Integration.AspNetCore;

/// <summary>
/// Serialises every <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/>-backed
/// integration test. The <c>StartupValidationTests.WebApplicationFactory_InvalidProfile…</c>
/// test mutates the process-wide <c>SMARTMAPP_SAMPLE_INVALID</c> environment variable that the
/// MinimalApi sample's <c>Program.cs</c> reads at <c>WebApplication.CreateBuilder</c> time.
/// Running the other endpoint tests in parallel would pick up the invalid profile mid-flight
/// and fail. xUnit collections run their members sequentially — no other mechanism is needed.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AspNetCoreCollection
{
    public const string Name = "AspNetCore (serialised)";
}
