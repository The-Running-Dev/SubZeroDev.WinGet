# Slices — Prove the provisional claims and cut v0.2.0

The original riskiest assumption — that the packed consumer could obtain a non-null WinGet version on
GitHub-hosted Windows once the version-member classifier was narrowed — was exercised first and held:
S1 made the result falsifiable, S2 observed it against the constituted runtime, and the stop condition
in `design/20-contract.md` C23 was never taken. Every slice through S15 has landed, and `v0.2.0` is
tagged and confirmed on both feeds.

The riskiest assumption now is one the pipeline made about itself rather than about WinGet: that a
published version had been checked against the code being published. It had not. Publishing depends
only on the hermetic check, and the live jobs are scoped to pull requests, so the live half of a
release's evidence has been carried by a tree-identity argument rather than by a run against the
tagged commit. S16 replaces the argument with the run. Its own merge is the observation, so the
assumption is exercised by the slice that makes it rather than deferred to the next release.

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

## S16 — A released package's live checks ran against the code being released
Delivers: For the maintainer, and for anyone installing the package from a feed: when a version is
published, the read-only checks that exercise WinGet on a real machine have actually run against
that exact code, rather than against an earlier copy of it that someone judged close enough. Until
now publishing waited only on the checks that never touch WinGet, so a break in the live behaviour
could reach both feeds without any check that would have caught it ever running.
Touches: `.github/workflows/build.yml`; `SubZeroDev.WinGet.Tests/WorkflowCompositionTests.cs`;
         `design/10-design.md` § *Control flow*; `design/90-decisions.md`
Depends on: none — S11 and S12 have landed
Acceptance:
  - S16.1 The `machine-state` job carries no event condition restricting it to pull requests, so it
    runs for a pull request, a push to `main`, a `v*` tag push, and a `workflow_dispatch` alike. It
    must run for every event that can reach `release`; a `needs:` on a skipped job blocks its
    dependent, so an `if:` narrower than `release`'s own trigger set would make publishing
    unreachable rather than gated.
  - S16.2 `release` declares `needs: [build, machine-state]`. A `machine-state` failure on a pushed
    tag leaves `release` unrun and neither feed receives the package.
  - S16.3 The `catalog` job also loses its pull-request-only condition and records its evidence for
    push, tag, and dispatch events, and it is absent from `release`'s `needs`. A catalog failure on
    a tag is a recorded red job and does not stop publication.
  - S16.4 A hermetic test in `WorkflowCompositionTests.cs` fails when `release`'s `needs` omits
    `machine-state`, and a second fails when the `machine-state` job carries an event condition
    excluding any event `release` accepts. Each is demonstrated against a fixture that fails and the
    real workflow that passes, matching the paired-fixture style the existing composition tests use.
  - S16.5 `design/10-design.md` § *Control flow*'s release paragraph states that the machine-state
    check gates publishing for the tagged commit itself, and no longer asserts that no tagged commit
    can carry its own live evidence or that a release cites a pull-request run by tree identity.
  - S16.6 `design/90-decisions.md` gains one entry recording that the 2026-09-05 tree-identity
    substitution is superseded by direct evidence, that `catalog` was deliberately kept out of
    `release`'s `needs`, and what each choice leaves unguarded — a catalog-dependent regression can
    still reach a feed, and a WinGet-install or runner failure can now block a publication of code
    that is itself sound.
  - S16.7 On the merge of this slice's own pull request, the resulting `main` run shows
    `machine-state` completing successfully for that commit's SHA before the GitHub Packages publish
    step begins. The run id and SHA are recorded, and this criterion is ticked only from that
    observation, not from the workflow file reading correctly.
Out of scope: adding a contract invariant — the tree-identity row named in #96 is `/contract`'s, and
  this slice retiring the substitution is a reason to reconsider whether that row is still wanted at
  all, not a licence to write or delete one here. Also out of scope: changing branch-protection
  required statuses, adding `catalog` to the release gate, re-tagging or cutting any new release, and
  every product, mapper, and activation source file — this slice changes workflow composition, the
  tests that assert it, and the two design documents that describe it, and nothing else.

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
| S11 — Publishing succeeds only after both feeds confirm the intended version | #30 | `089814b` |
| S12 — The proven release is cut and observed on both feeds | #31 | `77bb631` |
