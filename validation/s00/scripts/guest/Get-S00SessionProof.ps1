#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Read-only proof that a validation case ran under the SCM as LocalSystem in Session 0 after a reboot, with no
    signed-in or disconnected interactive user. Never starts, stops or changes anything.
.DESCRIPTION
    PREPARED, NOT EXECUTED. Procedure: 05-run-reset-procedure.md, section "S01 run procedure".
    Run through PowerShell Direct from the host, only after the service has recorded CaseEnd. Never sign in to the
    guest console or RDP to collect evidence; that would itself create the interactive session being ruled out.
    The offline VHD route is withdrawn.

    -CaseEndUtc bounds the logon-event window to boot..case end, so the collector's own PowerShell Direct logon
    (whose logon type is not established; recorded, not asserted) falls outside it.
    The worker process may have exited; pass -WorkerSessionId as recorded by the service in its own evidence.
    Output has no user names: sessions keep id/state, logon events keep type/time/logon id only.
    It also exports the raw Security and System logs for boot..CaseEnd (wevtutil epl) next to the JSON and records
    their SHA256. The .evtx files do contain account names; keep them on the host, outside the repository.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ServiceName,
    [Parameter(Mandatory)] [DateTime] $CaseEndUtc,
    [Nullable[int]] $WorkerSessionId,
    [string] $OutFile = (Join-Path $env:ProgramData ("SubZeroDev\FileManager\Validation\Evidence\S01-session-proof-{0:yyyyMMddTHHmmssZ}.json" -f [DateTime]::UtcNow))
)
$ErrorActionPreference = 'Stop'

$os = Get-CimInstance Win32_OperatingSystem
$bootUtc = $os.LastBootUpTime.ToUniversalTime()
$caseEnd = $CaseEndUtc.ToUniversalTime()
$checks = @()
function Add-Check([string] $Name, [bool] $Pass, $Observed) {
    $script:checks += [pscustomobject]@{ Check = $Name; Pass = $Pass; Observed = $Observed }
}

$svc = Get-CimInstance Win32_Service -Filter "Name='$($ServiceName -replace "'", "''")'"
if (-not $svc) { throw "Service '$ServiceName' not found." }
Add-Check 'Service StartName is LocalSystem' ($svc.StartName -eq 'LocalSystem') $svc.StartName
Add-Check 'Service StartMode Auto (started by SCM at boot)' ($svc.StartMode -eq 'Auto') $svc.StartMode
$svcProc = if ($svc.ProcessId) { Get-CimInstance Win32_Process -Filter "ProcessId=$($svc.ProcessId)" }
if ($svcProc) {
    Add-Check 'Service process SessionId 0' ($svcProc.SessionId -eq 0) $svcProc.SessionId
    Add-Check 'Service process started after boot' ($svcProc.CreationDate.ToUniversalTime() -ge $bootUtc) $svcProc.CreationDate.ToUniversalTime().ToString('o')
    $children = @(Get-CimInstance Win32_Process -Filter "ParentProcessId=$($svc.ProcessId)")
    foreach ($c in $children) { Add-Check "Child $($c.Name) pid $($c.ProcessId) SessionId 0" ($c.SessionId -eq 0) $c.SessionId }
} else {
    Add-Check 'Service process running' $false $svc.State
}
if ($null -ne $WorkerSessionId) { Add-Check 'Worker SessionId 0 (service-recorded)' ($WorkerSessionId -eq 0) $WorkerSessionId }

# Interactive sessions now: quser lists Active and Disc user sessions; exit 1 with no output means none.
# With none, quser writes "No User exists for *" to stderr, which Windows PowerShell 5.1 turns into a terminating
# NativeCommandError under Stop; the function runs it under Continue.
function Invoke-Quser { $ErrorActionPreference = 'Continue'; & "$env:windir\System32\quser.exe" 2>$null }
$quser = @(Invoke-Quser | Select-Object -Skip 1 | ForEach-Object {
    $cols = ($_.Trim() -replace '\s{2,}', '|').Split('|')
    [pscustomobject]@{
        SessionId = $cols | Where-Object { $_ -match '^\d+$' } | Select-Object -First 1
        State     = $cols | Where-Object { $_ -in 'Active', 'Disc' } | Select-Object -First 1
    }
})
Add-Check 'No Active or Disc user sessions (quser)' ($quser.Count -eq 0) (($quser | ForEach-Object { "$($_.SessionId):$($_.State)" }) -join ',')
$winlogon = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
Add-Check 'Automatic sign-in disabled (AutoAdminLogon)' ("$($winlogon.AutoAdminLogon)" -ne '1') "$($winlogon.AutoAdminLogon)"
$shells = @(Get-CimInstance Win32_Process -Filter "Name='explorer.exe' OR Name='sihost.exe' OR Name='userinit.exe'")
Add-Check 'No interactive shell processes' ($shells.Count -eq 0) (($shells | ForEach-Object { "$($_.Name)@$($_.SessionId)" }) -join ',')

# Interactive logons between boot and case end: 2 Interactive, 7 Unlock, 10 RemoteInteractive, 11 CachedInteractive.
$logons = @(Get-WinEvent -FilterHashtable @{ LogName = 'Security'; Id = 4624; StartTime = $bootUtc.ToLocalTime() } -ErrorAction SilentlyContinue | ForEach-Object {
    $x = [xml]$_.ToXml()
    $d = @{}; foreach ($n in $x.Event.EventData.Data) { $d[$n.Name] = $n.'#text' }
    [pscustomobject]@{ TimeUtc = $_.TimeCreated.ToUniversalTime(); LogonType = [int]$d['LogonType']; TargetLogonId = $d['TargetLogonId']; TargetUserSid = $d['TargetUserSid'] }
})
$inWindow = @($logons | Where-Object { $_.TimeUtc -le $caseEnd })
$interactive = @($inWindow | Where-Object { $_.LogonType -in 2, 7, 10, 11 -and $_.TargetUserSid -notin 'S-1-5-18', 'S-1-5-19', 'S-1-5-20' -and $_.TargetUserSid -notlike 'S-1-5-90-*' -and $_.TargetUserSid -notlike 'S-1-5-96-*' })
Add-Check 'No interactive logon (types 2/7/10/11) between boot and case end' ($interactive.Count -eq 0) (($interactive | ForEach-Object { "$($_.LogonType)@$($_.TimeUtc.ToString('o'))" }) -join ',')

$system = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; Id = 6005, 6006, 6008, 6013, 1074, 7036; StartTime = $bootUtc.AddMinutes(-10).ToLocalTime() } -ErrorAction SilentlyContinue |
    Where-Object { $_.Id -ne 7036 -or $_.Message -like "*$($svc.DisplayName)*" } |
    ForEach-Object { [pscustomobject]@{ TimeUtc = $_.TimeCreated.ToUniversalTime().ToString('o'); Id = $_.Id; Message = ($_.Message -split "`r?`n")[0] } })
$rebootsAfterBoot = @($system | Where-Object { $_.Id -in 1074, 6006, 6008 -and [DateTime]::Parse($_.TimeUtc).ToUniversalTime() -gt $bootUtc -and [DateTime]::Parse($_.TimeUtc).ToUniversalTime() -le $caseEnd })
Add-Check 'No shutdown/restart between boot and case end' ($rebootsAfterBoot.Count -eq 0) $rebootsAfterBoot.Count

# Raw logs for boot..case end, preserved before anything else can roll them over.
$evtxQuery = "*[System[TimeCreated[@SystemTime>='{0}' and @SystemTime<='{1}']]]" -f $bootUtc.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'"), $caseEnd.ToString("yyyy-MM-ddTHH:mm:ss.fff'Z'")
$evtxBase = [IO.Path]::ChangeExtension($OutFile, $null).TrimEnd('.')
New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$rawLogs = foreach ($log in 'Security', 'System') {
    $path = "$evtxBase.$log.evtx"
    & "$env:windir\System32\wevtutil.exe" epl $log $path "/q:$evtxQuery" /ow:true
    if ($LASTEXITCODE -ne 0) { throw "wevtutil epl $log failed with $LASTEXITCODE" }
    [pscustomobject]@{ Log = $log; File = Split-Path $path -Leaf; Sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
}

$proof = [ordered]@{
    capturedUtc   = [DateTime]::UtcNow.ToString('o')
    lastBootUtc   = $bootUtc.ToString('o')
    caseEndUtc    = $caseEnd.ToString('o')
    service       = [ordered]@{ Name = $svc.Name; StartName = $svc.StartName; StartMode = $svc.StartMode; State = $svc.State; ProcessId = $svc.ProcessId; PathName = $svc.PathName }
    logonTypesSinceBoot = @($logons | Group-Object LogonType | ForEach-Object { "type $($_.Name): $($_.Count)" })
    logonsAfterCaseEnd  = @($logons | Where-Object { $_.TimeUtc -gt $caseEnd } | ForEach-Object { "type $($_.LogonType) @ $($_.TimeUtc.ToString('o')) id $($_.TargetLogonId)" })
    systemEvents  = $system
    pendingReboot = [ordered]@{
        CbsRebootPending  = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
        WuRebootRequired  = Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
        PendingFileRename = [bool](Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue)
    }
    rawLogs       = @($rawLogs)
    checks        = $checks
    collectorNote = 'Collected via PowerShell Direct after caseEndUtc; its own logon is expected after caseEndUtc and is listed in logonsAfterCaseEnd. The service CaseEnd record is the primary session evidence.'
}
$proof | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutFile -Encoding UTF8

$checks | Format-Table -AutoSize | Out-String -Width 240 | Write-Output
Write-Output $OutFile
$failed = @($checks | Where-Object { -not $_.Pass })
if ($failed) { Write-Output "FAILED $($failed.Count)"; exit 1 }
Write-Output 'PASS'
