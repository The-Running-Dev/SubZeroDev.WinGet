# SubZeroDev.PackageManagement — WinGet Client Library Specification

*Rewritten 2026-07-21. This repository now contains only the standalone WinGet client library — previously extracted into a subfolder of a larger WinUpdater web-app repo, now spun out as its own project. This document replaces the old root-level `SPECIFICATION.md` (legacy multi-project WinUpdater app) and `SPECIFICATION-v2.md` (WinUpdater v2 winget-based web app plan), which described a different repository's projects (`WinUpdater.API`, `.Client`, `.CLI`, `.Formulas`, `.Services`, `.Data`, a React SPA, etc.) that do not exist here and are out of scope for this repo. That history is condensed in §1; nothing else from those two files carries forward, since it described code and plans that live elsewhere.*

## 1. Origin & Scope

This library's lineage, briefly:

1. **WinUpdater (legacy)** — a from-scratch Homebrew/MacUpdater clone for Windows, predating `winget`, with a hand-maintained catalog of ~26 per-app "formula" recipes. Archived; not evolved.
2. **WinUpdater v2** — a plan to rebuild as a web UI + API around `winget`. Its first integration approach (shelling out to `winget.exe` and parsing column-aligned CLI text output) was tried and then **replaced** after discovering the WinGet **COM API** (`Microsoft.Management.Deployment`) gives structured, non-text-parsing access instead.
3. **This library** is that COM-API client, verified working end-to-end against a live machine (search, list installed, install/upgrade/uninstall, progress reporting), and judged a plausible candidate for standalone NuGet publication — hence its own repository.

**Scope of this repo:** just the client library and its tests. It has no dependency on, and makes no assumptions about, any consuming web app, CLI, or service. Anyone building a WinUpdater-style tool (or anything else that needs programmatic WinGet access) is a valid consumer.

## 2. What This Is

A C# client library over the **WinGet COM API** (`Microsoft.Management.Deployment`, the same in-process API `winget.exe` itself is a thin wrapper over), via Microsoft's official `Microsoft.WindowsPackageManager.ComInterop` interop package. No console output is parsed anywhere.

The public surface never leaks a COM/WinRT type — callers only ever see plain C# records, an enum, and two interfaces. That encapsulation is the main design property worth preserving as this evolves.

## 3. Project Layout

| Project | Role |
|---|---|
| [SubZeroDev.PackageManagement](SubZeroDev.PackageManagement) | The library. Class library, no external app dependencies. |
| [SubZeroDev.PackageManagement.Tests](SubZeroDev.PackageManagement.Tests) | NUnit tests: fast mocked unit tests + opt-in live integration tests. |
| [SubZeroDev.PackageManagement.sln](SubZeroDev.PackageManagement.sln) | Solution containing just these two projects. |

Project, namespace, and assembly names all match the repo (`SubZeroDev.PackageManagement`) as of 2026-07-21 — the naming inconsistency flagged in earlier drafts of this spec is resolved.

## 4. Public API Surface (as built today)

### Models (`SubZeroDev.PackageManagement.Models`)

```csharp
public sealed record PackageInfo(
    string Id,
    string Name,
    string? Publisher,
    string? InstalledVersion,
    string? AvailableVersion,
    bool IsInstalled,
    bool IsUpdateAvailable,
    string Source);

public enum PackageOperationState
{
    Queued, Downloading, Installing, PostInstall, Completed, Failed
}

public sealed record PackageOperationProgress(
    PackageOperationState State,
    double PercentComplete,
    string? StatusMessage);

public sealed record PackageOperationResult(
    bool Succeeded,
    string? ErrorMessage,
    int? ExtendedErrorCode,
    bool RebootRequired)
{
    public static PackageOperationResult Success(bool rebootRequired = false);
    public static PackageOperationResult Failure(string errorMessage, int? extendedErrorCode = null);
}
```

### Abstractions (`SubZeroDev.PackageManagement.Abstractions`)

```csharp
public interface IWinGetClient
{
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<PackageInfo>> GetInstalledPackagesAsync(CancellationToken cancellationToken);
    Task<PackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken);
    Task<PackageOperationResult> InstallAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken);
    Task<PackageOperationResult> UpgradeAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken);
    Task<PackageOperationResult> UninstallAsync(string packageId, IProgress<PackageOperationProgress>? progress, CancellationToken cancellationToken);
}

public interface IPackageManagementService
{
    Task<IReadOnlyList<PackageInfo>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackageInfo>> GetInstalledAsync(CancellationToken cancellationToken = default);
    Task<PackageInfo?> GetDetailsAsync(string packageId, CancellationToken cancellationToken = default);
    Task<PackageOperationResult> InstallAsync(string packageId, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<PackageOperationResult> UpdateAsync(string packageId, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<PackageOperationResult> UninstallAsync(string packageId, IProgress<PackageOperationProgress>? progress = null, CancellationToken cancellationToken = default);
}
```

### Implementations

- **`WinGetClient : IWinGetClient`** — the real COM-backed implementation. Owns a `PackageManager` instance; every method is a thin, verified-correct translation to/from the COM API (see §6).
- **`PackageManagementService : IPackageManagementService`** — business-logic layer (query trimming/validation, structured logging of outcomes). Depends only on `IWinGetClient`, so it's fully unit-testable without touching COM.
- **`ServiceCollectionExtensions.AddPackageManagement(IServiceCollection)`** — registers both as singletons.

### What each operation actually does today

| Method | Behavior |
|---|---|
| `SearchAsync` | Connects to `PredefinedPackageCatalog.OpenWindowsCatalog` only. Queries `Name`, `Moniker`, `Id`, and `Tag` **separately** (see §6.1 for why) and merges/de-duplicates by `Id`, capped at `limit`. |
| `GetInstalledPackagesAsync` | Connects to `LocalPackageCatalog.InstalledPackages` and returns everything, unfiltered. |
| `GetPackageAsync` | Looks in the installed catalog first (exact `Id` match), then the default source, so upgrade/uninstall always operate on the exact `CatalogPackage` instance WinGet already associates with the installed app. |
| `InstallAsync` / `UpgradeAsync` | `InstallOptions { PackageInstallScope = Any, AcceptPackageAgreements = true }` — no version pinning, architecture, installer-type, custom arguments, or install-location control yet. |
| `UninstallAsync` | Default `UninstallOptions()` — no "keep app data" / force flags exposed yet. |

## 5. Dependencies

| Package | Version | Why |
|---|---|---|
| `Microsoft.WindowsPackageManager.ComInterop` | `1.29.280` | The actual WinGet COM/WinRT projection. Version is pinned to match the original dev machine's installed WinGet (`winget --version` → `v1.29.280`) — **not yet validated against any other version**, see §9. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.10` | For `AddPackageManagement`. |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.10` | `ILogger<T>` in `PackageManagementService`. |
| (tests) `NUnit`, `FluentAssertions`, `Moq`, `coverlet.collector` | — | Standard NUnit test-tooling stack. |

## 6. Build & Platform Requirements

- **`net8.0-windows10.0.26100`** — the interop package requires this specific Windows-flavored TFM (minimum Windows SDK contract 10.0.26100). Confirmed by direct restore failure when a plain `net8.0`/`net9.0` TFM was tried first.
- **Platform must be pinned to `x64` (or `ARM64`)** — `Microsoft.Management.Deployment.dll` is not published `AnyCPU`. Both `.csproj` files set:
  ```xml
  <Platform Condition="'$(Platform)' == '' Or '$(Platform)' == 'AnyCPU'">x64</Platform>
  <Platforms>x64;ARM64</Platforms>
  <PlatformTarget>x64</PlatformTarget>
  ```
  so plain `dotnet build`/`dotnet test` resolves correctly without callers needing to pass `-p:Platform=x64` manually. **ARM64 is declared but not yet built/tested on real ARM64 hardware.**
- **Any project that actually *runs* code from this library needs a *direct* `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop`, not just a `ProjectReference` to `SubZeroDev.PackageManagement`.** The interop package's `.targets` file copies a native activation-factory DLL (`Microsoft.Management.Deployment.dll`) to the *directly-referencing* project's own output directory only — this does not flow transitively through a `ProjectReference`. Confirmed by a real failure: the test project could compile against `WinGetClient` but got `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` at runtime until it took the same direct package reference. **This is the single most important integration note for any consumer of this library**, published or internal.

## 7. Verified Findings (found by running it, not by reading docs)

Nearly the entire API surface matched public documentation/samples on the first attempt — these are the specific corrections that only surfaced by actually building, reflecting the real assembly, and executing against the live WinGet catalog.

### 7.1 `FindPackagesOptions.Filters` are ANDed, not ORed

Adding one filter per field (`Name`, `Moniker`, `Id`, `Tag`) with the same search value to a single `FindPackagesOptions` returned **zero** matches for a query that legitimately matched by name alone. `winget search <query>`'s CLI behavior — "match any of name/id/moniker/tag" — is not reproducible with one multi-filter COM call. `SearchAsync` works around this by issuing one query per field and merging results by `Id` itself.

### 7.2 Enumerating `IReadOnlyList<MatchResult>` via `foreach`/LINQ throws

```
System.InvalidCastException: No such interface supported
   at WinRT.IObjectReference.As[T](Guid iid)
   at System.Collections.Generic.IReadOnlyListImpl`1.Make_IEnumerableObjRef()
```

A real marshaling bug in this version's CsWinRT-projected collection enumerator (confirmed against `Microsoft.WindowsPackageManager.ComInterop` 1.29.280). **Indexer access (`.Count` / `[i]`) works reliably.** Every call site funnels through a shared `ToPackages()` helper that uses an indexed `for` loop — `foreach`, `.Select()`, or any other LINQ/enumerator-based traversal of a WinRT-projected list from this API should be treated as unsafe until proven otherwise on a newer interop version.

### 7.3 `InstallResult`/`UninstallResult.ExtendedErrorCode` is an `Exception`, not an `int`

Reflection showed:
```
Exception ExtendedErrorCode { get; }
InstallResultStatus Status { get; }      // not "InstallResultStatus" as a member name — the property is called Status
Boolean RebootRequired { get; }
```
Success/failure is read from `.Status` (`InstallResultStatus.Ok` / `UninstallResultStatus.Ok`); the numeric error code, when present, comes from `.HResult` on the `Exception`.

### 7.4 `InstallProgress` and `UninstallProgress` are different WinRT structs

Both are plain structs with **public fields** (not properties), and non-overlapping shapes:
```
InstallProgress:   State (PackageInstallProgressState), BytesDownloaded, BytesRequired, DownloadProgress, InstallationProgress
UninstallProgress: State (PackageUninstallProgressState), UninstallationProgress
```
Hence two separate progress-mapping functions (`ReportInstallProgress` / `ReportUninstallProgress`), not one shared one.

### 7.5 Basic (unpackaged, non-elevated) COM activation works fine

`new PackageManager()` and all read operations (search, list installed, get details) succeeded from a plain unpackaged console app — no special manifest, package identity, or elevation needed. This meaningfully de-risks whatever hosts this library later (a Windows Service, an ASP.NET Core app, etc.).

### 7.6 Live-verified end-to-end

- `SearchAsync("vscode", 10, ...)` → 10 real catalog matches (`Microsoft.VisualStudioCode` and related).
- `GetInstalledPackagesAsync()` → all real installed packages, including correctly-flagged non-WinGet entries (raw ARP registry key as `Id`, e.g. `ARP\Machine\X64\EditPad Pro 7`).
- `GetPackageAsync("Microsoft.VisualStudioCode")` → correct publisher (`Microsoft Corporation`) and available version.

## 8. Testing

| Suite | What | How to run | Status |
|---|---|---|---|
| Unit tests (`PackageManagementServiceTests`, `PackageOperationResultTests`) | Mocked `IWinGetClient` via Moq; zero COM dependency; ~0.5s | `dotnet test` (default) | 18/18 passing |
| Integration tests (`WinGetClientIntegrationTests`) | Real `WinGetClient` against the live WinGet catalog and this machine's real installed packages. Marked NUnit `[Explicit]` so they're excluded from normal runs. **Deliberately read-only** — no Install/Upgrade/Uninstall, since those would mutate whatever machine runs the tests. | `dotnet test --filter "FullyQualifiedName~WinGetClientIntegrationTests"` | 4/4 passing |

**Not yet covered by any test**: Install/Upgrade/Uninstall against the real API. Doing so safely needs a deliberately disposable, side-effect-free test package (see §9).

## 9. Roadmap — Toward a Fully Functional, Publishable WinGet Client

### 9.1 Functional gaps

- **Multi-source / composite catalogs.** The library only ever connects to `PredefinedPackageCatalog.OpenWindowsCatalog`. The COM API supports enumerating configured sources and building a *composite* catalog (installed + all remote sources merged) — that's the architecturally correct way to do "is there an update available" cross-referencing, rather than the current two-separate-connects-plus-manual-fallback approach in `FindByIdAsync`. **This is the single biggest real gap**, not just a missing feature but an architectural improvement over what exists.
- **Source management** — list/add/remove/reset/update the configured sources themselves (the `winget source` equivalent).
- **Richer install/uninstall/upgrade options.** Today only `PackageInstallScope` and `AcceptPackageAgreements` are set. A full client needs: version pinning, architecture selection, installer-type selection, custom/override installer arguments, install location, silent-vs-interactive, allow-reboot, uninstall-previous-on-upgrade, skip/only-dependencies.
- **Pin management** — the `winget pin` equivalent (prevent a package from upgrading, or pin to a specific version).
- **Export/import** — snapshot a machine's installed set to a manifest and restore it elsewhere. Broadly useful beyond any one consuming app (e.g., machine provisioning).
- **Download-only** — fetch the installer without running it.
- **Authenticated/private sources** — `PackageCatalogReference` already exposes `AuthenticationArguments`/`AuthenticationInfo`; unused so far. Matters for enterprise/private package feeds.

### 9.2 Robustness gaps

- **Graceful failure when WinGet/App Installer isn't present or is too old.** Right now a missing/incompatible WinGet on the host machine surfaces as a raw `COMException` — should be caught and re-thrown as a clear, typed exception (e.g. `WinGetUnavailableException`) with an actionable message.
- **Elevation behavior is still genuinely untested.** Deliberately out of scope for the current read-only integration tests. Open question: does a non-elevated caller installing a machine-scope package get a clean failure, a UAC prompt, or a silent fallback to user-scope?
- **Verify COM apartment/threading behavior** under a Windows Service host and under ASP.NET Core/Kestrel — only exercised from a console app and a VSTest host so far (and the VSTest host needed its own fix, see §6).
- **A real compatibility matrix.** What's the minimum WinGet/App Installer version required? Does the library degrade gracefully on older machines where the installed WinGet predates the pinned interop package's expected COM surface? ARM64/x86 are declared as supported platforms but never actually built or run.

### 9.3 Packaging gaps (specific to NuGet publication)

- XML doc comments across the full public surface, README, LICENSE, semantic versioning, and CI to build/pack/push.
- A public package name/id decision — see open question below.
- Live integration coverage for the mutating operations (Install/Upgrade/Uninstall), using a deliberately disposable, safe-to-repeatedly-install-and-remove test package rather than mutating arbitrary real software.

## 10. Open Questions

| # | Item |
|---|---|
| 1 | Minimum supported WinGet/App Installer version, and behavior on older machines. |
| 2 | Whether to build/validate the already-declared ARM64 platform. |
| 3 | Elevation behavior for machine-scope installs from a non-elevated caller. |
| 4 | Whether composite-catalog support (§9.1) should land before or after any first consuming app, given it changes `FindByIdAsync`'s current architecture. |
