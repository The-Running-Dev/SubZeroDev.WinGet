<#
.SYNOPSIS
    Runs the hermetic required check: tests, coverage, architecture and package.

.DESCRIPTION
    The post-build script of build.yml's `build` job, which calls the shared dotnet.yml workflow to
    check out, set up both SDKs, restore and build.

    One Nuke invocation for all requested targets. Nuke de-duplicates target execution only WITHIN
    a single invocation, so Test and Coverage must be requested together - separate `Test` then
    `Coverage` calls would re-run Test (and its Restore/Compile dependencies). Coverage depends on
    Test, so the plan runs Restore -> Compile -> Test -> Coverage exactly once, while
    ArchitectureTest cross-builds x64/ARM64 and verifies the emitted PE headers. PackageTest then
    validates the packed direct and transitive consumer contract without executing live COM calls.

    Runs through the build.ps1 bootstrapper, so the job needs no Nuke global tool.

    S14.6/C27: this required check never activates WinGet COM (C7), so it is blind to every C26
    live-licensed translation by construction. The hermetic evidence record says so whatever the
    outcome, so a green hermetic check is never read as evidence about one - the machine-state and
    catalog jobs' own evidence records are the only place any of them are licensed.

    Outside GitHub Actions (no GITHUB_STEP_SUMMARY) the coverage summary and the evidence record
    are written to the console.

.EXAMPLE
    ./build/ci/Test-HermeticCheck.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Write-Summary {
    param([string[]] $Line)

    if ($env:GITHUB_STEP_SUMMARY) { $Line | Add-Content -Path $env:GITHUB_STEP_SUMMARY }
    else { $Line | ForEach-Object { Write-Information $_ -InformationAction Continue } }
}

$exitCode = 1
try {
    & (Join-Path $root 'build.ps1') Test Coverage ArchitectureTest PackageTest --configuration Release
    $exitCode = $LASTEXITCODE

    $coverageSummary = Join-Path $root 'coverage' 'SummaryGithub.md'
    if ($exitCode -eq 0 -and (Test-Path -LiteralPath $coverageSummary)) {
        Write-Summary (Get-Content -LiteralPath $coverageSummary)
    }
}
finally {
    Write-Summary @(
        '## Hermetic evidence',
        "Commit: $env:GITHUB_SHA",
        "Run: $env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID (attempt $env:GITHUB_RUN_ATTEMPT)",
        'This required check never activates WinGet COM, queries a network catalog, or invokes the WinGet CLI binary (C7).',
        'Non-assertion: every C26 live-licensed translation - ToPackages, ToPackageInfo, ToPackageDetails, CopyAgreements, CopyDocumentations, CopyIcons, ToPackageSource, GetPriority, FindVersionId, GetInstallerErrorCode - is licensed by nothing here. A regression in one is detectable only by the machine-state or catalog job whose named assertion covers it (or, for GetInstallerErrorCode, by nothing while install/upgrade live coverage remains a non-goal).'
    )
}

exit $exitCode
