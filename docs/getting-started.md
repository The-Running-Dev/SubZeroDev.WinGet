---
id: getting-started
title: Getting Started
sidebar_position: 2
---

# Getting Started

## Requirements

- Windows 10/11 with **WinGet (App Installer)** installed
- .NET 8, TFM `net8.0-windows10.0.26100`
- Platform **x64** (ARM64 is declared but not yet validated on hardware) — the WinGet interop assembly is not AnyCPU

## Installation

```shell
dotnet add package SubZeroDev.WinGet
dotnet add package Microsoft.WindowsPackageManager.ComInterop --version 1.29.280
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

:::warning The one integration rule

Any project that **runs** code from this library needs a **direct** `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop` — depending on SubZeroDev.WinGet alone is not enough.

The interop package's build targets copy a required *native* DLL (`Microsoft.Management.Deployment.dll`) only into the output directory of projects that reference it **directly**; it does not flow transitively. Without it, the first COM call fails at runtime with `COMException 0x80040154 (REGDB_E_CLASSNOTREG)`.

```xml
<PackageReference Include="Microsoft.WindowsPackageManager.ComInterop" Version="1.29.280" />
```

:::

Your project must also pin the platform, since the interop assembly is not AnyCPU:

```xml
<PropertyGroup>
  <Platform Condition="'$(Platform)' == '' Or '$(Platform)' == 'AnyCPU'">x64</Platform>
  <Platforms>x64;ARM64</Platforms>
  <PlatformTarget>x64</PlatformTarget>
  <TargetFramework>net8.0-windows10.0.26100</TargetFramework>
</PropertyGroup>
```

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

Operation failures (a failed install, a package not found by an operation) do **not** throw — they return a result record with `Succeeded`, a normalized `Status`, the WinGet `ExtendedErrorCode` HRESULT, and the installer's own exit code when present. See [Managing Packages](usage/packages).
