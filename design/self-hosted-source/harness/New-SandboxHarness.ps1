<#
.SYNOPSIS
    Builds a Windows Sandbox configuration and bootstrap script for FC24: does WAM
    interactive authentication complete inside a disposable, non-Entra-joined client?

.DESCRIPTION
    Generates a .wsb file plus a mapped-folder bootstrap script that, inside a fresh
    Sandbox instance: trusts the observation harness's self-signed certificate, points
    the hostname at this host, registers the same source FC25 used, and runs the same
    client operation - matching design/self-hosted-source/fc24-fc25-observation-plan.md
    FC24 steps 3-4 exactly, so the only thing left for the maintainer to do is watch
    what happens at the WAM consent step and record it.

    Each launch of the generated .wsb is itself a fresh, non-Entra-joined instance with
    no prior WAM state - the plan requires repeating this at least twice from separate
    fresh instances before treating either outcome as settled (step 6); re-run this
    script's .wsb output that many times rather than reusing one instance.

    Does not attempt to solve installing winget inside a fresh Sandbox unattended - that
    depends on Sandbox's network/Store access, which is environment-specific and not
    part of what FC24 is asking. If -WinGetMsixBundlePath is not given and the Sandbox
    image has no winget, the bootstrap script says so and stops rather than guessing.

.PARAMETER CertificateSubject
    Must match Start-InformationEndpoint.ps1's -CertificateSubject. The certificate is
    looked up by this subject in Cert:\LocalMachine\My and its public half (only) is
    exported into the mapped folder - the private key never leaves the host store,
    matching FC14's "the certificate private key... never in Git" naming (this harness
    is not Git, but the same reasoning applies: irreplaceable host state is not copied
    somewhere it could leak from).

.PARAMETER HostAddress
    This host's address as reachable from inside the Sandbox (Sandbox has network
    access by default via a NAT'd virtual switch, so the host's LAN address is normally
    correct - Start-InformationEndpoint.ps1 -UseHttps prints it). Required.

.PARAMETER EndpointPort
    Must match Start-InformationEndpoint.ps1's -Port. Default 8443.

.PARAMETER SourceName
    Must match Register-ObservationSource.ps1's -Name. Default FC24FC25Observation.

.PARAMETER WinGetMsixBundlePath
    Optional path to a winget (Microsoft.DesktopAppInstaller) .msixbundle to sideload
    inside the Sandbox if it lacks winget. Copied into the mapped folder.

.PARAMETER SharedFolderPath
    Host folder mapped read-write into the Sandbox (as
    C:\Users\WDAGUtilityAccount\Desktop\Shared). Holds the exported certificate, the
    bootstrap script, and the log the Sandbox writes back. Defaults under
    harness/output/, which is gitignored.

.PARAMETER MemoryInMB
    Sandbox memory allocation. Default 4096.

.PARAMETER WsbPath
    Where to write the .wsb file. Defaults under harness/output/.

.EXAMPLE
    ./New-SandboxHarness.ps1 -HostAddress 192.168.1.23
    Writes harness/output/sandbox-share/* and harness/output/FC24Observation.wsb.
    Double-click the .wsb to launch a fresh Sandbox instance.
#>
[CmdletBinding()]
param(
    [string] $CertificateSubject = 'winget-observation.local',

    [Parameter(Mandatory)]
    [string] $HostAddress,

    [int] $EndpointPort = 8443,

    [string] $SourceName = 'FC24FC25Observation',

    [string] $WinGetMsixBundlePath,

    [string] $SharedFolderPath = "$PSScriptRoot\output\sandbox-share",

    [int] $MemoryInMB = 4096,

    [string] $WsbPath = "$PSScriptRoot\output\FC24Observation.wsb"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq "CN=$CertificateSubject" } | Select-Object -First 1
if (-not $cert) {
    throw "No certificate found for CN=$CertificateSubject in Cert:\LocalMachine\My. Run Start-InformationEndpoint.ps1 -UseHttps (with a matching -CertificateSubject) first."
}

if (-not (Test-Path $SharedFolderPath)) {
    New-Item -ItemType Directory -Path $SharedFolderPath -Force | Out-Null
}

$cerPath = Join-Path $SharedFolderPath 'observation.cer'
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null
Write-Host "Exported public certificate (no private key) to $cerPath."

$msixArg = ''
if ($WinGetMsixBundlePath) {
    if (-not (Test-Path $WinGetMsixBundlePath)) {
        throw "-WinGetMsixBundlePath '$WinGetMsixBundlePath' does not exist."
    }
    $destBundle = Join-Path $SharedFolderPath (Split-Path $WinGetMsixBundlePath -Leaf)
    Copy-Item $WinGetMsixBundlePath $destBundle -Force
    $msixArg = Split-Path $destBundle -Leaf
    Write-Host "Copied winget bundle to $destBundle."
}

$bootstrapContent = @"
`$ErrorActionPreference = 'Stop'
`$shared = 'C:\Users\WDAGUtilityAccount\Desktop\Shared'
`$log = Join-Path `$shared 'fc24-run.log'

function Write-Log {
    param([string] `$Message)
    `$line = "[`$((Get-Date).ToUniversalTime().ToString('o'))] `$Message"
    Write-Host `$line
    Add-Content -Path `$log -Value `$line
}

Write-Log 'FC24 Sandbox bootstrap starting.'

Write-Log 'Importing observation certificate into LocalMachine\Root...'
Import-Certificate -FilePath (Join-Path `$shared 'observation.cer') -CertStoreLocation Cert:\LocalMachine\Root | Out-Null

Write-Log 'Adding hosts entry for $CertificateSubject -> $HostAddress...'
Add-Content -Path "`$env:WINDIR\System32\drivers\etc\hosts" -Value "$HostAddress`t$CertificateSubject"

`$wingetPresent = `$null -ne (Get-Command winget.exe -ErrorAction SilentlyContinue)
if (-not `$wingetPresent -and '$msixArg') {
    Write-Log 'winget not found; sideloading $msixArg...'
    try {
        Add-AppxPackage -Path (Join-Path `$shared '$msixArg')
        `$wingetPresent = `$true
    } catch {
        Write-Log "Sideload failed: `$(`$_.Exception.Message)"
    }
}

if (-not `$wingetPresent) {
    Write-Log 'winget.exe is not available in this Sandbox image and no -WinGetMsixBundlePath was supplied to sideload one. Install App Installer from the Microsoft Store inside this Sandbox, then re-run the lines below manually from a PowerShell window.'
} else {
    Write-Log 'Registering source $SourceName -> https://${CertificateSubject}:$EndpointPort ...'
    winget source add --name '$SourceName' --arg 'https://${CertificateSubject}:$EndpointPort' --type 'Microsoft.Rest' --explicit --accept-source-agreements 2>&1 | ForEach-Object { Write-Log "winget source add: `$_" }

    Write-Log 'Running: winget search <anything> --source $SourceName (this is FC24 step 4 - watch for the WAM window).'
    winget search test --source '$SourceName' --accept-source-agreements 2>&1 | ForEach-Object { Write-Log "winget search: `$_" }
    Write-Log "winget search exit code: `$LASTEXITCODE"
}

Write-Log 'Bootstrap complete. Record the outcome (per fc24-fc25-observation-plan.md) with New-ObservationRecord.ps1 on the host, using this log as the Run/Assertions source.'
"@

$bootstrapPath = Join-Path $SharedFolderPath 'bootstrap.ps1'
Set-Content -Path $bootstrapPath -Value $bootstrapContent -Encoding utf8
Write-Host "Wrote bootstrap script to $bootstrapPath."

$wsbContent = @"
<Configuration>
  <VGpu>Disable</VGpu>
  <Networking>Enable</Networking>
  <MemoryInMB>$MemoryInMB</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$SharedFolderPath</HostFolder>
      <SandboxFolder>C:\Users\WDAGUtilityAccount\Desktop\Shared</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe -ExecutionPolicy Bypass -NoExit -File C:\Users\WDAGUtilityAccount\Desktop\Shared\bootstrap.ps1</Command>
  </LogonCommand>
</Configuration>
"@

$wsbDir = Split-Path $WsbPath -Parent
if (-not (Test-Path $wsbDir)) {
    New-Item -ItemType Directory -Path $wsbDir -Force | Out-Null
}
Set-Content -Path $WsbPath -Value $wsbContent -Encoding utf8

Write-Host ''
Write-Host "Sandbox configuration ready: $WsbPath"
Write-Host 'Double-click it (or `Start-Process` it) to launch a fresh Sandbox instance. The bootstrap'
Write-Host 'script runs automatically at logon, keeps its window open (-NoExit), and the WAM sign-in'
Write-Host 'window should appear during the `winget search` step - sign in and consent as the'
Write-Host 'maintainer, then observe and record what happened per the plan.'
Write-Host ''
Write-Host "Run output lands back on the host at: $SharedFolderPath\fc24-run.log"
Write-Host 'The plan requires repeating this from at least two separate fresh instances before'
Write-Host 'treating either outcome as settled - re-launch this same .wsb again for the second run.'
