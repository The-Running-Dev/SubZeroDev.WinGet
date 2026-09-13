#Requires -Modules Microsoft.Graph.Applications, Microsoft.Graph.Authentication
<#
.SYNOPSIS
    Creates (or removes) the throwaway Entra app registration FC25 needs: an app that
    exposes a custom resource/scope, so the fixed WinGet client id has something to
    request a token for.

.DESCRIPTION
    Implements the "app registration in that tenant" prerequisite named in
    design/self-hosted-source/fc24-fc25-observation-plan.md. It does not touch the
    fixed, Microsoft-owned WinGet client id (7b8ea11a-7f45-4b3a-ab51-794d5863af15,
    verified against winget-cli@5c88b96f
    src/AppInstallerCommonCore/Authentication/WebAccountManagerAuthenticator.cpp:20,21) -
    that id is never registered as an app here. This script only creates the *resource*
    side: an app registration the maintainer's tenant owns, with an Application ID URI
    and one custom scope, matching the plan's "Shared prerequisites" section.

    No client secret or certificate is created on this registration - the client
    authenticating against it is the fixed client id, not a confidential client this
    script owns.

.PARAMETER DisplayName
    Display name for the app registration. Defaults to a name that is obviously
    throwaway and greppable for cleanup.

.PARAMETER ScopeName
    Name of the custom oauth2PermissionScope to expose. This is the "Scope" value
    /information will advertise under MicrosoftEntraIdAuthenticationInfo.

.PARAMETER PreAuthorizeWinGetClient
    When set, adds the fixed WinGet client id to this app's preAuthorizedApplications
    for the created scope, which suppresses the WAM consent prompt. Off by default,
    because FC25 step 5/6 wants the first run to show what actually happens at the
    consent step (a prompt-and-consent flow) and what tenant step (if any) is needed
    for the tenant to admit that client id - that is the observation, not something
    to route around by default. Set this only for a second run once step 6 has
    identified pre-authorization as the tenant step required.

.PARAMETER OutputPath
    Where to write the resulting resource/scope/tenant facts as JSON, consumed by
    Start-InformationEndpoint.ps1 and Register-ObservationSource.ps1. Defaults under
    harness/output/, which is gitignored - these are throwaway tenant identifiers, not
    evidence (the observation record under design/self-hosted-source/observations/ is).

.PARAMETER Remove
    Deletes the app registration and service principal named in -OutputPath's JSON
    (or -DisplayName if the file is missing) instead of creating one. Requires -Force,
    per this repository's convention that destructive operations gate on an explicit
    flag rather than a confirmation prompt.

.PARAMETER Force
    Required alongside -Remove to actually delete. Also allows -DisplayName to match
    and reuse/recreate over an existing registration found by display name.

.EXAMPLE
    ./New-EntraObservationApp.ps1
    Creates the throwaway registration and writes harness/output/entra-app.json.

.EXAMPLE
    ./New-EntraObservationApp.ps1 -Remove -Force
    Deletes the registration and service principal recorded in
    harness/output/entra-app.json.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $DisplayName = 'SubZeroDev.WinGet FC24-FC25 Observation (throwaway)',

    [string] $ScopeName = 'Access',

    [switch] $PreAuthorizeWinGetClient,

    [string] $OutputPath = "$PSScriptRoot\output\entra-app.json",

    [switch] $Remove,

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Verified against winget-cli@5c88b96f
# src/AppInstallerCommonCore/Authentication/WebAccountManagerAuthenticator.cpp:20,21.
# Read from that file at that commit if this ever needs re-checking - do not trust this
# literal over the cited source.
$script:WinGetFixedClientId = '7b8ea11a-7f45-4b3a-ab51-794d5863af15'

function Connect-ObservationGraph {
    $context = Get-MgContext
    if (-not $context -or ($context.Scopes -notcontains 'Application.ReadWrite.All')) {
        Connect-MgGraph -Scopes 'Application.ReadWrite.All' -NoWelcome
    }
}

function Remove-ObservationApp {
    param([Parameter(Mandatory)][string] $OutputPath, [Parameter(Mandatory)][string] $DisplayName)

    if (-not $Force) {
        throw 'Removal requires -Force (this repository gates destructive operations on an explicit flag, not a prompt).'
    }

    Connect-ObservationGraph

    $appId = $null
    if (Test-Path $OutputPath) {
        $saved = Get-Content $OutputPath -Raw | ConvertFrom-Json
        $appId = $saved.AppId
    }

    $app = if ($appId) {
        Get-MgApplication -Filter "appId eq '$appId'"
    } else {
        Get-MgApplication -Filter "displayName eq '$DisplayName'"
    }

    if (-not $app) {
        Write-Warning "No app registration found (appId='$appId', displayName='$DisplayName'). Nothing to remove."
        return
    }

    $sp = Get-MgServicePrincipal -Filter "appId eq '$($app.AppId)'"
    if ($sp) {
        Remove-MgServicePrincipal -ServicePrincipalId $sp.Id
        Write-Host "Removed service principal $($sp.Id)."
    }

    Remove-MgApplication -ApplicationId $app.Id
    Write-Host "Removed app registration $($app.AppId) ('$($app.DisplayName)')."

    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Force
    }
}

function New-ObservationApp {
    param(
        [Parameter(Mandatory)][string] $DisplayName,
        [Parameter(Mandatory)][string] $ScopeName,
        [Parameter(Mandatory)][string] $OutputPath,
        [switch] $PreAuthorizeWinGetClient,
        [switch] $Force
    )

    Connect-ObservationGraph

    $existing = Get-MgApplication -Filter "displayName eq '$DisplayName'"
    if ($existing -and -not $Force) {
        throw "An app registration named '$DisplayName' already exists (appId=$($existing.AppId)). Pass -Force to reuse/recreate, or remove it first with -Remove -Force."
    }
    if ($existing -and $Force) {
        Write-Warning "Reusing existing app registration $($existing.AppId) ('$DisplayName')."
        $app = $existing
    } else {
        Write-Host "Creating app registration '$DisplayName'..."
        $app = New-MgApplication -DisplayName $DisplayName -SignInAudience 'AzureADMyOrg'
    }

    $resource = "api://$($app.AppId)"
    $scopeId = [guid]::NewGuid().ToString()

    $scope = @{
        AdminConsentDescription = "Allows the WinGet REST client to call the throwaway $DisplayName endpoint for FC24/FC25 observation."
        AdminConsentDisplayName = $ScopeName
        Id                      = $scopeId
        IsEnabled               = $true
        Type                    = 'Admin'
        UserConsentDescription  = "Allow WinGet to access this throwaway observation endpoint."
        UserConsentDisplayName  = $ScopeName
        Value                   = $ScopeName
    }

    $apiSettings = @{
        Oauth2PermissionScopes = @($scope)
    }

    if ($PreAuthorizeWinGetClient) {
        Write-Host "Pre-authorizing the fixed WinGet client id ($script:WinGetFixedClientId) for scope '$ScopeName'..."
        $apiSettings['PreAuthorizedApplications'] = @(
            @{
                AppId                  = $script:WinGetFixedClientId
                DelegatedPermissionIds = @($scopeId)
            }
        )
    }

    Update-MgApplication -ApplicationId $app.Id -IdentifierUris @($resource) -Api $apiSettings

    $sp = Get-MgServicePrincipal -Filter "appId eq '$($app.AppId)'"
    if (-not $sp) {
        Write-Host 'Creating service principal...'
        $sp = New-MgServicePrincipal -AppId $app.AppId
    }

    $tenantId = (Get-MgContext).TenantId

    $result = [ordered]@{
        AppId            = $app.AppId
        ObjectId         = $app.Id
        ServicePrincipal = $sp.Id
        TenantId         = $tenantId
        Resource         = $resource
        Scope            = $ScopeName
        ScopeId          = $scopeId
        PreAuthorized    = [bool]$PreAuthorizeWinGetClient
        CreatedAt        = (Get-Date).ToUniversalTime().ToString('o')
    }

    $outputDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $outputDir)) {
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    }
    $result | ConvertTo-Json | Set-Content -Path $OutputPath -Encoding utf8

    Write-Host ''
    Write-Host 'Entra app registration ready:'
    Write-Host "  AppId (Application/client id of the RESOURCE app): $($app.AppId)"
    Write-Host "  Resource (Application ID URI):                     $resource"
    Write-Host "  Scope:                                             $ScopeName"
    Write-Host "  TenantId:                                          $tenantId"
    Write-Host "  Pre-authorized fixed WinGet client id:             $([bool]$PreAuthorizeWinGetClient)"
    Write-Host "  Saved to:                                          $OutputPath"
    Write-Host ''
    Write-Host 'Next: Start-InformationEndpoint.ps1 (reads this file by default), then'
    Write-Host 'Register-ObservationSource.ps1, then run winget against the registered source'
    Write-Host 'from an interactive session with no cached WAM account (FC25 step 4).'

    return $result
}

if ($Remove) {
    Remove-ObservationApp -OutputPath $OutputPath -DisplayName $DisplayName
} else {
    New-ObservationApp -DisplayName $DisplayName -ScopeName $ScopeName -OutputPath $OutputPath -PreAuthorizeWinGetClient:$PreAuthorizeWinGetClient -Force:$Force
}
