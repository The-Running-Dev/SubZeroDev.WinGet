@{
    # Product and technology names whose casing must stay consistent across
    # authored documentation. Each Required spelling lists the incorrect
    # variants to reject. Matching is case-sensitive and whole-word, and runs
    # only over prose: fenced code, inline code, link targets, and bare URLs are
    # masked before these rules apply, so `npm install`, ```powershell fences,
    # and github.com URLs are never flagged.
    #
    # These are the names most projects share. Add your own product name and its
    # common misspellings; that is usually the rule that earns its keep.
    Terminology = @(
        @{ Required = 'GitHub'; Variants = @('Github', 'GitHUB', 'Git Hub') }
        @{ Required = 'GitLab'; Variants = @('Gitlab', 'Git Lab') }
        @{ Required = 'PowerShell'; Variants = @('Powershell', 'Power Shell') }
        @{ Required = 'JavaScript'; Variants = @('Javascript', 'Java Script') }
        @{ Required = 'TypeScript'; Variants = @('Typescript', 'Type Script') }
        @{ Required = 'Node.js'; Variants = @('NodeJS', 'Nodejs', 'node js') }
        @{ Required = 'npm'; Variants = @('NPM', 'Npm') }
        @{ Required = 'Docusaurus'; Variants = @('DocuSaurus', 'docusaurus') }
        @{ Required = 'Dockerfile'; Variants = @('DockerFile', 'docker file', 'Docker file') }
        @{ Required = 'Docker Compose'; Variants = @('docker compose', 'Docker-Compose') }
        @{ Required = 'macOS'; Variants = @('MacOS', 'Mac OS', 'macos', 'OSX') }
        @{ Required = 'JSON'; Variants = @('Json') }
        @{ Required = 'YAML'; Variants = @('Yaml', 'yaml file') }
    )

    # Path segments never scanned. Generated, vendored, and dependency trees are
    # not authored here.
    #
    # The last three are the gitignored research clones CLAUDE.md names: they are
    # reference material for reading, not this repository's source, and their docs
    # are upstream's to fix. They are absent from a CI checkout, so leaving them
    # out of this list made the gate pass in CI and fail on any working tree that
    # has them - 23 findings, none of them in a file this repository owns. A gate
    # that cannot be reproduced locally is one nobody runs before pushing.
    ExcludedSegments = @(
        '.git'
        'artifacts'
        'build'
        'coverage'
        'dist'
        'node_modules'
        'UniGetUI'
        'Winget-AutoUpdate'
        'winget-cli'
    )

    # --- GeneratedFiles:start ---
    # Files generated from another file, checked for drift rather than scanned.
    # Each entry names the generated file, its source, and the script that
    # produces the expected content, all relative to the project root. The
    # generator and this check share that script so they cannot disagree.
    #
    # Set SiteUrl to the published origin, with a trailing slash, so absolute
    # links in the README resolve to site-relative links on the homepage.
    #
    # The start/end markers above and below are load-bearing: setup-docs.ps1
    # locates this exact block by those two comment lines to remove it entirely
    # when -NoHomepage is passed. Keep them if you edit this block by hand.
    GeneratedFiles = @(
        @{
            Path = 'docs/docs/index.md'
            Source = 'README.md'
            Generator = 'build/ConvertTo-DocumentationHomepage.ps1'
            SourceParameter = 'ReadmePath'
            Arguments = @{
                Title = 'SubZeroDev.WinGet'
                Description = ''
                SiteUrl = 'https://winget.subzerodev.com/'
            }
        }
    )
    # --- GeneratedFiles:end ---

    # Individual files excluded from scanning, relative to the project root.
    ExcludedFiles = @(
        'CHANGELOG.md'
    )

    # C1/C2: the five claim subjects, their single canonical owner (design/10-design.md
    # § Claim), and the vocabulary of build/live gate names a Claim's `Evidence:` line may
    # cite to justify its `Strength:`. `Scope` is the closed set of documents this repository's
    # support claims live in or reference each other from (the Persisted schemas row in
    # design/20-contract.md); a strength-assertion sentence outside a subject's own owner is
    # checked only inside that scope; there's no story here for prose about strength written
    # in an unrelated document.
    Claims = @{
        Owners = @{
            'consumer-architecture'  = 'README.md'
            'managed-assembly-shape' = 'README.md'
            'hosting-context'        = 'docs/docs/troubleshooting.md'
            'operation-coverage'     = 'docs/docs/testing.md'
            'runtime-version-floor'  = 'docs/docs/getting-started.md'
        }
        Scope = @(
            'README.md'
            'docs/docs/index.md'
            'docs/docs/getting-started.md'
            'docs/docs/architecture.md'
            'docs/docs/testing.md'
            'docs/docs/troubleshooting.md'
            'SPECIFICATION.md'
        )
        # Ranked low-to-high; a claimed Strength needs at least one cited token whose
        # MaxStrength is the same rank or higher (C2's "sufficient for that strength").
        EvidenceTokens = @(
            @{ Token = 'Test'; MaxStrength = 'contract-checked' }
            @{ Token = 'ArchitectureTest'; MaxStrength = 'contract-checked' }
            @{ Token = 'PackageTest'; MaxStrength = 'contract-checked' }
            @{ Token = 'MachineStateTest'; MaxStrength = 'executed' }
            @{ Token = 'CatalogIntegrationTest'; MaxStrength = 'executed' }
            @{ Token = 'PackedConsumerSmokeTest'; MaxStrength = 'executed' }
        )
    }
}
