<#
.SYNOPSIS
    Runs CatalogIntegrationTest, keeping the output in catalog.log.

.DESCRIPTION
    Write-LiveEvidence.ps1 reads the WinGet version line and the Non-assertion lines back from the
    log.

.EXAMPLE
    ./build/ci/Invoke-CatalogChecks.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

nuke CatalogIntegrationTest --configuration Release 2>&1 |
    Tee-Object -FilePath catalog.log
exit $LASTEXITCODE
