# Slices — Prove the provisional claims and cut v0.2.0

The riskiest remaining assumption is that the packed consumer can obtain a non-null WinGet version
on GitHub-hosted Windows after the version-member classifier is narrowed. S1 makes the result
falsifiable; S2 runs that exact packed-consumer call before any live status becomes required. If S2
does not observe a non-null version, the sequence stops at the brief/environment decision required by
`design/20-contract.md` C23. Later slices do not weaken the observable or substitute
`winget --version`.

`/track` should be run after this document is reviewed. Do not open issues from this command.

## How this document is kept

`## Outstanding` is the authoritative specification for work that has not landed. `/slices` appends
new slices at the next unused number; slice and criterion ids are never renumbered or reused.

A criterion that a later decision makes unsatisfiable is struck from its slice's `Acceptance:` list
and named on a `Retired:` line pointing at the decision that struck it. Its id stays a gap and is
never reused, so an existing issue's checkbox still refers to what it always referred to.

Once a slice's issue is closed, retire its full body to the `## Landed` index, preserving its id,
name, issue, and the commit at which its body was last authoritative. The body remains recoverable
from git history, while `/track` ignores landed slices when checking criterion drift.

## Outstanding

## S8 — Pull requests record live evidence without contaminating the hermetic check
Delivers: For every pull request, the maintainer can see separately whether local WinGet behaviour,
the packed consumer, and Microsoft's current catalog passed, with the machine-state result enforced
without turning catalog drift into a merge blocker.
Touches: `.github/workflows/build.yml`, `build/Build.cs`,
         `SubZeroDev.WinGet.Tests/WinGetClientIntegrationTests.cs`, CI evidence artifacts,
         repository branch-protection configuration
Depends on: S2, S7
Acceptance:
  - S8.1 Pull requests run `MachineStateTest` and `PackedConsumerSmokeTest` together in one
    machine-state job and run `CatalogIntegrationTest` in a separate catalog job; both jobs retain
    normal failing outcomes.
  - S8.2 The existing hermetic job remains required and has an automated negative dependency check
    proving it does not activate COM, run either live-test class, execute `winget.exe`, or contact a
    package feed.
  - S8.3 After S2's non-null hosted observation, branch protection requires the machine-state job in
    addition to the hermetic job and does not require the catalog job.
  - S8.4 Each live job records the exact commit, run identity, binary outcome, non-empty asserted
    facts, explicit non-assertions, and the environment facts required by C4 in a retained artifact.
  - S8.5 A GitHub-hosted Windows x64 run records all twelve project-reference integration tests green
    at their asserted seven/five counts and records the packed-consumer smoke separately.
  - S8.6 Partial success retains evidence for each passing assertion without becoming an all-suite
    pass; an empty input that makes an implication vacuously true is surfaced and licenses no claim.
  - S8.7 A catalog outage or changed witness fails only the catalog job and does not erase separately
    passing machine-state evidence.
Out of scope: ARM64 runtime claims, elevation, service/SYSTEM hosting, mutating-operation coverage, or
    swallowing a live failure to make a workflow green.

---

## S9 — Unit coverage has an exact ratcheting floor
Delivers: A maintainer gets a unit-only coverage gate that fails a regression at a checked-in floor
instead of merely reporting a percentage that no workflow enforces.
Touches: `build/Build.cs`, coverage test fixtures under `build/` or `tools/`,
         `.github/workflows/build.yml`
Depends on: S4, S6
Acceptance:
  - S9.1 After the S4–S6 unit tests land, the slice records the measured unit-only covered-line and
    valid-line counts and declares one private build-owned decimal floor exactly 0.1 percentage point
    below the one-decimal measured result.
  - S9.2 `Coverage` compares the exact integer counts against that decimal ratio; a synthetic report
    one line below the boundary fails and reports the counts and floor, while a report at the boundary
    passes.
  - S9.3 The gated report covers the whole library with no file, class, namespace, or COM-orchestration
    exclusions and is produced only by the unit run that excludes every `[Explicit]` test.
  - S9.4 Live targets cannot contribute files or counts to the report evaluated by the floor.
  - S9.5 The floor has one decimal place, is constrained to `0..100`, and has no command-line override
    or separate data file.
  - S9.6 Restoring report-only coverage makes the new negative gate test fail; lowering the checked-in
    floor requires a decision-log entry, while an increase carries its justifying measurement.
Out of scope: branch-coverage enforcement, per-file thresholds, exclusions that improve the displayed
    ratio, or an aspirational floor chosen before measurement.

---

## S10 — Support claims point to the evidence that licenses them
Delivers: A package consumer can tell which architecture, package-shape, hosting, and operation claims
were executed, contract-checked, or remain unvalidated without comparing contradictory documents.
Touches: `README.md`, `docs/docs/index.md`, `docs/docs/getting-started.md`,
         `docs/docs/architecture.md`, `docs/docs/testing.md`,
         `docs/docs/troubleshooting.md`, `SPECIFICATION.md`,
         `build/Test-Documentation.ps1`, documentation-gate tests
Depends on: S2, S8, S9
Acceptance:
  - S10.1 The five claim subjects have exactly the canonical owners assigned by C1, and each canonical
    statement names one valid strength plus the evidence sufficient for that strength.
  - S10.2 Windows x64 execution evidence is not presented as ARM64 evidence; ARM64 is stated only as
    build/package-contract support, while elevation, service/SYSTEM hosting, and mutating-operation
    live coverage remain `unvalidated`.
  - S10.3 Getting Started, Architecture, and the specification link or explicitly refer to the
    canonical statements instead of restating their assertion or strength; the generated homepage
    remains a projection of the README rather than a second authored owner.
  - S10.4 The documentation gate fails separate negative fixtures for a duplicate owner, invalid
    strength, missing evidence reference, evidence insufficient for the claimed strength, and a
    non-canonical restatement.
  - S10.5 The documentation gate passes the repository's real documents, including the regenerated
    homepage, only after every support claim matches evidence produced by the exact referenced run.
  - S10.6 Existing link, anchor, generated-file, warning, and terminology checks retain their current
    behaviour.
  - S10.7 The `runtime-version-floor` subject has a canonical owner stating that `GetWinGetVersion`
    requires WinGet 1.12 or newer and returns `null` below it, per C25; no document asserts a
    library-wide minimum WinGet version.
  - S10.8 Every document stating the enforced coverage floor states it as a lower bound on unit-only
    coverage that is blind to live evidence, per C11, and the gate fails a negative fixture presenting
    the floor as a measure of the library's proven, tested, or verified behaviour. This is a gate rule
    over any document, not a sixth claim subject; C1's five subjects are unchanged.
Out of scope: the documentation redesign, generated claim manifests, changing routes or information
    architecture, or promoting a claim from wording alone.

---

## S11 — Publishing succeeds only after both feeds confirm the intended version
Delivers: The maintainer can publish a release and have each publishing target fail unless its feed
can be queried back for the exact version, so a skipped duplicate or green push command is not
mistaken for delivery.
Touches: `build/Build.cs`, `.github/workflows/build.yml`, `GitVersion.yml`,
         `SubZeroDev.WinGet/SubZeroDev.WinGet.csproj`, `SPECIFICATION.md`, publication test fixtures
Depends on: S9
Acceptance:
  - S11.1 `PublishGitHubPackages` confirms through the intended GitHub Packages feed that the exact
    GitVersion-derived package version is visible after push; push success or `--skip-duplicate`
    without that observation fails the target.
  - S11.2 `PublishNuGet` confirms through NuGet.org that the exact project-version package is visible
    after push; command success without that observation fails the target.
  - S11.3 Each confirmation records the release tag/ref, exact commit, destination, intended version,
    observed version, and workflow run identity without exposing credentials.
  - S11.4 Publication tests make both targets fail for a successful push followed by a missing or
    mismatched feed version and pass only for an exact match.
  - S11.5 GitVersion and project version inputs both prepare `0.2.0`, the workflow publishes from the
    exact checked-out ref, and stale comments or specification claims about `v0.1.0` and previously
    exercised publishing paths are corrected to the verified history.
  - S11.6 Re-running confirmation for the same intended version is safe; changing the version or tag
    to escape a partial publish is not automated.
Out of scope: creating or pushing `v0.2.0`, moving `v0.1.0`, deleting a feed version, or publishing
    from a commit that has not passed the release prerequisites.

---

## S12 — The proven release is cut and observed on both feeds
Delivers: A package consumer can install the immutable `v0.2.0` release from either intended feed,
and the maintainer has run evidence tying both published packages to the exact tested commit.
Touches: git tag `v0.2.0`, GitHub Actions run history and evidence artifacts,
         GitHub Packages, NuGet.org, release references in canonical repository documentation
Depends on: S8, S10, S11
Acceptance:
  - S12.1 The exact release commit has green required hermetic and machine-state statuses, a recorded
    green catalog result for all five catalog tests, and the evidence artifacts required by C3–C4.
  - S12.2 One immutable `v0.2.0` tag is created at that commit and pushed once; `v0.1.0` still resolves
    to its original published commit.
  - S12.3 The tag workflow executes `PublishGitHubPackages`, and positive feed confirmation observes
    `SubZeroDev.WinGet` version `0.2.0` tied to the tagged commit.
  - S12.4 `PublishNuGet` executes against the same tagged commit, and positive NuGet.org confirmation
    observes `SubZeroDev.WinGet` version `0.2.0`.
  - S12.5 The release record references the tag, commit, both workflow runs, both feed confirmations,
    asserted facts, and explicit non-assertions; no document claims success before both confirmations
    exist.
  - S12.6 If only one feed confirms the package, that feed remains published state but the slice and
    release criterion remain incomplete; neither tag is moved and no replacement version is invented.
Out of scope: `v0.2.1`, moving or deleting published tags/packages, ARM64 runtime claims, or any
    validation excluded by the brief.

---

## S14 — Live runs name the translations they license
Delivers: The maintainer can read, from a live run's own record, exactly which package and source
translations that run checked and which it did not, so a green pull-request check is never mistaken
for evidence about the ones only a live run can exercise.
Touches: `SubZeroDev.WinGet.Tests/WinGetClientIntegrationTests.cs`,
         `.github/workflows/build.yml`, CI evidence artifacts
Depends on: S8, plus the `/contract` amendment raising `CatalogIntegrationTest`'s asserted count
            to six (`design/90-decisions.md`, 2026-08-31)
Acceptance:
  - S14.1 Each of the eight live-licensed translations reachable read-only — `ToPackages`,
    `ToPackageInfo`, `ToPackageDetails`, `CopyAgreements`, `CopyDocumentations`, `CopyIcons`,
    `ToPackageSource`, and `GetPriority` — is covered by at least one live assertion that names the
    member it licenses and fails when that member returns a wrong projection reading.
  - S14.2 Each named assertion compares at least one field the translation copies against an
    expected value. A non-null or non-empty check alone does not count as naming the member.
  - S14.3 The eight assertions above are added inside the existing twelve live tests, and the only
    test added is S14.7's. `MachineStateTest` still selects exactly seven and
    `CatalogIntegrationTest` selects exactly six, and both still fail before execution on any other
    count.
  - S14.4 Each live job's evidence record lists the translations its passing assertions licensed and
    records in its non-assertions the live-licensed translations that run did not check.
  - S14.5 Where the witness package's manifest supplies no agreements, documentation, or icons, the
    run records a non-assertion naming the missing witness rather than a passing assertion over an
    empty collection.
  - S14.6 The hermetic job's evidence record explicitly disclaims every live-licensed translation in
    its non-assertions, and no document reads a green hermetic check as evidence about one.
  - S14.7 One catalog-dependent live test calls `Download` with a pinned version and asserts that the
    resolved version equals the requested one, naming `FindVersionId` as the translation it licenses.
    The test cleans up whatever it writes and asserts nothing about installed machine state.
Out of scope: `GetInstallerErrorCode`, whose only call paths are install and upgrade — a binding
    non-goal, so it stays obliged by C26 and licensed by nothing; any live test beyond S14.7's;
    exercising install, upgrade, uninstall, repair, or import to reach a translation; and any fake,
    stub, or reflection substitute for a projected type.

## Landed

| Slice | Issue | Landed at |
|---|---|---|
| S1 — Version absence becomes falsifiable | #20 | `1a3d751` |
| S2 — A packed consumer proves the version path on hosted Windows | #21 | `1a3d751` |
| S3 — Cancelled operations return the cancelled result | #22 | `1a3d751` |
| S4 — Activation fallback is deterministic under unit test | #23 | `1a3d751` |
| S5 — Operation and request translations have one tested owner | #24 | `1a3d751` |
| S6 — DTO and collection projections have one tested owner | #25 | `ee7d660` |
| S7 — The live suite has stable risk-class entry points | #26 | `1a3d751` |
| S13 — Product invariants fail the build when they regress | #67 | `894c99f` |
| S15 — Every live check proves which WinGet it ran against | #70 | `ec16c6e` |
