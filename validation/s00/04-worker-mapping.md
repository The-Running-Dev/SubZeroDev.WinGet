# S00 worker mapping

This file fixes how the FM-37 worker maps each typed operation onto Microsoft.WinGet.Client 1.29.380. The worker is the private pwsh 7.6.6 hosted by the LocalSystem .NET service. It has no free-form arguments, does not call the WinGet CLI under SYSTEM, and installs nothing per-user.

All citations point to `microsoft/winget-cli` tag `v1.29.380`, commit `000f6b55151cb0f1afd2933bb54c62a4724b9ca8`.

**Status labels**

| Label | Meaning |
|---|---|
| **SUPPORTED** | The source shows the behaviour is reachable through the module. |
| **PARTIAL** | Reachable with gaps; the compensating controls are listed. |
| **UNSUPPORTED** | Not reachable through the module. |
| **UNCERTAIN** | Can only be settled at runtime. |

Every row below is a **[source]** conclusion. None of them has run in the guest. Nothing here proves a signed-out installation, and Gate A remains open.

## Fixed call shapes

The service starts the worker as the following command. It uses absolute paths and passes no user-supplied text:

```
"%ProgramFiles%\SubZeroDev\FileManager\Service\pwsh\pwsh.exe" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File <service-owned worker script>
```

The worker imports the module only by its absolute psd1 path:

```
%ProgramFiles%\SubZeroDev\FileManager\Service\Modules\Microsoft.WinGet.Client\1.29.380\Microsoft.WinGet.Client.psd1
```

It never relies on PSModulePath discovery, never runs `Repair-WinGetPackageManager`, and never uses `Install-Module`.

| Operation | Call (all values come from the profile, never from the request) | Profiles |
|---|---|---|
| Resolve | `Find-WinGetPackage -Id <Id> -Source winget -MatchOption Equals` → exactly one result with `.Id -ceq <Id>`, and `<Version>` in `.AvailableVersions` | S01, S02 |
| Install (S01) | `Install-WinGetPackage -Id PuTTY.PuTTY -Source winget -Version 0.85.0.0 -MatchOption Equals -Scope System -Architecture X64 -InstallerType Wix -Mode Silent -SkipDependencies` | S01 |
| Install (S02) | `Install-WinGetPackage -Id 7zip.7zip -Source winget -Version 26.01 -MatchOption Equals -Scope System -Architecture X64 -InstallerType Exe -Mode Silent -SkipDependencies` | S02 |
| Observe | `Get-WinGetPackage -Id <Id> -Source winget -MatchOption Equals`. Recorded only; it never decides the outcome (see [03-package-profiles.md](03-package-profiles.md)). | S01, S02 |

The following parameters are **never** passed:

- `-Override`: it replaces the manifest switches.
- `-Custom`: it appends to them.
- `-Log`
- `-Location`
- `-Force`
- `-AllowHashMismatch`
- `-Header`
- `-Locale`
- `-Query`
- `-Moniker`
- `-Name`

## Mapping

| # | Concern | Status | How it is enforced / what the module does | Citation |
|---|---|---|---|---|
| 1 | Source selection | SUPPORTED | `-Source winget` on Find, Get, Install, Update, Uninstall and Repair. The brief claimed the module "lacks a source selector"; that claim is **refuted**. | `src/PowerShell/Microsoft.WinGet.Client.Cmdlets/Cmdlets/Common/FinderCmdlet.cs` (Source parameter) |
| 2 | Exact identifier match | SUPPORTED | `Find-*` defaults to `ContainsCaseInsensitive`, so `-MatchOption Equals` is always passed. Install/Get default to `EqualsCaseInsensitive`. The worker also compares `.Id` ordinally. | `FinderCmdlet.cs:67`; `src/PowerShell/Microsoft.WinGet.Client.Cmdlets/Cmdlets/Common/PackageCmdlet.cs:25` |
| 3 | Exact version | SUPPORTED | `-Version` selects that manifest version. A missing version fails; it never falls back to the latest. | `InstallerPackageCommand.cs` → `GetInstallOptions(version, …)` |
| 4 | Installer type | SUPPORTED | `-InstallerType` is a strict requirement in installer selection, with no fallback to another type. This is the only field that rejects the 7-Zip x64 MSI. | `src/PowerShell/Microsoft.WinGet.Client.Cmdlets/Cmdlets/InstallerSelectionCmdlet.cs`; `src/AppInstallerCommonCore/Manifest/ManifestComparator.cpp:206-330, 730-800`; `src/Microsoft.Management.Deployment/Converters.cpp:245-262` |
| 5 | Machine scope | SUPPORTED | `-Scope System` maps to a required machine scope filter, so a user-scope installer is rejected instead of being chosen. | same as #4 |
| 6 | Architecture | SUPPORTED | `-Architecture X64` is a required architecture filter. | same as #4 |
| 7 | Silent execution | SUPPORTED | `-Mode Silent` sets `PackageInstallMode.Silent`, which uses the manifest's `Silent` switch (for 7-Zip that is `/S`). For wix/msi the install runs in-process through `MsiInstallProductW` with UI level none, not `msiexec`. | `src/PowerShell/Microsoft.WinGet.Client.Engine/Commands/Common/InstallCommand.cs:83`; `src/AppInstallerCLICore/Workflows/InstallFlow.cpp:62, 435` |
| 8 | In-proc under SYSTEM | SUPPORTED (source) / UNCERTAIN (runtime) | `UsesInProcWinget => ExecutingAsSystem`, so under LocalSystem the module uses the bundled `WindowsPackageManager.dll` instead of App Installer COM. Windows PowerShell 5.1 is refused, so the worker must be pwsh 7. Whether winrtact activation works under an unmanifested `pwsh.exe` in Session 0 is runtime-only. | `src/PowerShell/Microsoft.WinGet.Client.Engine/Common/Utilities.cs:54-58`; `src/PowerShell/Microsoft.WinGet.Client.Engine/Commands/Common/ManagementDeploymentCommand.cs:38` |
| 9 | Native dependency closure | SUPPORTED (static) / UNCERTAIN (dynamic) | The module ships `SharedDependencies\x64` with `WindowsPackageManager.dll`, `Microsoft.Management.Deployment.dll/.winmd` and `winrtact.dll`. Their static imports are inbox only (UCRT API sets, msi, winhttp, winsqlite3, icu, and so on), with no VCLibs or App Installer. Dynamic loads remain runtime-pending. | [prerequisites.lock.json](prerequisites.lock.json) `nativeClosure`; [evidence/host/module-x64-native-dependents.txt](evidence/host/module-x64-native-dependents.txt) |
| 10 | Package agreements | PARTIAL | `InstallOptions.AcceptPackageAgreements` defaults to **true**, and the module never sets it. The module therefore accepts any package agreement silently. The service must gate agreements itself before dispatch (FM-26): the profile carries an operator-approved agreement record bound to the manifest blobs, and the drift check (#16) refuses a resolved manifest whose `Agreements` differ. Both profiles declare none. | `src/Microsoft.Management.Deployment/InstallOptions.h:69` |
| 11 | Source agreements | PARTIAL / UNCERTAIN | `m_acceptSourceAgreements` defaults to true for the COM path as well, so the module would accept them silently. They are therefore captured as SYSTEM during guest preparation, before the checkpoint ([Get-S00SourceAgreements.ps1](scripts/guest/Get-S00SourceAgreements.ps1), [05 § 1](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint) step 7), and written to a pending approval record ([agreements/source-agreements.approval.json](agreements/source-agreements.approval.json)). No installation may run until the operator approves that record, including when it records "none presented". At run time the worker refuses to install if the source presents an agreement set whose digest differs from the approved one. What SYSTEM actually sees is runtime-pending. | `InstallOptions.h` / FindPackagesOptions defaults |
| 12 | Dependencies | SUPPORTED | `-SkipDependencies` sets `InstallOptions.SkipDependencies`, and the dependencies workflow then does not install dependencies. Admission is enforced by the profile: a resolved installer that declares dependencies is a mismatch. | `InstallCommand.cs:81`; `src/AppInstallerCLICore/Workflows/DependenciesFlow.cpp:246` |
| 13 | Native result | PARTIAL | `PSInstallResult` exposes `Status`, `InstallerErrorCode` (uint), `ExtendedErrorCode` (Exception) and `CorrelationData`. `InstallerErrorCode` is filled from `Data::OperationReturnCode` whenever the context holds it, so it carries the raw MSI/EXE exit code. | `src/PowerShell/Microsoft.WinGet.Client.Engine/PSObjects/PSInstallResult.cs:79-112`; `src/Microsoft.Management.Deployment/PackageManager.cpp:1089-1097` |
| 14 | Reboot detection | `RebootRequired` **UNSUPPORTED**; detection **SUPPORTED** through the return-code map | `PSInstallResult.RebootRequired` is always `false`: the COM layer hard-codes it (`// TODO - RebootRequired not yet populated`), so it is never read. Reboot detection is the worker's own interpretation of the raw `InstallerErrorCode` (#13) against the **applicable manifest return-code map** for the admitted installer, built the same way WinGet builds it (see [Reboot detection](#reboot-detection)). For example, under the WiX defaults 3010 is `RebootRequiredToFinish`, which WinGet turns into **S_OK** with an internal flag that never reaches COM, so `Status Ok` + `InstallerErrorCode 3010` means reboot required. OS pending-reboot markers are supplementary evidence only. | `PackageManager.cpp:1102-1103`; `src/AppInstallerCLICore/Workflows/InstallFlow.cpp:493-530`; `src/AppInstallerCommonCore/Manifest/ManifestCommon.cpp:1088-1145`; `src/AppInstallerCommonCore/Manifest/ManifestYamlPopulator.cpp:1352-1361` |
| 15 | No automatic reboot | SUPPORTED | The module has no `--allow-reboot` equivalent; only the CLI honours it. A 1641 from an installer would still be recorded. | `InstallFlow.cpp` (`AllowReboot` arg is CLI-only) |
| 16 | Installer URL / SHA256 drift | PARTIAL | `PackageInstallerInfo` exposes type, nested type, architecture, scope, locale, elevation and authentication, but **not** the URL or SHA256. The module cannot report which URL or hash it will use. Compensating controls are listed below. | `PSObjects` installer info; see [Drift detection](#drift-detection) |
| 17 | Hash enforcement | SUPPORTED (payload vs resolved manifest only) | WinGet verifies the downloaded installer against the `InstallerSha256` of the manifest **it resolved**, and fails on mismatch unless `AllowHashMismatch` is set, which is never passed. This does not show that the resolved manifest is the frozen one (see [Drift detection](#drift-detection)). | `InstallCommand.cs:80` |
| 18 | Cancellation | PARTIAL | `StopProcessing` → CancellationToken → WinRT `IAsyncOperation.Cancel` → `ContextOrchestrator::CancelQueueItem`. **Cancelling the worker does not stop the installer.** **EXE:** the wait loop exits and returns `{}`, so the installer process is **abandoned, not killed**, and may keep running and changing the machine after the worker reports cancellation. **MSI:** `MsiInstallProductW` is synchronous and cannot be interrupted, so cancellation takes effect only after it returns. Killing the worker process does not kill a ShellExecute'd installer either. A cancelled case therefore has no defined machine end state. Carried to S02 for validation (S02-V1 in [06](06-acceptance-checklist.md#carried-forward-to-s02-not-s00-work)); S00 and S01 do not exercise cancellation. | `src/AppInstallerCLICore/Workflows/ShellExecuteInstallerHandler.cpp:43-58`; `src/AppInstallerCLICore/ContextOrchestrator.cpp` |
| 19 | Experimental features / resume | SUPPORTED | Policy `EnableExperimentalFeatures=0` disables experimental features, including resume. This is set by [scripts/guest/Set-S00PolicyPins.ps1](scripts/guest/Set-S00PolicyPins.ps1). | `src/AppInstallerSharedLib/GroupPolicy.cpp`; `src/AppInstallerCommonCore/ExperimentalFeature.cpp:46-53` |
| 20 | Settings and logs location | PARTIAL / UNCERTAIN | Under SYSTEM, user settings resolve under the systemprofile `LOCALAPPDATA\Microsoft\WinGet\Settings\defaultState`. COM logs are presumed to be `WinGetCOM-*.log` in `%windir%\Temp\WinGet\defaultState`. Both paths are collected in S01; the exact path is runtime-pending. | runtime-pending |
| 21 | App Installer / execution alias / profile | Not used | The in-proc route needs no App Installer, user alias or user profile. The guest inventory records whether App Installer is present but does not require it. | #8 |

## Reboot detection

Decided by review (former Q-1): the reboot outcome is determined by interpreting the installer's return code with the applicable manifest mapping. Windows pending-reboot markers are supplementary evidence. The module's `RebootRequired` is never read (#14).

### Building the applicable map **[source]**

The worker builds the map for the admitted installer exactly as WinGet does, from the **frozen** manifest bound to the profile:

1. **`InstallerSuccessCodes`** declared by the manifest. Each one, and `0`, is success with no reboot.
2. **`ExpectedReturnCodes`** declared by the manifest, including **custom codes**. FM-39 fixtures declare their custom codes in their own frozen fixture manifests, and those entries are used as declared. A code mapped to `custom` is a failure carrying its `ReturnResponseUrl`.
3. **Installer-type defaults** (`GetDefaultKnownReturnCodes`) for codes that steps 1-2 do not already declare. A manifest declaration always wins over a default (`ManifestYamlPopulator.cpp:1352-1361`).
4. Any other non-zero code is an **unmapped failure**. The worker never guesses a reboot meaning for it.

Defaults by installer type in v1.29.380 (`ManifestCommon.cpp:1088-1145`):

| Installer type | Default map |
|---|---|
| `msi`, `wix`, `burn` | 1618 InstallInProgress; 112 DiskFull; 1601 ContactSupport; **3010 RebootRequiredToFinish**; **1641 RebootInitiated**; 1602 CancelledByUser; 1638 AlreadyInstalled; 1654, 1623, 1633 SystemNotSupported; 1625, 1644, 1643, 1649, 1640 BlockedByPolicy; 87, 1628, 1639, 1650 InvalidParameter |
| `inno` | 2, 5 CancelledByUser; **8 RebootRequiredForInstall** |
| `msix` | HRESULT set (MissingDependency, DiskFull, AlreadyInstalled, BlockedByPolicy, ...); no reboot codes |
| `exe`, `nullsoft`, `portable`, `zip` | **none** |

### Reboot meaning of each response **[source]**

How WinGet itself treats each response (`InstallFlow.cpp:493-530`), and the reboot outcome the worker records:

| Response | WinGet result | Worker reboot outcome |
|---|---|---|
| `0` or an `InstallerSuccessCodes` entry | Success | Not required |
| `RebootRequiredToFinish` | **Success** (S_OK); internal `RebootRequired` flag set but not exposed through COM | **Required to finish** |
| `RebootRequiredForInstall` | Failure; `RebootRequired` + `RegisterResume` flags set | **Required before install can complete** |
| `RebootInitiated` | Failure (the installer started a restart) | **Initiated by installer** |
| Any other mapped response, custom fixture code or unmapped code | Failure | Not established by the code; supplementary markers recorded |

The worker records the raw code, the map entry that matched (or `unmapped`), which step of the map it came from, and WinGet's `Status`. If WinGet's `Status` disagrees with the class implied by the frozen map (for example `Ok` for a code the frozen map calls a failure), the resolved manifest is not the frozen one, and the case is invalid (see [Drift detection](#drift-detection)).

### Applicable maps for the S00 profiles **[source]**

Neither frozen installer manifest declares `InstallerSuccessCodes` or `ExpectedReturnCodes` (checked in the blobs listed in [03](03-package-profiles.md)).

| Profile | Applicable map | Expected outcome |
|---|---|---|
| S01 PuTTY 0.85.0.0, `wix` | WiX defaults only. 3010 → reboot required to finish; 1641 → reboot initiated | `0`, reboot not required. The MSI has no `ScheduleReboot` or `ForceReboot` |
| S02 7-Zip 26.01, `exe` | Empty. Only `0` is success; every non-zero code is an unmapped failure, with no reboot meaning | `0`, reboot not required. Not inspectable statically (NSIS) |
| FM-39 fixtures | Each fixture's own frozen manifest declarations, then its type defaults | Declared per fixture. Fixtures are not built in S00 |

Machine-readable copies: `returnCodes` in [putty.s01.profile.json](profiles/putty.s01.profile.json) and [7zip.s02.profile.json](profiles/7zip.s02.profile.json). These expectations are **[source]**, not observed.

### Supplementary evidence (never decides the outcome alone)

Recorded before and after the case:

1. Windows pending-reboot markers:
   - `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending`
   - `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired`
   - `HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations`
2. `LastBootUpTime` and System events 1074/6005/6006/6008, to show whether a restart happened during the case.

A new marker with a "not required" code is recorded as a discrepancy for review, not as a reboot outcome. Markers can be set by servicing that is unrelated to the installer, and an installer can need a restart without setting one.

## Drift detection

The source is the live CDN index, not the frozen commit. The following controls work around the missing URL and hash (#16):

1. **Host pre-arm check.** Before arming, the operator runs [Test-S00FrozenManifests.ps1](scripts/host/Test-S00FrozenManifests.ps1) `-Live`, which lists the pinned version's directory on the current `winget-pkgs` default branch and requires exactly the frozen file set with the frozen blob ids. Any added, missing or changed file blocks arming. A host-only run on 2026-10-28 matched 8/8 with no extra files ([evidence/host/manifest-live-drift.txt](evidence/host/manifest-live-drift.txt)); that is not an S01 pre-arm run.
2. **In-worker resolve check.** `Find-WinGetPackage` must return exactly one package with that Id and version, and its installer info must match type, architecture and scope.
3. **WinGet hash enforcement.** Hash checking stays on (#17). This proves only that the payload matches the `InstallerSha256` of the manifest **WinGet resolved from the CDN index**. It does **not** prove that manifest is our frozen manifest: a CDN manifest with a different URL and a matching new hash would pass WinGet's check. Item 3 guards payload integrity, not manifest identity.
4. **Return-code consistency.** WinGet's `Status` must agree with the class the frozen map assigns to the raw code ([Reboot detection](#reboot-detection)). A disagreement shows the resolved map differs from the frozen one. Agreement does not prove identity.
5. **Post-install independent verification.** This uses MSI registration or ARP plus the exe version ([Test-S00PuTTY.ps1](scripts/guest/Test-S00PuTTY.ps1), [Test-S00SevenZip.ps1](scripts/guest/Test-S00SevenZip.ps1)). For PuTTY the ProductCode, version and Authenticode signer tie the installed product to the frozen payload. For 7-Zip (unsigned) only the version and registration are checked.

What the `-Live` check covers: it compares the frozen blobs with **GitHub `winget-pkgs`** only. It does not read the CDN index that WinGet uses.

Remaining gap: nothing available through the module proves that the manifest WinGet resolved at install time was byte-identical to the frozen manifest, because the module exposes neither the URL nor the hash (#16). Items 2, 4 and 5 detect some divergences after the fact. A divergence that keeps the type, architecture, scope, return-code behaviour and installed product the same, but changes the URL and hash, is not detected. This gap is recorded, not closed. Reading the pre-indexed source's own package data under SYSTEM before dispatch could narrow it, and is runtime-pending (S01 question 6).

## Runtime-only questions (S01 must observe; none observed in S00)

1. Does `Import-Module` plus the in-proc factory work under LocalSystem in Session 0 with no user signed in? This covers winrtact activation, the MTA thread and whether any App Installer path is touched. Partly observed in S00 provisioning attempt 5 (2026-10-28): as SYSTEM in Session 0, the module imported and `Get-WinGetSource -Name winget` returned the source from an STA pwsh, because the cmdlets move their work to an MTA thread; a direct `PackageManagerWrapper` call from the same STA thread threw `SingleThreadedApartmentException`. A worker host that calls the engine directly must start pwsh with `-MTA`.
2. Does the `winget` source update and open under SYSTEM with no network proxy prompt? Which source agreements it presents is captured during preparation (#11), not in the case.
3. What `Status`, `InstallerErrorCode` and `ExtendedErrorCode` does a successful PuTTY install actually produce?
4. Where exactly are the settings and log files for SYSTEM?
5. **.NET runtimes (accepted by review, subject to runtime validation).** pwsh 7.6.6 embeds .NET 10.0.12, and the S01 service is self-contained on 10.0.14. They are pinned separately and run in separate processes. Validation: the guest inventory records the runtime pwsh actually loads ([Get-S00Inventory.ps1](scripts/guest/Get-S00Inventory.ps1) `worker.PwshRuntime`, expected `10.0.12`), and S01 records the service's `Environment.Version` (expected `10.0.14`) and that the worker started and imported the module. Any other runtime version, or a failure to start either process, is a deviation.
6. Can the pre-indexed source's package data for the pinned version be read under SYSTEM before dispatch, to narrow the drift gap?

## Platform and WinGet inspection (no infrastructure duplicated)

| Repository | What exists | Reused? |
|---|---|---|
| `SubZeroDev.WinGet` @ `c27d7f8` | A C# wrapper using `Microsoft.WindowsPackageManager.ComInterop` 1.29.**280**. Activation tries Projection, then LocalServer, then LocalServerLowerTrust (out-of-proc App Installer COM; its own error text warns the server "may not be registered for this context" for services). It also has a CLI client. | **No.** It uses the wrong route for SYSTEM in-proc (FM-37), and its interop version differs from the module's 1.29.380 native set. The slice forbids mixing native DLL sets. |
| `SubZeroDev.Platform` @ `5b587d6` | No Windows-service host (`UseWindowsService`, `ServiceBase`) and no WinGet code. | Nothing to reuse for S00. The S01 host is not built in S00. |

Neither repository was modified.
