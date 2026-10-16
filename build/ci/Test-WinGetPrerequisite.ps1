<#
.SYNOPSIS
    Fails when App Installer is absent and records the version found before the pinned install.

.DESCRIPTION
    C4 requires the environment fact to be observed rather than inferred from a successful call.
    It is captured here, where it exists whether or not the checks later succeed - an earlier
    shape read the version out of the consumer's own stdout, so the run that most needed it was
    the one run that never recorded it. Written as the step output `before`.

.EXAMPLE
    ./build/ci/Test-WinGetPrerequisite.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$package = Get-AppxPackage Microsoft.DesktopAppInstaller -ErrorAction SilentlyContinue
if ($null -eq $package) {
    throw 'WinGet/App Installer is not installed on this runner.'
}

"before=$($package.Version)" | Add-Content -Path $env:GITHUB_OUTPUT
