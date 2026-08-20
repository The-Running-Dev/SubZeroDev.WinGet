# Decision log

Append-only. Newest at the top. The rejected alternatives are the point — without them, every future session relitigates the same choice.

## Open
<A staging area, not a home. Things noticed mid-slice that were deliberately not acted on. `/track` turns each into a GitHub issue and removes it from here. An item that is a *decision* rather than a *todo* belongs below as an entry, not in an issue.>

- **`agent.md` prunes.** The seeded `agent.md` was installed unpruned (`/install-all` cannot approve deletions unattended). Every lesson in it is inherited from other repositories, not earned here — review it and propose deletions for lessons that plainly do not apply to a single-language C#/COM library repo (e.g. anything about knowledge-graph tooling, or about stacks this repo does not have).
- **`codex/PROFILES.md`.** Skipped by default per the kit's install rule (no `.codex/` directory or profile reference found). The checked-out branch at install time was named `codex/add-design-audit`, which is suggestive of Codex use but is not one of the kit's named evidence types (a `.codex/` directory, a profile reference, or the user saying so) — flagged here rather than treated as evidence, since an unattended pass does not get to make that call itself.

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
