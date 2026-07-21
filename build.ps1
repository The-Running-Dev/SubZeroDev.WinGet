#!/usr/bin/env pwsh
# Nuke bootstrapper. Required: the Nuke global tool locates a build by searching for
# build.ps1/build.sh, so `nuke <Target>` does not work without this file present.
# Runs the build project directly, so no global tool install is strictly necessary.
[CmdletBinding()]
Param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

dotnet run --project build/_build.csproj --no-launch-profile -- $BuildArguments

exit $LASTEXITCODE
