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
| S16 — A released package's live checks ran against the code being released | #100 | `8bbf105` |
