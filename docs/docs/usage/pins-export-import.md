---
id: pins-export-import
title: Pins, Export & Import
sidebar_position: 3
---

# Pins, Export & Import

These features have **no COM API at any WinGet contract version** (verified against the winget-cli IDL). They are the library's single deliberate exception to "no CLI": they run `winget.exe` behind the isolated `IWinGetCliClient` interface, exposed through `IPackageManagementService`.

## Pinning packages

Three pin kinds exist:

| Kind | Effect |
|---|---|
| **Pinning** | Excluded from bulk "upgrade all"; explicit upgrade still allowed |
| **Gating** | May only upgrade within the pinned version pattern (e.g. `2.44.*`) |
| **Blocking** | Cannot be upgraded at all until the pin is removed |

```csharp
// List pins
var pins = await packages.GetPins();

// Plain pin
await packages.Pin("Git.Git");

// Gating pin: only 2.44.x upgrades allowed
await packages.Pin("Git.Git", version: "2.44.*");

// Blocking pin
await packages.Pin("Git.Git", blocking: true);

// Remove
await packages.Unpin("Git.Git");
```

`Pin`/`Unpin` always pin the package definition itself. The lower-level `IWinGetCliClient.AddPin`/`RemovePin` also accept a `pinInstalledVersion` flag to pin the currently-installed version specifically — not exposed through the service layer.

## Export

Snapshot the machine's installed packages to a `winget import`-compatible JSON file:

```csharp
var result = await packages.Export(@"C:\backup\packages.json", includeVersions: true);
```

:::note Non-zero exit with a valid file
`winget export` exits non-zero when some installed applications aren't available in any source — the file is still written for everything it could map. Check whether the file exists rather than treating a non-zero exit as total failure.
:::

## Import

Install everything a previously exported file lists:

```csharp
var result = await packages.Import(@"C:\backup\packages.json",
    ignoreUnavailable: true,    // skip packages no longer in any source
    ignoreVersions: false);     // false = honor exported versions
```

## CLI results

CLI-backed operations return a `CliOperationResult` — since there is no structured COM result, it captures the process outcome directly:

| Property | Meaning |
|---|---|
| `Succeeded` | `ExitCode == 0` |
| `ExitCode` / `ExitCodeHex` | Raw exit code; hex form matches WinGet's documented `APPINSTALLER_CLI_ERROR_*` HRESULTs |
| `Output` / `Error` | Captured stdout/stderr |

## How winget.exe is located

The shim resolves `winget.exe` via the per-user App Execution Alias first, then falls back to globbing the `WindowsApps` package directory and picking the highest version — the same strategy enterprise deployments (Winget-AutoUpdate) use, which matters because **service/SYSTEM contexts have no App Execution Alias**. If neither works, `WinGetUnavailableException` is thrown.
