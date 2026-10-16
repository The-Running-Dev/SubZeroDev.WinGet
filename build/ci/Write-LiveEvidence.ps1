<#
.SYNOPSIS
    Records the evidence of a live job (machine-state or catalog) on the run's summary page.

.DESCRIPTION
    Runs whatever the checks' outcome (`if: always()` on its step), so a failing run still records
    the environment it observed (C3/C4). The step passes the outcomes and App Installer versions:

    - PREREQUISITE_OUTCOME, INSTALL_OUTCOME, CHECKS_OUTCOME: the outcomes of those steps.
    - APP_INSTALLER_BEFORE, APP_INSTALLER_AFTER: the versions observed before and after the pinned
      install, empty when the step never got that far.
    - WINGET_VERSION: the pinned build, from the workflow `env`.

    A version the job never observed is recorded as "(not observed)", never carried over from
    elsewhere (S15.3).

.PARAMETER Gate
    MachineState reads machine-state.log; Catalog reads catalog.log.

.EXAMPLE
    ./build/ci/Write-LiveEvidence.ps1 -Gate MachineState
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('MachineState', 'Catalog')]
    [string] $Gate
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
$elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

$log = if ($Gate -eq 'MachineState') { 'machine-state.log' } else { 'catalog.log' }
$version = if (Test-Path $log) {
    Select-String -Path $log -Pattern '^WinGet version:' |
        Select-Object -Last 1 | ForEach-Object Line
}

$common = @(
    "Commit: $env:GITHUB_SHA",
    "Run: $env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID (attempt $env:GITHUB_RUN_ATTEMPT)",
    "OS: $([Environment]::OSVersion.VersionString)",
    "Architecture: $([Runtime.InteropServices.RuntimeInformation]::OSArchitecture)",
    "Interactive: $([Environment]::UserInteractive)",
    "Elevated: $elevated",
    "WinGet prerequisite: $env:PREREQUISITE_OUTCOME",
    "Pinned WinGet install: $env:INSTALL_OUTCOME (v$env:WINGET_VERSION)",
    "App Installer before: $(if ($env:APP_INSTALLER_BEFORE) { $env:APP_INSTALLER_BEFORE } else { '(not observed)' })",
    "App Installer after: $(if ($env:APP_INSTALLER_AFTER) { $env:APP_INSTALLER_AFTER } else { '(not observed)' })"
)

$record = if ($Gate -eq 'MachineState') {
    @('## Machine-state evidence') + $common + @(
        "MachineStateTest (7 tests, C8) + PackedConsumerSmokeTest: $env:CHECKS_OUTCOME",
        "Observed $($version ?? 'WinGet version: (not observed)')",
        'Configured-source state (C4) is asserted by GetSources_ReturnsTheConfiguredSources and GetSource_ForWinget_ReturnsIt within the checks above; per-test outcomes are in the retained log and trx artifact, not restated here.',
        # S14.4: this job's own share of the ten C26 live-licensed translations.
        'Licensed here: ToPackageSource, GetPriority (via GetSource_ForWinget_ReturnsIt).',
        'Non-assertion: ToPackages, ToPackageInfo, ToPackageDetails, CopyAgreements, CopyDocumentations, CopyIcons, FindVersionId are not reached by this job - see the catalog job''s evidence. GetInstallerErrorCode is reachable only from Install/Upgrade, a binding non-goal (C26), and is licensed by nothing.'
    )
}
else {
    # S14.4/S14.5: the log carries whichever CopyAgreements/CopyDocumentations/CopyIcons ran into
    # an empty witness collection this run (TestContext.WriteLine, captured by the Tee-Object'd
    # log the same way the WinGet version line above is) - read those back rather than asserting
    # statically that every run licenses all three.
    $nonAssertions = if (Test-Path $log) {
        Select-String -Path $log -Pattern '^\s*Non-assertion:' |
            ForEach-Object { $_.Line.Trim() } | Select-Object -Unique
    }

    @('## Catalog-integration evidence') + $common + @(
        "CatalogIntegrationTest (6 tests, C8): $env:CHECKS_OUTCOME",
        "Observed $($version ?? 'WinGet version: (not observed)')",
        'Remote-catalog reachability (C4) is the fact these six tests assert against the configured winget/msstore sources; per-test outcomes are in the retained log and trx artifact.',
        # S14.4: this job's own share of the ten C26 live-licensed translations.
        'Licensed here: ToPackages, ToPackageInfo, ToPackageDetails, FindVersionId, plus whichever of CopyAgreements/CopyDocumentations/CopyIcons the witness''s manifest actually populated this run (see Non-assertion lines below for the rest).',
        'Non-assertion: ToPackageSource, GetPriority are not reached by this job - see the machine-state job''s evidence. GetInstallerErrorCode is reachable only from Install/Upgrade, a binding non-goal (C26), and is licensed by nothing.'
    ) + $nonAssertions
}

$record | Add-Content -Path $env:GITHUB_STEP_SUMMARY
