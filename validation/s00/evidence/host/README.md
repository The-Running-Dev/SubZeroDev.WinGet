# Host evidence

Host observations only, captured on the validation host. None of this is in-guest evidence.

| File | Captured by | Notes |
|---|---|---|
| `host-inventory.txt`, `host-verifiers.txt`, `verifier-smoke.txt`, `manifest-live-drift.txt`, `module-*`, `putty-msi-tables.txt`, `7zip-exe-version.txt` | This run's non-elevated session | See [02](../../02-prerequisites.md) and [06](../../06-acceptance-checklist.md) |
| `vm-config.json` | [Get-S00VmConfig.ps1](../../scripts/host/Get-S00VmConfig.ps1), in the operator's elevated session, 2026-10-28T19:01:44Z | Host-side configuration of `Windows11-Test` and the guest's KVP OS facts, before preparation. Host and guest machine names, the guest FQDN and IP addresses are never written. The capturing script version flattened its `checks` array; `checks` was recomputed offline from the file's own `vm` and `guestKvp` fields with the corrected logic (`checksNote`). No captured field was changed |
| [`attempt-1-20261028T2036Z/`](attempt-1-20261028T2036Z/README.md) | The same script, 2026-10-28T20:36:57Z-20:38:46Z | The three captures of the first run: before preparation, after preparation, after provisioning attempt 1. They record the D-9 incident (the automatic checkpoint removed and merged by Hyper-V) |
| [`attempt-2-20261028T2056Z/`](attempt-2-20261028T2056Z/README.md) | The same script, 2026-10-28T20:56:45Z-21:07:40Z | The two captures around provisioning attempt 2, which stopped when the guest rejected the credential. No checkpoint; layout unchanged |
| [`attempt-3-20261028T2120Z/`](attempt-3-20261028T2120Z/README.md) | The same script, 2026-10-28T21:20:44Z-21:21:32Z | The two captures around provisioning attempt 3, which stopped at the baseline inventory. No checkpoint; layout unchanged |
| [`attempt-4-20261028T2126Z/`](attempt-4-20261028T2126Z/README.md) | The same script, 2026-10-28T21:26:02Z | The capture before provisioning attempt 4, which stopped at `Set-S00EvidenceAcl.ps1`. No checkpoint |
| [`attempt-5-20261028T2136Z/`](attempt-5-20261028T2136Z/README.md) | The same script, 2026-10-28T21:36:32Z | The capture before provisioning attempt 5, which stopped at the source-agreements capture (`not-observable`: STA). No checkpoint |
| [`attempt-6-20261028T2142Z/`](attempt-6-20261028T2142Z/README.md) | The same script, 2026-10-28T21:42:18Z-21:43:52Z | The two captures around provisioning attempt 6, which took `prerequisites-ready` as the root and only checkpoint |
| `vm-config.before-provisioning.json` (when present) | The same script, before the next [Invoke-S00Provisioning.ps1](../../scripts/host/Invoke-S00Provisioning.ps1) run | Same format. Must show no checkpoints and the base VHDX alone |
| `vm-config.after-checkpoint.json` (when present) | The same script, after [Invoke-S00Provisioning.ps1](../../scripts/host/Invoke-S00Provisioning.ps1) took `prerequisites-ready` | Same format. Records the D-9 layout with `prerequisites-ready` as the root and only checkpoint |

Redactions applied under [05 § 3](../../05-run-reset-procedure.md#3-redaction-and-evidence-rules), 2026-10-28:

| File | Line | Replacement |
|---|---|---|
| `host-inventory.txt` | 23 (`Get-VM` error text) | host machine name → `<redacted:host>` |
| `host-verifiers.txt` | 1 (run header) | host machine name → `<redacted:host>` |
| `verifier-smoke.txt` | 2 (run header) | host machine name → `<redacted:host>` |

`vm-config.json` needed no redaction: the script omits the names, and the VM name `Windows11-Test` and its path are not personal data. No other content was changed.
