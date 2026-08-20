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
- **`GitVersion.yml`'s documented behaviour is stale.** Its comment states that an untagged commit on
  `main` publishes `0.1.0-<n>`, verified before the `v0.1.0` tag existed. Run 32406423223 (2026-08-20)
  packed and pushed `0.1.1-17`. `SPECIFICATION.md` §11 item 6 ("No stable version has been released
  yet — that needs a tag") and item 8 ("the two publish targets are still unexercised") are stale the
  same way: the tag push on 2026-07-22 ran `PublishGitHubPackages` to success. Left uncorrected on
  purpose — all three sentences describe the release story that `design/10-design.md` § *Open
  questions* asks the maintainer to decide, and correcting them before that answer would freeze one
  reading of it.
- **Two further blanket `catch` clauses in `WinGetClient`.** `design/10-design.md` § *Failure modes*
  argues that the catch-everything around the version property is a defect because it makes a null
  unfalsifiable, not because catching is wrong. The same argument applies to the other two catch-all
  clauses in that file, which were not examined. Worth an audit once the version-property diagnostic
  has settled which exception types actually arrive.

---

### 2026-08-20 — Live verification runs beside the required check, never inside it
Context: the 2026-08-20 spike proved the WinGet COM server activates on a hosted `windows-latest` runner, which removed the blocker that `design/00-brief.md`'s "integration tests run in CI, or record why they do not" fork existed for. That makes *where* live tests run a real choice rather than a constraint.
Chosen: keep the required status check hermetic — unit tests, coverage, PE architecture assertions, package contract, none of which touch a live COM server, a network catalog or a package feed — and run live verification on a separate path whose failures are about the environment rather than about the change under review. Additionally: split the twelve live tests by risk class, since five assert against `Microsoft.VisualStudioCode` or a `git` match in Microsoft's catalog while seven depend only on the executing machine's own state.
Rejected: Folding the live tests into the existing required check — rejected because it makes every merge in this repository depend on Microsoft's catalog, a runner's WinGet install and network reach; the first outage blocks unrelated work and teaches everyone to merge past a red check, which destroys the check. Keeping them local-only and taking the brief's "or record why" branch — rejected because that branch exists for a real blocker and the spike removed it, so taking it now would write down a false reason permanently. Treating all twelve as one gate — rejected because a catalog edit upstream and a regression here would then be indistinguishable, and would carry the same blocking consequence.
Reversibility: cheap — trigger and blocking status are workflow configuration. The risk-class split is a test-organisation change and is also cheap while the suite is twelve tests.

### 2026-08-20 — The live packed-consumer smoke test is a separate gate, not an addition to the package-contract gate
Context: `design/00-brief.md`'s first definition-of-done item needs a live COM call from a *packed-package* consumer. The existing package-contract gate already builds several packed consumers; the obvious move is to make one of them call WinGet.
Chosen: reuse the consumer-construction step, but put the live call in its own gate. The contract gate stays hermetic and stays runnable on any Windows machine with an SDK and no WinGet installed.
Rejected: Adding the live call to the contract gate — rejected because portability and determinism are properties that gate currently has, and spending them buys only the convenience of one fewer target. A new standalone consumer project checked into the repository — rejected because it would be a second definition of "what a packed consumer looks like", free to disagree with the one the contract gate already builds; that is the duplicate-rule failure `CLAUDE.md` § *Single ownership* forbids.
Reversibility: cheap — the two gates share their construction step, so merging or separating them later is a build-target edit.

### 2026-08-20 — Mappers become testable through a no-path-to-activation boundary
Context: `design/00-brief.md` requires unit tests for `WinGetClient`'s enum mappers and DTO projections. They are private members of a type whose other members activate COM, and the measured deficit is concentrated there — 479 of the library's 772 uncovered lines are in `WinGetClient` (run 32406423223).
Chosen: define the boundary as a guarantee rather than a file — no path from a mapper to `WinGetFactory` or `WinGetComContext` — and reach them through the `InternalsVisibleTo` grant the library already declares. Reading an enum value from the projection loads metadata and activates nothing; that is what makes these unit tests rather than integration tests wearing the wrong attribute.
Rejected: Testing the mappers through the public API against a mocked client — rejected because the mappers translate projection types, so a mock that avoids the projection tests the mock instead of the mapping. Introducing an abstraction over the projection so it can be faked — rejected because it is a large refactor of settled working code in service of a test, and the projection's enums are inert data that need no faking.
Reversibility: cheap — whether the mappers stay in place with widened visibility or move to their own type is a slice-level call that the boundary does not constrain.

### 2026-08-20 — The coverage gate is one global ratcheting line floor, with no exclusions
Context: `design/00-brief.md` requires a stated threshold that the `Coverage` target actually fails below; today it computes a number and reports it. Measured state on `main` at run 32406423223: 444/1216 lines, 36.5%.
Chosen: a single checked-in line-coverage floor over the library, measured *after* the required unit tests land and set just below the observed value, ratcheting up only and lowered only by a decision-log entry. Branch coverage is reported but not gated initially. One invariant is load-bearing: the floor is evaluated against a run that excluded the `[Explicit]` tests, so live coverage can never inflate the unit-only number.
Rejected: Per-class or per-namespace floors — rejected because they encode the current shape of the code and must be edited by every refactor, decaying into noise. Excluding the COM-orchestration paths from measurement so the ratio looks better — rejected because an exclusion makes the number stop describing the library, and numbers that no longer describe the thing are the brief's actual complaint. Choosing the floor aspirationally before the tests exist — rejected because a gate that fails on unrelated work until someone lowers it trains everyone to lower it.
Reversibility: cheap for the number, expensive for the habit — a floor that gets lowered once is lowered again, which is why lowering it needs an entry here.

### 2026-08-20 — `PackageOperationStatus.Cancelled` is produced, not deleted
Context: `design/00-brief.md` requires the member to be produced by the code or removed from the enum. Reading the pinned projection metadata (`Microsoft.Management.Deployment.winmd`, 1.29.280) settles the mechanism: no result-status enum in the WinGet contract declares any cancelled member, so no mapper over those enums can reach it — while WinGet does define a cancelled-by-user error code, which this library already declares as a constant.
Chosen: produce it. Cancellation arrives as an error code on an otherwise-failed result, not as a status, so the mapping is from the extended error rather than from the status enum. This changes error semantics and is therefore `/contract`'s to record; the semantics themselves are decided here.
Rejected: Deleting the member — rejected because it names a real outcome the library can observe, and it would be a breaking change to an already-published stable version for no gain. Keeping it unreachable and pinning that with a test — rejected because a test asserting that a public value can never occur documents a defect rather than fixing one.
Reversibility: expensive — the member is on the public surface of a published package, so both producing it and removing it are one-way once a consumer depends on either behaviour.

### 2026-08-20 — The version property's guard is narrowed so its null becomes falsifiable
Context: the spike found `GetWinGetVersion` returning null on a runner where `winget --version` reports `v1.11.510` and every other live call succeeds. Reading the projection metadata locates the property on `IPackageManager7`, contract version 13 — well below what that runtime supports — so "the runtime is too old", the case the existing guard was written for, is not the explanation. Reaching it requires a `QueryInterface` beyond the default interface the factory activates against, which every passing call did not need.
Chosen: narrow the guard to the exception types that actually mean *this member does not exist on this runtime*, and let anything else propagate. The defect being fixed is not the null — it is that "the property returned null" and "the call failed" are indistinguishable from outside, which makes the brief's first definition-of-done item unfalsifiable.
Rejected: Leaving the blanket catch and rewording the criterion — rejected because it settles by assumption a question one diagnostic answers, and the diagnostic is cheap. Logging the swallowed exception instead — rejected because a client-layer `ILogger` is a binding non-goal in `design/00-brief.md`. Removing the guard entirely — rejected because the old-runtime case it protects is real, just not the case observed here.
Reversibility: cheap in code, but it is a change to error semantics on a published package, so the semantics need recording in `design/20-contract.md` before a slice implements it.

### 2026-08-20 — One canonical claim per subject, with a mechanical drift check
Context: the same architecture-support and package-shape claims are written across `README.md`, `docs/docs/getting-started.md`, `docs/docs/testing.md`, `docs/docs/architecture.md` and `SPECIFICATION.md`, and drift independently. `docs/docs/index.md` is generated from `README.md` and already drift-gated, so it is not a further copy.
Chosen: one canonical statement per claim subject, referenced from the others, with a drift rule added to the existing documentation gate — which already enforces a generated-file relationship and a terminology rule set, so a claim rule fits its shape rather than introducing a mechanism.
Rejected: Editing all five copies and relying on care — rejected because that is exactly the process that produced five copies saying different things. Generating the claim text from evidence records — rejected as a generator, a schema and a template for four sentences, and the documentation redesign is a binding non-goal.
Reversibility: cheap — the canonical location can move; the drift rule is one rule in an existing gate.

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
