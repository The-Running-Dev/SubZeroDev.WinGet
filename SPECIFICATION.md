# SubZeroDev.WinGet — WinGet Client Library Specification

*Updated 2026-07-21 after the "complete COM wrapper" expansion. The library grew from a minimal 6-method client into a full wrapper of the WinGet COM API surface (contract 29), informed by studying three reference codebases: the official winget-cli source (the authoritative IDL and CLSIDs), UniGetUI (real-world COM usage patterns and elevation workarounds), and Winget-AutoUpdate (operational lessons from enterprise-scale winget automation).*

*Updated again 2026-07-21 (same day, follow-up phase) to extract the workflow's dotnet-CLI steps into a generic [Nuke](https://nuke.build) build (§3, §11).*

*Updated 2026-07-22: the shipped product targets **.NET 8** (`net8.0-windows10.0.26100`) for the widest consumer reach — a net8 library is consumable by net8/net9/net10 apps. Only the Nuke build tooling in `build/` stays on net10, where Nuke.Common 10.x requires it (§2, §3, §9).*

## 1. Origin & Scope

This library's lineage, briefly:

1. **WinUpdater (legacy)** — a from-scratch Homebrew/MacUpdater clone for Windows, predating `winget`, with a hand-maintained catalog of ~26 per-app "formula" recipes. Archived; not evolved.
2. **WinUpdater v2** — a plan to rebuild as a web UI + API around `winget`. Its first integration approach (shelling out to `winget.exe` and parsing column-aligned CLI text output) was tried and then **replaced** after discovering the WinGet **COM API** (`Microsoft.Management.Deployment`) gives structured, non-text-parsing access instead.
3. **This library** is that COM-API client, spun out as its own repository and expanded into a general-purpose wrapper intended to be usable by any consumer (web apps, services, CLIs, fleet tooling) that needs programmatic WinGet access.

**Scope of this repo:** just the client library and its tests. No dependency on, or assumptions about, any consuming application.

## 2. What This Is

A C# client library over the **WinGet COM API** (`Microsoft.Management.Deployment`, the same in-process API `winget.exe` itself wraps), via Microsoft's official `Microsoft.WindowsPackageManager.ComInterop` interop package.

Design properties worth preserving:

- **No `Async` method-name suffix.** All operations are async (`Task`-returning) — suffixing every method is noise, and it's a deliberate project convention (2026-07-21) not to. External calls (COM projection, BCL, Moq/FluentAssertions) keep their own names. Do not "fix" this back.

- **The public surface never leaks a COM/WinRT type** — callers only see plain C# records, enums, and interfaces.
- **No console output is parsed** for anything the COM API can do. The single, deliberate exception is `WinGetCliClient` (§4.4): pin management and export/import have **no COM equivalent at any contract version** (verified against the winget-cli IDL), so those two features — and only those — shell out to `winget.exe`, isolated behind their own interface.
- **Composite catalogs everywhere.** Every lookup merges remote sources with local install state via `CreateCompositePackageCatalog` — the architecturally correct way (and what winget itself does) to correlate "installed" with "available".

## 3. Project Layout

| Project | Role |
|---|---|
| [SubZeroDev.WinGet](SubZeroDev.WinGet) | The library. Packs as the `SubZeroDev.WinGet` NuGet package (v0.1.0, MIT). |
| [SubZeroDev.WinGet.Tests](SubZeroDev.WinGet.Tests) | NUnit tests: 100 mocked unit tests + 12 opt-in live integration tests. |
| [SubZeroDev.WinGet.Examples](SubZeroDev.WinGet.Examples) | Console app with a runnable example per public API. Read-only examples run live by default; mutating ones require explicit arguments. Also demonstrates the direct-ComInterop-reference rule and Ctrl+C cancellation. |
| [SubZeroDev.WinGet.sln](SubZeroDev.WinGet.sln) | Solution containing these three projects. **Deliberately excludes** `build/_build.csproj` (§9). |
| [build/](build) | The Nuke build project (§9): `_build.csproj` (plain `net10.0`, not part of the main `.sln`) + `Build.cs` + `Configuration.cs`. Generic target-based replacement for the dotnet-CLI steps that used to live directly in the workflow YAML. |
| [.github/workflows/build.yml](.github/workflows/build.yml) | CI: two jobs. The **`build`** job (installs `Nuke.GlobalTool`, then `nuke Test` → `nuke Coverage`; the required status check) runs on every push to main **and** every pull request — PRs run tests + coverage only, never pack or publish. The **`release`** job (`needs: build`) runs only on a push (branch or tag) or a manual `workflow_dispatch`, and publishes: (1) **GitHub Packages** via `nuke PublishGitHubPackages` — a push to main publishes a prerelease, a `v*` tag push publishes the stable version (the `push` trigger declares both `branches: [main]` and `tags: ['v*']`; declaring only `branches` would make tag pushes not run the workflow at all), version from GitVersion, auth via the built-in `GITHUB_TOKEN`; (2) **NuGet.org** — manual only, `workflow_dispatch` with the `push_to_nuget` input gated on a `NUGET_API_KEY` secret, `.csproj`-pinned version, `nuke PublishNuGet`. Direct pushes to main are blocked by a repository ruleset — all changes land via PR, so "push to main" means "a merged PR". |
| [README.md](README.md) | Consumer-facing readme; embedded in the NuGet package. |
| [docs/](docs) | Markdown documentation (frontmatter + `_category_.json`): intro, getting started, usage guides (packages/sources/pins-export-import), examples, testing, architecture, troubleshooting. Also GitHub-browsable directly — every internal link is `.md`-suffixed. |
| [docs.ps1](docs.ps1) | Local docs entry point. The site itself is built by the published `ghcr.io/the-running-dev/docs-template` image from `docs/`, gated by [build/Test-Documentation.ps1](build/Test-Documentation.ps1), and deployed to GitHub Pages on push to `main` (`.github/workflows/docs-deploy.yml`). Kept separate from the main `.sln`/CI so a docs change never touches library CI and vice versa. |

Reference clones used for research (`winget-cli/`, `UniGetUI/`, `Winget-AutoUpdate/`) sit in the working directory but are git-ignored — they are not part of the repo.

## 4. Architecture

```
                    ┌─────────────────────────────┐
Consumers ────────► │ IPackageManagementService    │  validation, logging, retry policy,
                    │ IPackageSourceService        │  result normalization
                    └──────┬───────────────┬───────┘
                           │               │
              ┌────────────▼──┐   ┌────────▼─────────┐   ┌──────────────────┐
              │ IWinGetClient  │   │ IWinGetSourceClient│  │ IWinGetCliClient  │
              │ (packages)     │   │ (sources)          │  │ (pin, export/import│
              └────────┬───────┘   └────────┬──────────┘  │  — CLI shim)      │
                       │                    │             └────────┬──────────┘
              ┌────────▼────────────────────▼──────┐               │
              │ WinGetFactory (internal)            │        winget.exe process
              │ resilient COM activation chain      │        (ArgumentList, no shell)
              └────────┬────────────────────────────┘
                       │
              Microsoft.Management.Deployment (COM/WinRT, contract 29)
```

### 4.1 `WinGetFactory` — resilient COM activation (`Com/WinGetFactory.cs`)

Standard WinRT projection activation (`new PackageManager()`) is known to fail in some elevated/service process contexts — UniGetUI's production code and the WinGet PowerShell module both carry workarounds for this. The factory tries, in order:

1. **Projection activation** (`new T()`) — the normal path; works in interactive user contexts.
2. **`CoCreateInstance` (CLSCTX_LOCAL_SERVER)** against the out-of-proc production CLSIDs (taken from winget-cli's own `Microsoft.Management.Deployment.Projection/ClassesDefinition.cs`).
3. **`CoCreateInstance` with `CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION` (0x4000000)** — the documented mitigation for elevated hosts.

The first mode that works is cached and used for **every** subsequent COM object, so all objects share one activation context (mixing contexts between e.g. a `PackageManager` and an `InstallOptions` is unreliable). If all three fail, a public **`WinGetUnavailableException`** is thrown with an actionable message — no raw `COMException` escapes for the "WinGet isn't installed/available" case.

Note: the projection's default interfaces (`IPackageManager`, …) are `internal` in the ComInterop package, so IIDs for the CoCreateInstance path are resolved by reflection (`I` + class name from the projected class's assembly).

### 4.2 `WinGetClient` — package operations

Full package surface: `GetWinGetVersion`, `Search` (optionally restricted to one source), `GetInstalledPackages`, `GetAvailableUpgrades`, `GetPackage`, `GetPackageDetails` (full manifest metadata: description, license, agreements, docs, icons, tags, all available versions), `Install`, `Upgrade`, `Uninstall`, `Download` (installer download without install), `Repair`.

Request records (`InstallRequest`, `UninstallRequest`, `DownloadRequest`, `RepairRequest`) expose the full option surface of the IDL: version pinning, scope, silent/interactive mode, architecture, installer type, install location, log path, `--override`/`--custom` argument equivalents, force, hash-mismatch, skip-dependencies, agreements, correlation data.

Catalog strategy per operation:
- **Search** → composite of remote sources, `RemotePackagesFromAllCatalogs` (installed state correlated into results).
- **Installed / upgrades** → composite, `LocalCatalogs` behavior (remote correlation gives a meaningful `IsUpdateAvailable`).
- **Get-by-id / all mutating ops** → composite, `AllCatalogs`, exact-Id selector — returns the exact `CatalogPackage` WinGet associates with the installed app, which upgrade/uninstall/repair require.
- **Unreachable-source resilience**: if the composite fails to connect, each source is probed individually and the composite is rebuilt from the reachable subset (UniGetUI's production pattern).

### 4.3 `WinGetSourceClient` — source management

The `winget source` equivalent via COM (contract 12/28): `GetSources`, `GetSource`, `AddSource`, `RemoveSource` (with `preserveData` = the "reset" behavior), `RefreshSource`, `UpdateSource` (Explicit/Priority editing). Add/remove require an elevated caller (WinGet returns `AccessDenied` otherwise).

### 4.4 `WinGetCliClient` — the isolated CLI shim

Pin management (`GetPins`, `AddPin` with version-gating/blocking, `RemovePin`) and `Export`/`Import`. These features are CLI-only in WinGet — the contract-29 IDL has no pinning or import/export surface at all.

- winget.exe resolution: App Execution Alias first; for service/SYSTEM contexts (no alias), globs `Program Files\WindowsApps\Microsoft.DesktopAppInstaller_*` and picks the highest version — the strategy Winget-AutoUpdate uses at scale.
- Arguments passed via `ArgumentList` (no shell, no quoting bugs); `--disable-interactivity --accept-source-agreements` always set so nothing blocks waiting for console input.
- `pin list` is the only table parsing in the library: column offsets derived from the header row at runtime, never hardcoded.

### 4.5 Service layer — validation, logging, retry policy

`PackageManagementService` and `PackageSourceService` wrap the clients with argument validation, structured `ILogger` logging, and a small **documented auto-retry policy** derived from error handling proven in UniGetUI/Winget-AutoUpdate (each rule retries at most once):

| Condition | Action |
|---|---|
| Install fails with an "already installed"-family code (`0x8A150061`, `0x8A15010D`, `0x8A15010E`, `0x8A15004F`) | Normalize to success |
| Install/Upgrade fails `NoApplicableInstallers`/`NoApplicableUpgrade` **and** the request constrained architecture/installer-type/scope | Retry unconstrained (the constraint may exclude the only installer the package ships) |
| Upgrade fails with `UPGRADE_VERSION_UNKNOWN` (`0x8A150050`) | Retry with `AllowUpgradeToUnknownVersion` |

Raw single-attempt behavior is always available by calling `IWinGetClient` directly. Common WinGet HRESULTs are published as constants in `WinGetErrorCodes`.

### 4.6 DI registration

`services.AddPackageManagement()` registers all five interfaces as singletons over one shared `WinGetFactory`.

## 5. Dependencies

| Package | Version | Why |
|---|---|---|
| `Microsoft.WindowsPackageManager.ComInterop` | `1.29.280` | The WinGet COM/WinRT projection (contract 29). Pinned to match this dev machine's WinGet; not yet validated against other versions. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.10` | `AddPackageManagement`. |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.10` | `ILogger<T>` in the services. |
| (tests) `NUnit`, `FluentAssertions`, `Moq`, `coverlet.collector` | — | Standard NUnit stack. |

## 6. Build & Platform Requirements

- **`net8.0-windows10.0.26100`** — the interop package requires a Windows-flavored TFM (min SDK contract 10.0.26100). The product targets net8 for the widest reach (a net8 library is consumable by net8/net9/net10 apps via forward compatibility); validated on both the net8 and net10 runtimes — all 12 live COM integration tests pass on each.
- **Platform pinned to `x64` (or `ARM64`)** — `Microsoft.Management.Deployment.dll` is not `AnyCPU`; both `.csproj` files default `AnyCPU` → `x64` so plain `dotnet build`/`test` works. ARM64 declared but never built/tested on hardware.
- **Consumers that *run* code from this library need a direct `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop`**, not just a `ProjectReference`: the interop package's `.targets` copies the native activation-factory DLL only into the directly-referencing project's output. Verified by a real `COMException 0x80040154` until the test project took the direct reference. **The most important integration note for any consumer.**

## 7. Verified Findings (found by running it, not by reading docs)

### 7.1 Selectors are OR'd; Filters are AND'd

The original implementation added one filter per field to `FindPackagesOptions.Filters` and got zero results (Filters are AND'd), then worked around it with four separate queries. The IDL documents the real model: `(Selectors...) && Filters...` — **Selectors are OR'd**. One call with four selectors (Id/Name/Moniker/Tag, ContainsCaseInsensitive) reproduces `winget search` exactly. Verified live. (Supersedes the old four-queries-and-merge workaround.)

### 7.2 Enumerating CsWinRT-projected `IReadOnlyList<T>` via `foreach`/LINQ throws

`InvalidCastException: No such interface supported` from the projected enumerator (confirmed against interop 1.29.280). **Indexer access (`.Count`/`[i]`) is reliable** — every collection traversal in the library uses indexed `for` loops.

### 7.3 `*Result.ExtendedErrorCode` is an `Exception`, not an `int`

Success/failure comes from `.Status`; the numeric code, when present, is `.HResult` on the `ExtendedErrorCode` exception. `InstallerErrorCode`/`UninstallerErrorCode`/`RepairerErrorCode` are separate `uint`s only meaningful for the corresponding error status.

### 7.4 Every operation has its own WinRT progress struct

`InstallProgress`, `UninstallProgress`, `PackageDownloadProgress`, `RepairProgress` are distinct structs with public fields and non-overlapping shapes — four separate progress mappers, all normalized to one public `PackageOperationProgress` record.

### 7.5 The projection's default interfaces are `internal` in the ComInterop package

`typeof(IPackageManager)` doesn't compile against `Microsoft.WindowsPackageManager.ComInterop` (they're public in winget-cli's own projection, internal here). IIDs for the CoCreateInstance path are resolved via reflection from the projected class's assembly.

### 7.6 Pre-indexed source metadata quirks

`GetCatalogPackageMetadata()` works live against the winget source, but populates `ShortDescription` while leaving full `Description` empty for many packages. Treat every metadata field as optional.

### 7.7 Live-verified end-to-end (this machine, 2026-07-21)

12/12 integration tests green: version query; search (incl. single-source restriction returning only that source); installed list via composite (all entries `IsInstalled`); upgrades list (only `IsUpdateAvailable`); get-by-id hit and miss; full details (publisher, description, tags, versions); source list/get (`winget` present with type/argument); `pin list`; real `winget export` to JSON.

## 8. Testing

| Suite | What | How to run | Status |
|---|---|---|---|
| Unit (100 tests) | Services (validation, retry-policy edges, delegation), CLI argument-building contracts, `ParsePinList` variants, model defaults/records, DI registration, exception — all mocked, zero COM | `dotnet test` | 100/100 passing |
| Integration (12 tests) | Real COM API + real winget.exe on the machine. `[Explicit]`, **deliberately read-only** (export writes only a temp file) | `nuke MachineStateTest`, `nuke CatalogIntegrationTest`, or `nuke IntegrationTest` for both | 12/12 passing |

Coverage (measured 2026-07-22 on net8): unit-only **27.7% line** (290/1045); merged with the live integration run **54% line** (565/1045). Everything unit-testable is at or near 100% (`PackageSourceService` 100%, `PackageManagementService` 98.9%, DI registration 100%, models, CLI argument builders, pin parsing); the remaining uncovered code is COM-operation internals reachable only by mutating operations (`WinGetClient` 33%, `WinGetSourceClient` 40.8%, `WinGetFactory` 50% — install/upgrade/uninstall/repair paths, source add/remove, activation fallback modes) — tracked by the "disposable test package" roadmap item.

*Method coverage is no longer quoted: ReportGenerator 5.5.x gates that metric behind sponsorship, so it can't be reproduced from this repo's tooling.*

CI runs the same suite through the Nuke build (§9): `nuke Test Coverage` on GitHub Actions (`windows-latest`), on every push to `main` and every pull request. Integration tests are excluded automatically by `[Explicit]` — GitHub-hosted runners do have winget, but read-only live tests in CI are a deliberate non-goal for now. Verified locally with `act` in host mode (`act push -P windows-latest=-self-hosted`).

**Not covered**: mutating operations (install/upgrade/uninstall/repair/import, source add/remove/refresh, pin add/remove) against the real API — needs a disposable test package and (for sources) elevation.

## 9. Build Orchestration (Nuke)

The workflow's `dotnet`-CLI steps (restore, build, test, coverage rendering, pack, both publish paths) were extracted from `.github/workflows/build.yml` into [build/Build.cs](build/Build.cs), a generic [Nuke](https://nuke.build) build. The workflow YAML is hand-authored (not generated from a `[GitHubActions]` attribute) — it installs `Nuke.GlobalTool` and calls `nuke <Target>` instead of raw `dotnet` commands. It has two jobs: **`build`** (tests + coverage; runs on every push to main and every PR; the required status check) and **`release`** (`needs: build`; runs only on a push to main or a manual dispatch). Pull requests therefore never pack or publish — only the `build` job runs for them.

**Layout:** `build/_build.csproj` (plain `net10.0`, referencing `Nuke.Common` 10.1.0) + `build/Build.cs` + `build/Configuration.cs`, plus a `.nuke/` directory (`parameters.json` pointing at `SubZeroDev.WinGet.sln`) and the `build.ps1`/`build.sh` bootstrappers at the root. The build project is deliberately **not** added to `SubZeroDev.WinGet.sln` — it doesn't touch the WinGet COM interop package and has no reason to share the main solution's Windows-TFM/x64-platform pin. Nuke.Common 10.x ships `lib/net10.0` only, which forces the build project onto net10; this net10 requirement is quarantined to `build/` so it never touches the shipped product. The product (library/tests/examples) targets **net8** for reach (§2). CI therefore installs both SDKs: net8 builds and runs the product, net10 runs Nuke (which shells out to the net8 targets).

**Install/invocation model:** the global tool (`dotnet tool update --global Nuke.GlobalTool --version 10.1.0` — `update`, not `install`, so it's idempotent across the persistent host `act` uses — then `nuke <Target>`). Note that the global tool locates a build by **searching for `build.ps1`/`build.sh`** — it does not find `build/_build.csproj` on its own, and without those files it drops into an interactive "do you want to set up a build?" prompt that hard-fails in CI. The bootstrappers are therefore required, not optional; they simply delegate to `dotnet run --project build/_build.csproj`, which also means `./build.ps1 <Target>` works without installing the global tool at all.

**Targets:** `Restore`, `Compile`, `Test`, `MachineStateTest`, `CatalogIntegrationTest`, and `IntegrationTest` (opt-in; the aggregate composes the two stable risk-class targets and never enters the CI chain), `Coverage` (Nuke's `ReportGenerator` component), `Pack`, `PublishNuGet`, `PublishGitHubPackages` (Nuke's `[GitVersion]` component instead of manually installing GitVersion.Tool and JSON-parsing its output). `Pack` and `PublishGitHubPackages` remain independent pack paths exactly as the original two jobs were — the former uses the `.csproj`-pinned version, the latter overrides it with `GitVersion.SemVer` — since they run in genuinely separate CI jobs/checkouts.

**Nuke resolves NuGet-backed tools from declared packages.** Both the `Coverage` target and `[GitVersion]` injection shell out to tools that Nuke expects to find via `PackageDownload` entries in `build/_build.csproj` (`ReportGenerator`, `GitVersion.Tool`). They are not implicit: without them `Coverage` fails at runtime with *"Missing package reference/download"*, and `[GitVersion]` injection degrades to a warning that only becomes fatal when `PublishGitHubPackages` dereferences `GitVersion.SemVer`.

**On `fetch-depth: 0`:** `[GitVersion]` injection is eager (it runs regardless of which target was requested), but a failure to inject is a **warning**, not a hard startup failure — a shallow clone does not break `nuke Test`/`nuke Pack`. Full history is genuinely required only by `PublishGitHubPackages`. Both jobs currently fetch full history anyway, which is harmless and keeps the two checkouts identical.

**A bug fixed in passing:** the original NuGet.org publish step passed `--skip-duplicates` (plural) to `dotnet nuget push`; the real flag is singular (`--skip-duplicate`). This was never caught because that path (§11, item 7) has never actually executed. The Nuke port uses the correct singular flag via `.EnableSkipDuplicate()`.

**Validation status:** the Nuke build has been compiled and executed on a real Windows host and in GitHub Actions (2026-07-21). `nuke Test Coverage Pack --configuration Release` completes green through the global tool — the exact invocation path CI uses — producing `artifacts/SubZeroDev.WinGet.0.1.0.nupkg`, `coverage/Cobertura.xml`, and `coverage/SummaryGithub.md`, with `[GitVersion]` injecting cleanly. The publish targets themselves still have not run — they push packages, so they remain unexercised until a real release (§11, items 6–7).

### 9.1 Defects found when the port was first executed

The original port was authored without a Windows host or .NET SDK and had **never been compiled**. Four defects surfaced on first execution. Notably the `Build.cs` target code — written from Nuke's docs, the part its author flagged as most at risk — was entirely correct; every defect was environmental/packaging:

1. **TFM mismatch.** `Nuke.Common` 10.x ships `lib/net10.0` only, but the build project targeted `net8.0` → 43 compile errors, no Nuke type resolvable. Resolved by moving the build project to net10. (The whole repo was briefly moved to net10 too, then the product was moved back to net8 for reach — see §2/§3 — leaving only `build/` on net10.)
2. **`build/Configuration.cs` was missing.** `Build.cs` references the `Configuration` enumeration type that Nuke's templates normally generate alongside it.
3. **Bootstrappers were missing.** The global tool locates a build by searching for `build.ps1`/`build.sh`; without them `nuke <Target>` opens an interactive "set up a build?" prompt that hard-fails in CI (`Failed to read input in non-interactive mode`). This **reversed the port's documented "global tool, no bootstrapper scripts" decision** — that combination cannot work. The legacy `.nuke` marker *file* was also converted to the modern `.nuke/` directory.
4. **NuGet-backed tools weren't declared** — see the `PackageDownload` note above.

A claim in the original port's own documentation was also corrected: `[GitVersion]` injection does *not* fail outright on a shallow clone (see the `fetch-depth` note above).

**Known redundancy:** now that the bootstrappers exist, CI's `Install Nuke` step and the `NUKE_VERSION`/`Nuke.Common` version-sync requirement are unnecessary — the workflow could call `./build.ps1 <Target>` directly. Left as-is deliberately; the build system is expected to be replaced shortly.

## 10. Deliberately Out of Scope

- **`Microsoft.Management.Configuration`** (DSC-style declarative configuration) — a separate COM namespace and a separate concern; would be its own package if ever needed.
- **`PackageManagerSettings`** (caller telemetry id, state separation) — in-proc-only COM surface; revisit if a host needs state isolation.
- **Catalog TLS certificate pinning** (`ConnectionValidationHandler`, contract 29) — in-proc-only, niche; not exposed.

## Amendment — 2026-07-27: package consumer assets and COM owner context

This dated amendment records implementation decisions and validation evidence
that postdate the original specification. It does not rewrite or replace the
original normative text above.

- `SubZeroDev.WinGet` now packs a `buildTransitive` target with static
  `Microsoft.Management.Deployment.dll` payloads for `win-x64` and `win-arm64`,
  plus `Microsoft.Management.Deployment.winmd`. For an explicitly configured
  package consumer, it selects the native DLL in RID-first order and then by
  `PlatformTarget`/`Platform`, copies both native DLL and WinMD to build and
  publish output, and rejects unresolved AnyCPU or unsupported architectures.
- The managed library is IL-only AnyCPU; executable/test hosts are explicitly
  x64 or ARM64. `ArchitectureTest` verifies those PE shapes and `PackageTest`
  verifies package layout, exact payload architecture, direct and two-hop
  consumer import/copy behavior, diagnostics, and incremental clean behavior.
  PR CI runs `nuke Test Coverage ArchitectureTest PackageTest --configuration
  Release` before the release job.
- The library now uses a dedicated MTA `WinGetComContext` as the owner of
  activation and projected WinGet objects. Service and CLI awaits use
  `ConfigureAwait(false)`; COM-client flows retain the owner context. This is
  intentionally not a claim that projected objects are agile or may cross
  arbitrary worker threads.
- The package consumer contract is verified without executing live COM. A
  read-only Windows x64 packed-consumer smoke test, Windows UI responsiveness
  coverage for search/details/source operations, and real ARM64 hardware
  execution remain open. Therefore the AnyCPU managed package shape is
  provisional and ARM64 support is limited to build/package-contract evidence.
- Repository `ProjectReference` executable and test consumers remain an
  exception: they need a direct `Microsoft.WindowsPackageManager.ComInterop`
  reference because NuGet `buildTransitive` assets are not applied through a
  project reference.

## 11. Open Questions / Remaining Work

| # | Item |
|---|---|
| 1 | **Elevation behavior for mutating ops is still untested** — does a non-elevated caller installing a machine-scope package get a clean failure, a UAC prompt, or silent user-scope fallback? The activation chain is built for elevated hosts, but no mutating op has been run elevated. |
| 2 | **Windows Service / SYSTEM hosting unverified.** The factory's fallback chain and the WindowsApps winget.exe glob exist precisely for this, but no test has run under LocalSystem. Also note Winget-AutoUpdate's finding: SYSTEM-context installs may need winget's own settings.json patched to machine scope. |
| 3 | Minimum supported WinGet/App Installer version and a compatibility matrix (interop pinned at 1.29.280; contract-13+ members like `PackageManager.Version` are guarded, most others are not). |
| 4 | ARM64 declared but never built/run on hardware. |
| 5 | Live mutating-operation coverage using a disposable test package. |
| 6 | **First GitHub Packages publish is done** — `0.0.1-8` was pushed to the feed on the 2026-07-22 merge, which exposed that GitVersion derives the version from git history and ignores the `.csproj` `<Version>`. [`GitVersion.yml`](GitVersion.yml) now pins `next-version: 0.1.0`. Verified behaviour: an untagged commit on main publishes a distinct prerelease `0.1.0-<n>`; tagging a commit (`v0.1.0`) publishes the stable `0.1.0`. **No stable version has been released yet** — that needs a tag. Per-branch `deployment-mode` overrides were tested and make no difference; an untagged commit is always a prerelease. |
| 7 | First **NuGet.org** publish (separate from GitHub Packages): set the `NUGET_API_KEY` secret and run the workflow with `push_to_nuget`. Publishes the `.csproj`-pinned version. |
| 8 | The Nuke build (§9) is validated locally **and** in GitHub Actions. Remaining: the two publish targets are still unexercised (items 6–7), and the build system is expected to be replaced shortly, at which point §9 and the `build/` project go with it. |
