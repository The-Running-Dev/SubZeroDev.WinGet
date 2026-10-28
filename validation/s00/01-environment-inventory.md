# S00 environment inventory

Status legend: **[runtime]** observed on a machine; **[source]** concluded from published material; **[prepared]** artifact exists but has not run; **[blocked]** could not be produced.

## Summary

The validation **host** is inventoried. The target is the operator's existing VM **`Windows11-Test`**; no VM was created by this run. Its host-side configuration and the guest's published OS facts were read by [Get-S00VmConfig.ps1](scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session [runtime]; see [Target VM](#target-vm-windows11-test-runtime). The in-guest inventory, the source-agreement capture and the checkpoint are still pending the operator's provisioning run.

The baseline is **Windows 11 Pro 26H2 x64** (BC-1, approved by the operator 2026-10-28). Nothing in this bundle establishes 25H2 compatibility.

1. **B-1 (resolved for the operator's session, 2026-10-28).** `Get-VM` works in the operator's elevated session, which runs every host Hyper-V script. The account this run used is still non-elevated and not in Hyper-V Administrators, so it reads only the evidence the operator's session writes. No UAC or authorization workaround was attempted.
2. **B-2 (resolved 2026-10-28).** The operator supplied a 26H2 ISO whose SHA256 equals Microsoft's published value for English x64. See [Admitted media](#admitted-media-runtime).

## Host (validation host, not the target) [runtime]

Source: [evidence/host/host-inventory.txt](evidence/host/host-inventory.txt), captured 2026-10-28 14:32 +02:00 as a non-elevated user. The host machine name is redacted ([evidence/host/README.md](evidence/host/README.md)).

| Field | Value |
|---|---|
| Edition / ID | Enterprise / `Enterprise` (the registry `ProductName` reads "Windows 10 Enterprise", a known legacy value on Windows 11) |
| DisplayVersion | 25H2 |
| Build.UBR | 26200.9711 |
| Elevation | Admin token **False**; Hyper-V Administrators (S-1-5-32-578) **False** |
| Hyper-V services | `vmms` Running/Automatic, `vmcompute` Running/Manual |
| `Get-VM` | `ERROR: You do not have the required permission to complete this task. Contact the administrator of the authorization policy for the computer '<redacted:host>'.` |
| Tooling used for host evidence | pwsh 7.6.9 (host only, not the pinned worker), Windows PowerShell 5.1, dumpbin 14.44.35207 |

### Local ISO candidates: rejected

None of these has a verifiable Microsoft hash, so none is admitted. Under BC-1 only a download whose SHA256 equals Microsoft's published value is admitted:

| Path | Bytes | Reason |
|---|---|---|
| `D:\Hyper-V\Setup-Windows-Spike-windows.iso` | 8511985664 | Unknown origin; name suggests a custom build |
| `D:\Hyper-V\Setup-Windows-Test-windows.iso` | 8511985664 | Unknown origin |
| `D:\Temp\Microsoft_Windows_11_Enterprise_Version_26H1_X64-CYGiSO.iso` | 6610452480 | Third-party repack name; 26H1 and Enterprise, not 25H2 Pro |
| `D:\Temp\Windows-11.Custom.iso` | 8515420160 | Custom image |

## Installation media

### What Microsoft offers today

| Channel | Version served | Access | Published hash | Label |
|---|---|---|---|---|
| Public page `https://www.microsoft.com/en-us/software-download/windows11` ("Download Windows 11 Disk Image (ISO) for x64 devices") | **26H2 only** | Anonymous; the download link is session-bound and expires after 24 hours | Yes: a per-language SHA256 list, "Hash values for the ISO files for Each Language", shown on the same page after the language is confirmed | [runtime] page read 2026-10-28 |
| Visual Studio Subscriptions, `https://my.visualstudio.com/Downloads` | Not observed | Sign-in with an entitled subscription | Expected per file in the file details; not observed | Not observed |
| Microsoft 365 admin center, Billing → Your products → Volume licensing → Downloads | Business editions; versions not observed | Sign-in with a volume-licensing role | Not observed; without a hash the file is not admissible | Not observed |

Not checked: whether 25H2 is still listed in either signed-in portal, and how each shows its hash. That needs the operator's sign-in. No official anonymous 25H2 ISO source was found.

### Release facts [source]

From Windows release health (`https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information`):

| Version | GA | Current build | End of updates (Enterprise/Education) |
|---|---|---|---|
| 25H2 | 2025-09-30 | 26200.9725 | 2027-10-12 |
| 26H2 | 2026-09-29 | 26300.9725 | 2028-10-10 |

Both builds take the same monthly cumulative update (KB5134133 at capture). 26H2 is delivered to 25H2 devices as an enablement package (KB5121794) over a shared servicing branch, so the two differ mainly in which features are switched on.

### BC-1: baseline change (approved 2026-10-28)

The operator approved: "use Windows 11 Pro 26H2 x64 for the S00 validation VM. This does not establish 25H2 compatibility."

| | Before | Now |
|---|---|---|
| Baseline | Windows 11 Pro 25H2 x64 (handoff) | Windows 11 Pro **26H2** x64, CurrentBuild 26300 |
| Media | Signed-in portal (not available to this run) | Public page, admitted only by its published SHA256 |
| VM | New `SZD-FM-S00-W11Pro25H2` | The operator's existing `Windows11-Test` (no new VM) |
| Gate A | Would cover 25H2 | Covers 26H2 only. 25H2 stays untested |

The handoff's 25H2 references stay as written; this bundle records the deviation ([06 § Deviations](06-acceptance-checklist.md#deviations-from-approved-decisions-fm-01-to-fm-39)). Option A (25H2 from a signed-in portal) was not taken; its steps are no longer part of this procedure.

### Admitted media [runtime]

Checked 2026-10-28 on the validation host. The operator downloaded the file; this run only hashed and inspected it.

| Check | Result |
|---|---|
| Path | `D:\Downloads\Windows11_Client_x64_en-us_26300_9457.iso` |
| Size | 9047330816 bytes |
| SHA256 (`Get-FileHash`) | `BD4307DF32BC8AF33B39CCECB1174AEB345386630F89A2B86C7A4E36B55EA650` |
| Microsoft published value | "English 64-bit": `BD4307DF32BC8AF33B39CCECB1174AEB345386630F89A2B86C7A4E36B55EA650`, read from the public page's hash list after selecting "Windows 11 (multi-edition ISO for x64 devices)" and "English (United States)". Read again 2026-10-28 on the Bulgarian locale of the same page, `https://www.microsoft.com/bg-bg/software-download/windows11`, where the "Английски 64-bit" row carries the same value |
| Match | **Exact.** Admitted |
| Volume label | `CCCOMA_X64FRE_EN-US_DV9` (7-Zip listing) |
| `install.wim` images | 11, all build 10.0.26300.9457, architecture AMD64. Index 6 is `Professional` ("Windows 11 Pro"). Read from the WIM XML with the ISO mounted read-only, then dismounted |
| Origin | The operator states it was downloaded from the `bg-bg` page above (operator statement). The file has no `Zone.Identifier` stream, so the host records no download URL. Admission rests on the hash match |
| File name | Matches the link the `bg-bg` page generates for English (United States): `software.download.prss.microsoft.com/dbazure/Windows11_Client_x64_en-us_26300_9457.iso` (link read, not downloaded). Not a criterion; the hash is |

The image is build 26300.9457; the current 26H2 build is 26300.9725. The guest must be updated to the current cumulative update, with no pending reboot, before the checkpoint; the provisioning gate ([05 § 1 step 8](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint)) refuses otherwise.

## Target VM: `Windows11-Test` [runtime]

The operator created this VM and installed the guest before this run; this run did not create a VM. Source: [evidence/host/vm-config.json](evidence/host/vm-config.json), written by [Get-S00VmConfig.ps1](scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session, captured 2026-10-28T19:01:44Z with the VM running. The guest facts come from the Hyper-V KVP Exchange integration service, without guest credentials.

| Requirement (FM-38 / BC-1) | Observed | Result |
|---|---|---|
| Generation 2 | 2 (configuration version 12.0) | Meets |
| Secure Boot, `MicrosoftWindows` template | On, `MicrosoftWindows` | Meets |
| vTPM | Enabled | Meets |
| Guest Service Interface off | Off. Heartbeat, KVP, Shutdown, Time Sync and VSS on | Meets |
| Standard checkpoint type | Standard | Meets |
| Network | `Default Switch` | Meets |
| Guest edition / architecture | KVP `OSEditionId` 48 (Professional), `ProcessorArchitecture` 9 (x64) | Meets |
| Guest build | KVP `OSVersion` 10.0.26300 | Meets. DisplayVersion `26H2` is not in the KVP data; `Get-S00Inventory.ps1` confirms it in the guest |
| 4 vCPU | 8 | **Gap**, set by `Set-S00VmPreparation.ps1`. A preparation setting only: the operator ruled on 2026-10-28 that 8 vCPU is not a WinGet feasibility blocker |
| 8 GB static RAM | Dynamic, 4 GiB startup | **Gap**, fixed by `Set-S00VmPreparation.ps1` |
| Automatic checkpoints off | On | **Gap**, fixed by `Set-S00VmPreparation.ps1` |
| AutomaticStartAction Nothing | StartIfRunning | **Gap**, fixed by `Set-S00VmPreparation.ps1` |
| AutomaticStopAction ShutDown | Save | **Gap**, fixed by `Set-S00VmPreparation.ps1` |
| No ISO attached | The admitted 26H2 ISO | **Gap**, detached by `Set-S00VmPreparation.ps1` |
| One 80 GB dynamic VHDX, no checkpoints yet | A 127 GiB (136365211648-byte) differencing disk whose parent is the base VHDX, under one existing checkpoint | **Deviation D-9**, approved by the operator 2026-10-28. Since 20:38Z the same day the checkpoint is gone and the disk is the base VHDX alone; D-9 was revised ([§ D-9](#d-9-existing-127-gib-disk-approved-2026-10-28-revised-the-same-day)) |

Notes on the KVP data: `OSName` reads "Windows 10 Pro" (the same legacy value the registry `ProductName` shows) and `OSBuildNumber` reads 10011, which is not the Windows build. The check uses `OSVersion`.

### D-9: existing 127 GiB disk (approved 2026-10-28, revised the same day)

Operator decision, 2026-10-28T20:19Z: "Approve D-9 for S00: retain the existing 127 GiB disk and checkpoint chain. Do not delete or merge existing checkpoints. Record the layout, establish a dedicated S00 baseline checkpoint after preparation, and verify restoration before declaring S00 complete."

Revision, chosen by the operator on 2026-10-28 after the incident below: "Keep the merged 127 GiB disk as is. prerequisites-ready becomes the root and only S00 checkpoint." The retained-checkpoint rule no longer applies, because that checkpoint no longer exists.

Replacement, chosen by the operator on 2026-10-28 (UTC) after the second restore test, option "A: replace checkpoint": "remove prerequisites-ready (merge), re-provision and take a new root checkpoint, re-capture A-3". It covers only `prerequisites-ready` `fc9b536f-885d-48ef-bf1a-15f6e5adfea0`, taken by provisioning attempt 6, whose fixture holds the pre-fix `Get-S00Inventory.ps1` and `Get-S00SessionProof.ps1`. The operator removes it by hand ([§ Operator procedure](#replacement-of-attempt-6s-checkpoint-approved-2026-10-28)); no S00 script removes or merges a checkpoint, and no other removal is approved.

| | FM-38 | `Windows11-Test` [runtime, 2026-10-28T20:38:46Z] |
|---|---|---|
| Disk | One 80 GB dynamic VHDX | One dynamic VHDX, 127 GiB maximum, no differencing disk. **Deviation** (size only) |
| Checkpoints before `prerequisites-ready` | None | None. Meets |
| VM path | Not fixed (the unused creation script defaulted to `D:\Hyper-V\SZD-FM-S00`) | `D:\VMs\Windows11-TEst` |

#### Incident: the automatic checkpoint was removed and merged (2026-10-28) [runtime]

Every value below is from the captures in [evidence/host/attempt-1-20261028T2036Z/](evidence/host/attempt-1-20261028T2036Z/README.md) and [provisioning-20261028T203829Z](evidence/guest/provisioning-20261028T203829Z/provisioning.log).

| Time (UTC) | Source | State |
|---|---|---|
| 17:12:48 | Hyper-V | Automatic checkpoint "Automatic Checkpoint - Windows11-Test - (10/28/2026 - 7:12:48 PM)", Id `87637f8f-a956-40a9-b101-31d982f890cf`, created (root, type Standard) |
| 20:36:57 | `Get-S00VmConfig.ps1` (step 3) | Running, 8 vCPU, automatic checkpoints on. Checkpoint `87637f8f…` present. Current disk `Windows11-TEst_968803B7-CABD-4B0A-BA4A-413027706741.avhdx` (differencing, 30,068,965,376 bytes) over `Windows11-TEst.vhdx` (dynamic, 37,748,736 bytes) |
| 20:36:57-20:38:17 | `Set-S00VmPreparation.ps1` (step 4) | Graceful `Stop-VM`, then `Set-VM` with automatic checkpoints off and the other FM-38 settings |
| 20:38:17 | `Get-S00VmConfig.ps1` (step 4) | Off, 4 vCPU, automatic checkpoints off. **No checkpoints.** The `.avhdx` is still the current disk (30,067,916,800 bytes); the base VHDX has grown to 20,539,506,688 bytes: the merge is under way |
| 20:38:29-20:38:39 | `Invoke-S00Provisioning.ps1`, attempt 1 | Staged assets `PASS`; `Start-VM` failed with 0x800705AA (unable to allocate 8192 MB). No guest change, no checkpoint |
| 20:38:46 | `Get-S00VmConfig.ps1` (step 6) | Off. The only disk is `Windows11-TEst.vhdx`, dynamic, 31,578,914,816 bytes, no parent (chain depth 1). No checkpoints. The file is named `after-checkpoint` after the procedure step; no checkpoint was taken |

Cause: Hyper-V removed the automatic checkpoint and merged its `.avhdx` into the base VHDX between the 20:36:57 and 20:38:17 captures, while `Set-S00VmPreparation.ps1` shut the VM down and turned automatic checkpoints off. No S00 script issued a removal or merge; the host scripts contain no `Remove-VMCheckpoint`, `Remove-VMSnapshot`, `Merge-VHD`, `Resize-VHD`, `Optimize-VHD` or `Convert-VHD`. Which of the two operations triggered it was not established: the Hyper-V event log was not read. The risk that preparation would remove an automatic checkpoint was not assessed when D-9 was presented.

Consequence: the 17:12:48Z point-in-time state cannot be restored. The guest state current at shutdown was kept: it is now in the base VHDX. Nothing else on the VM changed.

Recorded layout [runtime], from [evidence/host/attempt-1-20261028T2036Z/vm-config.after-checkpoint.json](evidence/host/attempt-1-20261028T2036Z/vm-config.after-checkpoint.json):

| Layer | Item | Detail |
|---|---|---|
| Disk | `D:\VMs\Windows11-TEst\Virtual Hard Disks\Windows11-TEst.vhdx` | Dynamic, virtual size 136365211648 bytes (127 GiB), file size 31578914816 bytes, no parent. The VM's only disk |
| Checkpoints | None | |
| S00 baseline | `prerequisites-ready` | Taken by `Invoke-S00Provisioning.ps1` with the VM off, as the root and only checkpoint. Its differencing disk becomes the only level above the base VHDX. Attempt 6 took `fc9b536f-885d-48ef-bf1a-15f6e5adfea0` [runtime]; it is being replaced under the operator's decision above, and attempt 7 takes the new one [pending] |

Rules for S00:

- No `Remove-VMCheckpoint`, `Remove-VMSnapshot`, `Merge-VHD`, `Resize-VHD`, `Optimize-VHD`, `Convert-VHD` or checkpoint rename anywhere in S00. The host scripts contain none, and the offline host test checks it. The one exception is the operator's manual removal of attempt 6's `prerequisites-ready` under the replacement decision above.
- `Get-S00VmConfig.ps1` records the disk chain (`Disks[].Chain`) and each checkpoint's `ParentId`, and checks that there is one 127 GiB disk on the base VHDX with one differencing level per checkpoint, and that the only checkpoint, if any, is `prerequisites-ready` with no parent.
- `Invoke-S00Provisioning.ps1` refuses to start, before changing anything, if the VM has any checkpoint. It fails if `prerequisites-ready` does not end up as the root and only checkpoint. `checkpoint.json` records the whole tree.
- Every reset restores `prerequisites-ready`.
- S00 is complete only after [Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1) prints `RESTORE PASS`.

The larger disk changes no S00 or S01 criterion. Shrinking the disk to 80 GB was not attempted.

### Host scripts for this VM [prepared]

All four run in the operator's elevated pwsh session from `validation\s00`. None has run in this repository's session.

| Script | What it does |
|---|---|
| [Get-S00VmConfig.ps1](scripts/host/Get-S00VmConfig.ps1) | Read-only. Writes the JSON above and prints one `OK` or `GAP` line per requirement |
| [Set-S00VmPreparation.ps1](scripts/host/Set-S00VmPreparation.ps1) | Asks first, shuts the guest down gracefully (no `-TurnOff`), then sets 4 vCPU, 8 GB static RAM, automatic checkpoints off, start action Nothing, stop action ShutDown, and detaches the ISO. Leaves the VM off. It does not touch disks, checkpoints, Secure Boot, TPM, integration services or networking |
| [Invoke-S00Provisioning.ps1](scripts/host/Invoke-S00Provisioning.ps1) | Runs [05 § 1](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint) through PowerShell Direct with a guest administrator credential the operator enters (`Get-Credential`), exports and redacts the evidence, and takes `prerequisites-ready` only if every gate passed and the source-agreement capture exited 0, as the root and only checkpoint. Refuses to start if the VM already has a checkpoint |
| [Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1) | Requires the VM off. Asks first, restores `prerequisites-ready`, boots and writes a canary file, shuts down, restores again, and checks in the guest that the canary is gone, the fixture matches its hashes and the provisioning export, `Get-S00Inventory.ps1` passes the baseline and the step 8 gate, and both packages are absent. Then restores a third time so the VM is left off at the checkpoint, and confirms no checkpoint was added, removed or re-parented. Writes `evidence/guest/restore-<utc>/` and prints `RESTORE PASS` or `RESTORE FAIL` |

[New-S00Vm.ps1](scripts/host/New-S00Vm.ps1) is kept but **not used**: the target is the existing VM.

## Guest [pending provisioning run]

The following must be captured by [scripts/guest/Get-S00Inventory.ps1](scripts/guest/Get-S00Inventory.ps1) before the prerequisites-ready checkpoint. None of it has been captured yet:

- Edition, DisplayVersion, CurrentBuild and architecture, checked against the baseline (`Professional`, `26H2`, `26300`, `AMD64`; the script prints `BASELINE PASS` or exits 1), plus UBR, BuildLabEx and install date.
- Secure Boot and TPM state.
- Hotfix list and pending-reboot indicators (must all be false at checkpoint).
- Media provenance: carried from [Admitted media](#admitted-media-runtime) into the provisioning evidence.
- App Installer, VCLibs, UI.Xaml and WindowsAppRuntime packages (per-user and provisioned). These are recorded, not required.
- The `winget.exe` execution alias, if any (recorded; never used by the worker).
- The WinGet policy key.
- The private worker: pwsh FileVersion, the runtime version pwsh reports (`PwshRuntime`, expected 10.0.12), module file count and hashes, and absence from PATH and machine PSModulePath.
- PuTTY and 7-Zip absence.
- Sessions, with ids and states only and no user names.
- WinHTTP proxy and CDN reachability.

The source-agreement capture ([Get-S00SourceAgreements.ps1](scripts/guest/Get-S00SourceAgreements.ps1)) also runs before the checkpoint; see [05 § 1 step 7](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint).

## Operator procedure: prepare `Windows11-Test`

B-1 is resolved for the operator's elevated session. If the ISO is replaced, the new file must again match the value on `https://www.microsoft.com/en-us/software-download/windows11` (select **Windows 11 (multi-edition ISO for x64 devices)**, **Confirm**, the language, **Confirm**, then read "Hash values for the ISO files for Each Language"), and [Admitted media](#admitted-media-runtime) is updated before use.

In an elevated pwsh 7 session, from `validation\s00` in a SubZeroDev.WinGet checkout:

1. D-9 is approved and revised (2026-10-28): nothing to decide. The VM must have no checkpoint before provisioning; S00 scripts delete or merge none. The one approved removal, of attempt 6's checkpoint, is [below](#replacement-of-attempt-6s-checkpoint-approved-2026-10-28).
2. In the guest, apply all cumulative updates and reboot until no pending-reboot indicator remains. Sign out of VMConnect afterwards. The provisioning gate refuses a pending reboot.
3. Re-read the configuration. Done 2026-10-28T20:36:57Z; the capture is in [evidence/host/attempt-1-20261028T2036Z](evidence/host/attempt-1-20261028T2036Z/README.md). Never overwrite `vm-config.json`, the 19:01:44Z capture.
4. Fix the in-place gaps with `.\scripts\host\Set-S00VmPreparation.ps1`. Done 2026-10-28 (capture in the same folder); not repeated.
5. Re-read before provisioning: `.\scripts\host\Get-S00VmConfig.ps1 -OutFile .\evidence\host\vm-config.before-provisioning.json`. Every line must print `OK` except, because the VM is off, the three guest KVP lines and `Heartbeat`. Then provision: `.\scripts\host\Invoke-S00Provisioning.ps1 -Credential (Get-Credential)`. The host needs about 9 GB available for the 8192 MB static allocation plus overhead; check with `(Get-Counter '\Memory\Available MBytes').CounterSamples.CookedValue` and free memory first (for example `wsl --shutdown`). The first attempt, 2026-10-28T20:38:39Z, stopped at `Start-VM` with 0x800705AA (unable to allocate 8192 MB) before any guest change. The second, 2026-10-28T21:07:14Z, started the VM but the guest rejected the credential for 10 minutes; no guest change, no checkpoint ([captures](evidence/host/attempt-2-20261028T2056Z/README.md)). Before provisioning, check the credential: `Invoke-Command -VMName Windows11-Test -Credential (Get-Credential) -ScriptBlock { whoami; ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole('Administrators') }` must print the account and `True`. Prefer a local account: a Microsoft account needs its email address and account password (not the PIN), and its profile folder name is not covered by the automatic redaction. The scripts stop after three rejections while the guest heartbeat is `OK`. The third attempt, 2026-10-28T21:21:22Z, used a local administrator the operator created in the guest and passed the pre-check; it stopped at the baseline inventory on a Windows PowerShell 5.1 parameter error in `Get-S00Inventory.ps1`, since fixed. No guest change remained, no checkpoint ([captures](evidence/host/attempt-3-20261028T2120Z/README.md)). The fourth, 2026-10-28T21:27:05Z, installed the pinned worker and set the policy pin, then stopped at `Set-S00EvidenceAcl.ps1` on a 5.1 parameter error, since fixed. Those two guest changes remain without a checkpoint ([captures](evidence/host/attempt-4-20261028T2126Z/README.md)); a re-run verifies the installed files against the pinned archives by SHA256 and keeps them only if every file matches with no extras. The fifth, 2026-10-28T21:37:31Z, passed every step and gate but the source-agreements capture, which recorded `not-observable` because the private pwsh ran in STA and the direct wrapper call requires MTA; the task now starts pwsh with `-MTA`. No checkpoint was taken; the guest keeps the worker, the policy pin, the validation root with its ACL and the fixture ([captures](evidence/host/attempt-5-20261028T2136Z/README.md)). A re-run empties and re-copies the fixture. The sixth, 2026-10-28T21:42:28Z, passed every step and gate, captured the source agreements (none presented) and took `prerequisites-ready` (`fc9b536f-885d-48ef-bf1a-15f6e5adfea0`) as the root and only checkpoint ([captures](evidence/host/attempt-6-20261028T2142Z/README.md)). Its first restore test stopped at the D-9 check before any change, on a defect in the test (one checkpoint counted as three), since fixed. The second, 2026-10-28T21:50:50Z, passed the D-9 check, both restores, the canary and the fixture checks, then stopped at the guest inventory on a defect in [Get-S00Inventory.ps1](scripts/guest/Get-S00Inventory.ps1): With no user signed in, `quser.exe` writes "No User exists for *" to stderr, and Windows PowerShell 5.1 under `Stop` turned the redirected stderr into a terminating `NativeCommandError`; provisioning passed because a user was signed in. Fixed in the repo (and the same call in [Get-S00SessionProof.ps1](scripts/guest/Get-S00SessionProof.ps1)); the checkpoint's fixture still holds the defective copy, so the operator approved replacing the checkpoint ([below](#replacement-of-attempt-6s-checkpoint-approved-2026-10-28)). Enter the guest's local administrator in the prompt; the credential is never written to disk or evidence. It asks before the shutdown and checkpoint.
6. Record the layout after the checkpoint: `.\scripts\host\Get-S00VmConfig.ps1 -OutFile .\evidence\host\vm-config.after-checkpoint.json`. The D-9 lines must print `OK`: `prerequisites-ready` is the root and only checkpoint, over a two-level disk chain.
7. Verify restoration: `.\scripts\host\Test-S00Restore.ps1 -Credential (Get-Credential)`. It asks once before discarding the VM's current state. S00 is not complete until it prints `RESTORE PASS`.

#### Replacement of attempt 6's checkpoint (approved 2026-10-28)

Operator decision in [§ D-9](#d-9-existing-127-gib-disk-approved-2026-10-28-revised-the-same-day). In the same elevated session and folder, with the VM off:

1. `Remove-VMSnapshot -VMName Windows11-Test -Name prerequisites-ready -Confirm`. Irreversible: Hyper-V merges the checkpoint's differencing disk into the base VHDX in the background, and the guest keeps attempt 6's state.
2. Wait for the merge: `do { Start-Sleep 5; $p = (Get-VMHardDiskDrive -VMName Windows11-Test).Path } while ($p -like '*.avhdx'); $p` must print the base `.vhdx` path.
3. Repeat steps 5-7 as attempt 7. Attempt 6's host captures are in [evidence/host/attempt-6-20261028T2142Z](evidence/host/attempt-6-20261028T2142Z/README.md), so the step 5 and 6 output paths are free. Provisioning empties and re-copies the fixture from the repo, re-captures the source agreements (A-3) and takes the new `prerequisites-ready`; `Test-S00Restore.ps1` picks the newest provisioning evidence that has `checkpoint.json`.
