# SubZeroDev.WinGet — WinGet Client Library Specification

*Updated 2026-07-21 after the "complete COM wrapper" expansion. The library grew from a minimal 6-method client into a full wrapper of the WinGet COM API surface (contract 29), informed by studying three reference codebases: the official winget-cli source (the authoritative IDL and CLSIDs), UniGetUI (real-world COM usage patterns and elevation workarounds), and Winget-AutoUpdate (operational lessons from enterprise-scale winget automation).*

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
| [SubZeroDev.WinGet.sln](SubZeroDev.WinGet.sln) | Solution containing just these two projects. |
| [.github/workflows/build.yml](.github/workflows/build.yml) | CI: restore → build → unit test (failures stop the job before packaging) → coverage summary onto the run page via ReportGenerator → `dotnet pack` → artifact upload, on every push/PR to main (direct pushes to main are blocked by a repository ruleset — all changes land via PR). **Publishing** has two targets: (1) **GitHub Packages** — automatic on GitHub Release, `needs: build`, version from GitVersion (`.NET tool`) reading the release tag, auth via the built-in `GITHUB_TOKEN`; (2) **NuGet.org** — off by default, manual `workflow_dispatch` with the `push_to_nuget` input gated on a `NUGET_API_KEY` secret, publishing the `.csproj`-pinned version (left unchanged). |
| [README.md](README.md) | Consumer-facing readme; embedded in the NuGet package. |
| [docs/](docs) | Docusaurus-ready Markdown documentation (frontmatter + `_category_.json`): intro, getting started, usage guides (packages/sources/pins-export-import), examples, testing, architecture, troubleshooting. Drop into a Docusaurus `docs/` folder as-is. |

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

- **`net8.0-windows10.0.26100`** — the interop package requires this Windows-flavored TFM.
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
| Integration (12 tests) | Real COM API + real winget.exe on the machine. `[Explicit]`, **deliberately read-only** (export writes only a temp file) | `dotnet test --filter "FullyQualifiedName~IntegrationTests"` | 12/12 passing |

Coverage (2026-07-21): unit-only 28.9% line / 50.2% method; merged with the live integration run 54.9% line / 70.6% method. Everything unit-testable is at or near 100% (services 98.6–100%, models, DI, CLI argument builders, pin parsing); the remaining uncovered code is COM-operation internals reachable only by mutating operations (install/upgrade/uninstall/repair paths, source add/remove, factory fallback modes) — tracked by the "disposable test package" roadmap item.
| CI | GitHub Actions (`windows-latest`): restore, build, unit tests, pack, artifact. Integration tests are excluded automatically by `[Explicit]` — GitHub-hosted runners do have winget, but read-only live tests in CI are a deliberate non-goal for now. Verified locally with act in host mode (`-P windows-latest=-self-hosted`). | push/PR, or `act push -P windows-latest=-self-hosted` | passing |

**Not covered**: mutating operations (install/upgrade/uninstall/repair/import, source add/remove/refresh, pin add/remove) against the real API — needs a disposable test package and (for sources) elevation.

## 9. Deliberately Out of Scope

- **`Microsoft.Management.Configuration`** (DSC-style declarative configuration) — a separate COM namespace and a separate concern; would be its own package if ever needed.
- **`PackageManagerSettings`** (caller telemetry id, state separation) — in-proc-only COM surface; revisit if a host needs state isolation.
- **Catalog TLS certificate pinning** (`ConnectionValidationHandler`, contract 29) — in-proc-only, niche; not exposed.

## 10. Open Questions / Remaining Work

| # | Item |
|---|---|
| 1 | **Elevation behavior for mutating ops is still untested** — does a non-elevated caller installing a machine-scope package get a clean failure, a UAC prompt, or silent user-scope fallback? The activation chain is built for elevated hosts, but no mutating op has been run elevated. |
| 2 | **Windows Service / SYSTEM hosting unverified.** The factory's fallback chain and the WindowsApps winget.exe glob exist precisely for this, but no test has run under LocalSystem. Also note Winget-AutoUpdate's finding: SYSTEM-context installs may need winget's own settings.json patched to machine scope. |
| 3 | Minimum supported WinGet/App Installer version and a compatibility matrix (interop pinned at 1.29.280; contract-13+ members like `PackageManager.Version` are guarded, most others are not). |
| 4 | ARM64 declared but never built/run on hardware. |
| 5 | Live mutating-operation coverage using a disposable test package. |
| 6 | **First GitHub Packages publish**: cut a GitHub Release with a version tag (e.g. `v0.1.0`) — the `publish-github-packages` job packs at the GitVersion-derived version and pushes automatically (no secret needed). Nothing published yet; the path is wired and the workflow YAML validated, but has not run against a real release. |
| 7 | First **NuGet.org** publish (separate from GitHub Packages): set the `NUGET_API_KEY` secret and run the workflow with `push_to_nuget`. Publishes the `.csproj`-pinned version. |
