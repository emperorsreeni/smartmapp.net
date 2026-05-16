#!/usr/bin/env pwsh
# Sprint 8 · S8-T12 — consolidates the six BenchmarkDotNet per-class JSON reports under
# benchmarks/results/raw/results/ into a single sprint-8-baseline.json file committed at
# benchmarks/results/sprint-8-baseline.json. The shape is minimal and stable so
# compare-benchmarks.ps1 can diff PR-run numbers against the committed baseline without
# parsing the full BDN-internal schema (which carries machine-specific confidence intervals
# / percentiles unnecessary for regression gating).
#
# Usage:
#   pwsh benchmarks/scripts/consolidate-baseline.ps1 `
#        -RawDir benchmarks/results/raw/results `
#        -OutFile benchmarks/results/sprint-8-baseline.json

[CmdletBinding()]
param(
    [string]$RawDir = "benchmarks/results/raw/results",
    [string]$OutFile = "benchmarks/results/sprint-8-baseline.json"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $RawDir))
{
    throw "Raw results directory not found: $RawDir. Run 'dotnet run --project benchmarks/SmartMapp.Net.Benchmarks --framework net10.0 --configuration Release --no-build -- --filter *Sprint8* --job short --exporters json --artifacts benchmarks/results/raw' first."
}

$entries = [System.Collections.Generic.List[object]]::new()
$reports = Get-ChildItem -Path $RawDir -Filter '*Sprint8*-report-full-compressed.json' -File

if ($reports.Count -eq 0)
{
    throw "No Sprint 8 report JSON files found in '$RawDir'."
}

foreach ($report in $reports)
{
    $payload = Get-Content $report.FullName -Raw | ConvertFrom-Json

    foreach ($bench in $payload.Benchmarks)
    {
        $stats = $bench.Statistics
        $mem = $bench.Memory

        $entries.Add([pscustomobject]@{
                id            = $bench.FullName
                type          = $bench.Type
                method        = $bench.Method
                parameters    = $bench.Parameters
                meanNs        = [math]::Round($stats.Mean, 2)
                stdDevNs      = [math]::Round($stats.StandardDeviation, 2)
                medianNs      = [math]::Round($stats.Median, 2)
                minNs         = [math]::Round($stats.Min, 2)
                maxNs         = [math]::Round($stats.Max, 2)
                allocatedBytes = if ($null -ne $mem) { $mem.BytesAllocatedPerOperation } else { 0 }
            }) | Out-Null
    }
}

# Each benchmark emits two entries when both a [SimpleJob(...)] attribute and CLI `--job short`
# are present (BDN treats them as distinct configurations). Dedupe on the (id, parameters) pair,
# keeping the smaller mean — the hotter-cache measurement — so the baseline tracks the best
# steady-state number rather than being skewed by the slower of the two harnesses.
$deduped = $entries |
    Group-Object -Property { "$($_.id)|$($_.parameters)" } |
    ForEach-Object { $_.Group | Sort-Object -Property meanNs | Select-Object -First 1 }

$sortedEntries = $deduped | Sort-Object -Property id, parameters
$hostInfo = (Get-Content $reports[0].FullName -Raw | ConvertFrom-Json).HostEnvironmentInfo

$baseline = [pscustomobject]@{
    sprint     = 'Sprint 8 - v1.0.0-rc.1'
    generatedAt = (Get-Date).ToUniversalTime().ToString('u')
    host       = [pscustomobject]@{
        bdnVersion     = $hostInfo.BenchmarkDotNetVersion
        os             = $hostInfo.OsVersion
        runtime        = $hostInfo.RuntimeVersion
        architecture   = $hostInfo.Architecture
        configuration  = $hostInfo.Configuration
    }
    regressionTolerancePct = 10
    benchmarks = $sortedEntries
}

$outDir = Split-Path -Parent $OutFile
if ($outDir -and -not (Test-Path $outDir))
{
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

# Write as UTF-8 without BOM so diffs stay clean and cross-platform tooling (jq, node)
# handles the file without the 0xEF 0xBB 0xBF prefix. Windows PowerShell 5.1 doesn't support
# the null-conditional `?.` / `??` operators, so resolve the absolute path manually.
$json = $baseline | ConvertTo-Json -Depth 6
$absolutePath = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path (Get-Location) $OutFile }
[System.IO.File]::WriteAllText($absolutePath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Consolidated baseline written to: $OutFile"
Write-Host "Benchmark entries: $($sortedEntries.Count)"
