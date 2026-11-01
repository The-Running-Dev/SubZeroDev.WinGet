# S00 acceptance checklist

**S00 is NOT COMPLETE.** The target is the operator's existing VM `Windows11-Test`. Its host-side configuration has been read [runtime], and the scripts that fix its gaps and provision it are [prepared]. Provisioning attempt 6 produced the in-guest inventory, the source-agreement capture (A-3, not yet approved) and the prerequisites-ready checkpoint [runtime]. The reset has not passed: its first run stopped before any change on a test defect, and its second on a guest-script defect that the checkpoint's fixture still contains; both are fixed in the repo. The operator approved replacing the checkpoint (D-9): remove it, re-provision (attempt 7), re-capture A-3 and run the restore again.

**Signed-out WinGet installation and Gate A are UNPROVEN.** No VM was created, no package was installed, no service was built, and no S01 run took place.

**Baseline: Windows 11 Pro 26H2 x64** (BC-1, approved 2026-10-28). Nothing here establishes 25H2 compatibility.

Legend:

| Label | Meaning |
|---|---|
| **[runtime]** | Observed on a machine. In S00 so far this is the **host**, a public Microsoft web page, or the target VM's host-side configuration and KVP data read from the host. Nothing has been observed from inside the guest. |
| **[source]** | Concluded from manifests, installer metadata, binaries, `winget-cli` source or published Microsoft material. |
| **[prepared]** | Artifact or script exists in this repo and has not run in a guest. |
| **[blocked]** | Could not be produced; see the blocker. |

Status values: **Completed**, **Incomplete**, **Pending provisioning run** (needs the operator's run of [01 § Operator procedure](01-environment-inventory.md#operator-procedure-prepare-windows11-test)), **Pending approval**, **Pending capture** (A-3, filled from the provisioning run), **Pending restore verification** (needs [Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1) after the checkpoint), **Not in S00** (belongs to S01 by design).

## Decisions recorded 2026-10-28

The operator answered Q-1 to Q-5 from the previous review and added three further decisions. This bundle applies them as follows.

| Id | Decision | Applied in |
|---|---|---|
| Q-1 | Reboot detection interprets the installer return code with the **applicable manifest mappings**: manifest `InstallerSuccessCodes`, then manifest `ExpectedReturnCodes` (custom fixture codes included), then the installer-type defaults for codes not already declared. Windows pending-reboot markers are **supplementary** evidence only | [04 § Reboot detection](04-worker-mapping.md#reboot-detection), mapping #14; `returnCodes` in both profiles; [05 § 4](05-run-reset-procedure.md#4-s01-run-procedure-prepared-not-authorized) pass criteria |
| Q-2 | Source agreements are **captured during guest preparation**, and the operator approves them **before any installation** | [Get-S00SourceAgreements.ps1](scripts/guest/Get-S00SourceAgreements.ps1) [prepared]; [05 § 1 step 7](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint); record A-3 |
| Q-3 | Evidence is collected through **PowerShell Direct after CaseEnd**, preserving the signed-out session evidence | [05 § 2 and § 4](05-run-reset-procedure.md); [Get-S00SessionProof.ps1](scripts/guest/Get-S00SessionProof.ps1) exports Security/System `.evtx` up to CaseEnd. The offline-VHDX route is withdrawn |
| Q-4 | The separately pinned .NET runtimes are **accepted, subject to runtime validation** | Validation item V-1 below |
| Q-5 | The operator approves the agreement and dependency records | A-1 and A-2 **approved** by the operator 2026-10-28T20:19Z, bound to the frozen manifests (recorded at the operator's instruction). A-3 **pending capture**, then approval. Nothing was signed on the operator's behalf |
| D-6 | Correct the drift claim: WinGet's live hash check does not prove it used our frozen manifest | [04 § Drift detection](04-worker-mapping.md#drift-detection); [03](03-package-profiles.md); discrepancy 5 |
| D-7 | Find an official source for 25H2 media, or propose a baseline change | [01 § Installation media](01-environment-inventory.md#installation-media); BC-1 proposed, then **approved** (below) |
| BC-1 | Use Windows 11 Pro 26H2 x64 for the S00 validation VM; this does not establish 25H2 compatibility | [01 § BC-1](01-environment-inventory.md#bc-1-baseline-change-approved-2026-10-28); target VM `Windows11-Test` (existing; guest KVP shows Pro, 10.0.26300, x64); `Get-S00Inventory.ps1` baseline check (`Professional`, `26H2`, `26300`, `AMD64`); [05 § 1](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint) |
| D-8 | Cancelling a worker may leave an installer running; carry this to S02 validation | [Carried forward to S02](#carried-forward-to-s02-not-s00-work); mapping #18 |
| VM | Use the existing `Windows11-Test`; do not create another VM | [01 § Target VM](01-environment-inventory.md#target-vm-windows11-test-runtime); [Get-S00VmConfig.ps1](scripts/host/Get-S00VmConfig.ps1), [Set-S00VmPreparation.ps1](scripts/host/Set-S00VmPreparation.ps1), [Invoke-S00Provisioning.ps1](scripts/host/Invoke-S00Provisioning.ps1) [prepared]; [05](05-run-reset-procedure.md) uses the name. `New-S00Vm.ps1` is not used. Disk and checkpoint layout: D-9 (approved, revised) below |
| D-9 | Retain the existing 127 GiB disk and checkpoint chain; delete or merge no checkpoint; record the layout; take a dedicated S00 baseline checkpoint after preparation; verify restoration before S00 is complete. **Revised** after Hyper-V removed and merged the automatic checkpoint during preparation: keep the merged 127 GiB disk as is; `prerequisites-ready` is the root and only S00 checkpoint. **Replacement**: remove attempt 6's `prerequisites-ready` (merge), re-provision, take a new root checkpoint, re-capture A-3 | **Approved** by the operator, 2026-10-28; **revised** by the operator the same day; replacement approved the same day. [01 § D-9](01-environment-inventory.md#d-9-existing-127-gib-disk-approved-2026-10-28-revised-the-same-day) (incident and layout) [runtime]; [Get-S00VmConfig.ps1](scripts/host/Get-S00VmConfig.ps1) checks the chain; [Invoke-S00Provisioning.ps1](scripts/host/Invoke-S00Provisioning.ps1) refuses a VM that has any checkpoint and fails unless `prerequisites-ready` is the root and only one; [Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1) [prepared] verifies restoration |
| vCPU | 4 vCPU stays a preparation setting; 8 vCPU is not a WinGet feasibility blocker | Operator, 2026-10-28. [Set-S00VmPreparation.ps1](scripts/host/Set-S00VmPreparation.ps1) still sets 4; `Get-S00VmConfig.ps1` labels the check "preparation setting, not a feasibility gate" |

## Approvals required from the operator

Only the operator decides. A decision given in chat is written into the record at the operator's instruction, quoting it, with `recordedBy`; the record's `approval` block holds `status`, `decision`, `approver` and `decidedUtc`. Installation is blocked until all three are `approved`. See [agreements/README.md](agreements/README.md).

| Id | Record | What is approved | Status |
|---|---|---|---|
| A-1 | [package-agreements.none.json](agreements/package-agreements.none.json) | Neither frozen manifest declares package agreements [source] | **Approved** 2026-10-28T20:19Z, bound to the frozen manifests (blob ids in the record) |
| A-2 | [dependency-admission.json](agreements/dependency-admission.json) | Neither installer declares dependencies [source]; `-SkipDependencies` is mapped | **Approved** 2026-10-28T20:19Z, bound to the frozen manifests (blob ids in the record) |
| A-3 | [source-agreements.approval.json](agreements/source-agreements.approval.json) | The source agreements captured as SYSTEM during guest preparation. Expected empty [source]; captured 2026-10-28T21:43:05Z as S-1-5-18, Session 0, MTA: none presented, digest `4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945` [runtime]. Superseded by the re-capture in provisioning attempt 7 | **Pending capture** (re-capture in attempt 7), then approval |

BC-1 and D-9 are decided (approved 2026-10-28) and are listed under Decisions. A-1 and A-2 stay approved only while the record and manifest blob ids are unchanged.

## Validation items

| Id | Item | Status |
|---|---|---|
| V-1 | .NET runtimes (Q-4). The guest inventory records `PwshRuntime` from the private pwsh, which must be **10.0.12**. In S01 the service records its own `Environment.Version`, which must be **10.0.14**. A mismatch on either fails the run | [prepared] in [Get-S00Inventory.ps1](scripts/guest/Get-S00Inventory.ps1); service half is S01 |
| V-2 | Source-agreement capture route. Reaches `PackageCatalogReference.SourceAgreements` through the module's internal `PackageManagerWrapper`. If that cannot be reached, the capture records `not-observable`, exits 3, and installation stays blocked | [prepared], runtime-pending |

## Handoff deliverables (S00 must produce)

| # | Deliverable | Status | Evidence |
|---|---|---|---|
| 1 | OS edition/build/UBR, update state, image provenance | **Pending provisioning run** | Image provenance [runtime]: the 26H2 ISO matches Microsoft's published SHA256 ([01 § Admitted media](01-environment-inventory.md#admitted-media-runtime)). Guest KVP read from the host [runtime]: EditionId 48 (Pro), OSVersion 10.0.26300, x64 ([vm-config.json](evidence/host/vm-config.json)); DisplayVersion and UBR need the guest capture. Host only, [runtime]: [evidence/host/host-inventory.txt](evidence/host/host-inventory.txt). Guest capture is [prepared]: [Get-S00Inventory.ps1](scripts/guest/Get-S00Inventory.ps1). Local ISOs were rejected: [01](01-environment-inventory.md#local-iso-candidates-rejected) |
| 2 | Artifact versions, sources, hashes, native closure | **Completed** | [prerequisites.lock.json](prerequisites.lock.json), [02](02-prerequisites.md), [host-verifiers.txt](evidence/host/host-verifiers.txt) (7/7 OK, [runtime] host), [module-x64-native-dependents.txt](evidence/host/module-x64-native-dependents.txt), [module-1.29.380-file-inventory.sha256.txt](evidence/host/module-1.29.380-file-inventory.sha256.txt) |
| 3 | Frozen manifests, URLs/hashes, source identity, agreements, dependency admission | **Incomplete** | Manifests frozen and byte-exact (8/8): [host-verifiers.txt](evidence/host/host-verifiers.txt), [manifest-live-drift.txt](evidence/host/manifest-live-drift.txt). Profiles: [putty.s01](profiles/putty.s01.profile.json), [7zip.s02](profiles/7zip.s02.profile.json), [03](03-package-profiles.md). A-1 and A-2 approved. Open: A-3 pending capture (provisioning run) and approval |
| 4 | Protected fixture/evidence locations, checkpoint/reset procedure | **Incomplete** (procedure written; **Pending provisioning run** for execution) | [05](05-run-reset-procedure.md), [Set-S00EvidenceAcl.ps1](scripts/guest/Set-S00EvidenceAcl.ps1) [prepared]. No checkpoint exists |
| 5 | Fixed worker mapping | **Completed** as [source] | [04](04-worker-mapping.md). #14 `RebootRequired` stays UNSUPPORTED in the module; reboot detection uses the return-code mapping decided in Q-1. #18 cancellation is carried to S02 |
| 6 | Independent machine-scope verification for both packages | **Completed** as [prepared] + [source]; not run in a guest | [Test-S00PuTTY.ps1](scripts/guest/Test-S00PuTTY.ps1), [Test-S00SevenZip.ps1](scripts/guest/Test-S00SevenZip.ps1), [S00MachineScope.psm1](scripts/guest/S00MachineScope.psm1). Host smoke of the mechanics and fail-closed behaviour: [verifier-smoke.txt](evidence/host/verifier-smoke.txt). PuTTY MSI facts: [putty-msi-tables.txt](evidence/host/putty-msi-tables.txt). 7-Zip exe version: [7zip-exe-version.txt](evidence/host/7zip-exe-version.txt) |
| 7 | Procedure proving SCM LocalSystem / Session 0 after reboot with no signed-in or disconnected user | **Completed** as [prepared]; execution is S01 | [05 § 4](05-run-reset-procedure.md#4-s01-run-procedure-prepared-not-authorized), [Get-S00SessionProof.ps1](scripts/guest/Get-S00SessionProof.ps1). Collection is PowerShell Direct after CaseEnd (Q-3); the service's own CaseEnd session record is primary |

## Slice acceptance criteria (S00)

| Criterion | Status | Notes |
|---|---|---|
| Provision all prerequisites before testing | **Pending provisioning run** | Assets are staged and hash-verified on the host. Guest placement is [prepared]: [Install-S00Prerequisites.ps1](scripts/guest/Install-S00Prerequisites.ps1) |
| Record artifact versions/hashes | **Completed** | Deliverable 2 |
| Record existing App Installer / native runtime inventory | **Pending provisioning run** | The capture is in `Get-S00Inventory.ps1` (`appx`, `appxProvisioned`, `executionAliases`, `PwshRuntime`) |
| Record exact native dependency closure | **Completed** [source] | [02 § closure](02-prerequisites.md#module-native-dependency-closure-source). Runtime LoadLibrary/COM activation is not visible statically (mapping #9) |
| No runtime module download, App Installer repair, user alias or profile dependency | **Incomplete** | Holds by construction and [source]: absolute psd1 import, `-NoProfile`, no `Repair-WinGetPackageManager`, inbox-only imports. Not yet observed at runtime |
| Freeze source identity, manifests, URLs/hashes | **Completed** | Deliverable 3, first half. The live hash check does not prove the frozen manifest was used; see [04 § Drift detection](04-worker-mapping.md#drift-detection) |
| Agreement evidence / operator preapproval | **Pending capture** (A-3, re-capture in attempt 7) | A-1 and A-2 approved. A-3 was captured in provisioning attempt 6 (none presented); with that checkpoint replaced, attempt 7 re-captures it for the operator's decision |
| Dependency admission | **Completed** (approved) | A-2 approved 2026-10-28T20:19Z. Both installers declare no dependencies [source], and `-SkipDependencies` is mapped |
| Enforce installer type, source and version | **Completed** [source] | Mapping #1-#4. `-InstallerType` is a strict filter; this is the only field that excludes the 7-Zip x64 MSI |
| PuTTY: machine MSI registration + exe version | **Completed** [prepared] | Verifier exists and its mechanics are smoke-tested on the host. Not run in a guest |
| 7-Zip: machine uninstall registration + exe version | **Completed** [prepared] | Expected `26.01` read from the hash-verified payload [source]. Not run in a guest |
| Provider installed-list never sufficient | **Completed** [prepared] | Observe call is recorded only; [03](03-package-profiles.md) |
| Provisioning/update/reboot, both packages absent, shut down, checkpoint | **Pending provisioning run** (attempt 7; attempt 6's checkpoint is being replaced) | [05 § 1](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint), driven by [Invoke-S00Provisioning.ps1](scripts/host/Invoke-S00Provisioning.ps1) [prepared]. D-9 approved and revised: the checkpoint is created as the root and only checkpoint, and S00 scripts delete or merge nothing. Attempt 1 (2026-10-28T20:38Z) stopped at `Start-VM` for lack of host memory, before any guest change. Attempt 2 (2026-10-28T21:07Z) stopped when the guest rejected the credential; no guest change, no checkpoint. Attempt 3 (2026-10-28T21:21Z) stopped at the baseline inventory on a 5.1 parameter error in `Get-S00Inventory.ps1`, since fixed; staging removed, no checkpoint. Attempt 4 (2026-10-28T21:27Z) installed the worker and set the policy pin, then stopped on a 5.1 parameter error in `Set-S00EvidenceAcl.ps1`, since fixed; those changes remain, no checkpoint. `Install-S00Prerequisites.ps1` now accepts an existing install only if it matches the pinned archives by SHA256 (5.1 test on the host: fresh, re-run and tampered cases). Attempt 5 (2026-10-28T21:37Z) passed every step and gate except the source-agreements capture (`not-observable`: STA), so no checkpoint was taken; the capture task now starts pwsh with `-MTA`, and the script exits 1 when no checkpoint was taken. Attempt 6 (2026-10-28T21:43Z) passed every step and gate (baseline 26H2 build 26300, PuTTY and 7-Zip `Absent`) and took `prerequisites-ready` (`fc9b536f-885d-48ef-bf1a-15f6e5adfea0`) as the root and only checkpoint ([guest evidence](evidence/guest/provisioning-20261028T214228Z/provisioning.log), [host captures](evidence/host/attempt-6-20261028T2142Z/README.md)). Its fixture holds the pre-fix `Get-S00Inventory.ps1` and `Get-S00SessionProof.ps1`; the operator approved replacing it ([01 § Replacement](01-environment-inventory.md#replacement-of-attempt-6s-checkpoint-approved-2026-10-28)) |
| Restore between independent cases | **Pending restore verification** | Written in [05 § 2](05-run-reset-procedure.md#2-reset-between-every-case); [Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1) [prepared] restores `prerequisites-ready` twice with a canary between and checks the guest. First run (2026-10-28T21:44Z) stopped at the D-9 check before any change: the test unrolled a one-item checkpoint list and counted the dictionary's three keys; fixed and tested offline. Second run (2026-10-28T21:50Z) passed the D-9 check, restores 1 and 2, the canary written then absent, and the fixture (24 files, 0 bad, hash list equal to the provisioning export) [runtime], then stopped at the guest inventory: With no user signed in, `quser.exe` writes "No User exists for *" to stderr, and Windows PowerShell 5.1 under `Stop` turned the redirected stderr into a terminating `NativeCommandError`; provisioning passed because a user was signed in. Fixed in the repo and tested in 5.1 against the old and new versions; the checkpoint's fixture holds the old copy, so the operator approved replacing the checkpoint and the restore runs again on attempt 7's. The VM was left off at the checkpoint and the checkpoint list was unchanged. Not yet passed: the inventory gate and PuTTY and 7-Zip `Absent` after restore are unexercised. S00 is not complete until it passes |
| SYSTEM/admin-only ACLs, redaction, cleanup | **Incomplete** | ACL script [prepared], redaction rules in [05 § 3](05-run-reset-procedure.md#3-redaction-and-evidence-rules). Host evidence is redacted ([evidence/host/README.md](evidence/host/README.md)); nothing applied in a guest |
| Arm, reboot, run under SCM LocalSystem in Session 0, no signed-in or disconnected users | **Not in S00** | Procedure and proof script [prepared]. The S01 host is deliberately not built |
| Collect evidence via hypervisor/separate route, no target login | **Completed** as [prepared] (route decided) | Q-3: PowerShell Direct after CaseEnd. The proof script exports Security/System `.evtx` up to CaseEnd, so the PowerShell Direct logon itself falls outside the case window |
| Typed worker mapping; a missing mandatory mapping is a blocker | **Completed** [source] | Every mandatory concern has a row. Reboot detection is mapped by return code (Q-1); B-3 is closed |
| Gate A (baseline Windows 11 Pro 26H2 x64 under BC-1) | **Open**, UNPROVEN | Not exercised. When exercised it covers 26H2 only; 25H2 compatibility is not established by this bundle |
| FM-39 fixtures not built | **Completed** | No fixture, test source or test installer exists in this branch |

## What was not tested

- Anything inside the `Windows11-Test` guest. Only its host-side configuration and KVP data were read, by the operator's elevated session.
- `Install-S00Prerequisites`, `Set-S00PolicyPins`, `Set-S00EvidenceAcl`, `Get-S00Inventory`, `Get-S00SessionProof` (including its `.evtx` export), `Get-S00SourceAgreements` (including the internal `PackageManagerWrapper` route and the SYSTEM scheduled task), and both verifiers with `-Expect Present`. All parse cleanly under Windows PowerShell 5.1 and pwsh 7, but none has run in a guest.
- `Set-S00VmPreparation.ps1`, `Invoke-S00Provisioning.ps1` and `Test-S00Restore.ps1`; checkpoint creation and restore. `Get-S00VmConfig.ps1` was run by the operator; this run's account cannot reach Hyper-V.
- `New-S00Vm.ps1` (not used).
- Importing Microsoft.WinGet.Client under SYSTEM, in-proc activation, source open/update, any `Find-`/`Install-WinGetPackage` call.
- Any installation of PuTTY or 7-Zip.
- WinGet's resolution from the CDN index. The drift check compares GitHub only.
- The signed-in media portals. Only the public download page was read; BC-1 made them unnecessary.
- Installing Windows from the admitted ISO. Only its hash and `install.wim` metadata were checked on the host.
- Any 25H2 guest. BC-1 moved the baseline to 26H2.

## Deviations from approved decisions (FM-01 to FM-39)

FM-01 to FM-39 are unchanged. Deviations from the handoff:

| Id | Handoff | Now | Approved |
|---|---|---|---|
| BC-1 | Target VM is Windows 11 Pro 25H2 x64 | Windows 11 Pro 26H2 x64 (build 26300). Gate A and all S00/S01 evidence speak for 26H2 only; 25H2 compatibility is not established | Operator, 2026-10-28 |
| D-9 | FM-38: one 80 GB dynamic VHDX, `prerequisites-ready` the only checkpoint | Existing `Windows11-Test`: one 127 GiB dynamic VHDX; the earlier automatic checkpoint was removed and merged by Hyper-V during preparation; `prerequisites-ready` is the root and only checkpoint; S00 scripts delete or merge none; attempt 6's checkpoint is replaced by the operator's decision ([01 § D-9](01-environment-inventory.md#d-9-existing-127-gib-disk-approved-2026-10-28-revised-the-same-day)) | Operator, 2026-10-28; revised and replacement approved the same day |

The handoff's 25H2 references stay as written.

## Discrepancies with the approved inputs (recorded, not acted on)

1. **`RebootRequired` is never populated** in module 1.29.380 (mapping #14). Reboot detection uses the installer return code against the applicable manifest mappings (Q-1); Windows pending-reboot markers are supplementary.
2. **The brief's claim that the module "lacks a source selector" is refuted.** `-Source` exists on every relevant cmdlet (mapping #1). No decision depends on the claim.
3. **Two .NET runtimes.** pwsh 7.6.6 embeds .NET 10.0.12; the host targets 10.0.14. They run in separate processes. Accepted subject to V-1 (Q-4).
4. **`AcceptPackageAgreements` defaults to true** in the module. The service must gate agreements itself before calling it (FM-26; mapping #10).
5. **Installer URL and SHA256 are not exposed** by the module (mapping #16). WinGet's own hash check proves only that the payload matches the manifest it **resolved**, not that the resolved manifest is the frozen one. The drift controls in [04](04-worker-mapping.md#drift-detection) narrow the gap; a window remains where the CDN index can diverge after the pre-arm check.
6. **Official 25H2 media is no longer offered anonymously.** The public page serves 26H2 only. Resolved by BC-1 (26H2 baseline), not by finding 25H2 media.

## Carried forward to S02 (not S00 work)

| Id | Issue | What S02 must validate |
|---|---|---|
| S02-V1 | Cancelling a worker (stopping or killing the pwsh process) does not stop an installer it has already started. The installer can keep running, finish, or leave a partial install, and a reboot or a later case can pick that up (mapping #18) | Cancel during a running install; record whether the installer process survives, its final exit code and the machine state afterwards; define the service's response (wait, reap or quarantine) before S02 acceptance |

This issue is recorded only. S00 does not implement or test it.

## Blockers

| Id | Blocker | Effect | Resolution |
|---|---|---|---|
| B-1 | This run's host account is not an administrator and not in Hyper-V Administrators. `Get-VM` is denied to it | This run cannot inspect, change, checkpoint or restore the VM | **Resolved for the operator's elevated session, 2026-10-28**: `Get-VM` works there, and every host Hyper-V script runs there ([01 § Operator procedure](01-environment-inventory.md#operator-procedure-prepare-windows11-test)). No workaround was attempted |
| B-2 | ~~No installation media with verifiable Microsoft provenance~~ | — | **Closed 2026-10-28.** BC-1 approved; the operator's 26H2 ISO matches Microsoft's published SHA256 exactly ([01 § Admitted media](01-environment-inventory.md#admitted-media-runtime)) |
| B-3 | ~~Conditional on Q-1 being rejected~~ | — | **Closed.** Q-1 decided |

## S01 readiness

**S01 is NOT READY.**

1. S00 is not complete: restore verification ([Test-S00Restore.ps1](scripts/host/Test-S00Restore.ps1)) has not passed, and the `prerequisites-ready` fixture holds the pre-fix `Get-S00Inventory.ps1` and `Get-S00SessionProof.ps1`, which fail with no user signed in, as S01 runs. The operator approved replacing it; attempt 7 and a new restore run are pending.
2. A-3 is to be re-captured in attempt 7, then decided. A-1, A-2 and D-9 are approved.
3. S01 is not authorized.

The remaining S00 work: the operator runs [01 § Operator procedure](01-environment-inventory.md#operator-procedure-prepare-windows11-test) steps 2-7 (which carry out [05 § 1](05-run-reset-procedure.md#1-provisioning-once-before-the-checkpoint) and the restore verification in [05 § 2](05-run-reset-procedure.md#2-reset-between-every-case)); the guest evidence is committed under `evidence/guest/`; A-3 is filled from the capture and the operator decides it; this checklist is updated. No VM is created, and no checkpoint is deleted or merged except the operator's approved removal of attempt 6's `prerequisites-ready` ([01 § Replacement](01-environment-inventory.md#replacement-of-attempt-6s-checkpoint-approved-2026-10-28)).
