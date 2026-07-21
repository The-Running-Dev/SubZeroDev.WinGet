---
id: architecture
title: Architecture
sidebar_position: 5
---

# Architecture

```
                    ┌─────────────────────────────┐
Consumers ────────► │ IPackageManagementService    │  validation, logging, retry policy,
                    │ IPackageSourceService        │  result normalization
                    └──────┬───────────────┬───────┘
                           │               │
              ┌────────────▼──┐   ┌────────▼──────────┐   ┌───────────────────┐
              │ IWinGetClient  │   │ IWinGetSourceClient│   │ IWinGetCliClient   │
              │ (packages)     │   │ (sources)          │   │ (pin, export/import│
              └────────┬───────┘   └────────┬───────────┘   │  — CLI shim)      │
                       │                    │               └────────┬──────────┘
              ┌────────▼────────────────────▼──────┐                 │
              │ WinGetFactory (internal)            │          winget.exe process
              │ resilient COM activation chain      │          (ArgumentList, no shell)
              └────────┬────────────────────────────┘
                       │
              Microsoft.Management.Deployment (COM/WinRT, contract 29)
```

## Layers

**Service layer** (`IPackageManagementService`, `IPackageSourceService`) — what most consumers should use. Adds input validation, structured logging (`ILogger<T>`), result normalization, and the documented [auto-retry policy](usage/packages#the-built-in-retry-policy).

**Client layer** (`IWinGetClient`, `IWinGetSourceClient`, `IWinGetCliClient`) — thin, single-attempt translations to/from the COM API (or CLI for the two COM-gap features). Use these when you want full control over retry behavior.

**Activation layer** (`WinGetFactory`, internal) — creates every COM object through a resilient activation chain:

1. **WinRT projection activation** (`new PackageManager()`) — works in normal interactive contexts.
2. **`CoCreateInstance` (CLSCTX_LOCAL_SERVER)** against the out-of-proc production CLSIDs — from winget-cli's own projection sources.
3. **`CoCreateInstance` with `CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION`** — the documented mitigation for elevated hosts, used by the WinGet PowerShell module and UniGetUI.

The first mode that succeeds is cached and used for every subsequent object so all COM objects share one activation context. If all three fail, the typed `WinGetUnavailableException` is thrown.

## Composite catalogs everywhere

Every lookup merges remote sources with local install state via `CreateCompositePackageCatalog` — the architecturally correct way (and what winget itself does) to correlate "installed" with "available":

- **Search** → composite over remote sources with installed-state correlation.
- **Installed / upgrades** → composite in local-catalog mode, so `IsUpdateAvailable` is meaningful.
- **Get-by-id and all mutating operations** → composite over everything with an exact-Id selector, returning the exact `CatalogPackage` WinGet associates with the installed app (required for upgrade/uninstall/repair to work).
- **Unreachable-source resilience** — if the composite fails to connect, each source is probed individually and the composite is rebuilt from the reachable subset.

## Verified COM API findings

These were discovered by running the API, not reading docs — they shape the implementation and are worth knowing if you drop to the client layer:

| Finding | Consequence |
|---|---|
| `FindPackagesOptions.Selectors` are OR'd; `Filters` are AND'd | Multi-field search ("name or id or moniker or tag") uses one call with multiple Selectors |
| Enumerating CsWinRT-projected `IReadOnlyList<T>` via `foreach`/LINQ throws `InvalidCastException` (interop 1.29.280) | All collection traversal uses indexed `for` loops — do not "simplify" them |
| `InstallResult.ExtendedErrorCode` is an `Exception`, not an `int` | The HRESULT comes from `.HResult` on that exception |
| `InstallProgress` and `UninstallProgress` are different WinRT structs with public fields | Separate progress mappers per operation |
| Standard COM activation can fail in elevated/service hosts | The three-step activation chain above |
| Pins and export/import have no COM API at all (contract ≤ 29) | The isolated CLI shim |

## Error codes

`WinGetErrorCodes` exposes the well-known `APPINSTALLER_CLI_ERROR_*` HRESULTs (already `unchecked`-cast to `int` for direct comparison with `ExtendedErrorCode`): `CommandRequiresAdmin`, `UpdateNotApplicable`, `UpgradeVersionUnknown`, `InstallerProhibitsElevation`, `PackageAlreadyInstalled`, `PackageIsPinned`, `RebootRequiredToFinish`, and more. The full table is in [winget's return-code documentation](https://github.com/microsoft/winget-cli/blob/master/doc/windows/package-manager/winget/returnCodes.md).

## Hosting caveats

- **Elevated processes**: standard projection activation may fail; the factory's fallback chain handles the known cases automatically.
- **Windows Service / SYSTEM**: COM activation under `LocalSystem` and winget's scope behavior in profile-less contexts are *not yet validated* by this library's test suite — treat as an open item before production service hosting. The CLI shim already handles the missing App Execution Alias in such contexts.
- **Concurrency**: WinGet serializes some source operations internally; the library does not add its own global lock.
