<#
.SYNOPSIS
    Installs the Nuke global tool at NUKE_VERSION and puts it on the PATH of the later steps.

.DESCRIPTION
    `update` (not `install`) so this is idempotent: it installs the tool when missing and is a
    no-op success when the pinned version is already present. `install --version X` instead errors
    if a *different* version is already on the host - which happens under `act` host mode (the host
    environment persists between runs) whenever NUKE_VERSION is bumped.

    NUKE_VERSION comes from build.yml's workflow `env`, pinned to build/_build.csproj's Nuke.Common
    reference.

.EXAMPLE
    $env:NUKE_VERSION = '10.1.0'; ./build/ci/Install-Nuke.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

if (-not $env:NUKE_VERSION) { throw 'NUKE_VERSION is not set.' }

dotnet tool update --global Nuke.GlobalTool --version $env:NUKE_VERSION
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($env:GITHUB_PATH) {
    Add-Content -Path $env:GITHUB_PATH -Value (Join-Path $env:USERPROFILE '.dotnet' 'tools')
}
