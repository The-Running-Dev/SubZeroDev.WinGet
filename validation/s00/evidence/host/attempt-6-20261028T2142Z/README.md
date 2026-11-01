# Attempt 6 host captures (2026-10-28)

[Get-S00VmConfig.ps1](../../../scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session, around provisioning attempt 6 of [01 § Operator procedure](../../../01-environment-inventory.md#operator-procedure-prepare-windows11-test). Their `checks` were computed by the script version committed in `ead8bff`.

| File | capturedUtc | Procedure step | Notes |
|---|---|---|---|
| `vm-config.before-provisioning.json` | 2026-10-28T21:42:18.3372010Z | 5, before provisioning | VM running. All 19 checks `OK`, including both D-9 lines: base VHDX alone, no checkpoints |
| `vm-config.after-checkpoint.json` | 2026-10-28T21:43:52.2812093Z | 6, after [attempt 6](../../guest/provisioning-20261028T214228Z/provisioning.log) | VM off. The 15 VM-level checks `OK`. D-9 disk: a differencing disk, chain depth 2, on the base `.vhdx`. D-9 checkpoints: `prerequisites-ready` `fc9b536f-885d-48ef-bf1a-15f6e5adfea0` only, no parent. The three guest KVP lines and `Heartbeat OK` show `GAP`, as expected with the VM off |

No content was changed. No redaction was needed: the script writes no host or guest machine name, FQDN or IP address.
