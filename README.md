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

[SubZeroDev.WinGet.Examples](SubZeroDev.WinGet.Examples) has a runnable example for **every** public API:

```
cd SubZeroDev.WinGet.Examples
dotnet run                       # lists all examples
dotnet run -- search terminal    # read-only examples run live
dotnet run -- install <id>       # mutating examples require explicit arguments
```

Read-only examples (search, installed, upgrades, details, sources, pins, export, version) run safely against your machine; anything that would change it (install, uninstall, pin, source add/remove, import) refuses to run without explicit arguments.

## Requirements

- Windows 10/11 with **WinGet (App Installer)** installed
- .NET 8, TFM `net8.0-windows10.0.26100`
- Platform **x64** (or ARM64 — declared, not yet validated); the WinGet interop assembly is not AnyCPU

### ⚠️ The one integration rule

Any project that **runs** code from this library needs a **direct** `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop` — a `ProjectReference`/package dependency alone is not enough. The interop package's build targets copy a required native DLL (`Microsoft.Management.Deployment.dll`) only into the directly-referencing project's output directory. Without it you get `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` at runtime.

```xml
<PackageReference Include="Microsoft.WindowsPackageManager.ComInterop" Version="1.29.280" />
```

### Installing from GitHub Packages

Released versions are published to this repo's public **GitHub Packages** NuGet feed. Add it as a source (once), then install:

```
dotnet nuget add source https://nuget.pkg.github.com/The-Running-Dev/index.json --name github-trd
dotnet add package SubZeroDev.WinGet
```

GitHub requires authentication even for public-feed reads — use a personal access token with the `read:packages` scope as the source's password when prompted. (You still need the direct `Microsoft.WindowsPackageManager.ComInterop` reference described above.)

## Building & Testing

```
dotnet build SubZeroDev.WinGet.sln
dotnet test  SubZeroDev.WinGet.sln                                    # 100 mocked unit tests, no COM
dotnet test  SubZeroDev.WinGet.sln --filter "FullyQualifiedName~IntegrationTests"  # 12 live, read-only, needs WinGet
```

The integration tests are `[Explicit]`, read-only by design, and run against the machine's real WinGet catalog.

CI: [.github/workflows/build.yml](.github/workflows/build.yml) builds, tests, and packs on every push/PR — a failing test stops the build before packaging, and a code-coverage summary is rendered on each run's summary page.

**Publishing:**
- **GitHub Packages** — automatic when a **GitHub Release** is published. The build+test job must pass first; the package version is derived from the release tag by [GitVersion](https://gitversion.net/) (installed as a .NET tool). Uses the built-in `GITHUB_TOKEN`, so no secret setup is needed.
- **NuGet.org** — **off by default**; runs only on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret. Publishes the version pinned in the `.csproj`.

## Documentation

Full documentation lives in [docs/](docs) as Docusaurus-ready Markdown: [introduction](docs/intro.md), [getting started](docs/getting-started.md), usage guides for [packages](docs/usage/packages.md), [sources](docs/usage/sources.md), and [pins/export/import](docs/usage/pins-export-import.md), the [examples guide](docs/examples.md), [building & testing](docs/testing.md), [architecture](docs/architecture.md), and [troubleshooting](docs/troubleshooting.md).

## Design Notes

The full design document — including the verified COM API findings (OR'd selectors vs AND'd filters, the CsWinRT collection enumeration bug, activation quirks in elevated hosts) and the research summarized from winget-cli, UniGetUI, and Winget-AutoUpdate — lives in [SPECIFICATION.md](SPECIFICATION.md).

## License

MIT
