# Roadmap

Planned work for SubZeroDev.WinGet, grouped into phases. Phases are ordered by a rough
blend of user impact and risk — earlier phases fix things that are wrong or that every
consumer trips over; later phases add capability.

Nothing here is a blocker for the current `0.1.0` prerelease; the library's core (COM-first
package/source management, no console parsing, no WinRT types in the public surface) is
complete and working. These are the edges.

**Legend** — effort: `S` (< 1h), `M` (a few hours), `L` (a day or more).

## Where to start

The package-consumer target, architecture checks, and COM owner-context work are implemented.
PR CI now runs `nuke Test Coverage ArchitectureTest PackageTest --configuration Release`; it
checks architecture and package layout/consumer contracts without live COM execution. The next
highest-value validation is read-only Windows x64 runtime/UI coverage, followed by ARM64 hardware
execution.

[Phase 1](#phase-1-correctness-fixes) contains the remaining small, independent
correctness fixes.

The coordinated execution order for ARM64 correctness, package consumer targets, and
threading is tracked in
[HIGH-VALUE-IMPLEMENTATION-PLAN.md](HIGH-VALUE-IMPLEMENTATION-PLAN.md).

---

## Phase 1 — Correctness fixes

Small, self-contained edits that fix things that are currently wrong. Good first batch.

- [x] **`S` Fix architecture output and package layout.** Executable/test projects now use
  `<PlatformTarget>$(Platform)</PlatformTarget>` and `ArchitectureTest` checks x64/ARM64 PE
  output. The library is IL-only AnyCPU and the package selects x64/ARM64 native assets through
  its consumer target. This is a package-contract result, not ARM64 hardware validation.

- [ ] **`S` Resolve the unreachable `PackageOperationStatus.Cancelled`.**
  Declared at [PackageOperationResult.cs:25](SubZeroDev.WinGet/Models/PackageOperationResult.cs)
  but never produced anywhere. Cancellation surfaces as a thrown `TaskCanceledException` out
  of `WinGetClient.AwaitOperation`, not as a result. The API currently promises a mode it
  never delivers. Pick one:
  - catch `OperationCanceledException` in `AwaitOperation` and map to a `Cancelled` result, or
  - delete the enum member and document on `IWinGetClient` / `IPackageManagementService` that
    cancellation throws.

  Whichever way it goes, add a unit test pinning the chosen behaviour.

- [ ] **`S` Use `TryAddSingleton` in `AddPackageManagement`.**
  [ServiceCollectionExtensions.cs:13](SubZeroDev.WinGet/ServiceCollectionExtensions.cs) uses
  plain `AddSingleton` for all five registrations, so calling the extension twice duplicates
  every registration, and a consumer who registers a fake `IWinGetClient` *before* calling it
  is silently overridden. `TryAdd*` makes the library defer to consumer registrations, which
  is the conventional behaviour for a `services.AddX()` extension.

- [ ] **`S` Stop `GetPins` swallowing every failure as "no pins".**
  [WinGetCliClient.cs:20-31](SubZeroDev.WinGet/WinGetCliClient.cs) treats any non-zero exit as
  an empty pin list. The comment justifies this for the genuinely-empty case, but a missing
  `winget.exe`, an access denial, and a malformed invocation are all indistinguishable from
  "no pins configured". Distinguish the empty case (match on exit code / output) and surface
  the rest.

- [ ] **`S` Remove or wire up the dead `AuthenticationArguments` factory entry.**
  Present in both `Clsids` and `ProjectionActivators`
  ([WinGetFactory.cs:43,66](SubZeroDev.WinGet/Com/WinGetFactory.cs)) but no `Create*` method
  exposes it. Either delete both lines or complete it as part of
  [Phase 7 — authenticated sources](#phase-7-new-capabilities).

- [ ] **`S` Don't abandon the stdout/stderr read tasks on cancellation.**
  [WinGetCliClient.cs:150-171](SubZeroDev.WinGet/WinGetCliClient.cs) — on the cancellation path
  the process is killed and the exception rethrown without awaiting `outputTask`/`errorTask`,
  leaving two faulted unobserved tasks. Benign on modern .NET, but untidy. Await them in a
  `finally`, or swallow deliberately with a comment.

---

## Phase 2 — Threading and reliability

The failure mode this phase addresses is the one most likely to produce bug reports that
can't be reproduced locally: UI freezes and permanently-poisoned singletons.

- [x] **`M` Isolate WinGet COM work on its owned MTA context.**
  `WinGetComContext` now owns projection activation, projected objects, synchronous catalog
  work, and COM continuations on one dedicated MTA thread. Public client calls dispatch complete
  COM flows there, so callers do not run catalog enumeration or projected-object access on their
  own synchronization context. This is structurally tested; read-only Windows UI responsiveness
  validation remains open.

- [x] **`S` Apply the owned-context await design.** Service and CLI awaits use
  `ConfigureAwait(false)`; COM-client flows intentionally remain on the dedicated MTA owner
  context so projected objects do not cross threads. Windows UI responsiveness validation remains open.

- [x] **`M` Stop `Lazy<T>` caching activation failures forever.** ✅ Done.
  The default `LazyThreadSafetyMode.ExecutionAndPublication` caches the *exception* as well as
  the value, so on singleton-registered types one transient COM activation failure (or
  `winget.exe` resolution failure) poisoned the instance for the whole process lifetime, with no
  recovery even after WinGet was repaired. Both sites now use
  `LazyThreadSafetyMode.PublicationOnly`, which re-runs the factory after a failure:
  `PackageManager` in [Com/WinGetComContext.cs](SubZeroDev.WinGet/Com/WinGetComContext.cs) (safe
  because it is only touched from the single owner thread) and `_wingetPath` in
  [WinGetCliClient.cs](SubZeroDev.WinGet/WinGetCliClient.cs) (safe because resolution is a pure
  filesystem lookup, so a raced duplicate returns the same path).

- [ ] **`M` Make `ParsePinList` locale-independent.**
  [WinGetCliClient.cs:192-195](SubZeroDev.WinGet/WinGetCliClient.cs) derives column offsets
  from `header.IndexOf("Id")`, `"Version"`, `"Source"`, `"Pin type"`. `winget pin list`
  localizes its headers, so on a non-English Windows install this returns an empty list
  silently. Splitting rows on runs of two-or-more spaces is language-independent and about as
  robust as the current column-offset approach. Add unit tests with captured non-English
  output.

---

## Phase 3 — Packaging and distribution

- [x] **`M` Ship transitive build targets so packaged consumers need only `SubZeroDev.WinGet`.**
  `PackageTest` validates the exact nupkg entries, managed AnyCPU layout, x64/ARM64 native DLLs,
  WinMD, direct and two-hop consumers, precedence/diagnostics, and build/publish copies. A
  repository `ProjectReference` executable still needs a direct ComInterop reference. Windows
  x64 runtime smoke and ARM64 hardware execution remain open.

- [ ] **`S` Collapse the two sources of truth for version.**
  `<Version>0.1.0</Version>` in [SubZeroDev.WinGet.csproj:22](SubZeroDev.WinGet/SubZeroDev.WinGet.csproj)
  drives the manual NuGet.org path, while GitVersion drives GitHub Packages —
  [GitVersion.yml](GitVersion.yml) explicitly instructs bumping both by hand, which will drift.
  Drop the csproj `<Version>` and have `PublishNuGet` set `GitVersion.SemVer` the way
  `PublishGitHubPackages` already does ([build/Build.cs](build/Build.cs)).

- [ ] **`S` Lower the `Microsoft.Extensions.*` references to the minimum supported version.**
  The library targets `net8.0-windows10.0.26100` but references
  `Microsoft.Extensions.DependencyInjection.Abstractions` and `.Logging.Abstractions` at
  `10.0.10` ([SubZeroDev.WinGet.csproj:42-43](SubZeroDev.WinGet/SubZeroDev.WinGet.csproj)).
  It resolves, but it force-upgrades every net8 consumer's DI and logging stack to 10.x.
  Libraries should reference the lowest version they support — `8.0.x` here. (The 10.x pins in
  the test and example projects are correct and should stay.)

- [ ] **`S` Add packaging hygiene to the NuGet package.**
  Missing today: SourceLink, `<Deterministic>`, `<ContinuousIntegrationBuild>` (set in CI only),
  `.snupkg` symbol package, `PackageIcon`, `PackageReleaseNotes`. Each is a few lines and
  together they're the difference between a package that works and one that looks finished.

- [ ] **`M` Introduce `Directory.Build.props` and `Directory.Packages.props`.**
  The platform pin / TFM / nullable / implicit-usings block is copy-pasted across three csproj
  files and has already drifted (`LangVersion` appears only in the test project). Central
  Package Management would also remove the duplicated version strings for
  `Microsoft.WindowsPackageManager.ComInterop` and the `Microsoft.Extensions.*` packages.

---

## Phase 4 — Build & CI hygiene

- [ ] **`S` Add an `.editorconfig`.**
  `EnforceCodeStyleInBuild=true` is set in
  [SubZeroDev.WinGet.csproj](SubZeroDev.WinGet/SubZeroDev.WinGet.csproj) but there is no
  `.editorconfig`, so it currently enforces nothing beyond compiler defaults. Codify the
  conventions the codebase already follows (blank line before `return`, `using` grouping,
  file-scoped namespaces, no `Async` suffix).

- [ ] **`S` Turn on analyzers.**
  Add `Microsoft.CodeAnalysis.NetAnalyzers` and consider `TreatWarningsAsErrors` for the
  library project. `CA2007` in particular would enforce the `ConfigureAwait` work in
  [Phase 2](#phase-2-threading-and-reliability).

- [ ] **`S` Gate the build on a coverage threshold.**
  The `Coverage` target in [build/Build.cs](build/Build.cs) generates a report and surfaces it
  on the run summary, but never fails. Add a minimum line-coverage check so a regression in
  coverage actually breaks the build. Set the initial floor at whatever the current number is.

- [ ] **`S` Add Dependabot.**
  `.github/dependabot.yml` for NuGet + GitHub Actions. Worth pinning the Nuke version bump
  path explicitly since `NUKE_VERSION` in
  [build.yml](.github/workflows/build.yml) must stay in sync with `build/_build.csproj`.

- [ ] **`S` Add CodeQL scanning.**
  Standard `github/codeql-action` workflow for C#.

- [ ] **`S` Pin FluentAssertions below v8 and document why.**
  The test project references FluentAssertions `6.12.0`
  ([SubZeroDev.WinGet.Tests.csproj](SubZeroDev.WinGet.Tests/SubZeroDev.WinGet.Tests.csproj)),
  which is Apache-2.0. Version 8 changed to a license that requires payment for commercial use;
  7.x is the last freely-licensed line. This matters specifically because the Dependabot item
  above would otherwise propose that upgrade automatically and a routine "bump test
  dependencies" merge would silently take on a licensing obligation. Add an explicit version
  ceiling (or a Dependabot `ignore` rule for major versions) plus a comment explaining it.
  Alternative if the ceiling becomes limiting: migrate the assertions to
  [Shouldly](https://github.com/shouldly/shouldly) or `AwesomeAssertions`, both still permissive.

---

## Phase 5 — Documentation

- [x] **`M` Stand up the Docusaurus site and deploy to GitHub Pages.** ✅ Done.
  [docs/](docs/docs) is built by the containerised `docs-template` image (one source
  of truth) and deploys to GitHub Pages on push to `main` via
  [.github/workflows/docs-deploy.yml](.github/workflows/docs-deploy.yml). Live at
  <https://winget.subzerodev.com/> via a custom domain (`website/static/CNAME`), linked from
  the README. This is now the only supported URL: `docusaurus.config.js` builds with
  `baseUrl: '/'` for the custom domain, so every internal link and asset path is generated
  root-relative. The previous default `the-running-dev.github.io/SubZeroDev.WinGet/` project-pages
  URL is superseded, not a working secondary access point — its own internal navigation and
  assets would resolve against the wrong prefix if that URL is used directly.

  This also fixed the 12 internal links that were extensionless (originally reported as
  11 — six in [docs/intro.md](docs/docs/intro.md), one each in getting-started/packages/
  troubleshooting/architecture, two in testing.md) and 404'd when browsed on GitHub: all
  now carry `.md` suffixes, which resolve correctly both on GitHub and under Docusaurus.
  `onBrokenLinks`/`onBrokenMarkdownLinks` are set to `'throw'` in `docusaurus.config.js`,
  so the doc site's own build is the link checker going forward — a future broken
  cross-reference fails CI instead of shipping.

  **One manual step outside this repo:** GitHub Settings → Pages → Source must be set to
  "GitHub Actions" once, or the deploy job fails with no Pages target configured.

- [x] **`S` Add a README roadmap pointer.** ✅ Done — see the [Roadmap](README.md#roadmap) section.

---

## Phase 6 — API improvements

- [ ] **`M` Introduce a `SearchRequest` and expose `Filters`.**
  Two gaps in one place. `IPackageManagementService.Search` hardcodes
  `DefaultSearchLimit = 50` ([PackageManagementService.cs:28](SubZeroDev.WinGet/PackageManagementService.cs))
  with no caller override. And neither layer exposes `FindPackagesOptions.Filters` — only the
  four hardcoded OR'd Selectors in
  [WinGetClient.cs:69](SubZeroDev.WinGet/WinGetClient.cs).

  The Selectors-OR / Filters-AND distinction is one of the verified findings in
  [SPECIFICATION.md](SPECIFICATION.md) and [docs/architecture.md](docs/docs/architecture.md) — it's
  what makes queries like "packages by publisher X *and* tag Y" possible — but it was never
  surfaced. A `SearchRequest` record (`Limit`, `MatchFields`, `MatchOption`, `Filters`) fixes
  both, and keeps the current behaviour as its default.

- [ ] **`M` Cache the connected composite catalog.**
  Every call performs a fresh `ConnectComposite`
  ([WinGetClient.cs:392](SubZeroDev.WinGet/WinGetClient.cs)) — one of the slowest operations in
  the COM API. Cache the connected catalog per `CompositeSearchBehavior` with an explicit
  `Refresh()` for invalidation. `GetInstalled` followed by `GetAvailableUpgrades` currently
  pays the cost twice for identical work.

- [ ] **`M` Allow operating on an already-resolved package.**
  Every mutating operation re-resolves by id via `FindById`, which is a full composite connect
  ([WinGetClient.cs:308](SubZeroDev.WinGet/WinGetClient.cs)). A `Search` → `Install` flow pays
  for the same lookup twice. Add overloads taking a resolved `PackageInfo` (or an opaque
  handle) — pairs naturally with the catalog caching above.

- [ ] **`S` Add `ILogger` to the client layer.**
  All structured logging lives in `PackageManagementService` / `PackageSourceService`. Anyone
  who drops to `IWinGetClient` for raw single-attempt control — which the README explicitly
  recommends — loses observability entirely.

- [ ] **`M` Extract the mapping logic so it can be unit tested.**
  `MapStatus` (×4), `MapScope`, `MapInstallMode`, `MapUninstallMode`, `MapUninstallScope`,
  `MapRepairMode`, `MapRepairScope`, `MapArchitecture`, `MapInstallerType`, `ToPackageInfo`,
  `ToPackageDetails` — roughly 200 lines of pure, deterministic, private static logic locked
  inside the COM-requiring `WinGetClient`, and therefore covered by **zero** of the mocked unit
  tests. Move to an `internal static WinGetMappings`; `InternalsVisibleTo` is already set on
  the library. Cheapest large coverage win available.

- [ ] **`M` Unit-test `WinGetFactory`'s activation-mode selection.**
  The three-step fallback chain ([WinGetFactory.cs:98-130](SubZeroDev.WinGet/Com/WinGetFactory.cs))
  is the library's most important resilience mechanism and has no tests. Injecting a seam for
  the per-mode activator would make the ordering, the caching of the first successful mode, and
  the `WinGetUnavailableException` aggregation all testable without COM.

- [ ] **`M` Add streaming search via `IAsyncEnumerable<PackageInfo>`.**
  `GetInstalledPackages` materializes the entire installed catalog before returning anything.
  On a machine with hundreds of installed packages, a caller that wants to render progressively
  can't.

---

## Phase 7 — New capabilities

- [ ] **`L` `UpgradeAll` / bulk operations.**
  The single most common real-world WinGet automation task, and the reason Winget-AutoUpdate
  exists. Should be pin-aware, accept a skip list, report per-package progress, and return an
  aggregate result with per-package outcomes. Every primitive already exists — this is
  composition plus a good result shape.

- [ ] **`L` Authenticated REST sources (Entra ID).**
  The highest-value missing capability for enterprise adoption: it's what unblocks private
  corporate catalogs. `AuthenticationArguments` is already half-wired into the factory
  ([WinGetFactory.cs:43,66](SubZeroDev.WinGet/Com/WinGetFactory.cs)) — see the Phase 1 item.
  Needs `AddPackageSourceRequest` to carry authentication configuration and
  `WinGetSourceClient.AddSource` to pass it through.

- [ ] **`S` Add a non-throwing availability probe.**
  Something like `Task<bool> IsAvailable()` or a `WinGetAvailability` record reporting whether
  COM activation succeeded, which mode was used, and the WinGet version. Today the only way to
  find out is to catch `WinGetUnavailableException`, which is awkward for health checks,
  startup gating, and DI diagnostics.

- [ ] **`L` Validate and test Windows Service / SYSTEM hosting.**
  [docs/architecture.md](docs/docs/architecture.md) flags this explicitly as *not yet validated* and
  calls it "an open item before production service hosting" — and it's the context most likely
  to break in production, since it's exactly where the activation fallback chain and the
  `winget.exe` alias-less resolution path matter. Needs a real service-hosted integration test,
  which means CI infrastructure beyond `windows-latest`.

---

## Explicitly out of scope

Recorded so the decision doesn't get re-litigated. From [SPECIFICATION.md](SPECIFICATION.md):

- **`winget configure` / DSC** — a large separate surface with its own resource model.
- **`PackageManagerSettings`** (admin settings) — machine policy configuration, not package management.
- **TLS certificate pinning** for custom sources.

Revisit only if a concrete consumer need appears.
