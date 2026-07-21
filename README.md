# SubZeroDev.PackageManagement

[![Build](https://github.com/The-Running-Dev/SubZeroDev.PackageManagement/actions/workflows/build.yml/badge.svg)](https://github.com/The-Running-Dev/SubZeroDev.PackageManagement/actions/workflows/build.yml)

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
using SubZeroDev.PackageManagement;
using SubZeroDev.PackageManagement.Abstractions;
using SubZeroDev.PackageManagement.Models;

var services = new ServiceCollection()
    .AddLogging()
    .AddPackageManagement()
    .BuildServiceProvider();

var packages = services.GetRequiredService<IPackageManagementService>();

// Search across all configured sources (installed state included in results)
var results = await packages.SearchAsync("vscode");

// Install with options
var result = await packages.InstallAsync("Microsoft.VisualStudioCode", new InstallRequest
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
var upgrades = await packages.GetAvailableUpgradesAsync();

// Sources
var sources = services.GetRequiredService<IPackageSourceService>();
var configured = await sources.GetSourcesAsync();
```

Prefer raw, single-attempt behavior without the service layer's retry policy? Use `IWinGetClient` / `IWinGetSourceClient` / `IWinGetCliClient` directly — they are registered by `AddPackageManagement()` too.

## Requirements

- Windows 10/11 with **WinGet (App Installer)** installed
- .NET 8, TFM `net8.0-windows10.0.26100`
- Platform **x64** (or ARM64 — declared, not yet validated); the WinGet interop assembly is not AnyCPU

### ⚠️ The one integration rule

Any project that **runs** code from this library needs a **direct** `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop` — a `ProjectReference`/package dependency alone is not enough. The interop package's build targets copy a required native DLL (`Microsoft.Management.Deployment.dll`) only into the directly-referencing project's output directory. Without it you get `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` at runtime.

```xml
<PackageReference Include="Microsoft.WindowsPackageManager.ComInterop" Version="1.29.280" />
```

## Building & Testing

```
dotnet build SubZeroDev.PackageManagement.sln
dotnet test  SubZeroDev.PackageManagement.sln                                    # 44 mocked unit tests, no COM
dotnet test  SubZeroDev.PackageManagement.sln --filter "FullyQualifiedName~IntegrationTests"  # 12 live, read-only, needs WinGet
```

The integration tests are `[Explicit]`, read-only by design, and run against the machine's real WinGet catalog.

CI: [.github/workflows/build.yml](.github/workflows/build.yml) builds, tests, and packs on every push/PR — a failing test stops the build before packaging, and a code-coverage summary is rendered on each run's summary page. Publishing to NuGet.org is **off by default** — it only runs on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret.

## Design Notes

The full design document — including the verified COM API findings (OR'd selectors vs AND'd filters, the CsWinRT collection enumeration bug, activation quirks in elevated hosts) and the research summarized from winget-cli, UniGetUI, and Winget-AutoUpdate — lives in [SPECIFICATION.md](SPECIFICATION.md).

## License

MIT
