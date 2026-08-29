# Context — self-hosted WinGet repository

## Purpose

Provide a personal, owner-controlled WinGet source that can be proven locally before
being made available from a remote host. The source must support normal WinGet discovery,
show, installation, and upgrade flows without Azure Functions or Cosmos DB.

## Established constraints

- The existing product is a C# client library for the WinGet COM API. It does not own a
  CLI, GUI, or a package-feed server.
- Existing public library interfaces and the service/client/COM dependency direction stay
  unchanged unless a future approved contract explicitly says otherwise.
- The source must be registered explicitly so it cannot silently compete with built-in
  WinGet sources during ordinary commands.
- WinGet REST sources require HTTPS. Local validation may use a machine-trusted,
  development-only certificate; remote clients require a certificate they trust.
- Package manifests are reviewable, versioned metadata and belong in Git. Installer
  binaries and private keys do not.
- Each installer must retain a SHA-256 hash in its manifest. Code-signing verification is
  required where the installer format and publisher make it available.
- The remote host, DNS, firewall, certificate renewal, backups, operating-system
  patching, and service monitoring are operational responsibilities. Eliminating cloud
  service costs does not eliminate those responsibilities.

## Candidate runtime

`rewinged` is the current candidate. It is a MIT-licensed, self-hosted WinGet REST
server that reads ordinary WinGet YAML manifests from a directory, can serve installers
from local storage, and has no application-database dependency. It can terminate HTTPS
itself or sit behind a reverse proxy. It is pre-1.0, so any production adoption must pin
a release, verify its release artifact, validate upgrades locally, and preserve a
rollback copy.

The candidate is external to this repository. Its behavior, release cadence, and security
properties require independent verification before a final decision.

## Intended deployment progression

1. Run the pinned runtime on the maintainer workstation with a local HTTPS certificate.
2. Register an explicit local source and prove it with a non-production pilot package on
   a disposable Windows client.
3. Deploy the same pinned runtime and manifest revision to an always-on home server, NAS,
   or low-cost VPS.
4. Place the remote endpoint behind HTTPS, favoring VPN-only access for a personal source.
5. Publish only after backup, restoration, certificate-renewal, access-boundary, and
   clean-client installation checks are recorded.

## Evidence

- WinGet source types and explicit-source behavior: Microsoft Learn,
  <https://learn.microsoft.com/windows/package-manager/winget/source>.
- Candidate runtime and configuration: rewinged,
  <https://github.com/jantari/rewinged>.
- WinGet manifest requirements and validation: Microsoft Learn,
  <https://learn.microsoft.com/windows/package-manager/package/manifest>.
