#!/usr/bin/env pwsh
# Sprint 8 · S8-T12 — benchmark regression gate. Consumes the committed baseline
# (`benchmarks/results/sprint-8-baseline.json`) and a PR-run consolidated JSON and fails
# the build if any benchmark's `meanNs` exceeds the baseline by more than the declared
# tolerance (default 10%, per spec §S8-T12 AC bullet 3: "CI pipeline fails on > 10%
# regression vs sprint-8-baseline.json").
#
# Usage:
#   pwsh benchmarks/scripts/compare-benchmarks.ps1 `
#        -Baseline benchmarks/results/sprint-8-baseline.json `
#        -Current benchmarks/results/pr-run.json
#
# Exit code: 0 on pass, 1 on regression, 2 on load / shape errors.

[CmdletBinding()]
param(
    [string]$Baseline = "benchmarks/results/sprint-8-baseline.json",
    [string]$Current  = "benchmarks/results/current.json",
    [double]$TolerancePct = 10.0
)

$ErrorActionPreference = 'Stop'

function Load-Baseline([string]$path)
{
    if (-not (Test-Path $path)) { throw "Benchmark JSON not found: $path" }
    return Get-Content $path -Raw | ConvertFrom-Json
}

try
{
    $baselineDoc = Load-Baseline $Baseline
    $currentDoc  = Load-Baseline $Current
}
catch
{
    Write-Error "Could not load benchmark JSON: $_"
    exit 2
}

# Prefer the baseline's declared tolerance over the parameter default.
if ($null -ne $baselineDoc.regressionTolerancePct)
{
    $TolerancePct = [double]$baselineDoc.regressionTolerancePct
}

# Key current-run entries by id+parameters for O(1) lookup during the compare walk.
$currentIndex = @{}
foreach ($entry in $currentDoc.benchmarks)
{
    $key = "$($entry.id)|$($entry.parameters)"
    $currentIndex[$key] = $entry
}

$regressions = [System.Collections.Generic.List[object]]::new()
$missing     = [System.Collections.Generic.List[string]]::new()
$improved    = [System.Collections.Generic.List[object]]::new()

foreach ($base in $baselineDoc.benchmarks)
{
    $key = "$($base.id)|$($base.parameters)"
    if (-not $currentIndex.ContainsKey($key))
    {
        $missing.Add($key) | Out-Null
        continue
    }

    $curr = $currentIndex[$key]
    if ([double]$base.meanNs -le 0) { continue }    # skip degenerate baselines
    if ([double]$curr.meanNs -le 0) { continue }

    $deltaPct = (([double]$curr.meanNs - [double]$base.meanNs) / [double]$base.meanNs) * 100.0

    if ($deltaPct -gt $TolerancePct)
    {
        $regressions.Add([pscustomobject]@{
                id       = $base.id
                params   = $base.parameters
                baseline = $base.meanNs
                current  = $curr.meanNs
                deltaPct = [math]::Round($deltaPct, 2)
            }) | Out-Null
    }
    elseif ($deltaPct -lt -5)   # track notable speedups purely for CI-log signal.
    {
        $improved.Add([pscustomobject]@{
                id       = $base.id
                baseline = $base.meanNs
                current  = $curr.meanNs
                deltaPct = [math]::Round($deltaPct, 2)
            }) | Out-Null
    }
}

Write-Host "Regression tolerance: +$TolerancePct %"
Write-Host "Compared $($baselineDoc.benchmarks.Count) baseline entries against $($currentDoc.benchmarks.Count) current entries."

if ($improved.Count -gt 0)
{
    Write-Host ""
    Write-Host "Notable improvements (> 5% faster):"
    foreach ($i in $improved)
    {
        Write-Host ("  {0}: {1:N2} ns -> {2:N2} ns ({3:N2}%)" -f $i.id, $i.baseline, $i.current, $i.deltaPct)
    }
}

if ($missing.Count -gt 0)
{
    Write-Host ""
    Write-Warning "Baseline entries missing from current run (treated as non-fatal):"
    foreach ($m in $missing) { Write-Warning "  $m" }
}

if ($regressions.Count -eq 0)
{
    Write-Host ""
    Write-Host "OK: no benchmark regressed by more than $TolerancePct % vs baseline."
    exit 0
}

Write-Host ""
Write-Error "FAIL: $($regressions.Count) benchmark regression(s) exceeded the +$TolerancePct % tolerance:"
foreach ($r in $regressions)
{
    Write-Error ("  {0}[{1}]: {2:N2} ns -> {3:N2} ns (+{4:N2}%)" -f $r.id, $r.params, $r.baseline, $r.current, $r.deltaPct)
}
exit 1
