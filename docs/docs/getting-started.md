---
id: getting-started
title: Getting Started
sidebar_position: 3
---

# Getting Started

## Requirements

- Windows 10/11 with **WinGet (App Installer)** installed
- .NET 8 or newer — the package targets `net8.0-windows10.0.26100`, so net8/net9/net10 apps can all consume it
- An explicit **x64** or **ARM64** consumer architecture. See the [README's architecture configuration](https://github.com/The-Running-Dev/SubZeroDev.WinGet#architecture-configuration) for what each is checked against.
- WinGet 1.12 or newer if your code calls `GetWinGetVersion` — see [below](#runtime-version-floor).

## Installation

```shell
dotnet add package SubZeroDev.WinGet
```

### Installing from GitHub Packages

Released versions are published to this repository's public **GitHub Packages** NuGet feed. Register the source once, then install:

```shell
dotnet nuget add source https://nuget.pkg.github.com/The-Running-Dev/index.json --name github-trd
dotnet add package SubZeroDev.WinGet
```

:::note
GitHub requires authentication even to read a public packages feed. Supply a personal access token with the `read:packages` scope as the source password (e.g. `--username <you> --password <token>` on `dotnet nuget add source`, or in your `nuget.config`).
:::

The package provides its own transitive build target. For an explicitly supported consumer it selects and copies the matching x64/ARM64 native DLL plus WinMD into build and publish output; no direct ComInterop reference is needed. Set either a supported runtime identifier or platform:

```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PlatformTarget>x64</PlatformTarget>
  <TargetFramework>net8.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

Use `win-arm64` and `ARM64` for an ARM64 application. Do not leave the architecture as `AnyCPU`: the package fails that ambiguous configuration rather than selecting a mismatched native DLL. See the [README's architecture configuration](https://github.com/The-Running-Dev/SubZeroDev.WinGet#architecture-configuration) for what's checked, and by which runs, for the managed AnyCPU assembly and each supported architecture.

:::note Repository project references

When an executable uses `ProjectReference` to the repository's `SubZeroDev.WinGet` project instead of the packed package, add a direct `Microsoft.WindowsPackageManager.ComInterop` reference to that executable. NuGet `buildTransitive` assets are package behavior and do not participate in a project-reference build.

:::

## Registering with dependency injection

`AddPackageManagement()` registers the full surface as singletons:

```csharp
using Microsoft.Extensions.DependencyInjection;
using SubZeroDev.WinGet;

var services = new ServiceCollection()
    .AddLogging()
    .AddPackageManagement()
    .BuildServiceProvider();
```

This registers:

| Interface | Role |
|---|---|
| `IPackageManagementService` | The recommended entry point: validation, structured logging, and a documented auto-retry policy on top of the client |
| `IPackageSourceService` | Source management with validation/logging |
| `IWinGetClient` | Lower-level COM client — single attempt, no retry policy |
| `IWinGetSourceClient` | Lower-level COM source client |
| `IWinGetCliClient` | The isolated winget.exe shim (pins, export/import only) |

## First call

```csharp
using SubZeroDev.WinGet.Abstractions;

var packages = services.GetRequiredService<IPackageManagementService>();

var version = await packages.GetWinGetVersion();
Console.WriteLine($"WinGet {version}");

var results = await packages.Search("vscode");

foreach (var package in results)
{
    Console.WriteLine($"{package.Id} — {package.Name} ({package.AvailableVersion})");
}
```

## Runtime version floor

<!-- claim:runtime-version-floor
strength: executed
evidence: PackedConsumerSmokeTest, Test
-->
`GetWinGetVersion` requires a WinGet runtime exposing COM contract 13, first shipped in WinGet 1.12; below that it returns `null` instead of failing. `Test` covers the classifier that converts exactly `InvalidCastException` with HRESULT `0x80004002` to `null` and nothing else. `PackedConsumerSmokeTest` observed a non-null version (`1.29.280`) from a real packed consumer on a GitHub-hosted Windows x64 runner after installing that pinned build.

This floor is scoped to this one member, not a library-wide minimum: earlier live runs against WinGet `1.11.510` — below the floor — passed the rest of the twelve-test live suite. See [Architecture → Hosting caveats](architecture.md#hosting-caveats), [Building & Testing → Live integration tests](testing.md#live-integration-tests), and [Troubleshooting](troubleshooting.md) for what remains unvalidated elsewhere.

## Error handling

If WinGet is missing, too old, or COM activation is blocked in your process context, any call throws the typed **`WinGetUnavailableException`** with an actionable message — a raw `COMException` never escapes for that case:

```csharp
try
{
    var installed = await packages.GetInstalled();
}
catch (WinGetUnavailableException ex)
{
    Console.WriteLine($"WinGet is unavailable here: {ex.Message}");
}
```

Operation failures (a failed install, a package not found by an operation) do **not** throw — they return a result record with `Succeeded`, a normalized `Status`, the WinGet `ExtendedErrorCode` HRESULT, and the installer's own exit code when present. See [Managing Packages](usage/packages.md).
