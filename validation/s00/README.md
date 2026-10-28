# S00: validation fixture and prerequisites

Review bundle for slice S00. **S00 is not complete, and signed-out WinGet installation and Gate A are unproven.** Start with [06-acceptance-checklist.md](06-acceptance-checklist.md), which lists the approvals awaiting the operator.

| Doc | Bundle item |
|---|---|
| [01-environment-inventory.md](01-environment-inventory.md) | 1. Environment inventory, installation media and BC-1, blockers, unblock procedure |
| [02-prerequisites.md](02-prerequisites.md) | 2. Pinned prerequisites and native closure |
| [03-package-profiles.md](03-package-profiles.md) | 3. Package profiles, agreements, dependency admission, independent verification |
| [04-worker-mapping.md](04-worker-mapping.md) | 4. Typed worker mapping with `winget-cli` v1.29.380 citations |
| [05-run-reset-procedure.md](05-run-reset-procedure.md) | 5. Provisioning, checkpoint, reset, redaction, proposed S01 procedure |
| [06-acceptance-checklist.md](06-acceptance-checklist.md) | 6. Decisions recorded, approvals pending, acceptance status, S02 carry-over, blockers, S01 readiness |

Status labels used throughout:

| Label | Meaning |
|---|---|
| **[runtime]** | Observed on a machine. In S00 this is always the host, never the target |
| **[source]** | Concluded from manifests, installer metadata, binaries or source |
| **[prepared]** | Artifact or script exists and has not run in a guest |
| **[blocked]** | Could not be produced |

## Provenance

Gate A (S00-S02 and the package parts of S04-S08) moved to SubZeroDev.WinGet on 2026-10-29 ([decision](../../design/90-decisions.md)). This folder is a copy, without history, of `validation/s00` at commit `0b5cfda` of The-Running-Dev/SubZeroDev.FileManager PR #1 (branch `s00/validation-fixture`, which keeps the history). Only this section, the operator folder in [01 § Operator procedure](01-environment-inventory.md#operator-procedure-prepare-windows11-test), the guest-evidence row below and `.gitattributes` (moved from the FileManager root) were changed.

The guest paths (`%ProgramFiles%\SubZeroDev\FileManager\Service`, `%ProgramData%\SubZeroDev\FileManager\Validation`), the host staging root in the lock file and the evidence schema names still say FileManager. Guest evidence was captured under them [runtime], and the VM's guest still holds them. Renaming them is a separate decision, to be made before S01.

Gate A runs `Microsoft.WinGet.Client` 1.29.380 in a private pwsh worker. It does not use or change this repository's C# library, which stays pinned to interop 1.29.280.

## Layout

| Path | Contents |
|---|---|
| `prerequisites.lock.json` | Pinned assets with sizes, SHA256/SHA512 and sources |
| `profiles/` | Machine-readable S01 and S02 validation profiles |
| `manifests/winget-pkgs-aaaf44e17204/` | Frozen, byte-exact upstream manifests (`-text` in `.gitattributes`) |
| `agreements/` | Approval records for package agreements, dependency admission and source agreements, pending operator decision (see [agreements/README.md](agreements/README.md)) |
| `scripts/host/` | Host-side: VM configuration check, preparation and provisioning of `Windows11-Test`, staged-asset check, frozen/live manifest check (`New-S00Vm.ps1` kept, not used) |
| `scripts/guest/` | Guest-side: placement, policy, ACLs, inventory, source-agreement capture, verifiers, session proof. None executed in a guest |
| `evidence/host/` | Host observations only, host name redacted ([README](evidence/host/README.md)). `vm-config.json` holds the target VM's host-side configuration |
| `evidence/guest/` | Guest evidence from the provisioning and restore attempts ([README](evidence/guest/README.md)) |

Staged binaries live outside the repository, under the `stagingRoot` recorded in the lock file.
