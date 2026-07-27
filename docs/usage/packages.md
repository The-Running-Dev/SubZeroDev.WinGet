---
id: packages
title: Managing Packages
sidebar_position: 1
---

# Managing Packages

All examples below use `IPackageManagementService` from DI (see [Getting Started](../getting-started.md)). Every method accepts an optional `CancellationToken`; in-flight installs and downloads honor cancellation.

## Searching

One call searches name, id, moniker, and tag across every configured source. Installed state is already correlated into each result — no second "list installed" call needed:

```csharp
var results = await packages.Search("vscode");

foreach (var p in results)
{
    if (p.IsInstalled && p.IsUpdateAvailable)
        Console.WriteLine($"{p.Id}: {p.InstalledVersion} -> {p.AvailableVersion}");
}
```

Restrict to a single source:

```csharp
var wingetOnly = await packages.Search("vscode", sourceName: "winget");
```

## Listing installed packages

```csharp
var installed = await packages.GetInstalled();
```

:::note Non-WinGet software
Software installed outside WinGet appears with a raw ARP registry key as its `Id` (e.g. `ARP\Machine\X64\Some App 1.0`). Those entries cannot be targeted by id for install/upgrade — check for the `ARP\` prefix before offering an Update button.
:::

## Listing available upgrades

The `winget upgrade` equivalent:

```csharp
var upgrades = await packages.GetAvailableUpgrades();
```

## Getting one package

```csharp
var package = await packages.GetPackage("Microsoft.VisualStudioCode");   // null if not found
var details = await packages.GetDetails("Microsoft.VisualStudioCode");   // full manifest metadata
```

`GetDetails` returns the full catalog manifest: description, publisher URLs, license, copyright, release notes, agreements, documentation links, icons, tags, and every available version — plus `Author`, `PrivacyUrl`, `InstallationNotes`, and `PurchaseUrl` for manifests that populate them.

## Installing

```csharp
using SubZeroDev.WinGet.Models;

var result = await packages.Install("Microsoft.VisualStudioCode", new InstallRequest
{
    Version = null,                                  // null => latest applicable
    Scope = PackageScope.User,                       // Any / User / System
    Mode = PackageOperationMode.Silent,              // Silent / Interactive / Default
    Architecture = PackageArchitecture.Default,      // or X64 / Arm64 / ...
    InstallerType = PackageInstallerKind.Default,    // or force Msi / Msix / Zip / ...
    // PreferredInstallLocation = @"C:\Tools\App",
    // OverrideArguments = "/S",                     // REPLACES installer args (--override)
    // AdditionalArguments = "/NoShortcut",          // appended args (--custom)
    AcceptPackageAgreements = true,
},
progress: new Progress<PackageOperationProgress>(p =>
    Console.WriteLine($"[{p.State}] {p.PercentComplete:F0}%")));

if (!result.Succeeded)
{
    Console.WriteLine($"{result.Status}: {result.ErrorMessage} (0x{result.ExtendedErrorCode:X8})");
}
```

An empty `InstallRequest` (or `null`) reproduces a plain `winget install <id>`.

### Advanced install options

Beyond what's shown above, `InstallRequest` also has:

| Property | Meaning |
|---|---|
| `Force` | Bypass WinGet's applicability checks (hash mismatch, blocking pins, etc.) |
| `AllowHashMismatch` | Proceed even if the installer's hash doesn't match the manifest |
| `SkipDependencies` | Don't install declared dependencies first |
| `LogOutputPath` | Where the underlying installer should write its own log |

`Update` shares this same request type, so all of the above apply to upgrades too.

## Upgrading

```csharp
var result = await packages.Update("Microsoft.VisualStudioCode");
```

`Update` takes the same `InstallRequest` as `Install`.

## Uninstalling

```csharp
var result = await packages.Uninstall("Microsoft.VisualStudioCode",
    new UninstallRequest { Mode = PackageOperationMode.Silent });
```

`UninstallRequest` also has `Scope` (relevant only to MSIX packages), `Force`, `LogOutputPath`, and `CorrelationData`.

## Download-only

Fetch the installer without running it (the `winget download` equivalent):

```csharp
var result = await packages.Download("Microsoft.VisualStudioCode",
    new DownloadRequest(@"C:\Installers"));
```

`DownloadRequest` accepts the same shape as `InstallRequest` (`Version`, `Architecture`, `InstallerType`, `Scope`, `AllowHashMismatch`, `SkipDependencies`, `AcceptPackageAgreements`, `CorrelationData`), plus two download-specific properties:

| Property | Meaning |
|---|---|
| `Locale` | Preferred installer locale, if the manifest offers more than one |
| `SkipMicrosoftStoreLicense` | Skip downloading the license file for Microsoft Store packages |

## Repairing

The `winget repair` equivalent — requires the package's installer technology to support repair:

```csharp
var result = await packages.Repair("Microsoft.VisualStudioCode");
```

`RepairRequest` has the same options as `UninstallRequest` (`Mode`, `Scope`, `Force`, `LogOutputPath`, `CorrelationData`), plus `AllowHashMismatch` and `AcceptPackageAgreements`.

## Reading operation results

Every operation returns a `PackageOperationResult`:

| Property | Meaning |
|---|---|
| `Succeeded` | Overall outcome |
| `Status` | Normalized status enum (`Ok`, `DownloadError`, `InstallError`, `NoApplicableInstallers`, `BlockedByPolicy`, …) |
| `ErrorMessage` | Human-readable failure description |
| `ExtendedErrorCode` | The WinGet HRESULT (`APPINSTALLER_CLI_ERROR_*`); well-known values are exposed on `WinGetErrorCodes` |
| `InstallerErrorCode` | The native installer's own exit code, when the failure came from the installer |
| `RebootRequired` | Whether a restart is needed to finish |

## The built-in retry policy

`IPackageManagementService` applies a small, documented auto-retry policy:

- **Already installed / not newer** (`PACKAGE_ALREADY_INSTALLED`, `INSTALL_ALREADY_INSTALLED`, `INSTALL_DOWNGRADE`, `UPGRADE_VERSION_NOT_NEWER`) → normalized to **success**.
- **No applicable installer under requested constraints** (architecture / installer type / scope) → retried once **without** the constraints.
- **Installed version unknown** during upgrade → retried once with `AllowUpgradeToUnknownVersion`.

If you don't want any of that, use `IWinGetClient` directly — same operations, single attempt, no normalization.

:::note Method names differ at the client layer
`IWinGetClient` isn't just `IPackageManagementService` without the retry policy — several methods have different names: `GetInstalled` → `GetInstalledPackages`, `GetDetails` → `GetPackageDetails`, `Update` → `Upgrade`. Its `Search` also takes a required `int limit` where the service picks a default. See [Architecture](../architecture.md) for the full layer breakdown.
:::
