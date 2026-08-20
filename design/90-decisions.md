# Decision log

Append-only. Newest at the top. The rejected alternatives are the point — without them, every future session relitigates the same choice.

## Open
<A staging area, not a home. Things noticed mid-slice that were deliberately not acted on. `/track` turns each into a GitHub issue and removes it from here. An item that is a *decision* rather than a *todo* belongs below as an entry, not in an issue.>

- **`agent.md` prunes.** The seeded `agent.md` was installed unpruned (`/install-all` cannot approve deletions unattended). Every lesson in it is inherited from other repositories, not earned here — review it and propose deletions for lessons that plainly do not apply to a single-language C#/COM library repo (e.g. anything about knowledge-graph tooling, or about stacks this repo does not have).
- **`codex/PROFILES.md`.** Skipped by default per the kit's install rule (no `.codex/` directory or profile reference found). The checked-out branch at install time was named `codex/add-design-audit`, which is suggestive of Codex use but is not one of the kit's named evidence types (a `.codex/` directory, a profile reference, or the user saying so) — flagged here rather than treated as evidence, since an unattended pass does not get to make that call itself.
- **`GetWinGetVersion` returns null under the COM API on hosted CI.** The 2026-08-20 spike below
  found `PackageManager.Version` yielding null on a runner where `winget --version` reports
  `v1.11.510` and every other live call succeeds. `WinGetClient.GetWinGetVersionCore` catches
  everything and returns null, so "the property threw" and "the property returned null" cannot be
  told apart from outside. This is the exact assertion `design/00-brief.md`'s first definition-of-done
  item makes, so whether it is a defect, an upstream contract gap, or a criterion that needs
  rewording is a design-tier judgement, deliberately not made here.
- **The integration-test filter in `WinGetClientIntegrationTests.cs`'s doc comment under-selects.**
  It names `FullyQualifiedName~WinGetClientIntegrationTests`, which matches one of the three fixtures
  in that file and runs 8 of the 12 tests while still reporting a clean total. `CLAUDE.md`'s
  documented `FullyQualifiedName~IntegrationTests` selects all three. A filter that silently
  under-selects and still reports success is the same failure shape as a coverage gate that never
  fails, which the brief already calls out separately.

---

### 2026-08-20 — Probe hosted-CI COM activation before writing the design
Context: four items in `design/00-brief.md`'s definition of done depend on one fact nobody had checked — whether the WinGet COM server can be activated from a GitHub-hosted `windows-latest` session. Left unanswered, `/design` would have had to carry both branches of it, and the brief's own "integration tests run in CI, or record why they do not" fork would have been settled by assumption.
Chosen: run a throwaway workflow (`.github/workflows/spike-com-activation.yml`, branch `spike/ci-com-activation`) that executes the 12 `[Explicit]` integration tests on `windows-latest` and records the runner's WinGet state, before any design work. Result: **11 of 12 passed**. COM activation succeeded even though neither production CLSID is registered in `HKEY_CLASSES_ROOT`, which exercises `WinGetFactory`'s fallback chain in an environment it had never run in; live catalog calls against both configured sources succeeded; and the two `winget.exe` shim tests passed, so the CLI path works there too. Runner: WinGet `v1.11.510`, App Installer `1.26.510.0`, sources `winget` and `msstore`, interactive session. The sole failure is the null `GetWinGetVersion` recorded under `## Open` above.
Rejected: Writing the design against both branches of the unknown and naming the spike as its own first slice — rejected because the design document would have stayed conditional until the spike ran anyway, and the spike costs one CI run. Settling the CI fork by assumption and recording a rationale under the brief's "or record why" branch — rejected because that branch exists for a real blocker, and taking it without evidence would have written a false one down permanently. Probing with a bespoke activation test rather than the real integration tests — rejected because the brief's done-condition names those 12 tests, so evidence about anything else would not have closed the question.
Reversibility: cheap — the finding is evidence, not a commitment. The spike workflow is throwaway and triggers only on `spike/**` branches and manual dispatch, so it never affects the required `build` check; it is to be deleted or promoted into `build.yml` once the design decides whether integration tests run in CI.

---

### 2026-08-20 — Install the two bounded session hooks
Context: an attended `/install` found PowerShell 7 available and no existing `SessionEnd` or `UserPromptSubmit` hooks. The target's `.claude/settings.local.json` contains only permissions, so neither event has an existing hook to preserve.
Chosen: create `.claude/settings.json` containing only the kit's `Measure-Session.ps1` hooks: `-Hook` on `SessionEnd` and `-Watch` on `UserPromptSubmit`.
Rejected: Omitting both hooks — rejected because it leaves the installed measurement and context-warning capability inactive despite both events being available; appending to `.claude/settings.local.json` or adding any other settings — rejected because that file and every setting outside these two hook events remain target-owned.
Reversibility: cheap — remove `.claude/settings.json` if these are its only contents, or remove the two event keys if the target later adds unrelated settings.

### 2026-08-19 — First kit install: `AGENTS.md`/`CLAUDE.md` direction kept inverted
Context: `/install-all` first-installed the agent kit into a repo where `CLAUDE.md` already held real, non-overlapping project guidance (build commands, architecture, constraints, retry policy, CI/releasing, known gaps) and `AGENTS.md` was absent. `INSTALL.md`'s rule for "one holds content, the other absent" is to keep the existing direction as the smaller change.
Chosen: `CLAUDE.md` remains the content-holding contract file — the kit's `AGENTS.md` sections were merged into it as a baseline, with the repository's own pre-existing content preserved verbatim underneath a "Repository specifics" heading. `AGENTS.md` was installed as a one-line pointer to `CLAUDE.md` (direction reversed from the kit's own default of `AGENTS.md` holding content and `CLAUDE.md` pointing to it).
Rejected: Flipping the direction to match the kit's own default (move the existing `CLAUDE.md` content into `AGENTS.md`, make `CLAUDE.md` the pointer) — rejected because it is the larger change and the project's history and tooling already refer to `CLAUDE.md`.
Reversibility: cheap — the content can be moved to the other file and the pointer flipped without loss, since nothing here references `AGENTS.md` by path outside this entry.

### 2026-08-19 — Dropped two duplicate/inapplicable lines from the merged kit content
Context: merging the kit's `AGENTS.md` baseline into this repository's `CLAUDE.md` surfaced two lines that either duplicated an existing target rule or did not apply to this host.
Chosen: (1) Dropped the kit's "No AI attribution" bullet from the merged `House conventions` section — the target's own `CI and releasing` section already states this repository's version of the same rule, and per the kit's own merge rule the target's wording wins over the kit's when both state the same rule. (2) Dropped the kit's "Windows host, projects under `D:\Dropbox\Projects\`. PowerShell Core for scripts." bullet — that is the *kit* repository's own host convention, observably not this repository's (this install ran from a `/Users/ben/Dropbox/Projects/` path), and keeping it would misstate where this repository lives.
Rejected: Keeping both lines verbatim (as literal duplicates) — rejected because two copies of a rule is exactly what the kit's own "Single ownership" section warns against, and the host-path line was actively wrong for this repository as stated.
Reversibility: cheap — both are one-line additions if a future session decides otherwise.

---
