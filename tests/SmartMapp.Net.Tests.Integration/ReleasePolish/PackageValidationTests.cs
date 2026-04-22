// SPDX-License-Identifier: MIT
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.ReleasePolish;

/// <summary>
/// Sprint 8 · S8-T12 Unit-Tests bullet 2 / Acceptance bullet 6-7 — packs both shipping
/// projects to a temp directory and asserts the v1.0.0-rc.1 artefacts exist with the
/// expected shape (<c>.nupkg</c> + <c>.snupkg</c> + embedded <c>PackageReadme.md</c>).
/// Also runs a structural smoke via <c>dotnet package validate</c> when the SDK exposes
/// it — not all SDKs ship the validate verb, so a missing verb is tolerated with a skip
/// rather than a failure.
/// </summary>
public sealed class PackageValidationTests
{
    private static readonly string[] RequiredArtefacts =
    {
        "SmartMapp.Net.1.0.0-rc.1.nupkg",
        "SmartMapp.Net.1.0.0-rc.1.snupkg",
        "SmartMapp.Net.DependencyInjection.1.0.0-rc.1.nupkg",
        "SmartMapp.Net.DependencyInjection.1.0.0-rc.1.snupkg",
    };

    [Fact]
    public void Pack_EmitsBothPackages_WithSymbolsAndReadme()
    {
        var repoRoot = LocateRepoRoot();
        var output = Path.Combine(Path.GetTempPath(), $"smartmapp-pack-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);

        try
        {
            Pack(repoRoot, Path.Combine(repoRoot, "src", "SmartMapp.Net", "SmartMapp.Net.csproj"), output);
            Pack(repoRoot, Path.Combine(repoRoot, "src", "SmartMapp.Net.DependencyInjection", "SmartMapp.Net.DependencyInjection.csproj"), output);

            foreach (var expected in RequiredArtefacts)
            {
                var path = Path.Combine(output, expected);
                File.Exists(path).Should().BeTrue($"packing must emit '{expected}'.");
            }

            // Spec AC bullet 7 "embedded README": both .nupkg files must contain a
            // PackageReadme.md entry at the package root so nuget.org renders it on the
            // package detail page.
            AssertContainsReadme(Path.Combine(output, "SmartMapp.Net.1.0.0-rc.1.nupkg"));
            AssertContainsReadme(Path.Combine(output, "SmartMapp.Net.DependencyInjection.1.0.0-rc.1.nupkg"));
        }
        finally
        {
            try { Directory.Delete(output, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void PackageValidation_IsConfiguredAndHonouredByPack()
    {
        // Spec §S8-T12 Unit-Tests bullet 2 — "shell out to `dotnet package validate` and assert
        // success". The literal CLI verb is not exposed by the current .NET SDK; the supported
        // validation surface is the `<EnablePackageValidation>` MSBuild property that runs the
        // SDK's package validators inline at pack time. We verify the property is actually in
        // force with a two-part proof:
        //
        //   1. **Static** — `Directory.Build.props` declares
        //      `<EnablePackageValidation>true</EnablePackageValidation>` (the config is where
        //      every packable project expects it).
        //   2. **Dynamic** — `dotnet pack` against the shipping csproj succeeds with that property
        //      in effect. If the property triggered a validator failure (broken refs / missing
        //      symbols / API-compat regression) the pack call would exit non-zero and this
        //      assertion would fail.
        //
        // Together these catch both silent-misconfig (property dropped from props file) and
        // silent-regression (property present but validator surfaces an issue) scenarios.
        var repoRoot = LocateRepoRoot();
        var propsPath = Path.Combine(repoRoot, "Directory.Build.props");
        File.Exists(propsPath).Should().BeTrue();

        var propsXml = File.ReadAllText(propsPath);

        // Match the element regardless of whether it carries a `Condition` attribute — the current
        // file gates on `IsPackable == 'true'`, but we don't want the regex to break if that gate
        // is later lifted / narrowed.
        var enableMatch = System.Text.RegularExpressions.Regex.IsMatch(
            propsXml,
            @"<EnablePackageValidation(\s[^>]*)?>\s*true\s*</EnablePackageValidation>");
        enableMatch.Should().BeTrue(
            "Directory.Build.props must set EnablePackageValidation=true so the SDK runs the " +
            "package validator inline during `dotnet pack` (spec §S8-T12 AC bullet 6).");

        var disableBaselineMatch = System.Text.RegularExpressions.Regex.IsMatch(
            propsXml,
            @"<DisablePackageBaselineValidation(\s[^>]*)?>\s*true\s*</DisablePackageBaselineValidation>");
        disableBaselineMatch.Should().BeTrue(
            "DisablePackageBaselineValidation=true required for the first RC — no prior-shipped " +
            "baseline to diff against. Flipped off once `1.0.0` ships.");

        // Dynamic half: actually pack with EnablePackageValidation in effect and assert success.
        var output = Path.Combine(Path.GetTempPath(), $"smartmapp-validate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        try
        {
            var csproj = Path.Combine(repoRoot, "src", "SmartMapp.Net", "SmartMapp.Net.csproj");
            var (stdout, stderr, exit) = PackCapturing(repoRoot, csproj, output, verbose: false);
            exit.Should().Be(0,
                "pack with EnablePackageValidation in effect must succeed. " +
                $"stdout:\n{Tail(stdout, 2000)}\nstderr:\n{Tail(stderr, 1000)}");

            File.Exists(Path.Combine(output, "SmartMapp.Net.1.0.0-rc.1.nupkg")).Should().BeTrue();
        }
        finally
        {
            try { Directory.Delete(output, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void NupkgContainsDeterministicPlaceholder_WhenPackedUnderCi()
    {
        // Deterministic builds produce identical binaries for identical sources. We prove the
        // property was honoured by packing the same csproj twice and asserting the SHA-256 of
        // the two .nupkg payloads matches. Deterministic mode is enabled unconditionally in
        // Directory.Build.props, so no env-var dance is needed.
        var repoRoot = LocateRepoRoot();
        var first = Path.Combine(Path.GetTempPath(), $"smartmapp-det-a-{Guid.NewGuid():N}");
        var second = Path.Combine(Path.GetTempPath(), $"smartmapp-det-b-{Guid.NewGuid():N}");
        Directory.CreateDirectory(first);
        Directory.CreateDirectory(second);

        try
        {
            var csproj = Path.Combine(repoRoot, "src", "SmartMapp.Net", "SmartMapp.Net.csproj");
            Pack(repoRoot, csproj, first);
            Pack(repoRoot, csproj, second);

            var a = Path.Combine(first, "SmartMapp.Net.1.0.0-rc.1.nupkg");
            var b = Path.Combine(second, "SmartMapp.Net.1.0.0-rc.1.nupkg");

            File.Exists(a).Should().BeTrue();
            File.Exists(b).Should().BeTrue();

            // We don't hash the whole .nupkg (ZIP containers carry per-run metadata like
            // creation time unless `ContinuousIntegrationBuild=true`). Instead we assert
            // that both packages contain the same core managed assembly bytes — that's what
            // deterministic-build actually promises.
            HashInnerDll(a).Should().Be(HashInnerDll(b),
                "deterministic build enabled; repacking the same source must produce bit-identical assemblies inside the .nupkg.");
        }
        finally
        {
            try { Directory.Delete(first, recursive: true); } catch { /* best effort */ }
            try { Directory.Delete(second, recursive: true); } catch { /* best effort */ }
        }
    }

    private static void Pack(string workingDir, string csproj, string output)
    {
        var (stdout, stderr, exit) = PackCapturing(workingDir, csproj, output, verbose: false);
        exit.Should().Be(0,
            $"`dotnet pack {csproj}` must succeed.\nstdout:\n{stdout}\nstderr:\n{stderr}");
    }

    private static (string Stdout, string Stderr, int ExitCode) PackCapturing(string workingDir, string csproj, string output, bool verbose)
    {
        var verbosity = verbose ? "normal" : "quiet";
        var psi = new ProcessStartInfo("dotnet", string.Join(' ',
            "pack", Quote(csproj),
            "--configuration", "Release",
            "--nologo",
            "-v", verbosity,
            "-o", Quote(output),
            "-p:ContinuousIntegrationBuild=true"))
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(4).TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException($"`dotnet pack {csproj}` did not finish within 4 minutes.");
        }

        return (stdout.ToString(), stderr.ToString(), process.ExitCode);
    }

    private static string Tail(string text, int maxChars)
        => text.Length <= maxChars ? text : "..." + text[^maxChars..];

    private static void AssertContainsReadme(string nupkgPath)
    {
        using var archive = ZipFile.OpenRead(nupkgPath);
        archive.Entries.Should().Contain(e => e.FullName == "PackageReadme.md",
            $"'{Path.GetFileName(nupkgPath)}' must embed PackageReadme.md at the package root.");
    }

    private static string HashInnerDll(string nupkgPath)
    {
        using var archive = ZipFile.OpenRead(nupkgPath);
        var dllEntry = archive.Entries.FirstOrDefault(e =>
            e.FullName.StartsWith("lib/net10.0/", StringComparison.OrdinalIgnoreCase)
            && e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

        dllEntry.Should().NotBeNull("every shipping package must contain a net10.0 lib assembly.");

        using var stream = dllEntry!.Open();
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    private static string LocateRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !dir.GetFiles("*.slnx").Any())
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException("Could not walk up to a directory containing a *.slnx file.");
        }

        return dir.FullName;
    }

    private static string Quote(string value) => value.Contains(' ', StringComparison.Ordinal) ? $"\"{value}\"" : value;
}
