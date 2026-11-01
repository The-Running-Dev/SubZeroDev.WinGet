#Requires -Version 7.0
<#
.SYNOPSIS
    Proves that prerequisites-ready restores cleanly (05-run-reset-procedure.md section 2). S00 is not complete until
    this passes. Run elevated on the host after Invoke-S00Provisioning.ps1.
.DESCRIPTION
    The operator supplies the guest administrator credential (-Credential (Get-Credential)); this script never writes it.
    Never deletes, merges or replaces a checkpoint (D-9). Restore-VMCheckpoint discards only the VM's current state,
    so the VM must be Off and the operator confirms before anything changes.
    Steps, each stopping the test on failure:
      1  record every checkpoint; prerequisites-ready must exist, match the provisioning checkpoint.json Id, have no
         parent and be the only checkpoint (D-9 as revised 2026-10-28)
      2  restore prerequisites-ready, boot, write a canary file in the guest, shut down gracefully
      3  restore prerequisites-ready again and boot
      4  in the guest: canary absent; every fixture file matches Fixture\fixture.sha256.txt, and that list equals the
         one exported at provisioning; Get-S00Inventory.ps1 prints BASELINE PASS and meets the 05 step 8 conditions;
         Test-S00PuTTY / Test-S00SevenZip -Expect Absent
      5  shut down gracefully and restore prerequisites-ready a third time, so the VM is left Off at the checkpoint
      6  the checkpoint list (names, Ids, parents) equals step 1
    Writes evidence/guest/restore-<utc>/ (restore.log, restore.json, step outputs) with the same redaction as
    Invoke-S00Provisioning.ps1. Exits 0 only if every check passed.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string] $Name = 'Windows11-Test',
    [Parameter(Mandatory)] [pscredential] $Credential,
    [string] $Checkpoint = 'prerequisites-ready',
    # Defaults to the newest evidence\guest\provisioning-* folder that has a checkpoint.json.
    [string] $ProvisioningEvidence
)
$ErrorActionPreference = 'Stop'

$s00 = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$stamp = '{0:yyyyMMddTHHmmssZ}' -f [DateTime]::UtcNow
$outDir = Join-Path $s00 "evidence\guest\restore-$stamp"
$logFile = Join-Path $outDir 'restore.log'
$canary = 'C:\S00RestoreCanary.txt'
$work = 'C:\S00Restore'
New-Item -ItemType Directory -Force $outDir | Out-Null
if (-not $ProvisioningEvidence) {
    $ProvisioningEvidence = Get-ChildItem -LiteralPath (Join-Path $s00 'evidence\guest') -Directory -Filter 'provisioning-*' -ErrorAction SilentlyContinue |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'checkpoint.json') } | Sort-Object Name | Select-Object -Last 1 -ExpandProperty FullName
}

function Write-Step([string] $Text) {
    $line = '[{0:o}] {1}' -f [DateTime]::UtcNow, $Text
    Write-Host $line
    Add-Content -LiteralPath $logFile -Value $line
}

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string] $Check, [bool] $Ok, $Observed) {
    $results.Add([ordered]@{ check = $Check; ok = $Ok; observed = "$Observed" })
    Write-Step ('{0,-4} {1}  [{2}]' -f $(if ($Ok) { 'OK' } else { 'FAIL' }), $Check, $Observed)
    if (-not $Ok) { throw "$Check failed." }
}

function Get-CheckpointList {
    # The leading comma keeps a one-checkpoint list an array; unrolled, .Count would count the dictionary's keys.
    , @(Get-VMSnapshot -VMName $Name | Sort-Object CreationTime | ForEach-Object { [ordered]@{ Name = $_.Name; Id = "$($_.Id)"; ParentId = "$($_.ParentSnapshotId)" } })
}

function Open-GuestSession {
    $deadline = [DateTime]::UtcNow.AddMinutes(10)
    $rejected = 0
    while ($true) {
        try { return New-PSSession -VMName $Name -Credential $Credential -ErrorAction Stop }
        catch {
            # A booted guest (heartbeat OK) that keeps rejecting the credential will not accept it later; stop instead of waiting out the deadline.
            $heartbeat = "$((Get-VMIntegrationService -VMName $Name -Name Heartbeat).PrimaryStatusDescription)"
            if ($_.Exception.Message -match 'credential is invalid' -and $heartbeat -like 'OK*') { $rejected++ } else { $rejected = 0 }
            if ($rejected -ge 3) { throw "The guest rejected the credential 3 times with its heartbeat OK: $($_.Exception.Message) Enter a local administrator of the guest (a Microsoft account needs its email address and account password, not a PIN)." }
            if ([DateTime]::UtcNow -gt $deadline) { throw "PowerShell Direct unavailable after 10 minutes: $($_.Exception.Message)" }
            Start-Sleep -Seconds 10
        }
    }
}

function Stop-Guest {
    if ("$((Get-VM -Name $Name).State)" -eq 'Off') { return }
    Stop-VM -Name $Name
    if ("$((Get-VM -Name $Name).State)" -ne 'Off') { throw "$Name did not reach Off." }
    Write-Step "$Name shut down"
}

function Restore-Baseline([string] $Label) {
    Restore-VMCheckpoint -VMName $Name -Name $Checkpoint -Confirm:$false
    $vm = Get-VM -Name $Name
    Write-Step "$Label`: restored $Checkpoint; state $($vm.State), current state under $($vm.ParentSnapshotId)"
    $vm
}

function Invoke-GuestScript([string] $Script, [string[]] $Arguments = @(), [string] $SaveAs) {
    $r = Invoke-Command -Session $session -ScriptBlock {
        param($File, $ArgList)
        $out = & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $File @ArgList 2>&1 | ForEach-Object { "$_" }
        [pscustomobject]@{ Output = @($out); ExitCode = $LASTEXITCODE }
    } -ArgumentList "$guestScripts\$Script", $Arguments
    $r.Output | Set-Content -LiteralPath (Join-Path $outDir $SaveAs) -Encoding utf8
    Write-Step "$Script $($Arguments -join ' ') -> exit $($r.ExitCode); output in $SaveAs"
    $r
}

# Copy of Invoke-S00Provisioning.ps1's gate (05 section 1 step 8 conditions beyond BASELINE PASS); keep them identical.
function Assert-InventoryGate([string] $Json, [string] $Label) {
    $inv = $Json | ConvertFrom-Json
    $fail = @(
        foreach ($p in $inv.updates.PendingReboot.PSObject.Properties) { if ($p.Value) { "PendingReboot.$($p.Name) true" } }
        if ($inv.autoAdminLogon -eq '1') { 'autoAdminLogon is 1' }
        if ($inv.worker.PwshOnPath) { 'worker.PwshOnPath true' }
        if ($inv.worker.ModuleInMachinePSModulePath) { 'worker.ModuleInMachinePSModulePath true' }
        foreach ($p in $inv.packagesAbsent.PSObject.Properties) { if ($p.Value) { "packagesAbsent.$($p.Name) true" } }
    )
    Write-Step "$Label gate: $(if ($fail) { $fail -join '; ' } else { 'pass' })"
    if ($fail) { throw "$Label gate failed: $($fail -join '; ')" }
}

$session = $null
$guestName = $null
$guestScripts = $null
$restored = $false
$allPassed = $false
$before = $null
try {
    # 1. Preconditions and checkpoint record.
    if (-not $ProvisioningEvidence) { throw 'No evidence\guest\provisioning-* folder with checkpoint.json; run Invoke-S00Provisioning.ps1 first.' }
    Write-Step "Provisioning evidence: $(Split-Path -Leaf $ProvisioningEvidence)"
    $provCp = Get-Content -LiteralPath (Join-Path $ProvisioningEvidence 'checkpoint.json') -Raw | ConvertFrom-Json
    $before = Get-CheckpointList
    $baseline = @($before | Where-Object { $_.Name -eq $Checkpoint })
    Add-Result "$Checkpoint exists once, Id as at provisioning" ($baseline.Count -eq 1 -and $baseline[0].Id -eq $provCp.Id) (($baseline | ForEach-Object { $_.Id }) -join '; ')
    Add-Result "D-9: $Checkpoint is the root and the only checkpoint" ($before.Count -eq 1 -and -not $baseline[0].ParentId) (($before | ForEach-Object { "$($_.Name) [$($_.Id)] parent=$($_.ParentId)" }) -join '; ')
    $state = "$((Get-VM -Name $Name).State)"
    Add-Result 'VM is Off before the first restore' ($state -eq 'Off') $state
    if (-not $PSCmdlet.ShouldProcess($Name, "Restore $Checkpoint three times, boot twice and write a canary; the VM's current state is discarded")) {
        throw 'Not confirmed; nothing changed.'
    }

    # 2. First restore, boot, canary.
    Restore-Baseline 'Restore 1' | Out-Null
    $restored = $true
    Start-VM -Name $Name
    $session = Open-GuestSession
    $guestName, $guestProgramData = Invoke-Command -Session $session -ScriptBlock { $env:COMPUTERNAME; $env:ProgramData }
    $token = [guid]::NewGuid().ToString()
    Invoke-Command -Session $session -ScriptBlock { param($P, $T) Set-Content -LiteralPath $P -Value $T } -ArgumentList $canary, $token
    $seen = Invoke-Command -Session $session -ScriptBlock { param($P) Get-Content -LiteralPath $P -Raw } -ArgumentList $canary
    Add-Result 'Canary written after restore 1' ("$seen".Trim() -eq $token) $canary
    Remove-PSSession -Session $session; $session = $null
    Stop-Guest

    # 3. Second restore and boot.
    Restore-Baseline 'Restore 2' | Out-Null
    Start-VM -Name $Name
    $session = Open-GuestSession
    $fixture = "$guestProgramData\SubZeroDev\FileManager\Validation\Fixture"
    $guestScripts = "$fixture\scripts\guest"

    # 4. Guest state equals the checkpoint.
    $present = Invoke-Command -Session $session -ScriptBlock { param($P) Test-Path -LiteralPath $P } -ArgumentList $canary
    Add-Result 'Canary absent after restore 2' (-not $present) $canary
    $fx = Invoke-Command -Session $session -ScriptBlock {
        param($Root)
        $lines = @(Get-Content -LiteralPath (Join-Path $Root 'fixture.sha256.txt'))
        $bad = @(foreach ($line in $lines) {
            $hash, $rel = $line -split '  ', 2
            $p = Join-Path $Root $rel
            if (-not (Test-Path -LiteralPath $p)) { "missing $rel" }
            elseif ((Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash -ne $hash) { "changed $rel" }
        })
        [pscustomobject]@{ Lines = $lines; Bad = $bad }
    } -ArgumentList $fixture
    $fx.Bad | Set-Content -LiteralPath (Join-Path $outDir 'fixture-verify.txt') -Encoding utf8
    Add-Result 'Fixture files match fixture.sha256.txt' ($fx.Lines.Count -gt 0 -and $fx.Bad.Count -eq 0) "$($fx.Lines.Count) files, $($fx.Bad.Count) bad"
    $exported = @(Get-Content -LiteralPath (Join-Path $ProvisioningEvidence 'fixture.sha256.txt'))
    $same = ($exported.Count -eq $fx.Lines.Count) -and -not (Compare-Object -ReferenceObject $exported -DifferenceObject @($fx.Lines) -CaseSensitive -SyncWindow 0)
    Add-Result 'fixture.sha256.txt equals the provisioning export' $same "$($exported.Count) / $($fx.Lines.Count) lines"
    Invoke-Command -Session $session -ScriptBlock { param($P) New-Item -ItemType Directory -Force $P | Out-Null } -ArgumentList $work
    $inv = Invoke-GuestScript 'Get-S00Inventory.ps1' @('-OutFile', "$work\inventory.json") 'inventory.txt'
    Add-Result 'Get-S00Inventory BASELINE PASS' ($inv.ExitCode -eq 0 -and ($inv.Output -match '^BASELINE PASS')) "exit $($inv.ExitCode)"
    Copy-Item -FromSession $session -Path "$work\inventory.json" -Destination $outDir
    Assert-InventoryGate (Get-Content -LiteralPath (Join-Path $outDir 'inventory.json') -Raw) 'Inventory'
    Add-Result 'Inventory gate (05 step 8)' $true 'pass'
    foreach ($t in @(@('Test-S00PuTTY.ps1', 'putty-absent.txt'), @('Test-S00SevenZip.ps1', 'sevenzip-absent.txt'))) {
        $r = Invoke-GuestScript $t[0] @('-Expect', 'Absent') $t[1]
        Add-Result "$($t[0]) -Expect Absent" ($r.ExitCode -eq 0) "exit $($r.ExitCode)"
    }
    Remove-PSSession -Session $session; $session = $null
    $allPassed = $true
}
catch {
    Write-Step "STOPPED: $($_.Exception.Message)"
}
finally {
    if ($session) { Remove-PSSession -Session $session; $session = $null }
    # 5. Leave the VM Off at the checkpoint; everything since restore 1 is disposable.
    if ($restored) {
        try {
            Stop-Guest
            $vm = Restore-Baseline 'Restore 3'
            $cpNow = @(Get-VMSnapshot -VMName $Name | Where-Object Name -eq $Checkpoint)
            Add-Result 'VM Off at the checkpoint after restore 3' ("$($vm.State)" -eq 'Off' -and $cpNow.Count -eq 1 -and "$($vm.ParentSnapshotId)" -eq "$($cpNow[0].Id)") "state $($vm.State), under $($vm.ParentSnapshotId)"
        } catch { Write-Step "Final restore: $($_.Exception.Message)"; $allPassed = $false }
    }
    # 6. No checkpoint was added, removed or re-parented.
    if ($before) {
        $after = Get-CheckpointList
        $same = (($before | ConvertTo-Json -Compress) -ceq ($after | ConvertTo-Json -Compress))
        try { Add-Result 'Checkpoint list unchanged (names, Ids, parents)' $same "$($after.Count) checkpoints" } catch { $allPassed = $false }
    }
}

[ordered]@{
    capturedUtc = [DateTime]::UtcNow.ToString('o'); vmName = $Name; checkpoint = $Checkpoint
    provisioningEvidence = $(if ($ProvisioningEvidence) { Split-Path -Leaf $ProvisioningEvidence })
    checkpointsBefore = $before; checkpointsAfter = (Get-CheckpointList); passed = $allPassed; results = $results
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outDir 'restore.json') -Encoding utf8

# Redaction (05 section 3), as in Invoke-S00Provisioning.ps1: record counts, never the values.
$terms = [ordered]@{
    'host'        = $env:COMPUTERNAME
    'host-user'   = $env:USERNAME
    'guest-host'  = $guestName
    'guest-admin' = ($Credential.UserName -split '\\')[-1]
}
$rows = foreach ($f in Get-ChildItem -LiteralPath $outDir -Recurse -File -Include *.json, *.txt, *.log, *.xml) {
    $text = Get-Content -LiteralPath $f.FullName -Raw
    if ($null -eq $text) { continue }
    $original = $text
    foreach ($kind in $terms.Keys) {
        $value = $terms[$kind]
        if (-not $value) { continue }
        $pattern = '(?i)(?<![A-Za-z0-9])' + [regex]::Escape($value) + '(?![A-Za-z0-9])'
        $n = [regex]::Matches($text, $pattern).Count
        if ($n) {
            $text = [regex]::Replace($text, $pattern, "<redacted:$kind>")
            "| ``$($f.FullName.Substring($outDir.Length + 1))`` | $kind | $n |"
        }
    }
    if ($text -cne $original) { Set-Content -LiteralPath $f.FullName -Value $text -NoNewline -Encoding utf8 }
}
@('# Redactions', '', 'Applied automatically by Test-S00Restore.ps1 under 05 section 3. Values are not recorded.', '',
  '| File | Kind | Count |', '|---|---|---|') + @($rows) | Set-Content -LiteralPath (Join-Path $outDir 'REDACTIONS.md') -Encoding utf8

Write-Host ''
Write-Host "Evidence: $outDir"
if (-not $allPassed) { Write-Host 'RESTORE FAIL'; exit 1 }
Write-Host 'RESTORE PASS'
exit 0
