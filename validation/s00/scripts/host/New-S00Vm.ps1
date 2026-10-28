#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Creates the disposable S00 target VM (FM-38): Hyper-V Gen2, Secure Boot (Microsoft Windows template), vTPM,
    4 vCPU, 8 GB static RAM, 80 GB dynamic VHDX, no integration shares, no checkpoint-on-start.
.DESCRIPTION
    NOT USED in S00: the operator's existing VM Windows11-Test is the target (01-environment-inventory.md), and that
    VM is brought to FM-38 by Set-S00VmPreparation.ps1. Kept for reference only; never executed.
    Refuses to run unless the ISO hash matches -ExpectedIsoSha256, which must come from Microsoft's published
    hash for the exact Windows 11 26H2 x64 multi-edition media (baseline BC-1, approved 2026-10-28; install the
    Pro edition). The VM says nothing about 25H2. Does not start the VM.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)] [string] $IsoPath,
    [Parameter(Mandatory)] [ValidatePattern('^[0-9A-Fa-f]{64}$')] [string] $ExpectedIsoSha256,
    [Parameter(Mandatory)] [string] $SwitchName,
    [string] $Name = 'SZD-FM-S00-W11Pro26H2',
    [string] $VmRoot = 'D:\Hyper-V\SZD-FM-S00'
)
$ErrorActionPreference = 'Stop'

if (Get-VM -Name $Name -ErrorAction SilentlyContinue) { throw "VM '$Name' already exists. Refusing to modify it." }

$actual = (Get-FileHash -LiteralPath $IsoPath -Algorithm SHA256).Hash
if ($actual -ne $ExpectedIsoSha256.ToUpperInvariant()) {
    throw "ISO hash $actual does not match the expected Microsoft hash $ExpectedIsoSha256."
}
Write-Output "ISO SHA256 $actual matches."

$vhd = Join-Path $VmRoot "$Name.vhdx"
if (-not $PSCmdlet.ShouldProcess($Name, 'Create S00 VM')) { return }

New-Item -ItemType Directory -Force $VmRoot | Out-Null
New-VM -Name $Name -Generation 2 -Path $VmRoot -MemoryStartupBytes 8GB -NewVHDPath $vhd -NewVHDSizeBytes 80GB -SwitchName $SwitchName | Out-Null
Set-VM -Name $Name -ProcessorCount 4 -StaticMemory -AutomaticCheckpointsEnabled $false -CheckpointType Standard `
    -AutomaticStartAction Nothing -AutomaticStopAction ShutDown
Set-VMFirmware -VMName $Name -EnableSecureBoot On -SecureBootTemplate MicrosoftWindows
Set-VMKeyProtector -VMName $Name -NewLocalKeyProtector
Enable-VMTPM -VMName $Name

# No host folder shares or clipboard/file copy into the guest: Guest Service Interface stays off.
Disable-VMIntegrationService -VMName $Name -Name 'Guest Service Interface'
Set-VM -Name $Name -EnhancedSessionTransportType VMBus

$dvd = Add-VMDvdDrive -VMName $Name -Path $IsoPath -Passthru
Set-VMFirmware -VMName $Name -FirstBootDevice $dvd

Get-VM -Name $Name | Select-Object Name, Generation, ProcessorCount, MemoryStartup, State
Get-VMFirmware -VMName $Name | Select-Object SecureBoot, SecureBootTemplate
Get-VMSecurity -VMName $Name | Select-Object TpmEnabled
