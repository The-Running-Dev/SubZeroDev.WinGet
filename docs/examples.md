---
id: examples
title: Running the Examples
sidebar_position: 4
---

# Running the Examples

The [SubZeroDev.WinGet.Examples](https://github.com/The-Running-Dev/SubZeroDev.WinGet/tree/main/SubZeroDev.WinGet.Examples) console project contains a runnable example for **every** public API.

```shell
cd SubZeroDev.WinGet.Examples
dotnet run                        # lists every example
dotnet run -- <example> [args]    # runs one
```

Press **Ctrl+C** during any long-running example to see cooperative cancellation in action.

## Read-only examples

These run live against your machine's WinGet and change nothing:

| Command | Demonstrates |
|---|---|
| `dotnet run -- version` | `GetWinGetVersion` |
| `dotnet run -- search [query]` | `Search` across all sources |
| `dotnet run -- search-source [query] [source]` | `Search` restricted to one source |
| `dotnet run -- installed` | `GetInstalled`, including flagging non-WinGet (ARP) entries |
| `dotnet run -- upgrades` | `GetAvailableUpgrades` |
| `dotnet run -- get [package-id]` | `GetPackage` |
| `dotnet run -- details [package-id]` | `GetDetails` (full manifest metadata) |
| `dotnet run -- sources` | `GetSources` |
| `dotnet run -- source-get [name]` | `GetSource` |
| `dotnet run -- pins` | `GetPins` (CLI-backed) |
| `dotnet run -- export [file]` | `Export` to a temp JSON file |
| `dotnet run -- low-level [query]` | Using `IWinGetClient` directly, bypassing the retry policy |

## Mutating examples

These **change the machine** and therefore require explicit arguments — run without arguments, they print usage and exit:

| Command | Demonstrates |
|---|---|
| `dotnet run -- install <id> [version]` | `Install` with full options and progress |
| `dotnet run -- update <id>` | `Update` |
| `dotnet run -- uninstall <id>` | `Uninstall` |
| `dotnet run -- download <id> [dir]` | `Download` (installer only, no install) |
| `dotnet run -- repair <id>` | `Repair` |
| `dotnet run -- pin <id> [version] [--blocking]` | `Pin` (plain, gating, or blocking) |
| `dotnet run -- unpin <id>` | `Unpin` |
| `dotnet run -- import <file>` | `Import` |
| `dotnet run -- source-add <name> <uri>` | `AddSource` (elevated) |
| `dotnet run -- source-remove <name>` | `RemoveSource` (elevated) |
| `dotnet run -- source-refresh <name>` | `RefreshSource` |
| `dotnet run -- source-edit <name> explicit\|priority <value>` | `UpdateSource` |

## What the project itself demonstrates

Beyond the individual calls, the Examples project is a working reference for:

- The repository's **direct `Microsoft.WindowsPackageManager.ComInterop` reference** required by this ProjectReference-based executable. A packed `SubZeroDev.WinGet` consumer needs only the library package; its transitive target supplies the native DLL and WinMD.
- **DI composition** via `AddPackageManagement()`.
- **Progress reporting** (`IProgress<PackageOperationProgress>` for packages, `IProgress<double>` for sources).
- **Ctrl+C cancellation** wired through `CancellationToken`.
- **Typed unavailability handling** (`WinGetUnavailableException` exit path).
