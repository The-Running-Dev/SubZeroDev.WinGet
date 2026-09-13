<#
.SYNOPSIS
    Writes a dated FC24 or FC25 observation record in the shape
    design/self-hosted-source/fc24-fc25-observation-plan.md § "Recording an observation"
    requires.

.DESCRIPTION
    This script only writes the file - it does not decide the outcome, does not run any
    check itself, and does not adjudicate a negative result. Per the plan: "a negative
    result is adjudicated by the maintainer... and is not resolved by whoever runs this
    plan." Fill in -Outcome, -Assertions, and -NonAssertions from what was actually
    observed (the Sandbox log at harness/output/sandbox-share/fc24-run.log for FC24, or
    the interactive session's own outcome for FC25) - do not let this script's defaults
    stand in for an actual observation.

.PARAMETER Question
    FC24 or FC25.

.PARAMETER Outcome
    Positive or Negative, per the plan's own definition of each question's outcomes.

.PARAMETER Run
    Free text: date, who ran it, which machine/Sandbox instance. Defaults to a
    plausible value from the current session, but the plan asks for this "as actually
    observed, not assumed" - review it before accepting the default, especially the
    Sandbox instance identity, which this script cannot know.

.PARAMETER EnvironmentFacts
    One or more "key: value" strings covering the FC15 client-environment fields the
    plan names: Entra-joined or not, WAM account cached for the fixed client id or not,
    interactive or not, source registered Explicit or not - plus, for FC24, that the
    session was a fresh Sandbox instance.

.PARAMETER Assertions
    One or more strings naming exactly what was exercised. Must be non-empty - the
    contract's own "vacuous assertion" error variant exists for a check that appears to
    pass without exercising anything.

.PARAMETER NonAssertions
    One or more strings naming what this harness does NOT prove (the plan's own list -
    FC6/FC13/FC19/FC20/manifest tree/no-store header/production certificate - is a
    reasonable starting point; add anything else the run didn't touch).

.PARAMETER TenantConfigurationStep
    FC25 only: any tenant configuration step (admin-consent grant, API permission
    shape, conditional-access exclusion) that step 6 found necessary. Leave unset if
    none was needed.

.PARAMETER OutputDir
    Defaults to design/self-hosted-source/observations, created if missing (the plan
    says to create it with the first record).

.PARAMETER Force
    Overwrite an existing record for the same question and date instead of refusing.

.EXAMPLE
    ./New-ObservationRecord.ps1 -Question FC25 -Outcome Positive `
        -Run 'Ben Richards, this machine, 2026-09-14' `
        -EnvironmentFacts 'Entra-joined: yes','WAM account cached: no (fresh sign-in)','Interactive: yes','Source Explicit: yes' `
        -Assertions 'Interactive token acquisition for the fixed client id against resource api://<appId>, from a signed-in session with no prior cached account.' `
        -NonAssertions 'Does not exercise FC6, FC13, FC19, FC20, the manifest tree, or the production certificate.'
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('FC24', 'FC25')]
    [string] $Question,

    [Parameter(Mandatory)]
    [ValidateSet('Positive', 'Negative')]
    [string] $Outcome,

    [string] $Run = "$(try { [Environment]::UserName } catch { 'unknown' }), $(try { [Environment]::MachineName } catch { 'unknown' }), $(Get-Date -Format 'yyyy-MM-dd')",

    [Parameter(Mandatory)]
    [string[]] $EnvironmentFacts,

    [Parameter(Mandatory)]
    [string[]] $Assertions,

    [Parameter(Mandatory)]
    [string[]] $NonAssertions,

    [string] $TenantConfigurationStep,

    [string] $OutputDir = "$PSScriptRoot\..\observations",

    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Assertions.Count -eq 0) {
    throw 'At least one -Assertion is required - an observation record with no assertions is the vacuous-assertion failure the contract names, not evidence.'
}

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created $OutputDir."
}

$dateStamp = Get-Date -Format 'yyyy-MM-dd'
$fileName = "$dateStamp-$($Question.ToLowerInvariant()).md"
$filePath = Join-Path $OutputDir $fileName

if ((Test-Path $filePath) -and -not $Force) {
    throw "$filePath already exists. Pass -Force to overwrite, or rename the existing record first."
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("# $Question observation - $dateStamp")
$lines.Add('')
$lines.Add("Recorded per [`fc24-fc25-observation-plan.md`](../fc24-fc25-observation-plan.md) `` Recording an observation``.")
$lines.Add('')
$lines.Add("**Outcome:** $Outcome")
$lines.Add('')
$lines.Add("**Run:** $Run")
$lines.Add('')
$lines.Add('**Environment facts:**')
$lines.Add('')
foreach ($fact in $EnvironmentFacts) {
    $lines.Add("- $fact")
}
$lines.Add('')
$lines.Add('**Assertions:**')
$lines.Add('')
foreach ($assertion in $Assertions) {
    $lines.Add("- $assertion")
}
$lines.Add('')
$lines.Add('**Non-assertions:**')
$lines.Add('')
foreach ($nonAssertion in $NonAssertions) {
    $lines.Add("- $nonAssertion")
}

if ($Question -eq 'FC25') {
    $lines.Add('')
    $lines.Add('**Tenant configuration step:**')
    $lines.Add('')
    $lines.Add($(if ($TenantConfigurationStep) { $TenantConfigurationStep } else { 'None required.' }))
}

Set-Content -Path $filePath -Value ($lines -join "`n") -Encoding utf8

Write-Host "Wrote $filePath."
if ($Outcome -eq 'Negative') {
    Write-Host ''
    Write-Host "This is a NEGATIVE outcome for $Question. Per the plan, this is not resolved by whoever ran"
    Write-Host 'it - report it to the maintainer rather than narrowing, retrying with a weaker check, or'
    Write-Host 'treating it as a slicing detail.'
}
