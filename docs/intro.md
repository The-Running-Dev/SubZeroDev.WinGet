---
id: intro
title: Introduction
sidebar_position: 1
slug: /
---

# SubZeroDev.WinGet

A C# client library for the **WinGet COM API** (`Microsoft.Management.Deployment`) — the same in-process API `winget.exe` itself is built on.

## Why this library

Automating WinGet traditionally means shelling out to `winget.exe` and parsing column-aligned console tables — fragile against column widths, truncation ellipses, and non-Latin characters. The COM API gives structured, typed access to the same engine, but using it directly means dealing with CsWinRT projections, COM activation quirks in elevated hosts, AND-only search filters, and WinRT structs.

SubZeroDev.WinGet wraps all of that:

- **No console output parsing** for anything the COM API can do.
- **No COM/WinRT types in the public surface** — callers only ever see plain C# records, enums, and interfaces.
- **No `Async` method-name suffix** — every operation returns a `Task`; the suffix would be noise.

## Feature overview

| Area | Operations |
|---|---|
| Packages | Search (all sources or one), list installed, list available upgrades, get package, get full manifest details |
| Operations | Install, upgrade, uninstall, download-only, repair — with progress reporting, cancellation, and full option control |
| Sources | List, get, add, remove, refresh, edit (the `winget source` equivalent, via COM) |
| Pins | List, add (version gating and blocking), remove |
| Export/Import | Snapshot installed packages to a `winget import`-compatible JSON file and restore |
| Resilience | Elevation-aware COM activation fallback chain, unreachable-source recovery, typed `WinGetUnavailableException`, documented auto-retry policy |

## The one deliberate exception

Pin management and export/import have **no COM equivalent at any WinGet contract version** (verified against the winget-cli IDL). Those two features — and only those — run `winget.exe` behind an isolated `IWinGetCliClient` interface. Everything else is pure COM.

## Where to go next

- [Getting Started](getting-started.md) — install, requirements, and the one integration rule you must not skip.
- [Managing Packages](usage/packages.md) — search, install, upgrade, uninstall, download, repair.
- [Managing Sources](usage/sources.md) — the `winget source` equivalent.
- [Pins, Export & Import](usage/pins-export-import.md) — the CLI-backed features.
- [Running the Examples](examples.md) — a runnable example for every public API.
- [Architecture](architecture.md) — layers, retry policy, and the verified COM API findings.
