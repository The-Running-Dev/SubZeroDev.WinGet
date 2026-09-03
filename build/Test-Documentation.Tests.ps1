#Requires -Version 7.0
#Requires -Modules Pester

<#
  S10.4/S10.5: the documentation gate's Claim and coverage-floor-scope rules
  (build/Test-Documentation.ps1, functions Test-DocumentationClaim and
  Test-DocumentationCoverageFloorScope) are exercised here against isolated fixtures
  rather than the real docs, so a rule's failure mode is provable without depending on
  the repository's own prose staying exactly as written.

  Fixtures live under a throwaway directory inside the repository (not under 'build/',
  which build/Test-Documentation.ps1's own ExcludedSegments would otherwise skip) because
  the script resolves $repositoryRoot from its own location via the nearest '.git', not
  from -Path; Claim ownership and scope are matched by path relative to that root, so a
  fixture has to live inside the repository tree for its relative path to mean anything.
  Each fixture gets its own settings file naming exactly its own paths, and the whole
  fixture directory is removed after every test.
#>

BeforeAll {

$script:RepositoryRoot = Split-Path $PSScriptRoot -Parent
$script:GatePath = Join-Path $PSScriptRoot 'Test-Documentation.ps1'

function New-ClaimFixture {
    param (
        [Parameter(Mandatory)]
        [hashtable] $Files,

        [Parameter()]
        [hashtable] $Owners = @{
            'consumer-architecture'  = $null
            'managed-assembly-shape' = $null
            'hosting-context'        = $null
            'operation-coverage'     = $null
            'runtime-version-floor'  = $null
        },

        [Parameter()]
        [string[]] $Scope
    )

    $fixtureName = "doc-claim-fixture-$([Guid]::NewGuid().ToString('N'))"
    $fixtureRoot = Join-Path $script:RepositoryRoot $fixtureName
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null

    foreach ($relative in $Files.Keys) {
        $target = Join-Path $fixtureRoot $relative
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Set-Content -LiteralPath $target -Value $Files[$relative] -NoNewline
    }

    $resolvedOwners = @{}
    foreach ($subject in $Owners.Keys) {
        if ($Owners[$subject]) {
            $resolvedOwners[$subject] = "$fixtureName/$($Owners[$subject])"
        }
    }

    $resolvedScope = if ($Scope) {
        @($Scope | ForEach-Object { "$fixtureName/$_" })
    }
    else {
        @($resolvedOwners.Values)
    }

    $settingsPath = Join-Path $fixtureRoot '.DocumentationRules.psd1'
    $settingsContent = @"
@{
    Terminology = @()
    ExcludedSegments = @('.git')
    ExcludedFiles = @()
    Claims = @{
        Owners = @{
$(($resolvedOwners.GetEnumerator() | ForEach-Object { "            '$($_.Key)' = '$($_.Value)'" }) -join "`n")
        }
        Scope = @(
$(($resolvedScope | ForEach-Object { "            '$_'" }) -join "`n")
        )
        EvidenceTokens = @(
            @{ Token = 'ArchitectureTest'; MaxStrength = 'contract-checked' }
            @{ Token = 'MachineStateTest'; MaxStrength = 'executed' }
        )
    }
}
"@
    Set-Content -LiteralPath $settingsPath -Value $settingsContent -NoNewline

    return [pscustomobject]@{
        Root         = $fixtureRoot
        SettingsPath = $settingsPath
    }
}

function Invoke-ClaimGate {
    param (
        [Parameter(Mandatory)]
        [pscustomobject] $Fixture
    )

    # A terminating `throw` inside the gate aborts a plain `$x = & ...` assignment before it
    # captures anything already streamed, which is exactly the per-finding Write-Host lines
    # this needs to assert on - so every stream is redirected straight to a file instead,
    # which keeps whatever was written up to the throw.
    $capturePath = Join-Path ([IO.Path]::GetTempPath()) "$([Guid]::NewGuid().ToString('N')).log"
    $threw = $false

    try {
        & $script:GatePath -Path $Fixture.Root -SettingsPath $Fixture.SettingsPath *> $capturePath
    }
    catch {
        $threw = $true
    }

    $output = if (Test-Path -LiteralPath $capturePath) {
        $content = Get-Content -LiteralPath $capturePath -Raw
        Remove-Item -LiteralPath $capturePath -Force -ErrorAction SilentlyContinue
        $content
    }
    else {
        ''
    }

    return @{ Threw = $threw; Output = $output }
}

function Remove-ClaimFixture {
    param (
        [Parameter(Mandatory)]
        [pscustomobject] $Fixture
    )

    Remove-Item -LiteralPath $Fixture.Root -Recurse -Force -ErrorAction SilentlyContinue
}

}

Describe 'Documentation gate: Claim validation (S10.4/S10.5)' {

    It 'passes a single well-formed Claim with sufficient evidence' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md' = @"
# Owner

<!-- claim:hosting-context
strength: contract-checked
evidence: ArchitectureTest
-->
Elevation support is contract-checked by ArchitectureTest.
"@
        }

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeFalse
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'passes an unvalidated Claim with no evidence at all' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md' = @"
# Owner

<!-- claim:hosting-context
strength: unvalidated
evidence:
-->
Elevation support is unvalidated.
"@
        }

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeFalse
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'fails a Claim block declared outside its configured canonical owner (duplicate owner)' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md' = "# Owner`n`nNo claim declared here.`n"
            'other.md' = @"
# Other

<!-- claim:hosting-context
strength: unvalidated
evidence:
-->
Restated somewhere it shouldn't be.
"@
        } -Scope @('owner.md', 'other.md')

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeTrue
            $result.Output | Should -Match 'ClaimDuplicateOwner'
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'fails a Claim block with an invalid strength' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md' = @"
# Owner

<!-- claim:hosting-context
strength: aspirational
evidence: ArchitectureTest
-->
Not one of the three strengths.
"@
        }

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeTrue
            $result.Output | Should -Match 'ClaimInvalidStrength'
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'fails a non-unvalidated Claim block with a missing evidence reference' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md' = @"
# Owner

<!-- claim:hosting-context
strength: contract-checked
evidence:
-->
Claims contract-checked but cites nothing.
"@
        }

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeTrue
            $result.Output | Should -Match 'ClaimMissingEvidence'
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'fails a Claim block whose evidence is insufficient for the claimed strength' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md' = @"
# Owner

<!-- claim:hosting-context
strength: executed
evidence: ArchitectureTest
-->
Claims executed but only names a contract-checked-level gate.
"@
        }

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeTrue
            $result.Output | Should -Match 'ClaimInsufficientEvidence'
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'fails a strength restated in prose outside any file that owns a Claim subject' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md'    = @"
# Owner

<!-- claim:hosting-context
strength: unvalidated
evidence:
-->
Elevation support is unvalidated.
"@
            'restated.md' = "# Restated`n`nElevation support is unvalidated in this document too.`n"
        } -Scope @('owner.md', 'restated.md')

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeTrue
            $result.Output | Should -Match 'ClaimRestatement'
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'passes a document that links to the canonical statement instead of restating it' {
        $fixture = New-ClaimFixture -Owners @{ 'hosting-context' = 'owner.md' } -Files @{
            'owner.md'      = @"
# Owner

<!-- claim:hosting-context
strength: unvalidated
evidence:
-->
Elevation support is unvalidated.
"@
            'referring.md' = "# Referring`n`nSee [Owner](owner.md) for hosting support.`n"
        } -Scope @('owner.md', 'referring.md')

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeFalse
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }
}

Describe 'Documentation gate: coverage-floor scope (S10.8/C11)' {

    It 'fails a document presenting the coverage floor as proven/tested/verified behaviour, undisclaimed' {
        $fixture = New-ClaimFixture -Files @{
            'coverage.md' = "# Coverage`n`nThe coverage floor proves the library is fully tested.`n"
        } -Scope @('coverage.md')

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeTrue
            $result.Output | Should -Match 'CoverageFloorScope'
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'passes the same claim when the same line discloses it as a unit-only lower bound' {
        $fixture = New-ClaimFixture -Files @{
            'coverage.md' = (
                "# Coverage`n`n" +
                "The coverage floor is a unit-only lower bound and proves nothing beyond that " +
                "about tested or verified behaviour.`n"
            )
        } -Scope @('coverage.md')

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeFalse
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }

    It 'does not scan a document outside Claims.Scope for the coverage-floor rule' {
        $fixture = New-ClaimFixture -Files @{
            'coverage.md' = "# Coverage`n`nThe coverage floor proves the library is fully tested.`n"
        } -Scope @()

        try {
            $result = Invoke-ClaimGate -Fixture $fixture
            $result.Threw | Should -BeFalse
        }
        finally {
            Remove-ClaimFixture -Fixture $fixture
        }
    }
}

Describe 'Documentation gate: the real repository (S10.5)' {

    # $script:RepositoryRoot is set in the top-level BeforeAll, which runs during Pester's Run
    # phase - too late for a -Skip condition, which is evaluated during Discovery. This
    # recomputes the same path directly from $PSScriptRoot, which discovery does have.
    It 'passes the repository''s own documents, including the regenerated homepage' -Skip:(
        -not (Test-Path (Join-Path (Split-Path $PSScriptRoot -Parent) '.git'))
    ) {
        { & $script:GatePath } | Should -Not -Throw
    }
}
