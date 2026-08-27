# Handoff — Opus design session

## Task

Turn this preliminary proposal into a maintainer-approved, implementation-ready design
only after resolving the open decisions below. Preserve the existing library contract:
this work must not add public library APIs or make the C# client library own a feed-server
runtime by implication.

## Required reading

1. `CLAUDE.md`, especially Project identity, Source of truth, Hard rules, Single ownership,
   and Git and delivery.
2. `agent.md`.
3. `design/00-brief.md`, `design/10-design.md`, `design/20-contract.md`,
   `design/30-slices.md`, and `design/90-decisions.md`.
4. This proposal's `README.md` and `context.md`.

## Required outcome

- Ask the maintainer to author or explicitly approve a new brief before writing a binding
  design. Do not overwrite the existing v0.2 brief.
- Decide whether this is:
  - deployment/configuration documentation and assets in this repository while rewinged
    stays external; or
  - a first-party hosted-service capability requiring an explicit product and contract
    amendment.
- If the first option is chosen, define the minimal repository boundary: declarative
  manifests, deployment configuration, operational documentation, and validation
  scripts only. Keep service source and runtime behavior external.
- Record each approved durable choice in `design/90-decisions.md` with the rejected
  alternatives and reversibility.
- Produce a new, separate binding design sequence without renumbering or weakening the
  active v0.2 slices.

## Decisions requiring maintainer input

1. Remote host: home server/NAS, a VPS, or another existing always-on machine.
2. Exposure model: VPN-only, private LAN, or public HTTPS.
3. Client trust: internal certificate authority, publicly trusted certificate, or both.
4. Package scope: only first-party/private installers, curated mirrors of public
   packages, or both.
5. Authentication: network boundary alone, Microsoft Entra ID, or another supported
   mechanism. Do not promise an authentication path until it is validated with the
   target WinGet client.
6. Artifact retention, backup destination, and recovery objective.

## Design questions to resolve with evidence

- Does the candidate server support the WinGet client version used by this project for
  search, show, install, and upgrade against multi-file manifests?
- How are authenticated REST-source requests performed by this WinGet client, and does
  that match the candidate's implementation?
- Can the target host expose the service over trusted HTTPS without opening a wider
  network boundary than the maintainer accepts?
- How will manifests and installer binaries be promoted together, rolled back, and
  restored without committing secrets or binaries to Git?
- Which end-to-end tests can run in Windows Sandbox or another disposable client without
  mutating the maintainer workstation?

## Non-negotiable validation

- Validate each manifest with `winget validate`.
- Test source registration, refresh, search, show, install, upgrade, and removal against
  the explicit custom source.
- Verify installer hashes and available code signatures before publication.
- Verify failure behavior for expired or untrusted certificates, missing artifacts, a
  stopped service, and unauthorized access where authentication is enabled.
- Prove backup restoration and rollback before treating remote publication as operational.

## Stop conditions

- Stop for a brief and contract amendment if the proposed work adds a public library
  interface, changes client/COM layering, or makes the library responsible for serving
  packages.
- Stop if the selected remote host, exposure model, certificate authority, or
  authentication method is not maintainer-approved.
- Stop if the candidate runtime lacks required WinGet client compatibility or its
  licensing/security posture is unsuitable.
