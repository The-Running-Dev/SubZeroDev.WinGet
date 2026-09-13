<#
.SYNOPSIS
    Serves the one HTTP response FC24/FC25 actually need: a WinGet REST /information
    endpoint advertising a microsoftEntraId resource, plus a /manifestSearch stub that
    logs whether a bearer token arrived.

.DESCRIPTION
    This is the "minimal harness" design/self-hosted-source/fc24-fc25-observation-plan.md
    describes: it does not serve a manifest tree, search results, or anything from the
    production design - it exists only to give winget.exe something to authenticate
    against, and to observe whether that authentication happens. Do not point a real
    source name or the production certificate at this - it is throwaway, and
    Register-ObservationSource.ps1's default name says so.

    Two things this script deliberately does NOT do, because FC24/FC25 do not need them:
      - Validate the bearer token's signature. Claims are decoded and logged for
        observation only; treat every logged claim as unverified, self-reported data.
      - Serve any package. /manifestSearch always answers an empty result set once a
        bearer token is present - "the search need not return results, only trigger the
        authentication attempt" (the plan, FC25 step 4).

    Serves /information with Cache-Control: no-store, matching the design's chosen
    behaviour (10-design.md "Client-side information caching"), not the client's
    sixty-second fallback - this harness is not testing that fallback.

.PARAMETER ConfigPath
    Path to the JSON New-EntraObservationApp.ps1 wrote. -Resource/-Scope override its
    contents when given directly.

.PARAMETER Resource
    The Application ID URI to advertise under MicrosoftEntraIdAuthenticationInfo.Resource.
    Read from -ConfigPath if omitted.

.PARAMETER Scope
    The custom scope to advertise. Read from -ConfigPath if omitted.

.PARAMETER Port
    Port to listen on. Defaults to 8443 for HTTPS, 8080 for HTTP.

.PARAMETER UseHttps
    Bind TLS using a self-signed certificate. The observation plan notes winget.exe may
    require HTTPS to register a source at all - trusting a self-signed certificate
    locally (and, for FC24, inside the Sandbox - see New-SandboxHarness.ps1) is enough
    for this throwaway harness. Requires an elevated PowerShell session (binds an
    sslcert and, if listening on 0.0.0.0, reserves a URL ACL).

.PARAMETER CertificateSubject
    DNS name the self-signed certificate is issued for, and the name winget must be
    told to reach (via a hosts entry - see New-SandboxHarness.ps1 and the printed
    instructions). Defaults to a name that is obviously not a real host.

.PARAMETER TimeoutMinutes
    This is a throwaway dev-machine listener, not a service - it stops itself after
    this much wall-clock time with no request, so a forgotten window doesn't bind the
    port indefinitely. Default 30.

.PARAMETER LogPath
    Where request/claim observations are appended. Defaults under harness/output/,
    which is gitignored.

.EXAMPLE
    ./Start-InformationEndpoint.ps1 -UseHttps
    Reads harness/output/entra-app.json, creates/reuses a self-signed certificate for
    winget-observation.local, binds HTTPS on 8443, and serves /information and
    /manifestSearch until 30 minutes pass with no request or Ctrl+C is pressed.
#>
[CmdletBinding()]
param(
    [string] $ConfigPath = "$PSScriptRoot\output\entra-app.json",

    [string] $Resource,

    [string] $Scope,

    [int] $Port,

    [switch] $UseHttps,

    [string] $CertificateSubject = 'winget-observation.local',

    [int] $TimeoutMinutes = 30,

    [string] $LogPath = "$PSScriptRoot\output\information-endpoint.log"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $Resource -or -not $Scope) {
    if (-not (Test-Path $ConfigPath)) {
        throw "No -Resource/-Scope given and config file not found at '$ConfigPath'. Run New-EntraObservationApp.ps1 first, or pass -Resource/-Scope directly."
    }
    $config = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    if (-not $Resource) { $Resource = $config.Resource }
    if (-not $Scope) { $Scope = $config.Scope }
}

if (-not $Port) {
    $Port = if ($UseHttps) { 8443 } else { 8080 }
}

$logDir = Split-Path $LogPath -Parent
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

function Write-ObservationLog {
    param([string] $Message)
    $line = "[$((Get-Date).ToUniversalTime().ToString('o'))] $Message"
    Write-Host $line
    Add-Content -Path $LogPath -Value $line
}

function ConvertFrom-Base64Url {
    param([Parameter(Mandatory)][string] $Value)
    $padded = $Value.Replace('-', '+').Replace('_', '/')
    switch ($padded.Length % 4) {
        2 { $padded += '==' }
        3 { $padded += '=' }
    }
    [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($padded))
}

function Read-UnvalidatedBearerClaims {
    param([string] $AuthorizationHeader)

    if (-not $AuthorizationHeader -or $AuthorizationHeader -notlike 'Bearer *') {
        return $null
    }
    $token = $AuthorizationHeader.Substring('Bearer '.Length)
    $parts = $token.Split('.')
    if ($parts.Count -lt 2) {
        return 'unparseable token (not a JWT)'
    }
    try {
        $claims = ConvertFrom-Base64Url $parts[1] | ConvertFrom-Json
        return "UNVALIDATED claims - aud=$($claims.aud) tid=$($claims.tid) appid=$($claims.appid) scp=$($claims.scp)"
    } catch {
        return "token present but claims could not be decoded: $($_.Exception.Message)"
    }
}

$certThumbprint = $null
$urlPrefix = $null
$sslCertBound = $false

if ($UseHttps) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        throw '-UseHttps needs an elevated PowerShell session (binds an sslcert with netsh and reserves a URL ACL).'
    }

    $existingCert = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq "CN=$CertificateSubject" } | Select-Object -First 1
    $cert = if ($existingCert) {
        Write-ObservationLog "Reusing existing self-signed certificate for CN=$CertificateSubject (thumbprint $($existingCert.Thumbprint))."
        $existingCert
    } else {
        Write-ObservationLog "Creating self-signed certificate for CN=$CertificateSubject..."
        New-SelfSignedCertificate -DnsName $CertificateSubject -CertStoreLocation Cert:\LocalMachine\My -KeyExportPolicy Exportable -NotAfter (Get-Date).AddDays(30)
    }
    $certThumbprint = $cert.Thumbprint

    $appGuid = '{4c9e2c2e-2f1a-4b7a-9e2b-7c9a2b7f1a10}'
    $binding = netsh http show sslcert ipport=0.0.0.0:$Port 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $binding) {
        Write-ObservationLog "Binding certificate to 0.0.0.0:$Port..."
        netsh http add sslcert ipport=0.0.0.0:$Port certhash=$certThumbprint appid=$appGuid | Out-Null
        $sslCertBound = $true
    } else {
        Write-ObservationLog "An sslcert binding already exists for 0.0.0.0:$Port; leaving it as-is."
    }

    $urlPrefix = "https://+:$Port/"

    $lanAddress = Get-NetIPAddress -AddressFamily IPv4 |
        Where-Object { $_.IPAddress -notlike '169.254.*' -and $_.IPAddress -ne '127.0.0.1' -and $_.PrefixOrigin -ne 'WellKnown' } |
        Select-Object -First 1 -ExpandProperty IPAddress

    Write-Host ''
    Write-Host "This machine's certificate is issued for CN=$CertificateSubject, not an IP address."
    Write-Host "Any client reaching this endpoint (including a Windows Sandbox instance for FC24) must"
    Write-Host "resolve '$CertificateSubject' to this host's address and trust the certificate. On this"
    Write-Host "host's LAN, that address is currently: $lanAddress"
    Write-Host "New-SandboxHarness.ps1 wires this up automatically for the FC24 run; for a direct FC25"
    Write-Host "run from this machine, add a hosts entry mapping $CertificateSubject to $lanAddress (or"
    Write-Host "127.0.0.1 if registering the source from this same machine)."
    Write-Host ''
} else {
    $urlPrefix = "http://+:$Port/"
    Write-ObservationLog 'Serving over HTTP. The observation plan notes winget.exe may require HTTPS to register a source at all - if registration fails, re-run with -UseHttps.'
}

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add($urlPrefix)

try {
    $listener.Start()
} catch [System.Net.HttpListenerException] {
    throw "Failed to bind $urlPrefix - if this is HTTP on 0.0.0.0, an elevated session or a urlacl reservation (netsh http add urlacl url=$urlPrefix user=Everyone) is likely needed. Original error: $($_.Exception.Message)"
}

Write-ObservationLog "Listening on $urlPrefix - GET /information, POST /manifestSearch."
Write-ObservationLog "Advertising Resource='$Resource' Scope='$Scope'."
Write-ObservationLog "Stops automatically after $TimeoutMinutes minute(s) with no request, or on Ctrl+C."

try {
    while ($true) {
        $contextTask = $listener.GetContextAsync()
        if (-not $contextTask.Wait([TimeSpan]::FromMinutes($TimeoutMinutes))) {
            Write-ObservationLog "No request received within $TimeoutMinutes minute(s); stopping."
            break
        }
        $context = $contextTask.Result
        $request = $context.Request
        $response = $context.Response

        try {
            $authHeader = $request.Headers['Authorization']
            $claimsSummary = Read-UnvalidatedBearerClaims -AuthorizationHeader $authHeader

            if ($request.HttpMethod -eq 'GET' -and $request.Url.AbsolutePath -eq '/information') {
                Write-ObservationLog "GET /information from $($request.RemoteEndPoint) - Authorization present: $([bool]$authHeader)."
                $body = [ordered]@{
                    Data = [ordered]@{
                        SourceIdentifier         = 'FC24FC25ObservationHarness'
                        ServerSupportedVersions  = @('1.7.0')
                        Authentication           = [ordered]@{
                            AuthenticationType               = 'microsoftEntraId'
                            MicrosoftEntraIdAuthenticationInfo = [ordered]@{
                                Resource = $Resource
                                Scope    = $Scope
                            }
                        }
                    }
                } | ConvertTo-Json -Depth 6

                $response.Headers['Cache-Control'] = 'no-store'
                $response.ContentType = 'application/json'
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
                $response.ContentLength64 = $bytes.Length
                $response.OutputStream.Write($bytes, 0, $bytes.Length)
                $response.StatusCode = 200
            } elseif ($request.HttpMethod -eq 'POST' -and $request.Url.AbsolutePath -eq '/manifestSearch') {
                if (-not $authHeader) {
                    Write-ObservationLog "POST /manifestSearch from $($request.RemoteEndPoint) - NO Authorization header. Refusing with 401 (this is the FC25 positive-path precondition, not FC20's guard check - see the plan)."
                    $response.Headers['WWW-Authenticate'] = 'Bearer'
                    $response.StatusCode = 401
                } else {
                    Write-ObservationLog "POST /manifestSearch from $($request.RemoteEndPoint) - Authorization present. $claimsSummary"
                    $body = '{"Data":[]}'
                    $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
                    $response.ContentType = 'application/json'
                    $response.ContentLength64 = $bytes.Length
                    $response.OutputStream.Write($bytes, 0, $bytes.Length)
                    $response.StatusCode = 200
                }
            } else {
                Write-ObservationLog "Unhandled $($request.HttpMethod) $($request.Url.AbsolutePath) from $($request.RemoteEndPoint) - 404."
                $response.StatusCode = 404
            }
        } finally {
            $response.OutputStream.Close()
        }
    }
} finally {
    $listener.Stop()
    $listener.Close()

    if ($sslCertBound) {
        Write-ObservationLog "Removing the sslcert binding this run created (0.0.0.0:$Port). The certificate itself is left in Cert:\LocalMachine\My for reuse - remove it by thumbprint ($certThumbprint) if this harness will not be run again."
        netsh http delete sslcert ipport=0.0.0.0:$Port | Out-Null
    }
}
