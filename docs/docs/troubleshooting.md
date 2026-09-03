---
id: troubleshooting
title: Troubleshooting
sidebar_position: 9
---

# Troubleshooting

## Build error: consumer architecture is unresolved or unsupported

The package intentionally supports explicit x64 and ARM64 configurations only. Set `RuntimeIdentifier` to `win-x64`/`win-arm64`, or `PlatformTarget`/`Platform` to `x64`/`ARM64`. Do not use ambiguous `AnyCPU`; the build target refuses to guess which native WinGet DLL to copy. See [Getting Started](getting-started.md#installation).

## `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` with a repository `ProjectReference`

The packed NuGet package supplies its own native DLL and WinMD. A repository `ProjectReference` does not import those packaged `buildTransitive` assets, however. Add a direct `Microsoft.WindowsPackageManager.ComInterop` reference to the executable project, then build again. This caveat does not apply to normal package consumers.

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

<!-- claim:hosting-context
strength: unvalidated
evidence:
-->
Elevation, Windows Service, and SYSTEM hosting are unvalidated: no elevated, Service, or SYSTEM host has actually run this library's operations. The COM activation fallback chain (WinRT projection → `CoCreateInstance` local server → local server with lower-trust registration, [Architecture](architecture.md#layers)) and the CLI shim's App-Execution-Alias fallback are unit-tested in isolation, but that is coverage of the fallback mechanism, not evidence that a live elevated or SYSTEM run succeeds end to end.

Two known considerations, both inherited from WinGet itself:

- There is no App Execution Alias in service contexts — the library's CLI shim already falls back to locating winget.exe in the `WindowsApps` package directory.
- WinGet's install-scope preference in profile-less contexts can behave unexpectedly; enterprise deployments patch winget's own `settings.json` to default to machine scope. Validate your specific service-hosting scenario before production.
