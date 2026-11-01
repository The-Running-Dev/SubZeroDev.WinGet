# S00 run and reset procedure

Status: **[prepared]**. No step below has been executed. The target is the operator's existing VM `Windows11-Test` ([01 § Target VM](01-environment-inventory.md#target-vm-windows11-test-runtime)); no checkpoint named `prerequisites-ready` has been taken or restored. Following this procedure, or reading it, is not evidence of a signed-out installation. Gate A remains open.

All guest steps run from an elevated Windows PowerShell 5.1 prompt reached through PowerShell Direct from the host (`Enter-PSSession -VMName` / `Invoke-Command -VMName`), using the guest's local administrator. The Guest Service Interface is off, so files move only through PowerShell Direct (`Copy-Item -ToSession` / `-FromSession`). Evidence is collected only through PowerShell Direct; the offline VHDX route is withdrawn.

Paths in the guest:

| Root | Purpose |
|---|---|
| `%ProgramFiles%\SubZeroDev\FileManager\Service\pwsh` | Private pwsh 7.6.6 |
| `%ProgramFiles%\SubZeroDev\FileManager\Service\Modules\Microsoft.WinGet.Client\1.29.380` | Private module |
| `%ProgramData%\SubZeroDev\FileManager\Validation\Fixture` | Frozen profiles, manifests, agreement and admission records, verifier scripts |
| `%ProgramData%\SubZeroDev\FileManager\Validation\Evidence` | Case evidence, written as SYSTEM |
| `%ProgramData%\SubZeroDev\FileManager\Validation\Work` | Transient worker scratch |

## 1. Provisioning (once, before the checkpoint)

Precondition: [01 § Operator procedure](01-environment-inventory.md#operator-procedure-prepare-windows11-test) steps 1-4 are done: D-9 approved and revised (the existing 127 GiB disk kept; the VM has no checkpoint), the guest updated, and `Set-S00VmPreparation.ps1` applied. The guest is Windows 11 **Pro 26H2** x64 (CurrentBuild 26300), the baseline approved under BC-1 on 2026-10-28, installed from the admitted ISO ([01 § Admitted media](01-environment-inventory.md#admitted-media-runtime)), fully updated, rebooted, with no pending-reboot indicator. Results from this guest say nothing about 25H2.

[Invoke-S00Provisioning.ps1](scripts/host/Invoke-S00Provisioning.ps1) [prepared] drives steps 1-12 from the host: `.\scripts\host\Invoke-S00Provisioning.ps1 -Credential (Get-Credential)` in an elevated pwsh 7 session, the operator entering the guest administrator. It writes everything under `evidence/guest/provisioning-<utc>/` with a `provisioning.log`, runs the step 8 gate on an inventory taken before installation as well as on the final one, and applies the § 3 redaction (recorded in that folder's `REDACTIONS.md`). It takes the checkpoint only when every gate passed and step 7 exited 0, and asks first; with `-SkipCheckpoint` it stops before step 12.

1. **Host: verify staged assets.** `pwsh -File scripts/host/Test-S00StagedAssets.ps1` must print `OK` for every lock entry. Any `MISSING` or `MISMATCH` stops provisioning.
2. **Copy into the guest.** `Copy-Item -ToSession $s` the pwsh zip, the module nupkg, `prerequisites.lock.json` and `scripts/guest/*` to a staging folder such as `C:\S00Staging`. Copy only the assets whose lock placement is the guest.
3. **Place the worker.** `Install-S00Prerequisites.ps1 -StagingRoot C:\S00Staging -LockPath C:\S00Staging\prerequisites.lock.json`. It re-verifies size and SHA512, expands offline, never overwrites (an existing install is kept only if every file matches the pinned archives by SHA256 with no extras; otherwise it stops), and never contacts PSGallery or repairs App Installer.
4. **Pin policy.** `Set-S00PolicyPins.ps1` sets `EnableExperimentalFeatures=0`. Record its before/after output.
5. **Protect the validation root.** `Set-S00EvidenceAcl.ps1` creates `Fixture`, `Evidence` and `Work` with SYSTEM and Administrators only, and prints the SDDL. Record it.
6. **Place the fixture.** Empty `Fixture` (a re-run replaces the earlier copy), then copy `profiles/`, `manifests/`, `agreements/`, `prerequisites.lock.json` and `scripts/guest/` into `Fixture`. Write `Fixture\fixture.sha256.txt` with `Get-FileHash` of every file, and keep a copy on the host.
7. **Source agreements (captured here, approved before any install).** `Get-S00SourceAgreements.ps1`. It registers a one-shot task as S-1-5-18 that runs the private pwsh with `-NoProfile -MTA` (the direct wrapper call throws in pwsh's default STA), imports the module by absolute psd1, records `Get-WinGetSource -Name winget`, and reads `PackageCatalogReference.SourceAgreements`. It never accepts an agreement, opens or updates the source, or installs anything, and it unregisters the task afterwards.
   - Exit 0: copy `capturedUtc`, the agreements, `agreementsPresented` and `digestSha256` from the evidence file into `agreements/source-agreements.approval.json` (`capture` block, `status: "captured"`) on the host.
   - Exit 3 (`not-observable`): the internal route did not work. Record it; installation stays blocked and the route is revisited in review.
   - Any identity other than `S-1-5-18` invalidates the capture.
   - Creating the in-proc PackageManager may write WinGet settings or logs under the SYSTEM profile. That is now part of the checkpoint baseline, and step 8 records it.
   - The operator approves the captured set in the record (see [agreements/README.md](agreements/README.md)). Approval may happen after the checkpoint, but **no installation of any kind runs until all three approval records are approved**.
8. **Inventory.** `Get-S00Inventory.ps1`. It must print `BASELINE PASS` (EditionID `Professional`, DisplayVersion `26H2`, CurrentBuild `26300`, `AMD64`); `BASELINE FAIL` (exit 1) stops provisioning. The output must also show all `PendingReboot` values false, `autoAdminLogon` not `1`, `worker.PwshOnPath` false and `worker.ModuleInMachinePSModulePath` false, with no `PuTTY*` or `SevenZip*` value true.
9. **Package absence.** `Test-S00PuTTY.ps1 -Expect Absent` and `Test-S00SevenZip.ps1 -Expect Absent` must both print `PASS`.
10. **Export.** `Copy-Item -FromSession` the `Evidence` folder (including the source-agreement capture) and the SDDL and policy outputs to the host under `evidence/guest/provisioning-<utc>/`. Apply the redaction review in § 3.
11. **Clean.** Remove `C:\S00Staging`. Confirm no ISO is attached (`Set-S00VmPreparation.ps1` detached it). Close every PowerShell Direct session. If VMConnect was used for setup, sign out (not just close the window).
12. **Shut down and checkpoint.** On the host: `Stop-VM -Name Windows11-Test` (graceful), confirm `State Off`, then `Checkpoint-VM -Name Windows11-Test -SnapshotName prerequisites-ready`. Record the checkpoint `Id` and `CreationTime` from `Get-VMCheckpoint` in the provisioning evidence. Under D-9 (revised 2026-10-28) it must be the root and the only checkpoint: the script refuses to start if the VM already has a checkpoint, records the whole tree in `checkpoint.json` and fails otherwise. The script removes or merges no checkpoint; the one approved manual removal, of attempt 6's checkpoint, is in [01 § Operator procedure](01-environment-inventory.md#replacement-of-attempt-6s-checkpoint-approved-2026-10-28).

The checkpoint is taken with the VM off, so it contains no running session and no saved memory state.

## 2. Reset (between every case)

1. **Export evidence first.** `Copy-Item -FromSession` the case's `Evidence` folder and the WinGet log and settings folders through PowerShell Direct, after CaseEnd. Restore discards anything not exported.
2. `Stop-VM -Name Windows11-Test -TurnOff` if it is still running.
3. `Restore-VMCheckpoint -VMName Windows11-Test -Name prerequisites-ready -Confirm:$false`.
4. Confirm `Get-VMCheckpoint` still lists the same checkpoint `Id`.

`prerequisites-ready` is the only checkpoint (D-9); no other restore target exists.

**Restore verification (once, before S00 is complete).** [Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1) [prepared], with the VM off: `.\scripts\host\Test-S00Restore.ps1 -Credential (Get-Credential)`. It restores `prerequisites-ready`, writes a canary in the guest, restores again, and checks that the canary is gone, the fixture matches `fixture.sha256.txt` and the provisioning export, `Get-S00Inventory.ps1` passes the baseline and the § 1 step 8 gate, and both packages are absent. It leaves the VM off at the checkpoint and confirms the checkpoint list (names, Ids, parents) is unchanged. Evidence goes to `evidence/guest/restore-<utc>/` with the § 3 redaction. S00 is not complete until it prints `RESTORE PASS`.

Restore is the only reset. An in-guest uninstall, file deletion or registry cleanup does not substitute for it, and a case run on a guest that was not restored is invalid. Restoring also returns `Work` to empty.

## 3. Redaction and evidence rules

- Inventory and session-proof scripts already drop user names: session rows keep id and state, and logon events keep type, time and logon id.
- Before committing anything under `evidence/guest/`, search it for the guest administrator name, the host user name, the machine names and any email address. Replace hits with `<redacted:kind>`, and note each replacement in the folder's README.
- WinGet logs can contain the download URL and local paths. Keep URLs (they are public manifest data) and redact user profile paths.
- Evidence is never collected by signing in to the guest console or by RDP.

## 4. S01 run procedure [prepared, NOT AUTHORIZED]

S01 must not start until S00 is accepted. This section exists so that S01 can be reviewed, not run.

Preconditions:

- S00 accepted. Q-1 to Q-5 were decided on 2026-10-28 ([06](06-acceptance-checklist.md#decisions-recorded-2026-10-28)); BC-1 is approved (26H2 baseline).
- All three approval records in [agreements/](agreements/README.md) show `"decision": "approved"`, and the source-agreement digest matches the capture.
- S01 is explicitly authorized.
- The S01 SCM host has been built and reviewed in S01. It is **not** built in S00.

Steps:

1. **Reset.** Restore `prerequisites-ready` (§ 2).
2. **Host pre-arm checks.**
   - `pwsh -File scripts/host/Test-S00FrozenManifests.ps1 -Live` must print `PASS`. Any `LIVE MISMATCH` or `LIVE EXTRA` blocks arming.
   - `pwsh -File scripts/host/Test-S00StagedAssets.ps1` must print all `OK`.
3. **Install and arm (PowerShell Direct, as the guest administrator).**
   - Install the S01 service with StartType Automatic and account LocalSystem.
   - Arm exactly one fixed case: `s01-putty-0.85.0.0-x64-wix-machine`. The case is selected by profile id only; the request carries no free-form arguments.
4. **Leave no session behind.**
   - Exit and remove every PowerShell Direct session.
   - Do not open VMConnect or an enhanced session. Closing a window, including RDP, is not sign-out.
5. **Reboot.** `Restart-VM -Name Windows11-Test -Force` from the host. The SCM starts the service at boot. The service runs the case once and records, under `Evidence\`:
   - CaseStartUtc and CaseEndUtc;
   - the worker's process id and SessionId;
   - the full `PSInstallResult`, including `InstallerErrorCode` and `Status`;
   - the resolve and installer-info checks, and the source-agreement digest observed in-case compared with the approved digest (refuse on mismatch);
   - the reboot classification from [04-worker-mapping.md](04-worker-mapping.md#reboot-detection): `InstallerErrorCode` interpreted against the profile's `returnCodes.applicable` map (manifest success codes, manifest expected codes including custom fixture codes, then installer-type defaults), with the pending-reboot markers recorded as supplementary evidence only;
   - at CaseEnd, from inside the service: the interactive session list (WTS enumeration), interactive logons since boot, and the pending-reboot markers. This is the signed-out state at the moment the case ended, recorded before any collector connects.
6. **Wait without signing in.** Poll from the host only: `Get-VM` heartbeat, or wait for the service's completion marker through a short read-only PowerShell Direct call made after CaseEnd.
7. **Collect (PowerShell Direct, after CaseEnd only).** No PowerShell Direct session is opened between the reboot and the service's CaseEnd marker.
   - Run `Get-S00SessionProof.ps1 -ServiceName <name> -CaseEndUtc <recorded> -WorkerSessionId <recorded>`. It bounds the logon checks to boot..CaseEnd, lists the collector's own logon separately under `logonsAfterCaseEnd`, and exports raw `Security` and `System` event logs for boot..CaseEnd (`wevtutil epl`) with their SHA256 in the proof.
   - Then `Test-S00PuTTY.ps1 -Expect Present` and a post-case `Get-S00Inventory.ps1`.
   - The service's own CaseEnd record (step 5) is the primary signed-out evidence; the session proof is the independent cross-check. A disagreement between them invalidates the case.
8. **Export.** Copy the following to host `evidence/guest/s01-<utc>/`:
   - `Evidence\*`, including the raw `.evtx` exports. The `.evtx` files contain account names: keep them in the host evidence store, outside the repository, and commit only their SHA256 (already in the session proof).
   - `C:\Windows\Temp\WinGet\defaultState\*`
   - `C:\Windows\System32\config\systemprofile\AppData\Local\Microsoft\WinGet\Settings\defaultState\*`

   Then apply § 3.
9. **Reset.** Restore `prerequisites-ready` (§ 2).

The S01 result counts only if all of the following hold. A provider installed-list result alone never counts.

- `Get-S00SessionProof` prints `PASS`.
- `Test-S00PuTTY -Expect Present` prints `PASS`.
- `InstallerErrorCode` classifies, under the profile's applicable return-code map, as the profile's `rebootPolicy.expectedReturnCode` class (PuTTY: `0`, success, no restart). WinGet's `Status` agrees with that class; a disagreement invalidates the case ([04](04-worker-mapping.md#reboot-detection)).
- The pending-reboot markers and boot/shutdown events are recorded and are consistent with that classification. They support it; they never decide it.
- The service's CaseEnd session record and `Get-S00SessionProof` both show no signed-in or disconnected user.
