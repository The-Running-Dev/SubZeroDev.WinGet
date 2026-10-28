# S00 package profiles

Validation profiles only, not the production catalog (FM-25). Each profile binds source + package + exact version + installer type + architecture + scope + mode. Anything else the source returns is a mismatch and blocks dispatch. Machine-readable copies: [profiles/putty.s01.profile.json](profiles/putty.s01.profile.json), [profiles/7zip.s02.profile.json](profiles/7zip.s02.profile.json).

Status legend: **[prepared]** artifact exists in this repo; **[source]** conclusion from inspecting manifests/installer metadata; **[runtime-pending]** needs the guest.

## Source identity

| Field | Value | Status |
|---|---|---|
| Source name | `winget` | [source] |
| Type | `Microsoft.PreIndexed.Package` | [source] (winget-cli default source definition) |
| Argument | `https://cdn.winget.microsoft.com/cache` | [source] |
| Identifier | `Microsoft.Winget.Source_8wekyb3d8bbwe` | [source] |
| Frozen manifest origin | `microsoft/winget-pkgs` commit `aaaf44e17204afb742dcf006ce6ee8c2263bd59e` | [prepared] |
| Effective source and source agreements as seen by SYSTEM in the guest | not captured | [prepared] [Get-S00SourceAgreements.ps1](scripts/guest/Get-S00SourceAgreements.ps1), run during guest preparation ([05 § 1](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint) step 7) into [source-agreements.approval.json](agreements/source-agreements.approval.json); [runtime-pending] |

The pre-indexed source serves manifests from its CDN index, not from the git commit. The frozen manifests are the **expected** content. Before dispatch, S01 must compare what the module exposes about the resolved package (package ID, version, installer type, scope, architecture, ProductCode) with the frozen profile and refuse to dispatch on any difference. The module does not expose the resolved installer URL or SHA256. WinGet's own hash check proves that the payload matches the manifest WinGet resolved, **not** that the resolved manifest is the frozen one. A moving source cannot be "frozen" by this repo; drift is detected with the limits set out in [04 § Drift detection](04-worker-mapping.md#drift-detection), not prevented.

Frozen files, byte-exact with upstream blobs (`git hash-object --no-filters`; `.gitattributes` keeps them `-text`):

| File | Blob |
|---|---|
| [PuTTY.PuTTY.installer.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/p/PuTTY/PuTTY/0.85.0.0/PuTTY.PuTTY.installer.yaml) | `e443bd2f53927d00031936ff6c68dcfc855bc829` |
| [PuTTY.PuTTY.locale.en-US.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/p/PuTTY/PuTTY/0.85.0.0/PuTTY.PuTTY.locale.en-US.yaml) | `f8d8f59b59e257803535ebe8b1defb13ada5f7b4` |
| [PuTTY.PuTTY.locale.zh-CN.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/p/PuTTY/PuTTY/0.85.0.0/PuTTY.PuTTY.locale.zh-CN.yaml) | `960764f8f5e334aaf67ab29246410400828ad6a7` |
| [PuTTY.PuTTY.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/p/PuTTY/PuTTY/0.85.0.0/PuTTY.PuTTY.yaml) | `a5a9aa7ec1ba672d6b534eb89b6fca53531bcd26` |
| [7zip.7zip.installer.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/7/7zip/7zip/26.01/7zip.7zip.installer.yaml) | `681b2f73d42485dbec826280c6b3fe2516bc0ac6` |
| [7zip.7zip.locale.en-US.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/7/7zip/7zip/26.01/7zip.7zip.locale.en-US.yaml) | `4f6dea56e9122e7acf1d4f646b7bc4e29808e072` |
| [7zip.7zip.locale.zh-CN.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/7/7zip/7zip/26.01/7zip.7zip.locale.zh-CN.yaml) | `dbad5679b90cca5f0d2842b8b13425ed18b99975` |
| [7zip.7zip.yaml](manifests/winget-pkgs-aaaf44e17204/manifests/7/7zip/7zip/26.01/7zip.7zip.yaml) | `ce96e123002f876f3ecb71551bd5c1a2b3bbb798` |

Re-verify: `scripts/host/Test-S00FrozenManifests.ps1`.

## S01 profile: PuTTY.PuTTY 0.85.0.0, x64 WiX MSI

| Field | Value | Status |
|---|---|---|
| PackageIdentifier / Version | `PuTTY.PuTTY` / `0.85.0.0` | [prepared] |
| Admitted installer | Architecture `x64`, InstallerType `wix`, Scope `machine` | [prepared] |
| InstallerUrl | `https://the.earth.li/~sgtatham/putty/0.85/w64/putty-64bit-0.85-installer.msi` | [prepared] |
| InstallerSha256 | `2213EBACF962B709411D654A40087508A694756BC4E41C5A1C24AD86596B2511` (staged copy matches; Authenticode Valid, CN=Simon Tatham) | [prepared] |
| ProductCode / UpgradeCode | `{3B0346D3-155D-43E5-9561-B9BE51CF2EB6}` / `{C9EAA861-2B72-4FAF-9FEE-EEB1AD5FD15E}` | [prepared] |
| PackageCode (summary rev) | `{1DF327D3-777D-41B7-B5D6-51EDAEF2E1A2}` | [source] [evidence](evidence/host/putty-msi-tables.txt) |
| MSI properties | `ALLUSERS=1`, Template `x64;1033`, ProductVersion `0.85.0.0`, INSTALLDIR = `ProgramFiles64Folder\PuTTY` | [source] [evidence](evidence/host/putty-msi-tables.txt) |
| Restart behaviour | No `ScheduleReboot`/`ForceReboot` in InstallExecuteSequence; custom actions are only `WixUIValidatePath` (UI) and `LaunchApplication` (UI shell exec) | [source] |
| Machine side effects (expected) | Files in `%ProgramFiles%\PuTTY`, `.ppk` association, PATH component, Start menu folder, desktop shortcut component | [source] |
| Rejected alternatives | x86 wix, arm64 wix, **x64 zip/portable (no scope, creates command aliases)**, x86/arm64 zip | [source] |
| Agreements | none declared in manifests | [source] |
| Dependencies | none declared | [source] |
| License (informational, not a WinGet agreement) | MIT, `https://www.chiark.greenend.org.uk/~sgtatham/putty/licence.html` | [source] |

### Independent machine-scope verification (PuTTY)

All read by the evidence collector as SYSTEM, without the provider. Script: [scripts/guest/Test-S00PuTTY.ps1](scripts/guest/Test-S00PuTTY.ps1).

1. `MsiGetProductInfoEx({3B0346D3-…}, NULL, MSIINSTALLCONTEXT_MACHINE, "VersionString")` returns `0.85.0.0`. The same call with `MSIINSTALLCONTEXT_USERMANAGED`/`USERUNMANAGED` for every SID returns `ERROR_UNKNOWN_PRODUCT`.
2. `HKLM\SOFTWARE\Classes\Installer\Products\3D6430B3D5515E3459169BEB15FCE26B` exists (packed ProductCode). No per-user `HKU\<sid>\Software\Microsoft\Installer\Products\…` entry.
3. `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{3B0346D3-…}` has `DisplayVersion` `0.85.0.0` (64-bit view).
4. `%ProgramFiles%\PuTTY\putty.exe` exists with `FileVersion` `0.85.0.0` and a valid Authenticode signature from Simon Tatham.
5. Absent before the case (checkpoint verification) and present after it. An installed-list result from `Get-WinGetPackage` is recorded but never used alone.

## S02 profile: 7zip.7zip 26.01, x64 EXE (NSIS)

| Field | Value | Status |
|---|---|---|
| PackageIdentifier / Version | `7zip.7zip` / `26.01` | [prepared] |
| Admitted installer | Architecture `x64`, InstallerType **`exe`**, Scope `machine` (manifest root), mode silent | [prepared] |
| InstallerUrl | `https://7-zip.org/a/7z2601-x64.exe` | [prepared] |
| InstallerSha256 | `D64A0468F5B5B0B0FC5B2188450BCD655B70809D97B1C4535F2884635094377D` (staged copy matches; **NotSigned**) | [prepared] |
| Switches | Silent `/S`, SilentWithProgress `/S`, InstallLocation `/D="<INSTALLPATH>"` (manifest-owned) | [prepared] |
| ProductCode (ARP key name) | `7-Zip` | [prepared] |
| Restart behaviour | not inspectable statically (NSIS script). The applicable return-code map is empty (no manifest codes, no `exe` defaults), so only `0` is success and any other code is an unmapped failure; no code can signal a restart ([04](04-worker-mapping.md#applicable-maps-for-the-s00-profiles-source)). S02 must show `0`, no restart, and the supplementary pending-reboot markers unchanged | [source] map; [runtime-pending] behaviour |
| Rejected alternatives | **x64 wix MSI** (`7z2601-x64.msi`, ProductCode `{23170F69-40C1-2702-2601-000001000000}`, also Scope machine via manifest root, same version), x86/arm/arm64 exe, x86 wix | [source] |
| Agreements | none declared | [source] |
| Dependencies | none declared | [source] |
| License (informational) | LGPL-2.1 (+ unRAR restriction), `https://7-zip.org/license.txt` | [source] |

Installer type is the only field that separates the admitted EXE from the x64 MSI, since both share arch, scope and version. The worker must pass `-InstallerType Exe`, and the profile must still check the resolved installer afterwards. If the module cannot enforce the type, the profile is blocked (see [04-worker-mapping.md](04-worker-mapping.md)).

### Independent machine-scope verification (7-Zip)

Script: [scripts/guest/Test-S00SevenZip.ps1](scripts/guest/Test-S00SevenZip.ps1).

1. `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\7-Zip` (64-bit view) has `DisplayVersion` `26.01` and `InstallLocation` under `%ProgramFiles%`.
2. There is no `7-Zip` key under any `HKU\<sid>\…\Uninstall`, including `S-1-5-18`.
3. There is no MSI registration for `{23170F69-40C1-2702-2601-000001000000}`, which proves the MSI alternative was not used.
4. `%ProgramFiles%\7-Zip\7z.exe` and `7zFM.exe` exist with `ProductVersion` `26.01`. **[source]** The string was read from both binaries inside the hash-verified installer on the host ([evidence/host/7zip-exe-version.txt](evidence/host/7zip-exe-version.txt)); the installed files are observed only in S02.

## Agreement records

Approval procedure for all three records: [agreements/README.md](agreements/README.md). No installation, S01 included, runs until all three are approved by the operator.

- **Package agreements:** none. Neither installer nor locale manifest contains `Agreements` [source]. Per FM-26, no agreement record is fabricated. The record is an explicit "none applicable" attestation bound to the manifest blob ids: [agreements/package-agreements.none.json](agreements/package-agreements.none.json). Status: **approved** by the operator 2026-10-28T20:19Z, bound to those blob ids (A-1).
- **Source agreements:** captured as SYSTEM during guest preparation by [Get-S00SourceAgreements.ps1](scripts/guest/Get-S00SourceAgreements.ps1) and recorded with a digest in [agreements/source-agreements.approval.json](agreements/source-agreements.approval.json). The operator approves that digest before any install; at run time the worker refuses if the presented set's digest differs. `Get-WinGetSource` does not expose agreements, so the script reads `PackageCatalogReference.SourceAgreements` through the module's internal wrapper; if that is not reachable, the capture is `not-observable` and installation stays blocked. [source] expectation: none, because the pre-indexed source reference does not override `GetInformation`. Status: captured 2026-10-28T21:43:05Z as SYSTEM in Session 0, none presented, digest `4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945` [runtime]; superseded by the re-capture in provisioning attempt 7, then **pending approval** (A-3).
- License metadata is recorded for context only. It is not a WinGet agreement, and accepting it is not modelled.

## Dependency admission

Both admitted installers declare no `Dependencies` (no WindowsFeatures, WindowsLibraries, PackageDependencies or ExternalDependencies). Admission record: [agreements/dependency-admission.json](agreements/dependency-admission.json), **approved** by the operator 2026-10-28T20:19Z (A-2). If the resolved installer at runtime declares any dependency, the profile does not match and dispatch is refused. The module's own dependency installation must be skipped (see mapping).
