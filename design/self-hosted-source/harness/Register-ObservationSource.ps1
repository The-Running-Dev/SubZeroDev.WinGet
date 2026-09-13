<#
.SYNOPSIS
    Registers (or removes) the throwaway WinGet REST source pointing at
    Start-InformationEndpoint.ps1, explicit per the design's own choice (10-design.md
    "Control flow", "Because the source is Explicit...").

.DESCRIPTION
    A thin wrapper over `winget source add`/`winget source remove` so the FC24/FC25
    procedure steps ("register the endpoint as a WinGet source") are a named,
    re-runnable command rather than something typed once and forgotten. It does not
    invoke the client operation itself (FC25 step 4 / FC24 step 4) - that step is
    interactive by design (it is the WAM consent prompt being observed) and belongs in
    the maintainer's own session, not a script.

.PARAMETER Name
    Source name. Defaults to something that cannot be mistaken for a real source.

.PARAMETER Arg
    The endpoint's base URL, matching whatever Start-InformationEndpoint.ps1 is bound
    to (e.g. https://winget-observation.local:8443).

.PARAMETER Remove
    Unregisters the named source instead of registering it.

.EXAMPLE
    ./Register-ObservationSource.ps1 -Arg https://winget-observation.local:8443
    Registers the source, Explicit, ready for `winget search <anything> --source
    FC24FC25Observation`.

.EXAMPLE
    ./Register-ObservationSource.ps1 -Remove
    Unregisters it.
#>
[CmdletBinding()]
param(
    [string] $Name = 'FC24FC25Observation',

    [string] $Arg,

    [switch] $Remove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Remove) {
    Write-Host "Removing source '$Name'..."
    winget source remove --name $Name
    exit $LASTEXITCODE
}

if (-not $Arg) {
    throw '-Arg is required when registering (the endpoint URL Start-InformationEndpoint.ps1 is bound to).'
}

Write-Host "Registering source '$Name' -> $Arg (Explicit, type Microsoft.Rest)..."
winget source add --name $Name --arg $Arg --type 'Microsoft.Rest' --explicit --accept-source-agreements

if ($LASTEXITCODE -ne 0) {
    Write-Warning "winget source add exited $LASTEXITCODE. If this is a certificate trust failure, the self-signed certificate from Start-InformationEndpoint.ps1 -UseHttps must be trusted on this machine first (imported into Cert:\LocalMachine\Root, or Cert:\CurrentUser\Root for a per-user trust)."
    exit $LASTEXITCODE
}

Write-Host ''
Write-Host "Registered. Next (FC25 step 4, from an interactive session with no cached WAM account"
Write-Host "for the fixed client id):"
Write-Host "  winget search <anything> --source $Name"
Write-Host ''
Write-Host 'The search does not need to return results - only reach the authentication step. Observe'
Write-Host 'what happens at consent (plan step 5) and record it with New-ObservationRecord.ps1.'
