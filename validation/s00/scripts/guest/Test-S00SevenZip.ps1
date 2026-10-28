#Requires -Version 5.1
<#
.SYNOPSIS
    Independent machine-scope verifier for profile s02-7zip-26.01-x64-exe-machine. Read-only; never calls WinGet.
.DESCRIPTION
    PREPARED, NOT EXECUTED.
    -Expect Absent : checkpoint/pre-case state.
    -Expect Present: HKLM ARP "7-Zip" 26.01 in the 64-bit view, binaries in %ProgramFiles%\7-Zip, no per-user ARP
                     entry in any loaded hive (including S-1-5-18), and no MSI registration for the rejected x64 MSI.
    7z.exe ProductVersion '26.01' was read from the hash-verified installer payload on the host
    (evidence/host/7zip-exe-version.txt); the installed file is still observed only in the guest (S02).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Absent', 'Present')] [string] $Expect,
    [string] $ExpectedExeVersion = '26.01'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'S00MachineScope.psm1') -Force

$present = $Expect -eq 'Present'
$rejectedMsi = '{23170F69-40C1-2702-2601-000001000000}'
$checks = @()

$arp = Get-S00Uninstall '7-Zip'
$arp64 = $arp | Where-Object Path -notlike '*WOW6432Node*'
$arp32 = $arp | Where-Object Path -like '*WOW6432Node*'
$checks += New-S00Check 'HKLM ARP 7-Zip (64-bit view) DisplayVersion' ($(if ($present) { $arp64.Exists -and $arp64.DisplayVersion -eq '26.01' } else { -not $arp64.Exists })) $arp64
if ($present) {
    $loc = "$($arp64.InstallLocation)".TrimEnd('\')
    $checks += New-S00Check 'InstallLocation under %ProgramFiles%' ($loc -like "$($env:ProgramFiles)*") $loc
}
$checks += New-S00Check 'No WOW6432Node ARP entry' (-not $arp32.Exists) $arp32

$hkuHits = @(Get-S00UserHives | Where-Object { Test-Path "Registry::HKEY_USERS\$_\Software\Microsoft\Windows\CurrentVersion\Uninstall\7-Zip" })
$checks += New-S00Check 'No per-user ARP 7-Zip (incl. S-1-5-18)' ($hkuHits.Count -eq 0) ($hkuHits -join ',')

$msi = @(Get-S00MsiInstallations -ProductCode $rejectedMsi -ContextMask 7)
$checks += New-S00Check 'Rejected x64 MSI not registered (any context/user)' ($msi.Count -eq 0) (($msi | ForEach-Object { "$($_.Context):$($_.UserSid)" }) -join ',')

$dir = Join-Path $env:ProgramFiles '7-Zip'
if ($present) {
    foreach ($name in '7z.exe', '7zFM.exe') {
        $f = Join-Path $dir $name
        $pv = if (Test-Path -LiteralPath $f) { (Get-Item -LiteralPath $f).VersionInfo.ProductVersion }
        $checks += New-S00Check "$name ProductVersion" ("$pv".Trim() -eq $ExpectedExeVersion) $pv
    }
} else {
    $checks += New-S00Check '%ProgramFiles%\7-Zip absent' (-not (Test-Path $dir)) $null
}

$checks | Format-Table -AutoSize | Out-String -Width 240 | Write-Output
$failed = @($checks | Where-Object { -not $_.Pass })
if ($failed) { Write-Output "FAILED $($failed.Count) (expect $Expect)"; exit 1 }
Write-Output "PASS (expect $Expect)"
