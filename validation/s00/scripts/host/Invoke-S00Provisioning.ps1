#Requires -Version 7.0
<#
.SYNOPSIS
    Runs 05-run-reset-procedure.md section 1 against the S00 VM through PowerShell Direct. Run elevated on the host.
.DESCRIPTION
    The operator supplies the guest administrator credential (-Credential (Get-Credential)); this script never writes it.
    Steps, each stopping provisioning on failure:
      1  host staged-asset check (Test-S00StagedAssets.ps1)
      2  start the VM if needed and open a PowerShell Direct session
      3  copy the fixture and the two guest assets (powershell, winget-client-module) to C:\S00Staging
      4  baseline pre-check (Get-S00Inventory.ps1 must print BASELINE PASS and meet the 05 step 8 conditions) before
         anything is changed in the guest
      5  Install-S00Prerequisites, Set-S00PolicyPins, Set-S00EvidenceAcl
      6  place the fixture and write Fixture\fixture.sha256.txt
      7  Get-S00SourceAgreements (A-3 capture; exit 3 = not-observable)
      8  Get-S00Inventory (BASELINE PASS and the step 8 conditions), Test-S00PuTTY / Test-S00SevenZip -Expect Absent
      9  export Evidence and step outputs to evidence/guest/provisioning-<utc>/, remove C:\S00Staging, close the session
      10 graceful shutdown and checkpoint prerequisites-ready, only if every step passed and the capture exit was 0.
         D-9 (revised 2026-10-28): the VM must have no checkpoint, so prerequisites-ready is the root and the only one.
         A VM that already has a checkpoint is refused before anything changes; no checkpoint is deleted, merged or replaced.
         Test-S00Restore.ps1 then proves the restore before S00 is complete.
    Exported text is redacted (host and guest machine names, host user, guest administrator) and the replacements
    are listed in REDACTIONS.md. Guest scripts run in Windows PowerShell 5.1 with -ExecutionPolicy Bypass.
#>
[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [string] $Name = 'Windows11-Test',
    [Parameter(Mandatory)] [pscredential] $Credential,
    [switch] $SkipCheckpoint
)
$ErrorActionPreference = 'Stop'

$s00 = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$lockPath = Join-Path $s00 'prerequisites.lock.json'
$lock = Get-Content $lockPath -Raw | ConvertFrom-Json
$stamp = '{0:yyyyMMddTHHmmssZ}' -f [DateTime]::UtcNow
$outDir = Join-Path $s00 "evidence\guest\provisioning-$stamp"
$logFile = Join-Path $outDir 'provisioning.log'
$staging = 'C:\S00Staging'
$guestScripts = "$staging\fixture\scripts\guest"
New-Item -ItemType Directory -Force $outDir | Out-Null

function Write-Step([string] $Text) {
    $line = '[{0:o}] {1}' -f [DateTime]::UtcNow, $Text
    Write-Host $line
    Add-Content -LiteralPath $logFile -Value $line
}

function Invoke-GuestScript([string] $Script, [string[]] $Arguments = @(), [string] $SaveAs, [int[]] $Accept = @(0)) {
    $r = Invoke-Command -Session $session -ScriptBlock {
        param($File, $ArgList)
        $out = & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $File @ArgList 2>&1 | ForEach-Object { "$_" }
        [pscustomobject]@{ Output = @($out); ExitCode = $LASTEXITCODE }
    } -ArgumentList "$guestScripts\$Script", $Arguments
    $r.Output | Set-Content -LiteralPath (Join-Path $outDir $SaveAs) -Encoding utf8
    Write-Step "$Script $($Arguments -join ' ') -> exit $($r.ExitCode); output in $SaveAs"
    $r.Output | ForEach-Object { Write-Host "    $_" }
    if ($r.ExitCode -notin $Accept) { throw "$Script exited $($r.ExitCode)." }
    $r.ExitCode
}

# 05 section 1 step 8 conditions beyond BASELINE PASS, read from the inventory JSON.
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
$guestProgramData = $null
$allPassed = $false
$agreementsExit = $null
try {
    # D-9: prerequisites-ready must be the only checkpoint, so refuse a VM that already has one before changing anything.
    $existing = @(Get-VMSnapshot -VMName $Name)
    if ($existing.Count -and -not $SkipCheckpoint) { throw "D-9: $Name already has checkpoints ($(($existing | ForEach-Object { $_.Name }) -join '; ')); prerequisites-ready must be the only one. Nothing changed, nothing removed." }

    # 1. Host staged assets.
    $assetOut = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-S00StagedAssets.ps1') -LockPath $lockPath
    $assetExit = $LASTEXITCODE
    $assetOut | Set-Content -LiteralPath (Join-Path $outDir 'staged-assets.txt') -Encoding utf8
    Write-Step "Test-S00StagedAssets -> exit $assetExit"
    if ($assetExit -ne 0) { throw 'Staged assets did not verify.' }

    # 2. VM running and reachable through PowerShell Direct.
    if ("$((Get-VM -Name $Name).State)" -ne 'Running') { Write-Step "Starting $Name"; Start-VM -Name $Name }
    $deadline = [DateTime]::UtcNow.AddMinutes(10)
    $rejected = 0
    while (-not $session) {
        try { $session = New-PSSession -VMName $Name -Credential $Credential -ErrorAction Stop }
        catch {
            # A booted guest (heartbeat OK) that keeps rejecting the credential will not accept it later; stop instead of waiting out the deadline.
            $heartbeat = "$((Get-VMIntegrationService -VMName $Name -Name Heartbeat).PrimaryStatusDescription)"
            if ($_.Exception.Message -match 'credential is invalid' -and $heartbeat -like 'OK*') { $rejected++ } else { $rejected = 0 }
            if ($rejected -ge 3) { throw "The guest rejected the credential 3 times with its heartbeat OK: $($_.Exception.Message) Enter a local administrator of the guest (a Microsoft account needs its email address and account password, not a PIN). The VM is left running." }
            if ([DateTime]::UtcNow -gt $deadline) { throw "PowerShell Direct unavailable after 10 minutes: $($_.Exception.Message)" }
            Start-Sleep -Seconds 10
        }
    }
    $guestName, $guestProgramData = Invoke-Command -Session $session -ScriptBlock { $env:COMPUTERNAME; $env:ProgramData }
    Write-Step 'PowerShell Direct session open'

    # 3. Fixture and guest assets into C:\S00Staging.
    Invoke-Command -Session $session -ScriptBlock {
        param($Root)
        if (Test-Path -LiteralPath $Root) { throw "$Root already exists; remove it or restore the VM first." }
        New-Item -ItemType Directory -Force "$Root\fixture\scripts", "$Root\assets\powershell", "$Root\assets\winget-client-module" | Out-Null
    } -ArgumentList $staging
    foreach ($dir in 'profiles', 'manifests', 'agreements') {
        Copy-Item -ToSession $session -Path (Join-Path $s00 $dir) -Destination "$staging\fixture" -Recurse
    }
    Copy-Item -ToSession $session -Path (Join-Path $s00 'scripts\guest') -Destination "$staging\fixture\scripts" -Recurse
    Copy-Item -ToSession $session -Path $lockPath -Destination "$staging\fixture\prerequisites.lock.json"
    foreach ($id in 'powershell', 'winget-client-module') {
        $asset = $lock.assets | Where-Object id -eq $id
        Copy-Item -ToSession $session -Path (Join-Path $lock.stagingRoot $asset.file) -Destination (Join-Path "$staging\assets" $asset.file)
    }
    Write-Step 'Fixture and guest assets copied to C:\S00Staging'

    # 4. Baseline pre-check before the guest is changed.
    Invoke-GuestScript 'Get-S00Inventory.ps1' @('-OutFile', "$staging\pre-inventory.json") 'pre-inventory.txt' | Out-Null
    Copy-Item -FromSession $session -Path "$staging\pre-inventory.json" -Destination $outDir
    Assert-InventoryGate (Get-Content -LiteralPath (Join-Path $outDir 'pre-inventory.json') -Raw) 'Pre-inventory'

    # 5. Worker, policy pin, validation-root ACL.
    Invoke-GuestScript 'Install-S00Prerequisites.ps1' @('-StagingRoot', "$staging\assets", '-LockPath', "$staging\fixture\prerequisites.lock.json") 'install-prerequisites.txt' | Out-Null
    Invoke-GuestScript 'Set-S00PolicyPins.ps1' @() 'policy-pins.txt' | Out-Null
    Invoke-GuestScript 'Set-S00EvidenceAcl.ps1' @() 'evidence-acl-sddl.txt' | Out-Null

    # 6. Fixture placement and hash list.
    Invoke-Command -Session $session -ScriptBlock {
        param($From)
        $fixture = Join-Path $env:ProgramData 'SubZeroDev\FileManager\Validation\Fixture'
        # A re-run after a later step failed replaces the earlier fixture, so the hash list covers exactly this one.
        Get-ChildItem -LiteralPath $fixture -Force | Remove-Item -Recurse -Force
        Copy-Item -Path "$From\*" -Destination $fixture -Recurse
        $files = Get-ChildItem -LiteralPath $fixture -Recurse -File | Where-Object Name -ne 'fixture.sha256.txt' | Sort-Object FullName
        $files | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $_.FullName.Substring($fixture.Length + 1) } |
            Set-Content -LiteralPath (Join-Path $fixture 'fixture.sha256.txt') -Encoding UTF8
    } -ArgumentList "$staging\fixture"
    Copy-Item -FromSession $session -Path "$guestProgramData\SubZeroDev\FileManager\Validation\Fixture\fixture.sha256.txt" -Destination $outDir
    Write-Step 'Fixture placed; fixture.sha256.txt exported'

    # 7. Source agreements (A-3 capture). Exit 3 is recorded, not fatal here; it blocks the checkpoint below.
    $agreementsExit = Invoke-GuestScript 'Get-S00SourceAgreements.ps1' @() 'source-agreements.txt' -Accept 0, 3

    # 8. Inventory and package absence.
    $inventoryFile = "$guestProgramData\SubZeroDev\FileManager\Validation\Evidence\S00-inventory-$stamp.json"
    Invoke-GuestScript 'Get-S00Inventory.ps1' @('-OutFile', $inventoryFile) 'inventory.txt' | Out-Null
    Assert-InventoryGate (Invoke-Command -Session $session -ScriptBlock { param($F) Get-Content -LiteralPath $F -Raw } -ArgumentList $inventoryFile) 'Inventory'
    Invoke-GuestScript 'Test-S00PuTTY.ps1' @('-Expect', 'Absent') 'putty-absent.txt' | Out-Null
    Invoke-GuestScript 'Test-S00SevenZip.ps1' @('-Expect', 'Absent') 'sevenzip-absent.txt' | Out-Null

    # 9. Export.
    Copy-Item -FromSession $session -Path "$guestProgramData\SubZeroDev\FileManager\Validation\Evidence" -Destination $outDir -Recurse
    Write-Step 'Evidence exported'
    $allPassed = $true
}
catch {
    Write-Step "STOPPED: $($_.Exception.Message)"
}
finally {
    if ($session) {
        try { Invoke-Command -Session $session -ScriptBlock { param($Root) Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction SilentlyContinue } -ArgumentList $staging; Write-Step 'C:\S00Staging removed' } catch { Write-Step "Staging cleanup failed: $($_.Exception.Message)" }
        Remove-PSSession -Session $session
        Write-Step 'PowerShell Direct session closed'
    }
}

# 10. Checkpoint, only after a clean run with a captured agreement set.
if ($allPassed -and $agreementsExit -eq 0 -and -not $SkipCheckpoint) {
    if (Get-VMCheckpoint -VMName $Name -Name 'prerequisites-ready' -ErrorAction SilentlyContinue) {
        Write-Step 'A prerequisites-ready checkpoint already exists; not replaced.'
    } elseif (@(Get-VMSnapshot -VMName $Name).Count) {
        Write-Step 'D-9: the VM has another checkpoint; prerequisites-ready must be the only one. No checkpoint taken, none removed.'
        $allPassed = $false
    } elseif ($PSCmdlet.ShouldProcess($Name, 'Shut down the guest and take checkpoint prerequisites-ready')) {
        Stop-VM -Name $Name
        if ("$((Get-VM -Name $Name).State)" -ne 'Off') { throw "$Name did not reach Off." }
        Checkpoint-VM -Name $Name -SnapshotName 'prerequisites-ready'
        $cp = Get-VMCheckpoint -VMName $Name -Name 'prerequisites-ready'
        $all = @(Get-VMSnapshot -VMName $Name | Sort-Object CreationTime | ForEach-Object { [ordered]@{ Name = $_.Name; Id = "$($_.Id)"; ParentId = "$($_.ParentSnapshotId)" } })
        [ordered]@{ Name = $cp.Name; Id = "$($cp.Id)"; ParentId = "$($cp.ParentSnapshotId)"; CreationTimeUtc = $cp.CreationTime.ToUniversalTime().ToString('o'); VmState = "$((Get-VM -Name $Name).State)"; AllCheckpoints = $all } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outDir 'checkpoint.json') -Encoding utf8
        Write-Step "Checkpoint prerequisites-ready taken: $($cp.Id), parent $($cp.ParentSnapshotId)"
        if ($all.Count -ne 1 -or "$($cp.ParentSnapshotId)") { Write-Step "D-9: prerequisites-ready is not the root and only checkpoint: $(($all | ForEach-Object { "$($_.Name) [$($_.Id)] parent=$($_.ParentId)" }) -join '; ')"; $allPassed = $false }
    }
} else {
    Write-Step "No checkpoint taken (allPassed=$allPassed, agreementsExit=$agreementsExit, SkipCheckpoint=$SkipCheckpoint)."
}

# Redaction (05 section 3): replace names in every exported text file; record counts, never the values.
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
    # Files without a hit keep their original bytes.
    if ($text -cne $original) { Set-Content -LiteralPath $f.FullName -Value $text -NoNewline -Encoding utf8 }
}
@('# Redactions', '', 'Applied automatically by Invoke-S00Provisioning.ps1 under 05 section 3. Values are not recorded.', '',
  '| File | Kind | Count |', '|---|---|---|') + @($rows) | Set-Content -LiteralPath (Join-Path $outDir 'REDACTIONS.md') -Encoding utf8

Write-Host ''
Write-Host "Evidence: $outDir"
if (-not $allPassed) { exit 1 }
if ($SkipCheckpoint) { Write-Host 'Checkpoint skipped (-SkipCheckpoint).'; exit 0 }
if (-not (Test-Path -LiteralPath (Join-Path $outDir 'checkpoint.json'))) {
    Write-Host 'No checkpoint was taken by this run; S00 provisioning is not complete.'
    exit 1
}
Write-Host 'If VMConnect was used for setup, it is no longer needed; the checkpoint was taken with the VM off.'
Write-Host 'S00 is not complete until Test-S00Restore.ps1 passes.'
exit 0
