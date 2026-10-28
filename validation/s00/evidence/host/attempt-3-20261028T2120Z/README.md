# Attempt 3 host captures (2026-10-28)

[Get-S00VmConfig.ps1](../../../scripts/host/Get-S00VmConfig.ps1) in the operator's elevated session, around provisioning attempt 3 of [01 § Operator procedure](../../../01-environment-inventory.md#operator-procedure-prepare-windows11-test). Their `checks` were computed by the script version committed in `261ea0f`.

Before this attempt the operator signed in to the guest through VMConnect and created a local administrator account with Computer Management (name redacted). The credential pre-check through PowerShell Direct then returned the account and `True` for the Administrators role.

| File | capturedUtc | Procedure step | Notes |
|---|---|---|---|
| `vm-config.before-provisioning.json` | 2026-10-28T21:20:44.0250242Z | 5, before provisioning | VM running. Every line `OK`, including the KVP lines and `Heartbeat`. Both D-9 lines `OK`: base VHDX alone, no checkpoints |
| `vm-config.after-checkpoint.json` | 2026-10-28T21:21:32.6323863Z | 6, after provisioning attempt 3 | Named after the step only: no checkpoint was taken, because [attempt 3](../../guest/provisioning-20261028T212057Z/provisioning.log) stopped at the baseline inventory. Every line `OK`; layout unchanged |

No content was changed. No redaction was needed: the script writes no host or guest machine name, FQDN or IP address.
