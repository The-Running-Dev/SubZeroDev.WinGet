# Approval records

Three records gate installation on the validation guest. **No installation, including S01, may run until all three show `"decision": "approved"`.** Only the operator decides. A decision the operator gives in chat is written into the record at the operator's instruction, quoting the operator's words, with `recordedBy` saying so; nobody decides on the operator's behalf.

| Record | Approves | Status |
|---|---|---|
| [package-agreements.none.json](package-agreements.none.json) | No package agreements are declared for PuTTY 0.85.0.0 or 7-Zip 26.01, bound to the frozen manifest blob ids | **approved** 2026-10-28T20:19Z by the operator (in chat; recorded at the operator's instruction) |
| [dependency-admission.json](dependency-admission.json) | Empty dependency admission for both profiles | **approved** 2026-10-28T20:19Z by the operator (in chat; recorded at the operator's instruction) |
| [source-agreements.approval.json](source-agreements.approval.json) | The `winget` source agreements captured as SYSTEM during guest preparation | **pending-approval**. Captured 2026-10-28T21:43:05Z as S-1-5-18 in Session 0 during provisioning attempt 6: none presented, digest `4F53CDA18C2BAA0C0354BB5F9A3ECBE5ED12AB4D8E11BA873C2F11161202B945` (the SHA256 of `[]`). Not approved. Superseded by the re-capture in provisioning attempt 7, after the operator approved replacing attempt 6's checkpoint |

## How to approve

In each record's `approval` block, set:

- `decision`: `"approved"` or `"rejected"`
- `approver`: your name or handle
- `decidedUtc`: ISO 8601 UTC time, for example `2026-10-29T09:00:00Z`
- `status`: `"approved"` or `"rejected"`
- `notes`: optional

For the source-agreements record, also copy `capture.digestSha256` into `approval.approvedDigestSha256`. Approval is bound to that digest only.

Commit the edited records. A record whose bound blob ids or digest change afterwards is no longer approved.
