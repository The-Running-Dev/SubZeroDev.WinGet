<#
.SYNOPSIS
    Installs the WinGet build pinned by WINGET_VERSION and records the App Installer version after.

.DESCRIPTION
    The runner image pins no WinGet version at all and has shipped builds predating COM contract
    13, where PackageManager.Version does not exist - see design/90-decisions.md, 2026-08-21.
    Downloading a specific release tag is what makes C24's "explicitly pinned build" true; the
    published SHA256 sidecars make it verifiable rather than asserted, since a tag can be moved and
    a hash cannot.

    WINGET_VERSION comes from build.yml's workflow `env`, pinned to the library's
    Microsoft.WindowsPackageManager.ComInterop reference. The version found afterwards is written
    as the step output `after`.

.EXAMPLE
    ./build/ci/Install-PinnedWinGet.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'
# Invoke-WebRequest's progress rendering dominates the runtime of a 200 MB download.
$ProgressPreference = 'SilentlyContinue'

if (-not $env:WINGET_VERSION) { throw 'WINGET_VERSION is not set.' }

$base = "https://github.com/microsoft/winget-cli/releases/download/v$env:WINGET_VERSION"
$work = (New-Item -ItemType Directory -Force -Path (Join-Path $env:RUNNER_TEMP 'winget-pin')).FullName

foreach ($asset in 'Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle', 'DesktopAppInstaller_Dependencies.zip') {
    $file = Join-Path $work $asset
    Invoke-WebRequest "$base/$asset" -OutFile $file

    # Downloaded rather than read from the response body: GitHub serves release assets as
    # application/octet-stream, so Invoke-WebRequest hands back Content as bytes.
    $sidecar = Join-Path $work ([IO.Path]::GetFileNameWithoutExtension($asset) + '.txt')
    Invoke-WebRequest "$base/$([IO.Path]::GetFileName($sidecar))" -OutFile $sidecar

    $expected = (Get-Content -Path $sidecar -Raw).Trim()
    $actual = (Get-FileHash -Path $file -Algorithm SHA256).Hash

    if ($actual -ne $expected) {
        throw "$asset hash mismatch: expected $expected, got $actual."
    }
}

Expand-Archive -Path (Join-Path $work 'DesktopAppInstaller_Dependencies.zip') `
    -DestinationPath (Join-Path $work 'dependencies') -Force

# -DependencyPath installs only the dependencies the bundle actually needs, so an already-present
# VCLibs is skipped rather than failing as an unnecessary downgrade. The runner is x64; ARM64
# runtime execution is a binding non-goal in design/00-brief.md.
$dependencies = @(Get-ChildItem -Path (Join-Path $work 'dependencies') -Recurse -Include *.appx, *.msix |
        Where-Object { $_.FullName -match '[\\/]x64[\\/]' })

$arguments = @{
    Path                      = Join-Path $work 'Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle'
    ForceUpdateFromAnyVersion = $true
}

# An empty -DependencyPath is an error rather than a no-op, so omit it when the layout yielded
# nothing. A genuinely missing dependency then fails the bundle install below, which is the
# outcome that should fail the gate anyway.
if ($dependencies.Count -gt 0) { $arguments.DependencyPath = $dependencies.FullName }

Add-AppxPackage @arguments

$package = Get-AppxPackage Microsoft.DesktopAppInstaller
if ($null -eq $package) {
    throw 'App Installer is absent after installing the pinned build.'
}

"after=$($package.Version)" | Add-Content -Path $env:GITHUB_OUTPUT
