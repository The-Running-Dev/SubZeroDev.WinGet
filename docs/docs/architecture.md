---
id: architecture
title: Architecture
sidebar_position: 6
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
              │ WinGetComContext + WinGetFactory    │          winget.exe process
              │ dedicated MTA owner / activation    │          (ArgumentList, no shell)
              └────────┬────────────────────────────┘
                       │
              Microsoft.Management.Deployment (COM/WinRT, contract 29)
```

## Layers

**Service layer** (`IPackageManagementService`, `IPackageSourceService`) — what most consumers should use. Adds input validation, structured logging (`ILogger<T>`), result normalization, and the documented [auto-retry policy](usage/packages.md#the-built-in-retry-policy).

**Client layer** (`IWinGetClient`, `IWinGetSourceClient`, `IWinGetCliClient`) — thin, single-attempt translations to/from the COM API (or CLI for the two COM-gap features). Use these when you want full control over retry behavior.

**COM owner context and activation layer** (`WinGetComContext`, `WinGetFactory`, internal) — a dedicated MTA thread owns projection activation and all synchronous access to projected objects. Projected objects do not escape that context because agility has not been established. Client continuations that use those objects deliberately remain on the owner context; service and CLI awaits use `ConfigureAwait(false)` so they do not capture a caller UI synchronization context. This avoids treating `ConfigureAwait(false)` or arbitrary `Task.Run` as permission to move COM objects between threads.

The owner context creates every COM object through a resilient activation chain:

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
- **Windows Service / SYSTEM**: COM activation under `LocalSystem` and winget's scope behavior in profile-less contexts — see [Troubleshooting → Running under SYSTEM / as a Windows Service](troubleshooting.md#running-under-system-as-a-windows-service) for what this library's test suite does and does not cover here. The CLI shim already handles the missing App Execution Alias in such contexts.
- **Concurrency**: WinGet serializes some source operations internally; the library does not add its own global lock.
- **Consumer architecture and package shape**: see the [README's architecture configuration](https://github.com/The-Running-Dev/SubZeroDev.WinGet#architecture-configuration) for what's checked for x64/ARM64 and by which runs.
- **Live operation coverage**: unit tests cover owner-context dispatch, cancellation-before-start, result/exception propagation, repeated calls, and synchronization-context behavior. See [Building & Testing → Live integration tests](testing.md#live-integration-tests) for what runs live against the real COM API and what doesn't yet.
