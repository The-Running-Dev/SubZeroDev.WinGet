# SubZeroDev.WinGet

[![Build](https://github.com/The-Running-Dev/SubZeroDev.WinGet/actions/workflows/build.yml/badge.svg)](https://github.com/The-Running-Dev/SubZeroDev.WinGet/actions/workflows/build.yml)

A C# client library for the **WinGet COM API** (`Microsoft.Management.Deployment`) — the same in-process API `winget.exe` itself is built on. Search, install, upgrade, uninstall, download, and repair packages; manage sources; pin packages; export/import package sets.

**No console output parsing** for anything the COM API can do, and **no COM/WinRT types in the public surface** — callers only ever see plain C# records, enums, and interfaces.

## Features

| Area | Operations |
|---|---|
| Packages | Search (all sources or one), list installed, list available upgrades, get package, get full manifest details (description, license, agreements, icons, versions) |
| Operations | Install, upgrade, uninstall, download-only, repair — with progress reporting, cancellation, and full option control (version, scope, architecture, installer type, silent/interactive, custom arguments, install location) |
| Sources | List, get, add, remove, refresh, edit (the `winget source` equivalent, via COM) |
| Pins | List, add (including version gating and blocking), remove |
| Export/Import | Snapshot installed packages to a `winget import`-compatible JSON file and restore |
| Resilience | Elevation-aware COM activation fallback chain, unreachable-source recovery, typed `WinGetUnavailableException`, documented auto-retry policy for known-recoverable WinGet error codes |

Pin management and export/import have no COM equivalent in WinGet (verified against the winget-cli IDL), so those two features — and only those — run `winget.exe` behind an isolated `IWinGetCliClient` interface. Everything else is pure COM.

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using SubZeroDev.WinGet;
using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

var services = new ServiceCollection()
    .AddLogging()
    .AddPackageManagement()
    .BuildServiceProvider();

var packages = services.GetRequiredService<IPackageManagementService>();

// Search across all configured sources (installed state included in results)
var results = await packages.Search("vscode");

// Install with options
var result = await packages.Install("Microsoft.VisualStudioCode", new InstallRequest
{
    Scope = PackageScope.User,
    Mode = PackageOperationMode.Silent
},
progress: new Progress<PackageOperationProgress>(p =>
    Console.WriteLine($"{p.State}: {p.PercentComplete:F0}%")));

if (!result.Succeeded)
{
    Console.WriteLine($"{result.Status}: {result.ErrorMessage} (0x{result.ExtendedErrorCode:X8})");
}

// What can be upgraded?
var upgrades = await packages.GetAvailableUpgrades();

// Sources
var sources = services.GetRequiredService<IPackageSourceService>();
var configured = await sources.GetSources();
```

Prefer raw, single-attempt behavior without the service layer's retry policy? Use `IWinGetClient` / `IWinGetSourceClient` / `IWinGetCliClient` directly — they are registered by `AddPackageManagement()` too.

### Runnable examples

[SubZeroDev.WinGet.Examples](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/SubZeroDev.WinGet.Examples) has a runnable example for **every** public API:

```
cd SubZeroDev.WinGet.Examples
dotnet run                       # lists all examples
dotnet run -- search terminal    # read-only examples run live
dotnet run -- install <id>       # mutating examples require explicit arguments
```

Read-only examples (search, installed, upgrades, details, sources, pins, export, version) run safely against your machine; anything that would change it (install, uninstall, pin, source add/remove, import) refuses to run without explicit arguments.

## Requirements

- Windows 10/11 with **WinGet (App Installer)** installed
- .NET 8 or newer — the package targets `net8.0-windows10.0.26100`, so it also runs on net9/net10 apps
- An explicit supported architecture: **x64** or **ARM64**. The package supplies the matching native WinGet DLL and WinMD; see [Architecture configuration](#architecture-configuration) for what each is checked against.

### Architecture configuration

A package consumer needs only `SubZeroDev.WinGet`. Its transitive build target supplies the matching x64 or ARM64 `Microsoft.Management.Deployment.dll` and WinMD to build and publish output. Select the architecture explicitly with `RuntimeIdentifier` (`win-x64` or `win-arm64`) or `PlatformTarget`/`Platform` (`x64` or `ARM64`); ambiguous `AnyCPU` and unsupported platforms fail at build time.

<!-- claim:consumer-architecture
strength: contract-checked
evidence: ArchitectureTest, PackageTest, MachineStateTest, PackedConsumerSmokeTest
-->
Both architectures are package-contract-checked: `ArchitectureTest` verifies the PE shape of each executable/test host, and `PackageTest` verifies that the packed consumer's build/publish output selects the correct native DLL and WinMD for its `RuntimeIdentifier`/`Platform`. Windows x64 selection is additionally executed live — `MachineStateTest` and `PackedConsumerSmokeTest` run against a real packed consumer on a GitHub-hosted Windows x64 runner. ARM64 selection has the same package-contract checks but has not run on ARM64 hardware.

<!-- claim:managed-assembly-shape
strength: contract-checked
evidence: ArchitectureTest, PackageTest, PackedConsumerSmokeTest
-->
The library's managed assembly is IL-only AnyCPU. `ArchitectureTest` and `PackageTest` verify that shape and the packed layout for both architectures. On Windows x64, `PackedConsumerSmokeTest` goes further: it builds, publishes, and runs a real packed consumer against the AnyCPU assembly and observes a non-null WinGet version back, so that package shape is confirmed rather than left open. ARM64 has the same package-contract checks but no hardware execution.

If you consume the repository project through a `ProjectReference` instead of the packed NuGet package, retain a direct `Microsoft.WindowsPackageManager.ComInterop` reference on the executable project. Its build assets do not flow through `ProjectReference`.

### Installing from GitHub Packages

Released versions are published to this repo's public **GitHub Packages** NuGet feed. Add it as a source (once), then install:

```
dotnet nuget add source https://nuget.pkg.github.com/The-Running-Dev/index.json --name github-trd
dotnet add package SubZeroDev.WinGet
```

GitHub requires authentication even for public-feed reads — use a personal access token with the `read:packages` scope as the source's password when prompted.

## Building & Testing

```
dotnet build SubZeroDev.WinGet.sln
dotnet test  SubZeroDev.WinGet.sln                                    # mocked unit tests, no COM
./build.ps1 MachineStateTest                 # 7 local-machine live checks, read-only, needs WinGet
./build.ps1 CatalogIntegrationTest           # 5 remote-catalog live checks, read-only, needs WinGet
./build.ps1 IntegrationTest                  # all 12 live checks
```

The integration tests are `[Explicit]`, read-only by design, and run against the machine's real WinGet catalog.

CI runs the same steps through a generic [Nuke](https://nuke.build) build ([build/Build.cs](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/build/Build.cs)) instead of hand-written `dotnet` CLI steps. Equivalent locally:

```
./build.ps1 Test Coverage ArchitectureTest PackageTest
```

The library, tests, and examples target **.NET 8** (`net8.0-windows10.0.26100`). The Nuke build *tooling* (`build/`) targets **.NET 10** because Nuke.Common 10.x is net10-only — it's isolated from the product and not in the solution. So building via Nuke needs both SDKs (net8 to build/run the product, net10 to run Nuke); a plain `dotnet build`/`dotnet test` needs only the .NET 8 SDK.

See [docs/testing.md](https://winget.subzerodev.com/docs/testing#build-orchestration-with-nuke) for the full target list.

CI: [.github/workflows/build.yml](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/.github/workflows/build.yml) runs on every push to `main` and every pull request. It runs `Test Coverage ArchitectureTest PackageTest` before release: architecture checks verify the managed/executable PE shapes, and package checks build/publish direct and two-hop consumers without live COM activation. Pull requests never publish.

**Publishing** happens only after the build+test job passes:
- **GitHub Packages** — automatic, on two triggers. A **push to `main`** (every merged PR) publishes a distinct **prerelease** `0.1.0-<n>`; pushing a **`v*` tag** (`git push origin v0.1.0`) publishes the **stable** `0.1.0`. The version comes from [GitVersion](https://gitversion.net/) via [GitVersion.yml](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/GitVersion.yml), which derives it from git history rather than the `.csproj`. Auth uses the built-in `GITHUB_TOKEN`, so no secret setup is needed.
- **NuGet.org** — **off by default**; runs only on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret. Publishes the version pinned in the `.csproj`.

## Documentation

**[winget.subzerodev.com](https://winget.subzerodev.com/)** — the hosted docs site (built from `docs/` via Docusaurus, `website/`). The same content is also readable directly on GitHub under [docs/](https://winget.subzerodev.com/docs/); each section below links both.

| Topic | Site | GitHub |
|---|---|---|
| Introduction — why this library, feature overview, the one deliberate CLI exception | [Read](https://winget.subzerodev.com/) | [docs/intro.md](https://winget.subzerodev.com/docs/intro) |
| Getting Started — install, requirements, and explicit architecture configuration | [Read](https://winget.subzerodev.com/docs/getting-started) | [docs/getting-started.md](https://winget.subzerodev.com/docs/getting-started) |
| Managing Packages — search, install, upgrade, uninstall, download, repair, the retry policy | [Read](https://winget.subzerodev.com/docs/usage/packages) | [docs/usage/packages.md](https://winget.subzerodev.com/docs/usage/packages) |
| Managing Sources — the `winget source` equivalent | [Read](https://winget.subzerodev.com/docs/usage/sources) | [docs/usage/sources.md](https://winget.subzerodev.com/docs/usage/sources) |
| Pins, Export & Import — the CLI-backed features | [Read](https://winget.subzerodev.com/docs/usage/pins-export-import) | [docs/usage/pins-export-import.md](https://winget.subzerodev.com/docs/usage/pins-export-import) |
| Running the Examples — a runnable example for every public API | [Read](https://winget.subzerodev.com/docs/examples) | [docs/examples.md](https://winget.subzerodev.com/docs/examples) |
| Architecture — layers, retry policy, verified COM API findings | [Read](https://winget.subzerodev.com/docs/architecture) | [docs/architecture.md](https://winget.subzerodev.com/docs/architecture) |
| Building & Testing — Nuke targets, coverage, publishing | [Read](https://winget.subzerodev.com/docs/testing) | [docs/testing.md](https://winget.subzerodev.com/docs/testing) |
| Documentation System — the containerised `docs-template` image, its gate and deploy | [Read](https://winget.subzerodev.com/docs/documentation-system) | [docs/documentation-system.md](https://winget.subzerodev.com/docs/documentation-system) |
| Troubleshooting — common runtime errors and fixes | [Read](https://winget.subzerodev.com/docs/troubleshooting) | [docs/troubleshooting.md](https://winget.subzerodev.com/docs/troubleshooting) |

## Design Notes

The full design document — including the verified COM API findings (OR'd selectors vs AND'd filters, the CsWinRT collection enumeration bug, activation quirks in elevated hosts) and the research summarized from winget-cli, UniGetUI, and Winget-AutoUpdate — lives in [SPECIFICATION.md](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/SPECIFICATION.md).

## Roadmap

Known gaps and planned work — correctness fixes, threading, packaging, API expansion, and new capabilities — are tracked as phases in [ROADMAP.md](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/ROADMAP.md).

## License

MIT
