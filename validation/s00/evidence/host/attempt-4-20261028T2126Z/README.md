# Attempt 4 host capture (2026-10-28)

[Get-S00VmConfig.ps1](../../../scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session, before provisioning attempt 4 of [01 § Operator procedure](../../../01-environment-inventory.md#operator-procedure-prepare-windows11-test). Its `checks` were computed by the script version committed in `2104ead`.

| File | capturedUtc | Procedure step | Notes |
|---|---|---|---|
| `vm-config.before-provisioning.json` | 2026-10-28T21:26:02.2548035Z | 5, before provisioning | VM running. All 19 checks `OK`, including both D-9 lines: base VHDX alone, no checkpoints |

No after-checkpoint capture was taken: [attempt 4](../../guest/provisioning-20261028T212612Z/provisioning.log) stopped at `Set-S00EvidenceAcl.ps1` and took no checkpoint.

No content was changed. No redaction was needed: the script writes no host or guest machine name, FQDN or IP address.
