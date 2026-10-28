#Requires -Version 5.1
#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Captures, as SYSTEM, the source agreements the winget source presents to the private Microsoft.WinGet.Client
    1.29.380 module. Records them for operator approval. Never accepts an agreement, opens or updates the source,
    or installs anything.
.DESCRIPTION
    PREPARED, NOT EXECUTED. Procedure: 05-run-reset-procedure.md, section 1 step 7. Run during guest preparation,
    before the prerequisites-ready checkpoint, through PowerShell Direct from the host.

    Route: a one-shot Task Scheduler task running as S-1-5-18 starts the private pwsh with -NoProfile -MTA and an
    inner script. The inner script imports the module by absolute psd1 path, records Get-WinGetSource -Name winget, then
    reads PackageCatalogReference.SourceAgreements (Label/Text/Url).

    Get-WinGetSource returns PSSourceResult, which does not expose agreements (winget-cli v1.29.380,
    PSObjects/PSSourceResult.cs). The inner script therefore reaches the COM PackageCatalogReference through the
    module's internal PackageManagerWrapper.Instance.GetPackageCatalogByName(name), the same call
    Get-WinGetSource makes (Commands/Common/ManagementDeploymentCommand.cs:49-60). That is an internal type: if it
    cannot be reached, the capture records "not-observable" and exits 3, and installation stays blocked.

    -MTA: pwsh starts in STA by default. The module's cmdlets move their own work to an MTA thread, but a direct
    wrapper call does not, and throws SingleThreadedApartmentException in STA (observed in provisioning attempt 5).
    The capture records the apartment state it ran in.

    [source] For an unopened reference, SourceAgreements reads ISourceReference::GetInformation(); the PreIndexed
    (winget) factory does not override it, so the expected result is an empty list with no network access
    (Microsoft.Management.Deployment/PackageCatalogReference.cpp:241-257, AppInstallerRepositoryCore/ISource.h:85).
    This is runtime-pending until observed.

    State note: creating the in-proc PackageManager may write WinGet settings or log files under the SYSTEM profile.
    That happens before the checkpoint, so it is part of the baseline; the inventory records the SYSTEM WinGet
    state afterwards.

    Exit codes: 0 captured, 3 not observable, 1 any other failure.
#>
[CmdletBinding()]
param(
    [string] $OutFile = (Join-Path $env:ProgramData ("SubZeroDev\FileManager\Validation\Evidence\S00-source-agreements-{0:yyyyMMddTHHmmssZ}.json" -f [DateTime]::UtcNow)),
    [string] $SourceName = 'winget',
    [int] $TimeoutSeconds = 300
)
$ErrorActionPreference = 'Stop'

$serviceRoot = Join-Path $env:ProgramFiles 'SubZeroDev\FileManager\Service'
$pwshExe = Join-Path $serviceRoot 'pwsh\pwsh.exe'
$modulePsd1 = Join-Path $serviceRoot 'Modules\Microsoft.WinGet.Client\1.29.380\Microsoft.WinGet.Client.psd1'
$workDir = Join-Path $env:ProgramData 'SubZeroDev\FileManager\Validation\Work'
foreach ($p in $pwshExe, $modulePsd1) { if (-not (Test-Path -LiteralPath $p)) { throw "Missing prerequisite: $p" } }

$inner = @'
param([string] $ModulePsd1, [string] $SourceName, [string] $OutFile)
$ErrorActionPreference = 'Stop'
$result = [ordered]@{
    schema         = 'subzerodev.filemanager.s00.source-agreements-capture/1'
    capturedUtc    = [DateTime]::UtcNow.ToString('o')
    identitySid    = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    processSession = [Diagnostics.Process]::GetCurrentProcess().SessionId
    apartmentState = [Threading.Thread]::CurrentThread.GetApartmentState().ToString()
    pwshVersion    = $PSVersionTable.PSVersion.ToString()
    pwshRuntime    = [Environment]::Version.ToString()
    module         = $null
    source         = $null
    route          = 'Microsoft.WinGet.Client.Engine PackageManagerWrapper.Instance.GetPackageCatalogByName'
    status         = 'not-observable'
    agreementsPresented = $null
    agreements     = @()
    digestBasis    = 'SHA256 of UTF-8 ConvertTo-Json -Compress of the agreements array, each item ordered Label, Text, Url'
    digestSha256   = $null
    error          = $null
}
try {
    $m = Import-Module -Name $ModulePsd1 -Force -PassThru
    $result.module = [ordered]@{ Name = $m.Name; Version = $m.Version.ToString(); Path = $m.Path }
    $s = Get-WinGetSource -Name $SourceName
    $result.source = [ordered]@{ Name = $s.Name; Argument = $s.Argument; Type = $s.Type; TrustLevel = "$($s.TrustLevel)"; Explicit = $s.Explicit; Priority = $s.Priority }

    $wrapperType = $null
    foreach ($a in [AppDomain]::CurrentDomain.GetAssemblies()) {
        if ($a.GetName().Name -ne 'Microsoft.WinGet.Client.Engine') { continue }
        $wrapperType = $a.GetTypes() | Where-Object { $_.Name -eq 'PackageManagerWrapper' } | Select-Object -First 1
        if ($wrapperType) { break }
    }
    if (-not $wrapperType) { throw 'PackageManagerWrapper type not found in a loaded Microsoft.WinGet.Client.Engine assembly.' }
    $flags = [Reflection.BindingFlags]'Public, NonPublic, Static, Instance'
    $instance = $wrapperType.GetProperty('Instance', $flags).GetValue($null)
    $ref = $wrapperType.GetMethod('GetPackageCatalogByName', $flags).Invoke($instance, @($SourceName))
    if ($null -eq $ref) { throw "GetPackageCatalogByName('$SourceName') returned null." }

    $items = @(foreach ($x in $ref.SourceAgreements) { [ordered]@{ Label = "$($x.Label)"; Text = "$($x.Text)"; Url = "$($x.Url)" } })
    $result.agreements = $items
    $result.agreementsPresented = $items.Count
    $json = ConvertTo-Json -InputObject @($items) -Compress -Depth 3
    $sha = [Security.Cryptography.SHA256]::Create()
    $result.digestSha256 = -join ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json)) | ForEach-Object { $_.ToString('X2') })
    $result.status = 'captured'
} catch {
    $result.error = $_.Exception.ToString()
}
New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutFile -Encoding UTF8
if ($result.status -eq 'captured') { exit 0 } else { exit 3 }
'@

$taskPath = '\SubZeroDev\'
$taskName = 'SZD-FM-S00-SourceAgreements'
$innerFile = Join-Path $workDir 'Get-S00SourceAgreements.inner.ps1'
New-Item -ItemType Directory -Force $workDir | Out-Null
Set-Content -LiteralPath $innerFile -Value $inner -Encoding UTF8

$arguments = '-NoProfile -NonInteractive -MTA -ExecutionPolicy Bypass -File "{0}" -ModulePsd1 "{1}" -SourceName "{2}" -OutFile "{3}"' -f $innerFile, $modulePsd1, $SourceName, $OutFile
$action = New-ScheduledTaskAction -Execute $pwshExe -Argument $arguments
$principal = New-ScheduledTaskPrincipal -UserId 'S-1-5-18' -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds $TimeoutSeconds) -DontStopIfGoingOnBatteries -AllowStartIfOnBatteries

try {
    Register-ScheduledTask -TaskPath $taskPath -TaskName $taskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null
    Start-ScheduledTask -TaskPath $taskPath -TaskName $taskName
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds + 30)
    do {
        Start-Sleep -Seconds 2
        $state = (Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName).State
    } while ($state -eq 'Running' -and [DateTime]::UtcNow -lt $deadline)
    $taskResult = (Get-ScheduledTaskInfo -TaskPath $taskPath -TaskName $taskName).LastTaskResult
} finally {
    Unregister-ScheduledTask -TaskPath $taskPath -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $innerFile -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $OutFile)) { Write-Output "FAILED: no output (task state $state, result $taskResult)"; exit 1 }
$captured = Get-Content -LiteralPath $OutFile -Raw | ConvertFrom-Json
Write-Output $OutFile
Write-Output ("status={0} identity={1} session={2} apartment={3} presented={4} digest={5}" -f $captured.status, $captured.identitySid, $captured.processSession, $captured.apartmentState, $captured.agreementsPresented, $captured.digestSha256)
if ($captured.identitySid -ne 'S-1-5-18') { Write-Output 'FAILED: capture did not run as SYSTEM'; exit 1 }
if ($captured.status -ne 'captured') { Write-Output "NOT-OBSERVABLE: $($captured.error)"; exit 3 }
Write-Output 'Copy capture.* and digestSha256 into agreements/source-agreements.approval.json; the operator approves before any install.'
exit 0
