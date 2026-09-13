#Requires -Modules Microsoft.Graph.Applications, Microsoft.Graph.Authentication
<#
.SYNOPSIS
    Runs the FC24/FC25 observation harness end to end: app registration, endpoint,
    certificate trust, hosts entry, and source registration in one command, instead of
    the five scripts in README.md's "Run order" run and wired together by hand.

.DESCRIPTION
    Orchestrates, in order:
      1. New-EntraObservationApp.ps1 - create/reuse the throwaway app registration.
      2. Start-InformationEndpoint.ps1 -UseHttps - launched as its own elevated process
         (matching the README's "its own window"), polled until it accepts connections.
      3. Trusts the self-signed certificate Start-InformationEndpoint.ps1 created
         (Cert:\LocalMachine\My) by copying it into Cert:\LocalMachine\Root - the manual
         step Register-ObservationSource.ps1's own warning names.
      4. Adds a hosts entry mapping -CertificateSubject to 127.0.0.1, so this machine
         can resolve the endpoint without DNS.
      5. Register-ObservationSource.ps1 - registers the winget source.

    What this deliberately does NOT automate: the WAM consent prompt (FC25 step 4/5) is
    the observation itself, not setup toil, so scripting it away would remove the thing
    being measured. With -RunSearch it still fires the triggering `winget search`
    command for you - the only remaining action is watching, and signing into, the
    window that pops up. With -LaunchSandbox it also generates and launches the FC24
    Sandbox (New-SandboxHarness.ps1) instead of leaving that as a separate manual step.

    Writes harness/output/harness-state.json recording what this run created, so -Remove
    can tear down precisely that - and nothing it didn't create (an existing sslcert
    binding or hosts entry from a prior manual run is left alone).

.PARAMETER DisplayName
    Passed through to New-EntraObservationApp.ps1.

.PARAMETER ScopeName
    Passed through to New-EntraObservationApp.ps1.

.PARAMETER PreAuthorizeWinGetClient
    Passed through to New-EntraObservationApp.ps1. Off by default for the same reason
    named there: the first run should show the actual consent prompt.

.PARAMETER SourceName
    Passed through to Register-ObservationSource.ps1 and, with -LaunchSandbox, to
    New-SandboxHarness.ps1.

.PARAMETER CertificateSubject
    Passed through to Start-InformationEndpoint.ps1 and, with -LaunchSandbox, to
    New-SandboxHarness.ps1. Also the hosts-entry name this script manages.

.PARAMETER Port
    Passed through to Start-InformationEndpoint.ps1 and, with -LaunchSandbox, to
    New-SandboxHarness.ps1 as -EndpointPort.

.PARAMETER TimeoutMinutes
    Passed through to Start-InformationEndpoint.ps1.

.PARAMETER ReadyTimeoutSeconds
    How long to wait for the endpoint process to accept a TCP connection before giving
    up. Default 60.

.PARAMETER RunSearch
    After registration, runs `winget search test --source <SourceName>` directly in
    this session, so the WAM consent window appears immediately instead of leaving that
    command for you to type. Mutually exclusive with -LaunchSandbox (the sandbox's own
    bootstrap script runs the equivalent search inside the Sandbox instead).

.PARAMETER LaunchSandbox
    After registration, detects this host's LAN address, runs New-SandboxHarness.ps1,
    and launches the resulting .wsb - the FC24 path, generated and started instead of
    left as a manual follow-up step.

.PARAMETER WinGetMsixBundlePath
    Passed through to New-SandboxHarness.ps1 when -LaunchSandbox is set.

.PARAMETER OutputRoot
    Root for this harness's throwaway state (config, logs, state file). Defaults to
    harness/output/, matching every other script here.

.PARAMETER Remove
    Tears down exactly what a prior run of this script recorded in
    <OutputRoot>/harness-state.json: stops the endpoint process, removes its sslcert
    binding if still present, removes the hosts entry this script added, removes the
    trusted certificate copy this script added to Cert:\LocalMachine\Root, removes the
    winget source, and removes the app registration. Requires -Force.

.PARAMETER Force
    Required alongside -Remove to tear down. Also passed through to
    New-EntraObservationApp.ps1 as its own reuse/recreate flag when creating.

.EXAMPLE
    ./Initialize-ObservationHarness.ps1 -RunSearch
    Full FC25 setup from an elevated session, ending with the interactive consent
    prompt already on screen.

.EXAMPLE
    ./Initialize-ObservationHarness.ps1 -LaunchSandbox
    Full FC24 setup: endpoint up, source registered, Sandbox generated and launched.

.EXAMPLE
    ./Initialize-ObservationHarness.ps1 -Remove -Force
    Tears down everything the last run created.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $DisplayName = 'SubZeroDev.WinGet FC24-FC25 Observation (throwaway)',

    [string] $ScopeName = 'Access',

    [switch] $PreAuthorizeWinGetClient,

    [string] $SourceName = 'FC24FC25Observation',

    [string] $CertificateSubject = 'winget-observation.local',

    [int] $Port = 8443,

    [int] $TimeoutMinutes = 30,

    [int] $ReadyTimeoutSeconds = 60,

    [switch] $RunSearch,

    [switch] $LaunchSandbox,

    [string] $WinGetMsixBundlePath,

    [string] $OutputRoot = "$PSScriptRoot\output",

    [switch] $Remove,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($RunSearch -and $LaunchSandbox) {
    throw '-RunSearch and -LaunchSandbox are mutually exclusive - the Sandbox bootstrap runs its own search inside the Sandbox instead.'
}

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw 'This orchestrator needs an elevated PowerShell session - it starts an HTTPS endpoint (sslcert bind), trusts a certificate into LocalMachine\Root, and edits the hosts file. Re-run from an elevated session.'
}

$configPath = Join-Path $OutputRoot 'entra-app.json'
$endpointLogPath = Join-Path $OutputRoot 'information-endpoint.log'
$statePath = Join-Path $OutputRoot 'harness-state.json'
$hostsPath = "$env:WINDIR\System32\drivers\etc\hosts"

function Remove-ObservationHarness {
    if (-not $Force) {
        throw 'Removal requires -Force (this repository gates destructive operations on an explicit flag, not a prompt).'
    }
    if (-not (Test-Path $statePath)) {
        Write-Warning "No state file at '$statePath'. Nothing recorded to tear down - remove the individual pieces by hand if needed (see README.md's Cleanup section)."
        return
    }
    $state = Get-Content $statePath -Raw | ConvertFrom-Json

    if ($state.EndpointProcessId) {
        $proc = Get-Process -Id $state.EndpointProcessId -ErrorAction SilentlyContinue
        if ($proc) {
            Write-Host "Stopping endpoint process (PID $($state.EndpointProcessId))..."
            Stop-Process -Id $state.EndpointProcessId -Force
        }
    }

    $binding = netsh http show sslcert "ipport=0.0.0.0:$($state.Port)" 2>$null
    if ($LASTEXITCODE -eq 0 -and $binding) {
        Write-Host "Removing sslcert binding for 0.0.0.0:$($state.Port) (the endpoint process may have been force-stopped before its own cleanup ran)..."
        netsh http delete sslcert "ipport=0.0.0.0:$($state.Port)" | Out-Null
    }

    if ($state.SourceName) {
        Write-Host "Removing winget source '$($state.SourceName)'..."
        winget source remove --name $state.SourceName 2>$null | Out-Null
    }

    if ($state.HostsEntryAdded -and (Test-Path $hostsPath)) {
        $line = $state.HostsEntryLine
        $lines = Get-Content $hostsPath
        if ($lines -contains $line) {
            Write-Host "Removing hosts entry: $line"
            $lines | Where-Object { $_ -ne $line } | Set-Content -Path $hostsPath -Encoding ascii
        }
    }

    if ($state.TrustedCertificateThumbprint) {
        $trusted = Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Thumbprint -eq $state.TrustedCertificateThumbprint }
        if ($trusted) {
            Write-Host "Removing trusted certificate copy from Cert:\LocalMachine\Root (thumbprint $($state.TrustedCertificateThumbprint))..."
            Remove-Item "Cert:\LocalMachine\Root\$($state.TrustedCertificateThumbprint)" -Force
        }
    }

    & "$PSScriptRoot\New-EntraObservationApp.ps1" -OutputPath $configPath -DisplayName $DisplayName -Remove -Force

    Remove-Item $statePath -Force
    Write-Host ''
    Write-Host 'Harness torn down. The original certificate under Cert:\LocalMachine\My is left in place for reuse (per README.md).'
}

function Wait-EndpointReady {
    param([Parameter(Mandatory)][int] $Port, [Parameter(Mandatory)][int] $TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $client = [System.Net.Sockets.TcpClient]::new()
            $client.Connect('127.0.0.1', $Port)
            $client.Close()
            return $true
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    return $false
}

function Initialize-Harness {
    if (-not (Test-Path $OutputRoot)) {
        New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
    }

    $state = [ordered]@{
        Port                         = $Port
        SourceName                   = $SourceName
        CertificateSubject           = $CertificateSubject
        EndpointProcessId            = $null
        HostsEntryAdded              = $false
        HostsEntryLine               = $null
        TrustedCertificateThumbprint = $null
        CreatedAt                    = (Get-Date).ToUniversalTime().ToString('o')
    }

    Write-Host '== Step 1/5: Entra app registration =='
    & "$PSScriptRoot\New-EntraObservationApp.ps1" `
        -DisplayName $DisplayName -ScopeName $ScopeName -OutputPath $configPath `
        -PreAuthorizeWinGetClient:$PreAuthorizeWinGetClient -Force:$Force | Out-Null

    Write-Host ''
    Write-Host '== Step 2/5: information endpoint (own elevated process) =='
    $pwshPath = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
    if (-not $pwshPath) { $pwshPath = (Get-Command powershell).Source }

    $endpointArgs = @(
        '-NoExit', '-File', "$PSScriptRoot\Start-InformationEndpoint.ps1",
        '-UseHttps',
        '-ConfigPath', $configPath,
        '-Port', $Port,
        '-CertificateSubject', $CertificateSubject,
        '-TimeoutMinutes', $TimeoutMinutes,
        '-LogPath', $endpointLogPath
    )
    $endpointProcess = Start-Process -FilePath $pwshPath -ArgumentList $endpointArgs -PassThru
    $state.EndpointProcessId = $endpointProcess.Id
    Write-Host "Started endpoint process PID $($endpointProcess.Id). Waiting for it to accept connections..."

    if (-not (Wait-EndpointReady -Port $Port -TimeoutSeconds $ReadyTimeoutSeconds)) {
        $state | ConvertTo-Json | Set-Content -Path $statePath -Encoding utf8
        throw "Endpoint did not accept connections on 127.0.0.1:$Port within $ReadyTimeoutSeconds second(s). Check the endpoint window and $endpointLogPath. State was saved to $statePath - run -Remove -Force to clean up, or inspect the endpoint window directly."
    }
    Write-Host 'Endpoint is up.'

    Write-Host ''
    Write-Host '== Step 3/5: trust the endpoint certificate =='
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq "CN=$CertificateSubject" } | Select-Object -First 1
    if (-not $cert) {
        $state | ConvertTo-Json | Set-Content -Path $statePath -Encoding utf8
        throw "No certificate found for CN=$CertificateSubject in Cert:\LocalMachine\My after the endpoint reported ready. State was saved to $statePath."
    }
    $alreadyTrusted = Get-ChildItem Cert:\LocalMachine\Root | Where-Object { $_.Thumbprint -eq $cert.Thumbprint }
    if ($alreadyTrusted) {
        Write-Host "Certificate (thumbprint $($cert.Thumbprint)) is already trusted; leaving it - -Remove will not touch a certificate it did not add."
    } else {
        Write-Host "Trusting certificate (thumbprint $($cert.Thumbprint)) into Cert:\LocalMachine\Root..."
        Export-Certificate -Cert $cert -FilePath "$OutputRoot\endpoint-cert.cer" | Out-Null
        Import-Certificate -FilePath "$OutputRoot\endpoint-cert.cer" -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        $state.TrustedCertificateThumbprint = $cert.Thumbprint
    }

    Write-Host ''
    Write-Host '== Step 4/5: hosts entry =='
    $hostsLine = "127.0.0.1`t$CertificateSubject"
    $existingHosts = Test-Path $hostsPath
    $alreadyPresent = $existingHosts -and ((Get-Content $hostsPath) -contains $hostsLine)
    if ($alreadyPresent) {
        Write-Host "hosts already maps $CertificateSubject -> 127.0.0.1; leaving it."
    } else {
        Write-Host "Adding hosts entry: $hostsLine"
        Add-Content -Path $hostsPath -Value $hostsLine
        $state.HostsEntryAdded = $true
        $state.HostsEntryLine = $hostsLine
    }

    Write-Host ''
    Write-Host '== Step 5/5: register the winget source =='
    & "$PSScriptRoot\Register-ObservationSource.ps1" -Name $SourceName -Arg "https://${CertificateSubject}:$Port"

    $state | ConvertTo-Json | Set-Content -Path $statePath -Encoding utf8

    if ($LaunchSandbox) {
        Write-Host ''
        Write-Host '== FC24: generating and launching the Sandbox =='
        $lanAddress = Get-NetIPAddress -AddressFamily IPv4 |
            Where-Object { $_.IPAddress -notlike '169.254.*' -and $_.IPAddress -ne '127.0.0.1' -and $_.PrefixOrigin -ne 'WellKnown' } |
            Select-Object -First 1 -ExpandProperty IPAddress
        if (-not $lanAddress) {
            throw 'Could not auto-detect a LAN IPv4 address for the Sandbox to reach this host. Run New-SandboxHarness.ps1 -HostAddress <address> by hand instead.'
        }
        $sandboxArgs = @{
            CertificateSubject = $CertificateSubject
            HostAddress        = $lanAddress
            EndpointPort       = $Port
            SourceName         = $SourceName
        }
        if ($WinGetMsixBundlePath) { $sandboxArgs['WinGetMsixBundlePath'] = $WinGetMsixBundlePath }
        & "$PSScriptRoot\New-SandboxHarness.ps1" @sandboxArgs

        $wsbPath = Join-Path $OutputRoot 'FC24Observation.wsb'
        Write-Host "Launching $wsbPath ..."
        Start-Process -FilePath $wsbPath
        Write-Host 'Watch the Sandbox window for the WAM consent prompt during its bootstrap winget search.'
    } elseif ($RunSearch) {
        Write-Host ''
        Write-Host '== FC25: triggering the search (watch for the WAM consent window) =='
        winget search test --source $SourceName --accept-source-agreements
        Write-Host "winget search exit code: $LASTEXITCODE"
    } else {
        Write-Host ''
        Write-Host "Setup complete. Trigger the observation yourself when ready:"
        Write-Host "  winget search test --source $SourceName"
    }

    Write-Host ''
    Write-Host "State saved to $statePath. Record the outcome with New-ObservationRecord.ps1, then tear down with:"
    Write-Host '  ./Initialize-ObservationHarness.ps1 -Remove -Force'
}

if ($Remove) {
    Remove-ObservationHarness
} else {
    Initialize-Harness
}
