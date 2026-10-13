<#
.SYNOPSIS
    Publishes the package to GitHub Packages or NuGet.org and records the confirmation.

.DESCRIPTION
    GitHubPackages runs on every push to main (prerelease version) and every `v*` tag (stable
    version). The version comes from GitVersion, resolved by Nuke's GitVersion component, which is
    why the release job checks out the full history. Auth uses the built-in GITHUB_TOKEN, and
    --skip-duplicate makes re-pushing an unchanged version harmless.

    NuGet is the manual, opt-in release from workflow_dispatch. It needs NUGET_API_KEY and
    publishes the version pinned in the .csproj.

    C21/S11.1/S11.2: the targets themselves fail unless they positively confirm the exact pushed
    version back from the feed - push success or a skipped duplicate is not enough. The
    "Publication confirmed:" line PublicationConfirmation.Report writes is appended to the run
    summary, matching how the machine-state and catalog evidence is recorded.

    The step passes GITHUB_TOKEN and GITHUB_REPOSITORY_OWNER (GitHubPackages) or NUGET_API_KEY
    (NuGet) in the environment.

.PARAMETER Feed
    GitHubPackages or NuGet.

.EXAMPLE
    ./build/ci/Publish-Package.ps1 -Feed GitHubPackages
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('GitHubPackages', 'NuGet')]
    [string] $Feed
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

if ($Feed -eq 'GitHubPackages') {
    if (-not $env:GITHUB_TOKEN) { throw 'GITHUB_TOKEN is not set.' }
    $log = 'publish-github-packages.log'
    nuke PublishGitHubPackages --github-token $env:GITHUB_TOKEN --github-repository-owner $env:GITHUB_REPOSITORY_OWNER 2>&1 |
        Tee-Object -FilePath $log
}
else {
    if (-not $env:NUGET_API_KEY) { throw 'NUGET_API_KEY is not set.' }
    $log = 'publish-nuget.log'
    nuke PublishNuGet --nuget-api-key $env:NUGET_API_KEY 2>&1 |
        Tee-Object -FilePath $log
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Select-String -Path $log -Pattern '^Publication confirmed:' |
    ForEach-Object Line | Add-Content -Path $env:GITHUB_STEP_SUMMARY
