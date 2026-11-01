# S00 pinned prerequisites

Machine-readable lock: [prerequisites.lock.json](prerequisites.lock.json). Every asset was downloaded once to `D:\Validation\SubZeroDev.FileManager\S00\downloads`, which is outside the repository because the assets are large binaries. Each was admitted only after its hash matched an independent publisher-side value.

Re-verify on the host with [scripts/host/Test-S00StagedAssets.ps1](scripts/host/Test-S00StagedAssets.ps1). The last run reported 7/7 OK; see [evidence/host/host-verifiers.txt](evidence/host/host-verifiers.txt).

Status legend: **[prepared]** staged and hash-verified on the host; **[source]** derived from inspecting the artifact; **[runtime-pending]** needs the guest.

## Assets [prepared]

| Asset | Version | SHA256 | Verified against | Placement |
|---|---|---|---|---|
| .NET SDK win-x64 zip | 10.0.403 | `F398979A…5CC0` | SHA512 in Microsoft `releases.json` | Host only; builds the S01 host (not built in S00) |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.14 | `A3EA61B4…CB1F` | nuget.org catalog SHA512; repository- and author-signed | Host only; self-contained publish without network restore |
| PowerShell win-x64 zip | 7.6.6 (FileVersion 7.6.6.500) | `02FE458B…C860` | Release `hashes.sha256`; Authenticode Valid (Microsoft) | Guest `%ProgramFiles%\SubZeroDev\FileManager\Service\pwsh`. Not MSI, not on PATH |
| PowerShell `hashes.sha256` | 7.6.6 | `388B105A…C9F5` | Same release | Host |
| Microsoft.WinGet.Client nupkg | 1.29.380 | `3469E574…6619` | PSGallery OData SHA512; 45 payload files Authenticode Valid | Guest `…\Service\Modules\Microsoft.WinGet.Client\1.29.380` |
| PuTTY MSI (cross-check copy) | 0.85 | `2213EBAC…2511` | Frozen manifest `InstallerSha256`; signed by Simon Tatham | Host only; WinGet downloads its own copy in S01 |
| 7-Zip EXE (cross-check copy) | 26.01 | `D64A0468…377D` | Frozen manifest `InstallerSha256`; **NotSigned** | Host only; S02 |
| Windows 11 26H2 x64 multi-edition ISO (install Pro, index 6) | 26300.9457 | SHA256 `BD4307DF…A650` | Equals Microsoft's published "English 64-bit" SHA256 (BC-1 baseline) | Host `D:\Downloads\Windows11_Client_x64_en-us_26300_9457.iso`, found attached to the existing VM `Windows11-Test` and detached by `Set-S00VmPreparation.ps1`. **[runtime]** admitted; see [01 § Admitted media](01-environment-inventory.md#admitted-media-runtime) |

Full hashes, byte sizes and URLs are in the lock file. Placement in the guest is done by [scripts/guest/Install-S00Prerequisites.ps1](scripts/guest/Install-S00Prerequisites.ps1) **[prepared, not executed]**. That script:

- re-checks SHA512 and size;
- refuses to overwrite an existing install;
- expands the archives offline;
- drops nupkg metadata;
- sets Service ACLs to SYSTEM/Administrators full and Users read+execute;
- never runs `Repair-WinGetPackageManager`, never installs App Installer, and never contacts PSGallery.

## Module native dependency closure [source]

The module bundles the following under `net8.0-windows10.0.26100.0\SharedDependencies\x64\`:

| File | Bytes | Note |
|---|---|---|
| `WindowsPackageManager.dll` | 7642976 | FileVersion 1.29.380; the in-proc WinGet engine |
| `Microsoft.Management.Deployment.dll` | 53528 | Imports `WindowsPackageManager.dll` |
| `Microsoft.Management.Deployment.winmd` | 64296 | WinRT metadata |
| `winrtact.dll` | 149808 | Activation shim for unpackaged in-proc WinRT |

Static imports (`dumpbin /dependents`, [evidence/host/module-x64-native-dependents.txt](evidence/host/module-x64-native-dependents.txt)) are OS-inbox only:

- UCRT API sets
- icu
- winsqlite3
- msi
- WinINet and WinHTTP
- DWrite, Cabinet, XmlLite and RoMetadata
- core API sets

There is no `vcruntime140*`, no VCLibs and no App Installer import. The per-file hash inventory is in [evidence/host/module-1.29.380-file-inventory.sha256.txt](evidence/host/module-1.29.380-file-inventory.sha256.txt).

Taken together, this satisfies the slice rule of no runtime module download, App Installer repair, user alias or profile dependency, **as a source conclusion**. Runtime loads (LoadLibrary and COM activation) are not visible to dumpbin and remain **[runtime-pending]** until S01 observes the worker under SYSTEM.

## Recorded uncertainties

1. **Two .NET runtimes.** pwsh 7.6.6 embeds .NET 10.0.12, while the S01 host will be self-contained on 10.0.14. They are separate processes, so no in-process conflict is expected. This is recorded rather than substituted, because a different pwsh build would break the pinned hash.
2. **In-proc activation** of `winrtact.dll` from an unmanifested `pwsh.exe` in Session 0. See [04-worker-mapping.md](04-worker-mapping.md) row 8.
3. **App Installer inventory in the guest** is unknown until the guest exists. It is recorded, not required.
4. **Not mixed in:** `Microsoft.WindowsPackageManager.ComInterop` 1.29.280 from `SubZeroDev.WinGet`. Different version, different activation route.
