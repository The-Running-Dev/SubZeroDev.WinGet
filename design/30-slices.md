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
| S8 — Pull requests record live evidence without contaminating the hermetic check | #27 | `7b4ee09` |
| S9 — Unit coverage has an exact ratcheting floor | #28 | `a9eeff3` |
| S10 — Support claims point to the evidence that licenses them | #29 | `5194a4c` |
| S13 — Product invariants fail the build when they regress | #67 | `894c99f` |
| S14 — Live runs name the translations they license | #68 | `693fce9` |
| S15 — Every live check proves which WinGet it ran against | #70 | `ec16c6e` |
