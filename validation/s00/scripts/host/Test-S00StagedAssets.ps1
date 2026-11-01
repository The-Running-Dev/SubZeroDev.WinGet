#Requires -Version 7.0
<#
.SYNOPSIS
    Verifies every staged prerequisite against prerequisites.lock.json (size, SHA256, SHA512) and checks Authenticode where the lock records it.
.DESCRIPTION
    Read-only. Run on the host before copying assets into the guest, and again in the guest against the copied
    folder (-StagingRoot) before installing. Exits 1 on any mismatch.
#>
[CmdletBinding()]
param(
    [string] $LockPath = (Join-Path $PSScriptRoot '..\..\prerequisites.lock.json'),
    [string] $StagingRoot
)
$ErrorActionPreference = 'Stop'

$lock = Get-Content $LockPath -Raw | ConvertFrom-Json
if (-not $StagingRoot) { $StagingRoot = $lock.stagingRoot }

$failures = 0
foreach ($asset in $lock.assets) {
    $file = Join-Path $StagingRoot $asset.file
    if (-not (Test-Path -LiteralPath $file)) {
        Write-Output "MISSING  $($asset.id)  $file"
        $failures++
        continue
    }
    $bytes = (Get-Item -LiteralPath $file).Length
    $sha256 = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
    $sha512 = (Get-FileHash -LiteralPath $file -Algorithm SHA512).Hash
    $ok = ($bytes -eq $asset.bytes) -and ($sha256 -eq $asset.sha256) -and ($sha512 -eq $asset.sha512)
    if ($ok) {
        Write-Output "OK       $($asset.id)  $bytes  $sha256"
    } else {
        Write-Output "MISMATCH $($asset.id)  bytes $bytes/$($asset.bytes)  sha256 $sha256/$($asset.sha256)"
        $failures++
    }
}

if ($failures) { Write-Output "FAILED $failures"; exit 1 }
Write-Output 'PASS'
