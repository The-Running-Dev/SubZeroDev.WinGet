#Requires -Version 5.1
<#
.SYNOPSIS
    Independent machine-scope verifier for profile s01-putty-0.85.0.0-x64-wix-machine. Read-only; never calls WinGet.
.DESCRIPTION
    PREPARED, NOT EXECUTED.
    -Expect Absent : checkpoint/pre-case state (nothing of the product exists in any context).
    -Expect Present: post-case state (machine-context MSI registration, HKLM ARP, signed binary; no per-user registration).
    Emits one row per check and exits 1 if any check fails. Intended to run as SYSTEM from the evidence collector
    after the case, and as Administrator for checkpoint verification.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [ValidateSet('Absent', 'Present')] [string] $Expect
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'S00MachineScope.psm1') -Force

$productCode = '{3B0346D3-155D-43E5-9561-B9BE51CF2EB6}'
$version = '0.85.0.0'
$packed = ConvertTo-S00PackedGuid $productCode
if ($packed -ne '3D6430B3D5515E3459169BEB15FCE26B') { throw "Packed GUID self-check failed: $packed" }
$present = $Expect -eq 'Present'

$checks = @()

$machine = Get-S00MsiProperty -ProductCode $productCode -Context 4 -UserSid $null
$checks += New-S00Check 'MSI machine context VersionString' ($(if ($present) { $machine.ReturnCode -eq 0 -and $machine.Value -eq $version } else { $machine.ReturnCode -eq 1605 })) $machine

# Per-user contexts must never hold the product, before or after, for any user.
$perUser = @(Get-S00MsiInstallations -ProductCode $productCode -ContextMask 3)
$checks += New-S00Check 'No per-user MSI installation (any user)' ($perUser.Count -eq 0) (($perUser | ForEach-Object { "$($_.Context):$($_.UserSid)" }) -join ',')

$hklmProducts = Test-Path "HKLM:\SOFTWARE\Classes\Installer\Products\$packed"
$checks += New-S00Check 'HKLM Installer\Products packed key' ($hklmProducts -eq $present) $hklmProducts

$hkuHits = @(Get-S00UserHives | Where-Object { Test-Path "Registry::HKEY_USERS\$_\Software\Microsoft\Installer\Products\$packed" })
$checks += New-S00Check 'No per-user Installer\Products key in loaded hives' ($hkuHits.Count -eq 0) ($hkuHits -join ',')

$arp = Get-S00Uninstall $productCode
$arp64 = $arp | Where-Object Path -notlike '*WOW6432Node*'
$arp32 = $arp | Where-Object Path -like '*WOW6432Node*'
$checks += New-S00Check 'HKLM ARP (64-bit view) DisplayVersion' ($(if ($present) { $arp64.Exists -and $arp64.DisplayVersion -eq $version } else { -not $arp64.Exists })) $arp64
$checks += New-S00Check 'No WOW6432Node ARP entry' (-not $arp32.Exists) $arp32

$exe = Join-Path $env:ProgramFiles 'PuTTY\putty.exe'
if ($present) {
    $exists = Test-Path -LiteralPath $exe
    $fv = if ($exists) { (Get-Item -LiteralPath $exe).VersionInfo.FileVersion }
    $sig = if ($exists) { Get-AuthenticodeSignature -LiteralPath $exe }
    $checks += New-S00Check 'putty.exe FileVersion' ($exists -and $fv -eq $version) $fv
    $checks += New-S00Check 'putty.exe Authenticode Valid, Simon Tatham' ($exists -and $sig.Status -eq 'Valid' -and $sig.SignerCertificate.Subject -like '*Simon Tatham*') $(if ($sig) { "$($sig.Status) $($sig.SignerCertificate.Subject)" })
} else {
    $checks += New-S00Check '%ProgramFiles%\PuTTY absent' (-not (Test-Path (Join-Path $env:ProgramFiles 'PuTTY'))) $null
}

$checks | Format-Table -AutoSize | Out-String -Width 240 | Write-Output
$failed = @($checks | Where-Object { -not $_.Pass })
if ($failed) { Write-Output "FAILED $($failed.Count) (expect $Expect)"; exit 1 }
Write-Output "PASS (expect $Expect)"
