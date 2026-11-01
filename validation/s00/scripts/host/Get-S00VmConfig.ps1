#Requires -Version 5.1
<#
.SYNOPSIS
    Read-only check of the S00 target VM against FM-38 and the BC-1 guest baseline, from the host.
.DESCRIPTION
    Needs Hyper-V Administrators or an elevated administrator. Changes nothing.
    Guest facts come from the KVP Exchange integration service (no guest credentials). The KVP data has no
    DisplayVersion, so 26H2 is inferred from build 26300 here and confirmed in the guest by Get-S00Inventory.ps1.
    Disk and checkpoint checks follow deviation D-9 (approved 2026-10-28, revised the same day after Hyper-V merged the
    automatic checkpoint): the existing 127 GiB base VHDX is kept, and prerequisites-ready is the only checkpoint, with no
    parent. The full VHD parent chain is recorded. 4 vCPU is a preparation setting, not a WinGet feasibility gate.
    Writes JSON to -OutFile and prints one OK/GAP line per requirement. Host and guest machine names, the guest FQDN
    and IP addresses are left out (redaction rule, 05-run-reset-procedure.md).
#>
[CmdletBinding()]
param(
    [string] $Name = 'Windows11-Test',
    [Parameter(Mandatory)] [string] $OutFile,
    # D-9 layout, recorded in 01-environment-inventory.md.
    [string] $BaseVhdx = 'D:\VMs\Windows11-TEst\Virtual Hard Disks\Windows11-TEst.vhdx',
    [long] $DiskSizeBytes = 136365211648,
    [string] $BaselineCheckpoint = 'prerequisites-ready'
)
$ErrorActionPreference = 'Stop'

$vm  = Get-VM -Name $Name
$fw  = Get-VMFirmware -VMName $Name
$sec = Get-VMSecurity -VMName $Name
$ics = @(Get-VMIntegrationService -VMName $Name)
$gsi = $ics | Where-Object { $_.Name -eq 'Guest Service Interface' -or $_.Id -match '6C09BB55-D683-4DA0-8931-C9BF705F6480' }
$mem = Get-VMMemory -VMName $Name
# Current disk first, then each parent down to the base VHDX.
function Get-VhdChain([string] $Path) {
    $chain = New-Object System.Collections.Generic.List[object]
    while ($Path) {
        $v = Get-VHD -Path $Path
        $chain.Add([ordered]@{ Path = $Path; VhdType = "$($v.VhdType)"; SizeBytes = $v.Size; FileSizeBytes = $v.FileSize })
        $Path = $v.ParentPath
    }
    , $chain.ToArray()
}
$disks = @(Get-VMHardDiskDrive -VMName $Name | ForEach-Object {
    $v = Get-VHD -Path $_.Path
    [ordered]@{ Controller = "$($_.ControllerType) $($_.ControllerNumber):$($_.ControllerLocation)"; Path = $_.Path; VhdType = "$($v.VhdType)"; SizeBytes = $v.Size; FileSizeBytes = $v.FileSize; ParentPath = $v.ParentPath; Chain = (Get-VhdChain $_.Path) }
})
$dvds = @(Get-VMDvdDrive -VMName $Name | ForEach-Object { [ordered]@{ Controller = "$($_.ControllerNumber):$($_.ControllerLocation)"; Path = $_.Path } })
$nics = @(Get-VMNetworkAdapter -VMName $Name | ForEach-Object { [ordered]@{ Name = $_.Name; SwitchName = $_.SwitchName; Status = "$($_.Status)" } })
$snaps = @(Get-VMSnapshot -VMName $Name | ForEach-Object { [ordered]@{ Name = $_.Name; Id = "$($_.Id)"; CreationTimeUtc = $_.CreationTime.ToUniversalTime().ToString('o'); Parent = $_.ParentSnapshotName; ParentId = "$($_.ParentSnapshotId)"; Type = "$($_.SnapshotType)" } })
$baseline = @($snaps | Where-Object { $_.Name -eq $BaselineCheckpoint })
$others = @($snaps | Where-Object { $_.Name -ne $BaselineCheckpoint })

# Guest intrinsic KVP items (OS facts the guest publishes without credentials).
$kvp = [ordered]@{}
try {
    $cs = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_ComputerSystem -Filter "ElementName='$Name'"
    $ex = Get-CimAssociatedInstance -InputObject $cs -ResultClassName Msvm_KvpExchangeComponent
    foreach ($item in $ex.GuestIntrinsicExchangeItems) {
        $x = [xml]$item
        $k = ($x.INSTANCE.PROPERTY | Where-Object NAME -eq 'Name').VALUE
        $d = ($x.INSTANCE.PROPERTY | Where-Object NAME -eq 'Data').VALUE
        if ($k -notin 'FullyQualifiedDomainName', 'NetworkAddressIPv4', 'NetworkAddressIPv6', 'RDPAddressIPv4', 'RDPAddressIPv6') { $kvp[$k] = $d }
    }
} catch { $kvp['error'] = $_.Exception.Message }

function Check([string] $Requirement, [bool] $Ok, $Observed) { [ordered]@{ requirement = $Requirement; ok = $Ok; observed = "$Observed" } }
# KVP OSBuildNumber is not the Windows build (observed 10011 on build 26300); the build is the third part of OSVersion.
$guestBuild = if ($kvp['OSVersion']) { ($kvp['OSVersion'] -split '\.')[2] }
$checks = @(
    Check 'Generation 2' ($vm.Generation -eq 2) $vm.Generation
    Check 'Secure Boot on' ("$($fw.SecureBoot)" -eq 'On') $fw.SecureBoot
    Check 'Secure Boot template MicrosoftWindows' ($fw.SecureBootTemplate -eq 'MicrosoftWindows') $fw.SecureBootTemplate
    Check 'vTPM enabled' ([bool]$sec.TpmEnabled) $sec.TpmEnabled
    Check '4 vCPU (preparation setting, not a feasibility gate)' ($vm.ProcessorCount -eq 4) $vm.ProcessorCount
    Check '8 GB static RAM' ((-not $mem.DynamicMemoryEnabled) -and $mem.Startup -eq 8GB) "dynamic=$($mem.DynamicMemoryEnabled) startup=$($mem.Startup)"
    Check 'D-9 disk: one 127 GiB disk on the base VHDX, one differencing level per checkpoint' ($disks.Count -eq 1 -and $disks[0].SizeBytes -eq $DiskSizeBytes -and $disks[0].Chain[-1].Path -eq $BaseVhdx -and $disks[0].Chain.Count -eq (1 + $snaps.Count)) (($disks | ForEach-Object { "$($_.VhdType) $($_.SizeBytes) depth=$($_.Chain.Count) root=$(Split-Path -Leaf $_.Chain[-1].Path)" }) -join '; ')
    Check 'Guest Service Interface off' ([bool]$gsi -and -not $gsi.Enabled) $gsi.Enabled
    Check 'Automatic checkpoints off' (-not $vm.AutomaticCheckpointsEnabled) $vm.AutomaticCheckpointsEnabled
    Check 'Checkpoint type Standard' ("$($vm.CheckpointType)" -eq 'Standard') $vm.CheckpointType
    Check 'AutomaticStartAction Nothing' ("$($vm.AutomaticStartAction)" -eq 'Nothing') $vm.AutomaticStartAction
    Check 'AutomaticStopAction ShutDown' ("$($vm.AutomaticStopAction)" -eq 'ShutDown') $vm.AutomaticStopAction
    Check 'No ISO attached' (-not ($dvds | Where-Object { $_.Path })) (($dvds | ForEach-Object { $_.Path }) -join '; ')
    Check 'Network adapter connected' ([bool]($nics | Where-Object { $_.SwitchName })) (($nics | ForEach-Object { $_.SwitchName }) -join '; ')
    Check "D-9 checkpoints: none, or only $BaselineCheckpoint with no parent" ($others.Count -eq 0 -and $baseline.Count -le 1 -and ($baseline.Count -eq 0 -or -not $baseline[0].ParentId)) (($snaps | ForEach-Object { "$($_.Name) [$($_.Id)] parent=$($_.ParentId)" }) -join '; ')
    Check 'Guest EditionID Professional (KVP OSEditionId 48)' ($kvp['OSEditionId'] -eq '48') "$($kvp['OSEditionId']) $($kvp['OSName'])"
    Check 'Guest build 26300 (26H2, KVP OSVersion)' ($guestBuild -eq '26300') $kvp['OSVersion']
    Check 'Guest x64 (KVP ProcessorArchitecture 9)' ($kvp['ProcessorArchitecture'] -eq '9') $kvp['ProcessorArchitecture']
    Check 'Heartbeat OK' ("$($vm.Heartbeat)" -like 'Ok*') $vm.Heartbeat
)

$result = [ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('o')
    vm = [ordered]@{
        Name = $vm.Name; State = "$($vm.State)"; Generation = $vm.Generation; ConfigurationVersion = $vm.Version
        ProcessorCount = $vm.ProcessorCount; DynamicMemory = $mem.DynamicMemoryEnabled; MemoryStartupBytes = $mem.Startup
        Path = $vm.Path; CurrentParentCheckpointId = "$($vm.ParentSnapshotId)"; CheckpointType = "$($vm.CheckpointType)"; AutomaticCheckpointsEnabled = $vm.AutomaticCheckpointsEnabled
        AutomaticStartAction = "$($vm.AutomaticStartAction)"; AutomaticStopAction = "$($vm.AutomaticStopAction)"
        EnhancedSessionTransportType = "$($vm.EnhancedSessionTransportType)"; Heartbeat = "$($vm.Heartbeat)"
        SecureBoot = "$($fw.SecureBoot)"; SecureBootTemplate = $fw.SecureBootTemplate; TpmEnabled = $sec.TpmEnabled
        IntegrationServices = @($ics | ForEach-Object { [ordered]@{ Name = $_.Name; Enabled = $_.Enabled } })
        Disks = $disks; Dvd = $dvds; Nics = $nics; Checkpoints = $snaps
    }
    guestKvp = $kvp
    checks = $checks
}
New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutFile -Encoding UTF8
foreach ($c in $result.checks) { '{0,-4} {1}  [{2}]' -f $(if ($c.ok) { 'OK' } else { 'GAP' }), $c.requirement, $c.observed }
