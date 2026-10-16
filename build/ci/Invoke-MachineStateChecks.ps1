<#
.SYNOPSIS
    Runs MachineStateTest and PackedConsumerSmokeTest, keeping the output in machine-state.log.

.DESCRIPTION
    One Nuke invocation for both targets (Nuke de-duplicates Restore/Compile within it, same
    reasoning as the hermetic job). SetLoggers("trx") plus the detailed console logger in
    build/Build.cs give this a per-test retained record, not just a job-level pass/fail
    (S8.4/S8.6). Write-LiveEvidence.ps1 reads the WinGet version line back from the log.

.EXAMPLE
    ./build/ci/Invoke-MachineStateChecks.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

nuke MachineStateTest PackedConsumerSmokeTest --configuration Release 2>&1 |
    Tee-Object -FilePath machine-state.log
exit $LASTEXITCODE
