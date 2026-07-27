---
id: troubleshooting
title: Troubleshooting
sidebar_position: 7
---

# Troubleshooting

## `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` at runtime

The most common integration failure. Your executable project is missing the **direct** `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop`. The native activation DLL only gets copied to the output directory of projects that reference the interop package directly — it does not flow through a project or package dependency. See [Getting Started](getting-started.md#installation).

## `WinGetUnavailableException`

Thrown when the library cannot reach WinGet at all. Causes, in rough order of likelihood:

1. **WinGet (App Installer) is not installed** — install it from the Microsoft Store or [winget-cli releases](https://github.com/microsoft/winget-cli/releases).
2. **WinGet is too old** for the pinned interop contract (1.29.x) — update App Installer.
3. **COM activation is blocked in your process context** — some elevated or service hosts; the library already tries the lower-trust activation fallback before giving up. Check the inner exception for the underlying HRESULT.

## `InvalidCastException: No such interface supported` while enumerating

You (or your IDE's "simplify" suggestion) converted an indexed `for` loop over a CsWinRT-projected collection into `foreach`/LINQ. The projected collection enumerator is broken in interop 1.29.280 — only indexer access (`.Count` / `[i]`) works. The library's loops carry fence comments for exactly this reason; if you consume `IWinGetClient` results you are safe (they're plain `List<T>` copies).

## Install fails with `0x8A150056` (installer prohibits elevation)

The package's installer refuses to run elevated. Run your process non-elevated for that package. The library does not surface the installer manifest's elevation requirement ahead of time — `PackageDetails` has no such field — so this is discovered from the `0x8A150056` result itself, not decided in advance.

## Install fails with `0x8A150019` (command requires admin)

The opposite case: a machine-scope installer needs elevation your process doesn't have. Either elevate, or request `PackageScope.User` where the package supports it.

## `AccessDenied` adding/removing sources

Source add/remove modify machine-wide state and require an elevated caller. This is WinGet behavior, not the library's.

## Search returns entries with `ARP\...` ids

Not an error. `winget list` (and therefore the installed catalog) includes software installed outside WinGet, keyed by its raw Apps-and-Features registry entry. Those entries can't be targeted by id for install/upgrade — filter on the `ARP\` prefix when building update UIs.

## Export exits non-zero but the file exists

`winget export` exits non-zero when some installed applications aren't available in any configured source; the export file is still written for everything it could map. Treat "file written" as the success signal if partial coverage is acceptable.

## Running under SYSTEM / as a Windows Service

Two known considerations, both inherited from WinGet itself:

- There is no App Execution Alias in service contexts — the library's CLI shim already falls back to locating winget.exe in the `WindowsApps` package directory.
- WinGet's install-scope preference in profile-less contexts can behave unexpectedly; enterprise deployments patch winget's own `settings.json` to default to machine scope. Validate your specific service-hosting scenario before production — this path is not yet covered by the library's test suite.
