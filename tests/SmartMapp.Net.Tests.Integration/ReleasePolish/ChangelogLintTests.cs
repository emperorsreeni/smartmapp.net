// SPDX-License-Identifier: MIT
using FluentAssertions;
using Xunit;

namespace SmartMapp.Net.Tests.Integration.ReleasePolish;

/// <summary>
/// Sprint 8 · S8-T12 Unit-Tests bullet 3 — asserts <c>CHANGELOG.md</c> has a Sprint 8 /
/// 1.0.0-rc.1 section and at least one entry per shipped Sprint 8 task. Prevents the
/// `Sprint N+1` PR from merging while the Sprint 8 section is still empty or drafted.
/// </summary>
public sealed class ChangelogLintTests
{
    private static readonly string[] Sprint8Markers =
    {
        // At least one mention for every shipping task — the entries may be nested under
        // "Added" / "Changed" / "Fixed" so we do not require the marker at the start of a line.
        "S8-T00", "S8-T01", "S8-T02", "S8-T03", "S8-T04", "S8-T05",
        "S8-T06", "S8-T07", "S8-T08", "S8-T09", "S8-T10", "S8-T11", "S8-T12",
    };

    [Fact]
    public void Changelog_HasSprint8Section_WithRcVersionAndAllTaskMarkers()
    {
        var path = Path.Combine(LocateRepoRoot(), "CHANGELOG.md");
        File.Exists(path).Should().BeTrue();

        var text = File.ReadAllText(path);

        text.Should().Contain("1.0.0-rc.1",
            "the Sprint 8 release candidate section must declare the 1.0.0-rc.1 version.");
        text.Should().Contain("Sprint 8",
            "the changelog must contain at least one Sprint 8 section header.");

        var missing = Sprint8Markers
            .Where(marker => !text.Contains(marker, StringComparison.Ordinal))
            .ToList();

        missing.Should().BeEmpty(
            "every Sprint 8 task marker must appear in CHANGELOG.md. Missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void Changelog_HasAddedSectionUnderRcRelease()
    {
        var path = Path.Combine(LocateRepoRoot(), "CHANGELOG.md");
        var lines = File.ReadAllLines(path);

        // Walk forward from the first "[1.0.0-rc.1]" header (or [Unreleased] if RC header not
        // yet finalised) until the next top-level "[...]" heading; assert the slice contains a
        // "### Added" subsection per Keep a Changelog conventions.
        var startIndex = Array.FindIndex(lines, l =>
            l.Contains("[1.0.0-rc.1]", StringComparison.Ordinal)
            || l.Contains("[Unreleased]", StringComparison.Ordinal));
        startIndex.Should().BeGreaterThan(-1, "CHANGELOG must contain an [Unreleased] or [1.0.0-rc.1] section header.");

        var slice = new List<string>();
        for (var i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## [", StringComparison.Ordinal)) break;
            slice.Add(lines[i]);
        }

        slice.Any(l => l.TrimStart().StartsWith("### Added", StringComparison.Ordinal))
            .Should().BeTrue("the Sprint 8 / RC section must have at least an `### Added` subsection.");
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
}
