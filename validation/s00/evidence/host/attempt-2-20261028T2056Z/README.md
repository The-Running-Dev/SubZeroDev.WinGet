# Attempt 2 host captures (2026-10-28)

[Get-S00VmConfig.ps1](../../../scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session, around provisioning attempt 2 of [01 § Operator procedure](../../../01-environment-inventory.md#operator-procedure-prepare-windows11-test). Their `checks` were computed by the script version committed in `cffbeca`.

| File | capturedUtc | Procedure step | Notes |
|---|---|---|---|
| `vm-config.before-provisioning.json` | 2026-10-28T20:56:45.7860997Z | 5, before provisioning | VM off. Every line `OK` except the three guest KVP lines and `Heartbeat`, as expected with the VM off. Both D-9 lines `OK`: base VHDX alone, no checkpoints |
| `vm-config.after-checkpoint.json` | 2026-10-28T21:07:40.0388455Z | 6, after provisioning attempt 2 | Named after the step only: no checkpoint was taken, because [attempt 2](../../guest/provisioning-20261028T205700Z/provisioning.log) stopped when the guest rejected the credential. VM running; every line `OK`, including the KVP lines (EditionId 48, 10.0.26300, x64) and `Heartbeat` (`OkApplicationsUnknown`). The layout is unchanged: base VHDX alone, no checkpoints |

No content was changed. No redaction was needed: the script writes no host or guest machine name, FQDN or IP address.
