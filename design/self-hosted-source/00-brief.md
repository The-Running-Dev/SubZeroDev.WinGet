# Brief — A self-hosted, owner-controlled WinGet source

> **DRAFT — awaiting maintainer approval.** A brief is authored or explicitly approved by the
> maintainer, never originated by a model. This draft transcribes decisions the maintainer made on
> 2026-08-29, each recorded in `design/90-decisions.md` with its rejected alternatives. Approving
> the pull request that introduces this file is the explicit approval
> `design/proposals/self-hosted-repository/handoff-to-opus.md` requires. **Nothing downstream of it
> — design, contract, slices, implementation — may begin until that approval lands.**

This is a **second, parallel design sequence**. It does not amend, renumber, or weaken the active
v0.2 sequence in `design/00-brief.md` and its siblings, and it is authored by hand rather than
through the kit commands, because those commands hard-code the `design/*.md` paths and would
destroy the v0.2 documents. See the 2026-08-29 decision *The parallel design sequence is
hand-authored* for why, and what that costs.

## Problem

Distributing software to my own Windows machines currently has no owner-controlled path. Today the
only options are the public `winget` and `msstore` sources, which cannot carry anything of mine, or
manual installer copying, which has no version metadata, no upgrade path, and no integrity check.
Observably:

- First-party installers I build have no discoverable home. Getting one onto a machine is a manual
  file transfer, and getting a *newer* one onto that machine is the same transfer again, with
  nothing recording which version is installed or that a newer one exists.
- Public packages I depend on are only as available as their upstream. A package pulled, renamed, or
  re-signed upstream breaks the machines that expected it, with no local copy to fall back to.
- The published guidance for a private WinGet source assumes Azure Functions and Cosmos DB. That is
  a recurring cost and an operational surface out of proportion to a single maintainer's needs.

The result: I cannot install my own software the same way I install everything else, and I cannot
insulate the packages I depend on from upstream changes.

## Who it is for

Me, as the maintainer, and only me — my own Windows machines, enrolled in my own Entra tenant. This
is personal infrastructure, not a product, not multi-tenant, and not something a third party is
expected to deploy. That framing is what licenses several of the non-goals below.

It is **not** for consumers of the `SubZeroDev.WinGet` library. Nothing here changes what they get.

## Non-goals

The binding list. Everything here is out of scope for every agent, permanently, until this file
changes.

- **Any change to the `SubZeroDev.WinGet` public API surface.** Specifically, the library does not
  gain `AuthenticationArguments`, `AuthenticationMode`, or any other Entra ID surface under this
  brief. The consequence is accepted and stated: the library cannot connect to this source. See
  *Definition of done*, which requires that limitation be documented rather than hidden.
- **Any change to the service/client/COM layering**, or to the library's build, packaging, or
  release pipeline.
- **Owning, vendoring, forking, wrapping, or re-implementing `rewinged`.** It is an external,
  independently deployed runtime, pinned by release. Its bugs are upstream, not this repository's.
- **Serving packages from the library.** The repository holds declarative manifests, deployment
  configuration, operations documentation, and validation scripts. No service source, no runtime
  behaviour.
- **Installer binaries or private keys in Git.** Manifests are reviewable, versioned metadata and
  belong in Git. Binaries and secrets do not, under any justification.
- **Anything in the v0.2 sequence.** `design/00-brief.md`, `10-design.md`, `20-contract.md`,
  `30-slices.md`, the S-series slice numbering, and the tracker mirror in `design/state/` are
  untouched by this work. A slice here never carries an `S<n>` id.
- **Multi-tenant, multi-user, or third-party-deployable operation.** No tenant isolation, no
  role model, no supportability commitment to anyone else.
- **`winget configure`/DSC, `PackageManagerSettings`, and TLS certificate pinning** — already out of
  scope per `SPECIFICATION.md` §10, restated here so it is not relitigated in a new context.
- **A CI-hosted or cloud-hosted feed.** The host is owned hardware. Eliminating cloud cost is a
  premise of this brief, not an open question.

## Definition of done

Reaching all five steps of the deployment progression in
`design/proposals/self-hosted-repository/context.md` — operational and published, not merely proven
locally. Every item below is *recorded evidence*, not an assertion:

- A **pinned** `rewinged` release runs on the home server, its release artifact verified against
  upstream, with a rollback copy of the previous pinned release preserved and its restoration
  proven at least once.
- The source is registered **explicitly**, so it cannot silently compete with `winget` or `msstore`
  during ordinary commands, and that non-competition is demonstrated rather than assumed.
- Every manifest passes `winget validate`. Every installer carries a SHA-256 hash in its manifest,
  verified against the served artifact. Code signatures are verified wherever the installer format
  and publisher make them available, and the cases where they do not are listed.
- Both package classes are present and installable: at least one first-party/private installer, and
  at least one curated mirror of a public package.
- `winget.exe` on a **disposable** Windows client — Windows Sandbox or equivalent, never the
  maintainer workstation — completes source registration, refresh, search, show, install, upgrade,
  and removal against the explicit source, and the run is recorded.
- The endpoint is reachable over public HTTPS with a **publicly trusted** certificate, and
  Microsoft Entra ID authentication is enforced. An unauthenticated request is **refused**, and that
  refusal is recorded — a source that has never rejected anything is not known to be guarded.
- Failure behaviour is recorded for each of: an expired or untrusted certificate, a missing
  artifact, a stopped service, and an unauthorised request.
- Recovery is **proven, not designed**: a first-party installer is restored from the offsite backup,
  a manifest revision is rolled back, and a clean client installs successfully after the restore.
- The library's inability to connect to this source is stated in `docs/` as a known limitation with
  its reason, so a reader cannot mistake absence for oversight.
- Certificate renewal has either executed once unattended or has a recorded, exercised procedure —
  an unrenewed certificate is the most likely way this source silently dies.

## Environment

Single maintainer. The host is a home server or NAS on a residential connection; the certificate is
issued over ACME DNS-01, because inbound port 80 is not assumed to be available. Clients are my own
Windows machines, enrolled in my own Microsoft Entra tenant, plus a disposable Windows Sandbox
client used for every validation that would otherwise mutate a real machine.

Primary development happens on macOS. Unlike the library, most of this work — manifests, deployment
configuration, documentation — is platform-neutral and authorable there; only the client-side
validation requires Windows, and it requires a *disposable* Windows client specifically.

`rewinged` is pre-1.0 by its own statement, so its configuration, output, and behaviour may change
between releases. Every adoption pins a release, verifies its artifact, validates the upgrade on
the disposable client before the host, and keeps the previous release recoverable.

## Lifespan

Maintained for years, as personal infrastructure that other machines depend on for software they
cannot otherwise get. It is not published, not supported for anyone else, and carries no
compatibility promise — but it is expected to keep working unattended, which is why certificate
renewal, backup restoration, and rollback are definition-of-done items rather than later
improvements.
