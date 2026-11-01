#Requires -Version 5.1
<#
.SYNOPSIS
    Brings the existing S00 VM to the FM-38 settings that can be changed in place. Run elevated on the host.
.DESCRIPTION
    Changes only: 4 vCPU, 8 GB static memory, automatic checkpoints off, AutomaticStartAction Nothing,
    AutomaticStopAction ShutDown, ISO detached. These need the VM off, so it first shuts the guest down gracefully
    (Shutdown integration service; no -Force, no -TurnOff) and leaves the VM off.
    It does not touch disks, existing checkpoints, Secure Boot, TPM, integration services or networking.
    Asks before shutting down unless -Confirm:$false. Prints the before/after settings as JSON.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string] $Name = 'Windows11-Test'
)
$ErrorActionPreference = 'Stop'

function Get-S00VmSettings {
    $vm = Get-VM -Name $Name
    $mem = Get-VMMemory -VMName $Name
    [ordered]@{
        State                       = "$($vm.State)"
        ProcessorCount              = $vm.ProcessorCount
        DynamicMemory               = $mem.DynamicMemoryEnabled
        MemoryStartupBytes          = $mem.Startup
        AutomaticCheckpointsEnabled = $vm.AutomaticCheckpointsEnabled
        AutomaticStartAction        = "$($vm.AutomaticStartAction)"
        AutomaticStopAction         = "$($vm.AutomaticStopAction)"
        Iso                         = (@(Get-VMDvdDrive -VMName $Name | Where-Object Path | ForEach-Object Path) -join '; ')
    }
}

$before = Get-S00VmSettings
if (-not $PSCmdlet.ShouldProcess($Name, 'Shut down the guest and set 4 vCPU, 8 GB static memory, automatic checkpoints off, start action Nothing, stop action ShutDown, ISO detached')) { return }

if ($before.State -ne 'Off') {
    Stop-VM -Name $Name
    if ("$((Get-VM -Name $Name).State)" -ne 'Off') { throw "$Name did not reach Off." }
}
Set-VM -Name $Name -ProcessorCount 4 -StaticMemory -MemoryStartupBytes 8GB -AutomaticCheckpointsEnabled $false -AutomaticStartAction Nothing -AutomaticStopAction ShutDown
Get-VMDvdDrive -VMName $Name | Where-Object Path | Set-VMDvdDrive -Path $null

[ordered]@{ capturedUtc = [DateTime]::UtcNow.ToString('o'); before = $before; after = (Get-S00VmSettings) } | ConvertTo-Json -Depth 3
