# Brief — Prove the provisional claims and cut v0.2.0

> Written by me, not by a model. A model may interrogate it (`/brief-check`) but not author it.

## Problem

The library's public surface is finished and its *package contract* is rigorously verified — but
almost nothing about its *runtime behaviour* has ever been executed. Observably, today:

- Unit line coverage is **27.7%** (290/1045). `WinGetClient.cs` (949 lines), `WinGetSourceClient.cs`
  (257) and `Com/WinGetFactory.cs` (171) have **zero** non-`[Explicit]` tests between them.
  `WinGetFactory`'s activation-mode fallback is the library's main resilience mechanism and is
  covered by nothing.
- The 12 integration tests are `[Explicit]` and are invoked by no workflow, so no COM call in this
  repository has ever executed in CI.
- No packed-package consumer has ever made a single live COM call. The package is proven to *lay out*
  correctly and never proven to *run*.
- `PublishNuGet` has never executed. The `--skip-duplicates` typo survived in the pre-Nuke publish
  path precisely because that path never ran. (`PublishGitHubPackages` *has* run — the 2026-07-22
  `v0.1.0` tag push published stable `0.1.0` — but from code twenty-odd merges stale.)
- Consequently `README.md`, `docs/` and `SPECIFICATION.md` hedge in at least three places —
  "provisional", "remains open", "has not yet been validated". The only stable version ever tagged
  predates almost everything those hedges are about.

The result: a consumer cannot tell which claims are proven and which are aspirational, and neither
can I.

## Who it is for

Me, as the maintainer, first — I am the one who cannot currently answer "does this work?". Then .NET
developers consuming `SubZeroDev.WinGet` from GitHub Packages or NuGet.org: a small number, competent
with .NET and DI, with no knowledge of WinGet COM internals and no appetite for acquiring any.

## Non-goals

The binding list. Everything here is out of scope for every agent, permanently, until this file changes.

- **ARM64 hardware runtime validation.** No ARM64 hardware is available. ARM64 claims are to be
  narrowed to build- and package-contract evidence and stated that way — not left as a pending item.
- **Elevation behaviour for mutating operations**, and **Windows Service / SYSTEM hosting.**
- **Live coverage of mutating operations** (install/uninstall/repair/import, source add/remove, pin
  add/remove). Needs a disposable test package that does not exist.
- **Any new public API surface.** No `UpgradeAll`, no authenticated REST sources, no `SearchRequest`,
  no `IAsyncEnumerable` streaming search, no `IsAvailable()` probe, no client-layer `ILogger`.
- **The documentation redesign** called for by `DESIGN-IS-2026-08-14` (verdict REDESIGN, 12/30).
  Narrowing a claim to match evidence is in scope; restructuring the docs site is not.
- **Consolidating the six overlapping planning documents** (`ROADMAP.md`, `TODO-NEXT.md`,
  `HIGH-VALUE-IMPLEMENTATION-PLAN.md`, `PACKAGING-TARGETS-PLAN.md`, `SPECIFICATION.md` §11,
  `DESIGN-IS-2026-08-14/`). Real, but separate.
- **Replacing the Nuke build system**, however often `SPECIFICATION.md` §9 predicts its own demise.
- **`winget configure`/DSC, `PackageManagerSettings`, TLS certificate pinning.** Already out of scope
  per `SPECIFICATION.md` §10 and restated here so it is not relitigated.

## Definition of done

- A read-only smoke test executes `GetWinGetVersion` **from a packed-package consumer** (not a
  `ProjectReference`) on Windows x64 and returns a non-null version.
- All 12 `[Explicit]` integration tests run green on Windows x64, and the run is recorded.
- Integration tests run in CI, or `design/90-decisions.md` records why they do not.
- `WinGetClient`'s enum mappers and DTO projections have unit tests, and `WinGetFactory`'s
  activation-mode selection has unit tests.
- Unit-only line coverage reaches a stated threshold, and the `Coverage` target **fails** below it.
  Today it never fails.
- Every support claim in `README.md`, `docs/` and `SPECIFICATION.md` matches validation actually
  performed. Specifically: ARM64 is stated as build/package-contract evidence only, and the AnyCPU
  package shape is either confirmed or reverted — not left "provisional".
- `PackageOperationStatus.Cancelled` is either produced by the code or deleted from the enum, with a
  unit test pinning whichever was chosen.
- `v0.2.0` is tagged — `v0.1.0` is already spent on the 2026-07-22 code and is not rewritten — and
  `PublishGitHubPackages` and `PublishNuGet` have each executed successfully at least once against it.

## Environment

Single maintainer. Primary development happens on macOS, where **nothing in this repository builds or
runs** — the product targets `net8.0-windows10.0.26100`. Validation therefore happens on a Windows
x64 machine and on GitHub Actions `windows-latest` (x64). The runner image pins no WinGet
version and has shipped builds too old to expose parts of the COM surface, so hosted live gates
install one pinned WinGet build and record it rather than taking whatever the image happens to
carry. No ARM64 hardware exists in the loop.

The library is consumed in-process by .NET 8+ Windows desktop and console applications. Not a
service, not multi-tenant, no meaningful data volume. The only concurrency that matters is internal:
one owned MTA thread serialises all COM work, and projected WinRT objects are not proven agile.

## Lifespan

Maintained for years. It is a published package other projects will depend on, so the full pipeline
is worth running and the contract is worth being strict about.
