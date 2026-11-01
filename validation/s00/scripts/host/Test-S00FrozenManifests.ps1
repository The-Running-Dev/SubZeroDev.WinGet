#Requires -Version 7.0
<#
.SYNOPSIS
    Verifies the frozen winget-pkgs manifests are byte-identical to the upstream blobs recorded in the package profiles.
.DESCRIPTION
    Computes the git blob id of each file without filters (git hash-object --no-filters) and compares it with
    profiles/*.profile.json. Exits 1 on any mismatch or missing file. Read-only.

    -Live is the pre-arm drift check (05-run-reset-procedure.md): it also lists the manifest directory on the current
    microsoft/winget-pkgs default branch through `gh api` and requires exactly the frozen file set with the frozen
    blob ids. Any added, missing or changed file blocks arming. The CDN index the module reads can still differ
    from GitHub (04-worker-mapping.md, Drift detection).
#>
[CmdletBinding()]
param(
    [string] $Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch] $Live
)
$ErrorActionPreference = 'Stop'

$failures = 0
foreach ($profilePath in Get-ChildItem (Join-Path $Root 'profiles') -Filter '*.profile.json') {
    $prof = Get-Content $profilePath.FullName -Raw | ConvertFrom-Json
    $frozen = $prof.frozenManifests
    $dir = Join-Path $Root "manifests\winget-pkgs-$($frozen.commit.Substring(0, 12))\$($frozen.path)"
    foreach ($entry in $frozen.blobs.PSObject.Properties) {
        $file = Join-Path $dir $entry.Name
        if (-not (Test-Path -LiteralPath $file)) {
            Write-Output "MISSING  $($prof.profileId)  $($entry.Name)"
            $failures++
            continue
        }
        $actual = (& git hash-object --no-filters -- $file).Trim()
        if ($LASTEXITCODE -ne 0) { throw "git hash-object failed for $file" }
        if ($actual -eq $entry.Value) {
            Write-Output "OK       $($prof.profileId)  $($entry.Name)  $actual"
        } else {
            Write-Output "MISMATCH $($prof.profileId)  $($entry.Name)  expected $($entry.Value) actual $actual"
            $failures++
        }
    }
    if ($Live) {
        $listing = & gh api "repos/microsoft/winget-pkgs/contents/$($frozen.path)" --jq '.[] | select(.type == "file") | "\(.name) \(.sha)"'
        if ($LASTEXITCODE -ne 0) { throw "gh api failed for $($frozen.path)" }
        $upstream = @{}
        foreach ($line in $listing) { $name, $sha = $line -split ' ', 2; $upstream[$name] = $sha }
        foreach ($entry in $frozen.blobs.PSObject.Properties) {
            if ($upstream[$entry.Name] -eq $entry.Value) {
                Write-Output "LIVE OK  $($prof.profileId)  $($entry.Name)  $($entry.Value)"
            } else {
                Write-Output "LIVE MISMATCH $($prof.profileId)  $($entry.Name)  frozen $($entry.Value) upstream $($upstream[$entry.Name])"
                $failures++
            }
        }
        foreach ($name in $upstream.Keys | Where-Object { $_ -notin $frozen.blobs.PSObject.Properties.Name }) {
            Write-Output "LIVE EXTRA $($prof.profileId)  $name  $($upstream[$name])"
            $failures++
        }
    }
}

if ($failures) { Write-Output "FAILED $failures"; exit 1 }
Write-Output 'PASS'
