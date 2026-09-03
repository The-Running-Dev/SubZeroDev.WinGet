<#
.SYNOPSIS
Runs the repository Markdown link and terminology gate.

.DESCRIPTION
Validates authored Markdown that the documentation site build never sees.

Docusaurus already fails on unresolved links inside docs/, so this gate covers
the rest: root Markdown such as README.md and TODO.md, and cross-file relative
links and heading anchors everywhere.

Relative link targets must exist on disk, and a #fragment must match a heading
in the target document. External and site-absolute links are reported as out of
scope rather than fetched, so the gate never depends on network reachability.

Terminology rules enforce consistent product-name casing over prose only.
Fenced code, inline code, link targets, and bare URLs are masked first, so
commands, file paths, and URLs are never flagged.

.PARAMETER Path
One or more files or directories to scan. Defaults to the repository root.

.PARAMETER SettingsPath
Rules file to apply. Defaults to .config/DocumentationRules.psd1.

.PARAMETER TreatWarningsAsErrors
Fail the gate on any finding, including 'Warning' severity (currently
Terminology only). Without this switch, only 'Error' findings fail the gate;
warnings are printed but do not block.
#>
[CmdletBinding()]
param (
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string[]] $Path,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $SettingsPath,

    [Parameter()]
    [switch] $TreatWarningsAsErrors
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Find-DocumentationRepositoryRoot {
    <#
    .SYNOPSIS
    Walks upward from a starting path to the nearest directory containing
    '.git', which may be a directory (a normal clone) or a file (a submodule
    or worktree). Not '..' from the script: this script can be installed at
    any depth via -ScriptDir, so the root cannot be assumed to be one level up.
    #>
    param (
        [Parameter(Mandatory)]
        [string] $StartPath
    )

    $current = [IO.Path]::GetFullPath($StartPath)
    while ($true) {
        if (Test-Path -LiteralPath (Join-Path $current '.git')) {
            return $current
        }

        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) {
            throw [System.IO.DirectoryNotFoundException]::new(
                "Could not locate the repository root above '$StartPath': " +
                "no '.git' was found in any parent directory."
            )
        }
        $current = $parent
    }
}

$repositoryRoot = Find-DocumentationRepositoryRoot -StartPath $PSScriptRoot

if (-not $PSBoundParameters.ContainsKey('SettingsPath')) {
    $SettingsPath = Join-Path $repositoryRoot '.config' 'DocumentationRules.psd1'
}

if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) {
    throw [System.IO.FileNotFoundException]::new(
        "Documentation rules file not found: '$SettingsPath'."
    )
}

$settings = Import-PowerShellDataFile -LiteralPath $SettingsPath

if (-not $PSBoundParameters.ContainsKey('Path')) {
    $Path = @($repositoryRoot)
}

function Get-RelativeDocumentationPath {
    param (
        [Parameter(Mandatory)]
        [string] $FullPath,

        [Parameter(Mandatory)]
        [string] $Root
    )

    return [IO.Path]::GetRelativePath($Root, $FullPath).Replace('\', '/')
}

function Test-ExcludedDocumentationPath {
    param (
        [Parameter(Mandatory)]
        [string] $RelativePath,

        [Parameter(Mandatory)]
        [hashtable] $Settings
    )

    $segments = $RelativePath.Split('/')
    foreach ($segment in $Settings.ExcludedSegments) {
        if ($segments -contains $segment) {
            return $true
        }
    }

    foreach ($excluded in $Settings.ExcludedFiles) {
        if ($RelativePath -eq $excluded -or $RelativePath.StartsWith("$excluded/")) {
            return $true
        }
    }

    return $false
}

function Get-DocumentationFile {
    param (
        [Parameter(Mandatory)]
        [string[]] $SearchPath,

        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [hashtable] $Settings
    )

    $files = foreach ($item in $SearchPath) {
        $resolved = [IO.Path]::GetFullPath(
            $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($item)
        )

        if (Test-Path -LiteralPath $resolved -PathType Leaf) {
            Get-Item -LiteralPath $resolved
        }
        elseif (Test-Path -LiteralPath $resolved -PathType Container) {
            Get-ChildItem -LiteralPath $resolved -Filter '*.md' -Recurse -File
        }
        else {
            throw [System.IO.FileNotFoundException]::new(
                "Documentation path not found: '$resolved'."
            )
        }
    }

    # Comma keeps a single match an array instead of unrolling it to a scalar.
    return , @(
        $files |
            Where-Object { $_.Extension -eq '.md' } |
            Where-Object {
                -not (Test-ExcludedDocumentationPath `
                        -RelativePath (Get-RelativeDocumentationPath -FullPath $_.FullName -Root $Root) `
                        -Settings $Settings)
            } |
            Sort-Object FullName -Unique
    )
}

function Get-MaskedDocumentationLine {
    <#
    .SYNOPSIS
    Blanks code so rules apply to prose only, preserving line and column numbers.
    #>
    param (
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $Line,

        [Parameter()]
        [switch] $MaskLinkTarget
    )

    $masked = [string[]]::new($Line.Count)
    $fenceMarker = $null

    for ($index = 0; $index -lt $Line.Count; $index++) {
        $current = $Line[$index]
        $fenceMatch = [regex]::Match($current, '^\s{0,3}(`{3,}|~{3,})')

        if ($fenceMarker) {
            # Inside a fence: blank everything, and close on a matching marker.
            $masked[$index] = ' ' * $current.Length
            if ($fenceMatch.Success -and $fenceMatch.Groups[1].Value[0] -eq $fenceMarker[0] -and
                $fenceMatch.Groups[1].Value.Length -ge $fenceMarker.Length) {
                $fenceMarker = $null
            }
            continue
        }

        if ($fenceMatch.Success) {
            $fenceMarker = $fenceMatch.Groups[1].Value
            $masked[$index] = ' ' * $current.Length
            continue
        }

        # Inline code spans, longest runs first so ``a`b`` is handled correctly.
        $value = [regex]::Replace($current, '(`+)(?:.*?)\1', { ' ' * $args[0].Length })

        if ($MaskLinkTarget) {
            # Link and image targets plus bare URLs are addresses, not prose.
            $value = [regex]::Replace($value, '\]\([^)]*\)', { ' ' * $args[0].Length })
            $value = [regex]::Replace($value, '^\s{0,3}\[[^\]]+\]:\s*\S+', { ' ' * $args[0].Length })
            $value = [regex]::Replace($value, '<?https?://\S+>?', { ' ' * $args[0].Length })
        }

        $masked[$index] = $value
    }

    return , $masked
}

function ConvertTo-HeadingSlug {
    param (
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string] $Text
    )

    $value = $Text
    # Reduce inline markup to the text a reader sees before slugifying.
    $value = [regex]::Replace($value, '!?\[([^\]]*)\]\([^)]*\)', '$1')
    $value = [regex]::Replace($value, '[`*_~]', '')
    $value = $value.Trim().ToLowerInvariant()
    $value = [regex]::Replace($value, '[^a-z0-9 \-]', '')
    $value = [regex]::Replace($value, '\s+', '-')

    return $value.Trim('-')
}

function Get-DocumentationAnchor {
    param (
        [Parameter(Mandatory)]
        [string] $FullPath
    )

    $lines = @(Get-Content -LiteralPath $FullPath)
    $masked = Get-MaskedDocumentationLine -Line $lines
    $anchors = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase
    )
    $counts = @{}

    foreach ($line in $masked) {
        $match = [regex]::Match($line, '^\s{0,3}#{1,6}\s+(.+?)\s*$')
        if (-not $match.Success) {
            continue
        }

        $heading = $match.Groups[1].Value

        # Docusaurus honors an explicit {#custom-id} over the generated slug.
        $explicit = [regex]::Match($heading, '\{#([^}]+)\}\s*$')
        if ($explicit.Success) {
            $null = $anchors.Add($explicit.Groups[1].Value)
            continue
        }

        $slug = ConvertTo-HeadingSlug -Text $heading
        if ([string]::IsNullOrWhiteSpace($slug)) {
            continue
        }

        # Repeated headings get -1, -2 suffixes, matching Docusaurus and GitHub.
        if ($counts.ContainsKey($slug)) {
            $counts[$slug]++
            $null = $anchors.Add("$slug-$($counts[$slug])")
        }
        else {
            $counts[$slug] = 0
            $null = $anchors.Add($slug)
        }
    }

    return , $anchors
}

$anchorCache = @{}

function Get-CachedDocumentationAnchor {
    param (
        [Parameter(Mandatory)]
        [string] $FullPath,

        [Parameter(Mandatory)]
        [hashtable] $Cache
    )

    if (-not $Cache.ContainsKey($FullPath)) {
        $Cache[$FullPath] = Get-DocumentationAnchor -FullPath $FullPath
    }

    return , $Cache[$FullPath]
}

function New-DocumentationFinding {
    param (
        [Parameter(Mandatory)]
        [string] $RelativePath,

        [Parameter(Mandatory)]
        [int] $Line,

        [Parameter(Mandatory)]
        [int] $Column,

        [Parameter(Mandatory)]
        [string] $Severity,

        [Parameter(Mandatory)]
        [string] $Rule,

        [Parameter(Mandatory)]
        [string] $Message
    )

    return [pscustomobject]@{
        RelativePath = $RelativePath
        Line = $Line
        Column = $Column
        Severity = $Severity
        Rule = $Rule
        Message = $Message
    }
}

function Test-DocumentationLink {
    param (
        [Parameter(Mandatory)]
        [System.IO.FileInfo] $File,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $MaskedLine,

        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [hashtable] $Cache
    )

    $relativePath = Get-RelativeDocumentationPath -FullPath $File.FullName -Root $Root
    $directory = $File.DirectoryName

    for ($index = 0; $index -lt $MaskedLine.Count; $index++) {
        $lineNumber = $index + 1
        $targets = @()

        foreach ($match in [regex]::Matches(
                $MaskedLine[$index],
                '!?\[[^\]]*\]\(\s*([^)\s]+)(?:\s+"[^"]*")?\s*\)')) {
            $targets += [pscustomobject]@{
                Value = $match.Groups[1].Value
                Column = $match.Groups[1].Index + 1
            }
        }

        foreach ($match in [regex]::Matches(
                $MaskedLine[$index],
                '^\s{0,3}\[[^\]]+\]:\s*(\S+)')) {
            $targets += [pscustomobject]@{
                Value = $match.Groups[1].Value
                Column = $match.Groups[1].Index + 1
            }
        }

        foreach ($target in $targets) {
            $value = $target.Value.Trim('<', '>')

            # Addresses this gate deliberately does not resolve. Site-absolute
            # routes are checked by the Docusaurus build's own broken-link pass.
            if ($value -match '^(https?|mailto|ftp):' -or $value.StartsWith('/')) {
                continue
            }

            $fragment = ''
            $filePart = $value
            $hashIndex = $value.IndexOf('#')
            if ($hashIndex -ge 0) {
                $fragment = $value.Substring($hashIndex + 1)
                $filePart = $value.Substring(0, $hashIndex)
            }

            if ([string]::IsNullOrEmpty($filePart)) {
                # Same-document anchor.
                $anchors = Get-CachedDocumentationAnchor -FullPath $File.FullName -Cache $Cache
                if ($fragment -and -not $anchors.Contains($fragment)) {
                    New-DocumentationFinding `
                        -RelativePath $relativePath `
                        -Line $lineNumber `
                        -Column $target.Column `
                        -Severity 'Error' `
                        -Rule 'MarkdownAnchor' `
                        -Message "No heading in this document produces the anchor '#$fragment'."
                }
                continue
            }

            $decoded = [uri]::UnescapeDataString($filePart)
            $resolved = [IO.Path]::GetFullPath((Join-Path $directory $decoded))

            if (-not (Test-Path -LiteralPath $resolved)) {
                New-DocumentationFinding `
                    -RelativePath $relativePath `
                    -Line $lineNumber `
                    -Column $target.Column `
                    -Severity 'Error' `
                    -Rule 'MarkdownLink' `
                    -Message "Link target '$filePart' does not exist."
                continue
            }

            if ($fragment -and $resolved.EndsWith('.md', [StringComparison]::OrdinalIgnoreCase)) {
                $anchors = Get-CachedDocumentationAnchor -FullPath $resolved -Cache $Cache
                if (-not $anchors.Contains($fragment)) {
                    New-DocumentationFinding `
                        -RelativePath $relativePath `
                        -Line $lineNumber `
                        -Column $target.Column `
                        -Severity 'Error' `
                        -Rule 'MarkdownAnchor' `
                        -Message "Link target '$filePart' has no heading producing the anchor '#$fragment'."
                }
            }
        }
    }
}

function Test-DocumentationTerminology {
    param (
        [Parameter(Mandatory)]
        [System.IO.FileInfo] $File,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $MaskedLine,

        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [hashtable] $Settings
    )

    $relativePath = Get-RelativeDocumentationPath -FullPath $File.FullName -Root $Root

    for ($index = 0; $index -lt $MaskedLine.Count; $index++) {
        foreach ($rule in $Settings.Terminology) {
            foreach ($variant in $rule.Variants) {
                $pattern = '(?<![\w-])' + [regex]::Escape($variant) + '(?![\w-])'
                foreach ($match in [regex]::Matches($MaskedLine[$index], $pattern)) {
                    New-DocumentationFinding `
                        -RelativePath $relativePath `
                        -Line ($index + 1) `
                        -Column ($match.Index + 1) `
                        -Severity 'Warning' `
                        -Rule 'Terminology' `
                        -Message "Use '$($rule.Required)' instead of '$variant'."
                }
            }
        }
    }
}

function Test-DocumentationCoverageFloorScope {
    <#
    .SYNOPSIS
    C11/S10.8: the checked-in coverage floor is a lower bound on unit-only coverage, blind to
    live evidence, and no document may present it as a measure of the library's proven, tested,
    or verified behaviour. This is a gate rule over any document, not a sixth Claim subject.

    .DESCRIPTION
    Flags a line that mentions the coverage floor alongside a proven/tested/verified claim about
    the library without also carrying the disclaiming vocabulary ('unit-only', 'unit only',
    'lower bound', or 'blind') on the same line. Line-scoped, like Terminology above: a paragraph
    that makes both the claim and the disclaimer needs to say so in one place, not split the
    disclaimer into a sentence a reader - or this rule - might not reach.

    Checked only over $Settings.Claims.Scope, the same consumer-facing document set the Claim
    rules below use. design/90-decisions.md and design/20-contract.md are this system's own
    definition of 'proven'/'verified'/'tested' evidence and legitimately use that vocabulary
    densely when discussing the floor itself; they are not a support claim a package consumer
    reads, and scanning them here would flag the design record, not a drifted document.
    #>
    param (
        [Parameter(Mandatory)]
        [System.IO.FileInfo] $File,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [AllowEmptyString()]
        [string[]] $MaskedLine,

        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [hashtable] $Settings
    )

    $relativePath = Get-RelativeDocumentationPath -FullPath $File.FullName -Root $Root

    if (-not $Settings.Contains('Claims') -or @($Settings.Claims.Scope) -notcontains $relativePath) {
        return
    }

    $floorPattern = '(?i)\bcoverage\b.{0,40}\bfloor\b|\bfloor\b.{0,40}\bcoverage\b'
    $claimPattern = '(?i)\b(proves?|proven|tested|verified)\b'
    $disclaimerPattern = '(?i)unit-only|unit only|lower bound|blind'

    for ($index = 0; $index -lt $MaskedLine.Count; $index++) {
        $line = $MaskedLine[$index]
        if ([regex]::IsMatch($line, $floorPattern) -and
            [regex]::IsMatch($line, $claimPattern) -and
            -not [regex]::IsMatch($line, $disclaimerPattern)) {
            New-DocumentationFinding `
                -RelativePath $relativePath `
                -Line ($index + 1) `
                -Column 1 `
                -Severity 'Error' `
                -Rule 'CoverageFloorScope' `
                -Message (
                    'The coverage floor is presented alongside a proven/tested/verified claim ' +
                    "without stating it's a unit-only lower bound blind to live evidence (C11)."
                )
        }
    }
}

function Test-DocumentationClaim {
    <#
    .SYNOPSIS
    Validates the authored support-claim records against C1/C2 (design/20-contract.md):
    one canonical owner per subject, a valid strength, evidence sufficient for that
    strength, and no restatement of a strength outside its owner.

    .DESCRIPTION
    A Claim is declared as an HTML comment so it renders invisibly in both GitHub and
    Docusaurus:

        <!-- claim:consumer-architecture
        strength: contract-checked
        evidence: ArchitectureTest, PackageTest
        -->

    Ownership, strength, and evidence-sufficiency are checked structurally against
    $Settings.Claims; this never fetches a live run to confirm the reference is
    accurate (Public surface § Build and workflow commands), only that it names a
    recognized gate capable of the claimed strength.
    #>
    param (
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [System.IO.FileInfo[]] $File,

        [Parameter(Mandatory)]
        [string] $Root,

        [Parameter(Mandatory)]
        [hashtable] $Settings
    )

    if (-not $Settings.Contains('Claims')) {
        return
    }

    $claims = $Settings.Claims
    $owners = $claims.Owners
    $scope = @($claims.Scope)
    $strengthRank = @{ 'unvalidated' = 0; 'contract-checked' = 1; 'executed' = 2 }

    # S10.3: docs/docs/index.md is a rendering of README.md (GeneratedFiles, checked
    # separately by Test-GeneratedDocumentationFile), not a second authored owner - so it
    # carries the same <!-- claim: --> blocks as README.md by construction and must not be
    # scanned as if it declared them itself.
    $generatedPaths = if ($Settings.Contains('GeneratedFiles')) {
        @($Settings.GeneratedFiles | ForEach-Object { $_.Path })
    } else {
        @()
    }

    $blockPattern = '(?ms)<!--\s*claim:(?<subject>[a-z0-9-]+)\s*(?<body>.*?)-->'
    $strengthLinePattern = '(?im)^\s*strength:\s*(?<value>\S+)\s*$'
    $evidenceLinePattern = '(?im)^\s*evidence:\s*(?<value>.*)$'

    # C2's strength vocabulary asserted in prose rather than through the block above -
    # e.g. "ARM64 support remains unvalidated" typed directly into a document that does
    # not own that subject. Only checked in files that own no Claim at all (Owners'
    # values): an owner file legitimately uses this vocabulary for its own subject, and
    # disambiguating "its own subject" from "someone else's" in free text is exactly the
    # aspirational-wording problem this gate exists to catch, not solve by guessing.
    $restatementPattern = '(?i)\b(?:is|are|remain|remains|stays|stay)\s+(?:executed|contract-checked|unvalidated)\b'
    $ownerFiles = @($owners.Values | Sort-Object -Unique)

    $subjectLocations = @{}

    foreach ($f in $File) {
        $relativePath = Get-RelativeDocumentationPath -FullPath $f.FullName -Root $Root
        if ($scope -notcontains $relativePath -or $generatedPaths -contains $relativePath) {
            continue
        }

        $raw = [IO.File]::ReadAllText($f.FullName) -replace "`r`n?", "`n"

        foreach ($match in [regex]::Matches($raw, $blockPattern)) {
            $subject = $match.Groups['subject'].Value
            $body = $match.Groups['body'].Value
            $line = ($raw.Substring(0, $match.Index) -split "`n").Count

            if (-not $subjectLocations.ContainsKey($subject)) {
                $subjectLocations[$subject] = @()
            }
            $subjectLocations[$subject] += $relativePath

            $expectedOwner = if ($owners.ContainsKey($subject)) { $owners[$subject] } else { $null }
            $isMisplaced = (-not $expectedOwner) -or ($relativePath -ne $expectedOwner)
            $isRepeated = @($subjectLocations[$subject] | Where-Object { $_ -eq $relativePath }).Count -gt 1

            if ($isMisplaced -or $isRepeated) {
                New-DocumentationFinding `
                    -RelativePath $relativePath `
                    -Line $line `
                    -Column 1 `
                    -Severity 'Error' `
                    -Rule 'ClaimDuplicateOwner' `
                    -Message (
                        "Claim subject '$subject' is declared here, but its single canonical " +
                        "owner is $(if ($expectedOwner) { "'$expectedOwner'" } else { 'unassigned' })."
                    )
                continue
            }

            $strengthMatch = [regex]::Match($body, $strengthLinePattern)
            $strength = if ($strengthMatch.Success) { $strengthMatch.Groups['value'].Value.ToLowerInvariant() } else { $null }

            if (-not $strength -or -not $strengthRank.ContainsKey($strength)) {
                New-DocumentationFinding `
                    -RelativePath $relativePath `
                    -Line $line `
                    -Column 1 `
                    -Severity 'Error' `
                    -Rule 'ClaimInvalidStrength' `
                    -Message (
                        "Claim subject '$subject' has strength " +
                        "$(if ($strength) { "'$strength'" } else { '(missing)' }), " +
                        "not one of: $($strengthRank.Keys -join ', ')."
                    )
                continue
            }

            $evidenceMatch = [regex]::Match($body, $evidenceLinePattern)
            $evidenceText = if ($evidenceMatch.Success) { $evidenceMatch.Groups['value'].Value.Trim() } else { '' }

            if ($strength -ne 'unvalidated' -and [string]::IsNullOrWhiteSpace($evidenceText)) {
                New-DocumentationFinding `
                    -RelativePath $relativePath `
                    -Line $line `
                    -Column 1 `
                    -Severity 'Error' `
                    -Rule 'ClaimMissingEvidence' `
                    -Message "Claim subject '$subject' claims '$strength' but cites no evidence."
                continue
            }

            if ($strength -ne 'unvalidated') {
                $bestRank = -1
                foreach ($token in $claims.EvidenceTokens) {
                    $tokenPattern = '(?<![\w-])' + [regex]::Escape($token.Token) + '(?![\w-])'
                    if ([regex]::IsMatch($evidenceText, $tokenPattern) -and $strengthRank.ContainsKey($token.MaxStrength)) {
                        if ($strengthRank[$token.MaxStrength] -gt $bestRank) {
                            $bestRank = $strengthRank[$token.MaxStrength]
                        }
                    }
                }

                if ($bestRank -lt $strengthRank[$strength]) {
                    New-DocumentationFinding `
                        -RelativePath $relativePath `
                        -Line $line `
                        -Column 1 `
                        -Severity 'Error' `
                        -Rule 'ClaimInsufficientEvidence' `
                        -Message (
                            "Claim subject '$subject' claims '$strength' but its evidence " +
                            "('$evidenceText') names no gate capable of that strength."
                        )
                }
            }
        }

        if ($ownerFiles -notcontains $relativePath) {
            $lines = $raw -split "`n"
            $masked = Get-MaskedDocumentationLine -Line $lines -MaskLinkTarget

            for ($index = 0; $index -lt $masked.Count; $index++) {
                foreach ($restated in [regex]::Matches($masked[$index], $restatementPattern)) {
                    New-DocumentationFinding `
                        -RelativePath $relativePath `
                        -Line ($index + 1) `
                        -Column ($restated.Index + 1) `
                        -Severity 'Error' `
                        -Rule 'ClaimRestatement' `
                        -Message (
                            "'$($restated.Value)' restates a claim's strength in a document " +
                            'that owns no Claim subject. Link to the canonical statement instead.'
                        )
                }
            }
        }
    }
}

function Test-GeneratedDocumentationFile {
    <#
    .SYNOPSIS
    Reports a generated file whose committed copy no longer matches its source.
    #>
    param (
        [Parameter(Mandatory)]
        [hashtable] $Definition,

        [Parameter(Mandatory)]
        [string] $Root
    )

    # Repository configuration uses paths relative to the root; an absolute path
    # is honored as-is so a fixture can point somewhere else entirely.
    $resolve = {
        param([string] $Value)
        if ([IO.Path]::IsPathRooted($Value)) { $Value } else { Join-Path $Root $Value }
    }

    $generatedPath = & $resolve $Definition.Path
    $sourcePath = & $resolve $Definition.Source
    $generatorPath = & $resolve $Definition.Generator

    foreach ($required in @($generatedPath, $sourcePath, $generatorPath)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            New-DocumentationFinding `
                -RelativePath $Definition.Path `
                -Line 1 `
                -Column 1 `
                -Severity 'Error' `
                -Rule 'GeneratedFile' `
                -Message "Cannot check generated file: '$required' does not exist."
            return
        }
    }

    # Splatting needs a variable; an inline hashtable would be passed positionally.
    $generatorArguments = @{ $Definition.SourceParameter = $sourcePath }

    # Optional per-project values, such as the front matter title or the site
    # origin to rewrite. Kept in configuration so the generator itself stays
    # free of any one project's details.
    if ($Definition.ContainsKey('Arguments') -and $Definition.Arguments) {
        foreach ($argument in $Definition.Arguments.GetEnumerator()) {
            $generatorArguments[$argument.Key] = $argument.Value
        }
    }

    $expected = (& $generatorPath @generatorArguments) -join "`n"
    $actual = ([IO.File]::ReadAllText($generatedPath)) -replace "`r`n?", "`n"

    if ($expected.TrimEnd("`n") -ceq $actual.TrimEnd("`n")) {
        return
    }

    # Name the first differing line so the fix is obvious without a diff tool.
    $expectedLines = $expected -split "`n"
    $actualLines = $actual -split "`n"
    $limit = [Math]::Max($expectedLines.Count, $actualLines.Count)
    $firstDifference = $limit
    for ($i = 0; $i -lt $limit; $i++) {
        $e = if ($i -lt $expectedLines.Count) { $expectedLines[$i] } else { $null }
        $a = if ($i -lt $actualLines.Count) { $actualLines[$i] } else { $null }
        if ($e -cne $a) {
            $firstDifference = $i
            break
        }
    }

    New-DocumentationFinding `
        -RelativePath $Definition.Path `
        -Line ($firstDifference + 1) `
        -Column 1 `
        -Severity 'Error' `
        -Rule 'GeneratedFile' `
        -Message (
            "Generated from '$($Definition.Source)' but the committed copy differs. " +
            "Regenerate it, then commit the result."
        )
}

$documentationFiles = Get-DocumentationFile `
    -SearchPath $Path `
    -Root $repositoryRoot `
    -Settings $settings

$findings = @(
    if ($settings.Contains('GeneratedFiles')) {
        foreach ($generated in $settings.GeneratedFiles) {
            Test-GeneratedDocumentationFile -Definition $generated -Root $repositoryRoot
        }
    }

    foreach ($file in $documentationFiles) {
        $lines = @(Get-Content -LiteralPath $file.FullName)

        Test-DocumentationLink `
            -File $file `
            -MaskedLine (Get-MaskedDocumentationLine -Line $lines) `
            -Root $repositoryRoot `
            -Cache $anchorCache

        $maskedProse = Get-MaskedDocumentationLine -Line $lines -MaskLinkTarget

        Test-DocumentationTerminology `
            -File $file `
            -MaskedLine $maskedProse `
            -Root $repositoryRoot `
            -Settings $settings

        Test-DocumentationCoverageFloorScope `
            -File $file `
            -MaskedLine $maskedProse `
            -Root $repositoryRoot `
            -Settings $settings
    }

    Test-DocumentationClaim `
        -File $documentationFiles `
        -Root $repositoryRoot `
        -Settings $settings
)

if ($findings.Count -gt 0) {
    foreach ($finding in $findings | Sort-Object RelativePath, Line, Column, Rule) {
        Write-Host (
            "$($finding.RelativePath):$($finding.Line):$($finding.Column) " +
            "[$($finding.Severity)] $($finding.Rule): $($finding.Message)"
        )
    }
}

# Only 'Warning' is non-blocking. Anything else blocks, rather than matching
# 'Error' exactly -- a rule added later with a new or mistyped severity should
# fail loudly instead of being silently reported and passed over.
$warningFindings = @($findings | Where-Object Severity -eq 'Warning')
$blockingFindings = @($findings | Where-Object Severity -ne 'Warning')

if ($blockingFindings.Count -gt 0) {
    throw (
        "Documentation checks failed with $($blockingFindings.Count) error(s), " +
        "$($warningFindings.Count) warning(s)."
    )
}

if ($TreatWarningsAsErrors -and $warningFindings.Count -gt 0) {
    throw "Documentation checks failed with $($warningFindings.Count) warning(s) (-TreatWarningsAsErrors)."
}

if ($warningFindings.Count -gt 0) {
    Write-Host (
        "Documentation checks passed across $($documentationFiles.Count) Markdown file(s), " +
        "with $($warningFindings.Count) warning(s)."
    ) -ForegroundColor Yellow
}
else {
    Write-Host (
        "Documentation checks passed across $($documentationFiles.Count) Markdown file(s)."
    ) -ForegroundColor Green
}
