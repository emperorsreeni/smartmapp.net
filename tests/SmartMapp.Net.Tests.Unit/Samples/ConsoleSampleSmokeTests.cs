using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace SmartMapp.Net.Tests.Unit.Samples;

/// <summary>
/// Sprint 8 · S8-T09 — black-box process smoke test for the Console sample. Spawns
/// <c>dotnet run --no-build</c> against the sample csproj and asserts the process exits with
/// code 0 and stdout contains the scenario headers the sample advertises. A single failure
/// here is enough to fail the CI smoke-test step and guard spec §S8-T09 Unit-Tests.
/// </summary>
/// <remarks>
/// Tests are marked as a xUnit <c>Category=Slow</c> trait because the spawn + restore path
/// takes several seconds even with <c>--no-build</c>. The sample project is wired into the
/// test csproj as a <c>ProjectReference</c> with <c>ReferenceOutputAssembly=false</c> so it
/// builds transitively before tests run.
/// </remarks>
public class ConsoleSampleSmokeTests
{
    /// <summary>
    /// The scenario headers the sample prints, in order. Every entry must appear verbatim on
    /// stdout for the smoke test to pass — this locks down both the section count (9 per spec
    /// acceptance) and the canonical scenario names.
    /// </summary>
    private static readonly string[] ExpectedHeaders =
    {
        "1. Zero-Config Flat Mapping",
        "2. Flattening (Customer.Address.City -> CustomerAddressCity)",
        "3. Collection Mapping (MapAll)",
        "4. Polymorphic / Inheritance-Aware Mapping",
        "5. Inline Bind (options.Bind<S, D>)",
        "6. Blueprint Class (reusable MappingBlueprint)",
        "7. Attribute-Based Configuration ([MappedBy<T>] + [Unmapped])",
        "8. MapTo<T>() Object Extension (ambient sculptor via DI)",
        "9. Bidirectional Mapping (.Bidirectional)",
        "=== All scenarios completed successfully. ===",
    };

    [Fact]
    [Trait("Category", "Slow")]
    public void ConsoleSample_DotnetRun_ExitsZero_AndPrintsAllScenarios()
    {
        var csproj = ResolveSampleCsproj();

        var startInfo = new ProcessStartInfo("dotnet")
        {
            // Match the current test assembly's TFM so --no-build finds the freshly-compiled
            // sample output without triggering a second framework's build.
            Arguments = $"run --no-build --project \"{csproj}\" --framework net10.0",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(csproj)!,
        };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 90s is generous: the sample itself runs in < 1s once built, but the first invocation
        // of `dotnet run --no-build` still performs MSBuild restore/dependency analysis.
        var exited = process.WaitForExit(TimeSpan.FromSeconds(90));
        if (!exited)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
        }

        // Flush async readers so the captured buffers include any late-arriving lines.
        process.WaitForExit();

        var output = stdout.ToString();
        var errors = stderr.ToString();

        exited.Should().BeTrue(
            "the sample must complete within 90 seconds; stdout:\n{0}\nstderr:\n{1}",
            output, errors);

        process.ExitCode.Should().Be(0,
            "spec §S8-T09 Acceptance bullet 1: the sample must exit with code 0; stdout:\n{0}\nstderr:\n{1}",
            output, errors);

        foreach (var header in ExpectedHeaders)
        {
            output.Should().Contain(header,
                "spec §S8-T09 Acceptance bullet 2: every scenario header must appear on stdout.");
        }

        // Program.cs only writes to stderr from the top-level catch block, so any stderr
        // content implies a scenario threw even if the SDK's own `dotnet run` wrapper swallowed
        // the non-zero exit. Ignore lines MSBuild/dotnet itself emits (warnings about SDK
        // versions, NuGet restore info) — only fail on the sample's own "!!! Sample failed"
        // sentinel so the guard remains stable across host tooling updates.
        errors.Should().NotContain("!!! Sample failed",
            "no scenario should have thrown; sample stderr:\n{0}", errors);
    }

    /// <summary>
    /// Walks up from the test assembly's output directory to the repository root and returns
    /// the absolute path to <c>samples/SmartMapp.Net.Samples.Console/SmartMapp.Net.Samples.Console.csproj</c>.
    /// </summary>
    private static string ResolveSampleCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0)
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("repository root (marked by *.slnx) must be discoverable from the test binary.");

        var csproj = Path.Combine(
            dir!.FullName,
            "samples",
            "SmartMapp.Net.Samples.Console",
            "SmartMapp.Net.Samples.Console.csproj");

        File.Exists(csproj).Should().BeTrue(
            "the sample csproj must exist at the expected path: {0}", csproj);

        return csproj;
    }
}
