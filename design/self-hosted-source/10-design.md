# Design — A self-hosted, owner-controlled WinGet source

Designed against `design/self-hosted-source/00-brief.md`. This is the second, parallel sequence that
brief describes; it does not amend, renumber, or weaken the v0.2 sequence in `design/00-brief.md` and
its siblings, and it is hand-authored for the reason recorded on 2026-08-29 in
`design/90-decisions.md`.

The runtime is external and pinned. Its behaviour is not designed here and its bugs are upstream.
What is designed is the **publication system around it**: two stores that must agree but cannot hold
each other's content — a reviewable one in Git and an unreviewable one on the host — the order in
which they may be changed, and the evidence that licenses the claim that any of it works.

That last clause is why this is a design rather than a runbook. Every one of the brief's
definition-of-done items is of the form *X is proven and the run is recorded*. The object being
designed is therefore a feed **plus the record that it was observed working**, and the expensive
decisions are about where that record comes from and what invalidates it — not about which daemon
serves YAML.

Three external facts were verified before designing against them, in `winget-cli` at `5c88b96f`:

- A source may be registered **`Explicit`**, in which case operations that do not name it never
  consult it (`src/AppInstallerRepositoryCore/Public/winget/RepositorySource.h:157`,
  `src/AppInstallerRepositoryCore/RepositorySource.cpp:547,563`). This is a first-class client
  property, not a usage discipline, and the built-in font source already uses it
  (`src/AppInstallerRepositoryCore/SourceList.cpp:412`).
- Entra ID tokens are acquired through the Web Account Manager against a **fixed, Microsoft-owned
  client id**, authority `organizations`, for the resource the source advertises at `/information`,
  and are sent as a bearer token
  (`src/AppInstallerCommonCore/Authentication/WebAccountManagerAuthenticator.cpp:20,21,153`,
  `src/AppInstallerCommonCore/Authentication/Authentication.cpp:283`). The interactive path creates
  and foregrounds a window (`Authentication/Authentication.cpp:154,166`); the silent path requires an
  account already cached for that client id.
- The `/information` response is cached client-side **only as the server's `Cache-Control` directs**,
  and the parsed default when that header is absent is immediate expiry
  (`src/AppInstallerRepositoryCore/Rest/RestInformationCache.cpp`,
  `src/AppInstallerCommonCore/Public/AppInstallerDownloader.h:122-137`).

## Data model

Nothing here introduces a database, a service, or a generated index. The model is a discipline over
Git objects, bytes on a host, and recorded runs.

The WinGet manifest schema is external and is not restated. This section states only what this system
adds to it or constrains about it.

### Package

- **Identity** — WinGet's own `(PackageIdentifier, PackageVersion)`. This system does not mint a
  second identifier, and a mirrored package keeps the upstream identifier verbatim.
- **Provenance** — `first-party` or `mirror`. **Derived** from the manifest's position in the
  manifest tree: one subtree per provenance class, and membership is the whole of the fact. It is not
  a manifest field and not a sidecar record.
- **Ownership** — the repository owns the manifest. The artifact store owns the bytes the manifest
  points at. Neither owns the other.
- **Lifecycle** — *proposed* on a branch; *published* once merged, projected to the host, and
  observed served; *superseded* when a newer version publishes, which does not remove it; *withdrawn*
  when its manifest is removed and only then its bytes.
- **Persistence** — manifests in Git, permanently. Bytes never in Git, under any justification.

Provenance is derived rather than declared because both candidate declarations are worse: a custom
manifest field fails `winget validate`, which is the gate that makes a manifest trustworthy at all,
and a sidecar index is a second copy of something the tree already states — and the copy is the half
that rots.

### Artifact

- **Identity** — the SHA-256 of the bytes. Not the URL, not the filename, not the version.
- **Fields** — hash; size; and either a verified code signature or a recorded statement that the
  installer format and publisher make none available. The brief requires the second case be *listed*,
  so an unsigned artifact is a recorded fact rather than a silence.
- **Ownership** — the host serves every artifact. One offsite backup holds first-party bytes only.
- **Derivation direction** — nothing about an artifact is derived from its manifest. The direction is
  the reverse: the manifest records a hash *measured from* the artifact. A check comparing the
  recorded hash against the **served** bytes is the only thing that turns that record into evidence;
  comparing it against the file the manifest author had is comparing a claim with itself.
- **Recoverability** — first-party bytes are not re-obtainable and are backed up before the manifest
  referencing them merges. Mirrored bytes are re-obtainable and are deliberately not backed up;
  upstream is their backup, as decided on 2026-08-29.

### Source configuration

- **Identity** — the endpoint URL. It is the one identifier every client is configured against, which
  is why the exposure decision was recorded as expensive to reverse.
- **Repository half** — the pinned runtime release identity and the verification of its artifact, the
  runtime configuration, the HTTPS termination arrangement, the certificate acquisition parameters,
  and the Entra resource the endpoint advertises. All declarative, all reviewable, all in Git.
- **Host half** — the certificate private key, the running process, and the Entra registration in the
  tenant. None of it in Git; all of it named, so its absence is visible.
- **Derived, and by whom** — what a client believes about this source is derived from `/information`
  at request time, not from what the repository says. The design keeps the client's default immediate
  expiry by not advertising a positive `max-age` on that response, so a change to the advertised
  authentication configuration or supported versions takes effect on each client's next request
  rather than at an unknown later moment.

### Evidence, Gate, Environment

Not redefined. `design/10-design.md` §§ *Evidence*, *Gate*, *Environment* own the identity
`(gate, environment, commit)`, the binary-outcome rule for a gate, and the rule that evidence
licenses only the bounded environment it records. A second definition of *evidence* in one repository
is the duplication `AGENTS.md` § *Single ownership* forbids, and the shared decision log already
establishes that these two sequences share what can be shared.

This sequence adds environments that one does not have, and their observed facts are what make their
evidence time-bounded:

- **The host** — the pinned runtime release, the certificate's issuer and remaining validity, and the
  served manifest commit. Evidence recorded against one pinned release does not license the next, for
  exactly the reason runner-image drift does not license a later image.
- **A client** — whether it is Entra-joined, whether a WAM account is cached for the fixed client id,
  whether the session is interactive, and whether this source is registered `Explicit`. A run on a
  client with a cached account is not evidence about a client without one, which is why the refusal
  check below is required to run from an environment that has none.

### Claim

The brief requires exactly one consumer-facing statement: that `SubZeroDev.WinGet` cannot connect to
this source, with its reason, so absence is not mistaken for oversight. Under `design/10-design.md`'s
Claim model that statement's strength is `contract-checked` — it rests on the IDL declaring no
authentication type the library exposes, not on an execution. It is owned by this sequence's own
documentation rather than by the v0.2 canonical claim map, as decided on 2026-08-30; the cost of that
choice is that no existing gate polices it, so it is policed by this sequence's own check or not at
all.

## Module boundaries

1. **Manifest store** (repository) — owns declarative package metadata and, by position, provenance.
   Depends on nothing. Exposes a validated manifest set at a commit.
2. **Deployment configuration** (repository) — owns the pinned runtime release identity, its runtime
   configuration, HTTPS termination, and certificate acquisition. Depends on the manifest store only
   by naming what is served. Exposes a reproducible host.
3. **Validation scripts** (repository) — own the checks and the shape of the record each writes.
   Depend on 1, 2, and — for host-dependent checks — a reachable endpoint. **Nothing depends on
   them**; they observe and record.
4. **Operations documentation** (repository) — owns procedures: promote, withdraw, roll back, restore,
   renew. Depends on 1–3 by reference. Nothing depends on it at build time; the maintainer depends on
   it at three in the morning, which is the requirement it is written against.
5. **Artifact store** (host, plus one offsite backup for first-party bytes) — owns installer bytes
   keyed by hash. Depends on nothing in the repository.
6. **The runtime** (external, pinned) — depends on a projection of 1 and on 5. Owned upstream. Not
   vendored, forked, or wrapped.
7. **The client** (`winget.exe`, external) — depends on 6 over HTTPS and on Microsoft Entra ID.

The direction is `repository → host projection → runtime → client`, with validation reading across
every stage and nothing reading back. The graph is acyclic.

The acyclicity that matters most is negative: **`SubZeroDev.WinGet` appears in no edge.** No module
here depends on the library, and the library depends on none of them. That is what makes the brief's
first non-goal a structural property rather than a promise someone has to keep remembering.

The second joint worth naming is between the manifest store and the artifact store. They share
nothing but a hash written into a manifest, and that join is *verified by a check* rather than
maintained by a mechanism. Any design that let one mutate the other would put installer bytes within
reach of Git, which the brief forbids outright.

## Control flow

**A first-party package is published.** Triggered by a new build of the maintainer's own software.
The artifact is measured — hash, and either a verified signature or a recorded absence — then written
to the artifact store *and* to the offsite backup before anything merges. A manifest is authored
recording the measured hash. Manifest validation runs as a repository gate. On merge, the host's
served manifest set advances to the merged commit in one step, and the runtime's view of it is
**observed** rather than assumed. A disposable client then installs from the explicitly named source,
and that run is recorded.

**A public package is mirrored.** Triggered by a decision to insulate against upstream. The upstream
installer is downloaded, measured the same way, and written to the artifact store and deliberately
not to the backup. The manifest is authored under the **upstream identifier**, recording the measured
hash and this source's own installer location. Every step downstream is identical to the first-party
path; the only difference in the whole flow is which store counts as the backup.

**A client installs or upgrades.** Triggered by the maintainer, naming the source. The client
requests `/information`, learns the authentication type and resource, acquires a WAM bearer token for
the fixed client id, and re-requests with it. Search, show, install, and upgrade then resolve against
this source alone.

Two consequences of that path are load-bearing, and are stated here rather than discovered later:

- Because the source is `Explicit`, an unqualified `winget upgrade` **never** consults it. The mirror
  is a fallback the maintainer invokes deliberately, not one that arrives on its own. That is the
  price of the non-competition the brief requires, and it is accepted.
- Because tokens come from WAM, an interactive acquisition needs an interactive desktop session and a
  silent one needs an account already cached for the fixed client id. Neither is available to a
  process running as SYSTEM, so unattended upgrade from this source is not something this design
  provides.

**A certificate approaches expiry.** Triggered by a clock, unattended, over ACME DNS-01. The brief
names this the most likely way the source silently dies, so the design makes the *observable* the
remaining validity of the certificate **as served**, not the exit status of the renewal job. A
renewal that reports success proves that it ran; only the served certificate proves that it worked.

## Failure modes

**The host is unreachable.** Residential connection, power, DNS, or the process. Detected by any
client operation and by the endpoint check. There is no second host and nothing fails over. The user
sees a source failure from `winget.exe`. Nothing partial is left behind, and no cached `/information`
masks it — one of the reasons that cache is left disabled.

**The certificate is expired or untrusted.** Detected ahead of time by the served-validity check, and
after the fact by every client at once. TLS fails before any request, so no partial state exists. The
brief requires this behaviour be provoked and recorded once, deliberately, rather than first observed
in production.

**A manifest references an artifact that is not served.** Detected by checking the hash against bytes
retrieved from the **served** location. This is precisely the failure the publish ordering exists to
prevent, and the check exists because an ordering enforced by a human is one that will eventually be
violated.

**Served bytes do not match the recorded hash.** Detected by the same check, and independently by
`winget.exe`, which refuses the install. The system does not repair it. Recomputing the hash into the
manifest would convert an integrity failure into a silent acceptance, so a mismatch is investigated
as either a corrupted store or an unrecorded replacement.

**The runtime's view is stale after the projection advances.** Detected only by asking the source
what it now serves — which is why the publish path ends at an observation. Whether the pinned release
watches its directory or must be restarted is a property of that release, not a decision of this
design; the observation is identical either way, and specifying the observation rather than the
mechanism is what keeps this design from depending on behaviour it has not verified.

**A runtime upgrade breaks the source.** Detected on the disposable client, which is why the brief
sequences it before the host. Rollback is the retained previous pinned release. The manifest store
and artifact store are untouched by a runtime rollback, and that separation is what makes the
rollback safe to perform under pressure.

**Authentication fails.** Three causes with three different actions, and the design keeps them
distinguishable. *No cached account and no interactive session* is a client-environment fact. *The
tenant has not admitted the fixed client id for the advertised resource* is host configuration. *The
advertised resource is wrong or has changed* is also host configuration, and is the one the disabled
information cache makes correctable within one request rather than one cache lifetime.

**An unauthenticated request is not refused.** The brief states the standard: a source that has never
rejected anything is not known to be guarded. Detected only by making the request from an environment
with no cached account — a check run from a signed-in session proves nothing, because it never asked
the question. This is the same discipline as `AGENTS.md` § *Verification* on validators: the check is
not trusted until removing the guard makes it fail.

**The offsite backup is unrestorable.** Detected only by restoring, which is why the brief makes a
proven restore a definition-of-done item rather than a later improvement. A restore returning bytes
that match no manifest is a recovered artifact with no package — visible, and much the better of the
two possible half-states.

**Upstream pulls or re-signs a mirrored package.** Not a failure of this system; it is the case the
mirror exists for. The served copy keeps working. What fails is re-obtaining it, and that is the
accepted, recorded cost of not backing mirrors up.

**A manifest is invalid.** Detected by the repository gate before merge. Nothing is left behind
except an already-promoted artifact that no manifest references, which is inert — that direction of
leak is the deliberate consequence of the publish ordering, and the opposite direction would be a
broken package on a live source.

**The library is asked to use this source.** It cannot, and this is a design-time fact rather than a
runtime one: no authentication type the COM API declares is exposed on the library's public surface,
as recorded on 2026-08-29. A consumer that registers this source through the library fails at the
first authenticated request. The brief requires this be documented; this design requires the
documentation carry the *reason*, because an unexplained limitation reads as a bug and gets reported
as one.

## Concurrency and ordering

Almost nothing here is concurrent, and what enforces that is that there is one maintainer and one
host — a fact about the deployment, not a mechanism. So what follows names the orderings that are
real, and what breaks if that fact ever stops holding.

**Publish is ordered and never reordered**: bytes to the artifact store, and to the backup if
first-party → manifest merged → served set advanced → refresh observed. Every step is safe to repeat.
None is safe to reorder: an artifact with no manifest is invisible, while a manifest with no artifact
is a broken package on a live source.

**Withdraw is the exact inverse**: manifest removed, served set advanced, refresh observed — and only
then the bytes. A client mid-download during a withdrawal gets a failed download rather than a
corrupted install, because the integrity check belongs to the client.

**A runtime upgrade is ordered disposable-client-first**, with the previous pinned release retained
until the host is observed working.

**The served manifest set must advance atomically.** Client operations are concurrent with everything
else, and a client reading during a file-by-file synchronisation would see a set that never existed
as a commit. The resulting failures are intermittent and unreproducible, which is the worst possible
class of failure for a system whose entire output is recorded evidence. *How* the advance is made
atomic is a deployment-configuration choice and is not decided here; *that* it must be is decided
here.

**Certificate renewal is the one unattended actor** and can overlap any of the above. It must not
require the runtime to stop. If the pinned release cannot take a renewed certificate without a
restart, then renewal is a scheduled brief outage — acceptable, but only if it is stated in the
operations documentation rather than discovered as an unexplained nightly failure.

Inside the repository nothing is concurrent: one branch, one pull request, one merge.

## Alternatives considered

The five choices the brief already settled — host, exposure, certificate authority, package scope,
and authentication — were decided on 2026-08-29 with their rejected alternatives, and are not
reopened here.

**Mirror identity.** *Chosen:* the upstream `PackageIdentifier`, verbatim. *Rejected:* a namespaced
identifier, because `winget upgrade` matches an installed package by its identifier — a namespaced
mirror could not serve the machine that already has the package, which is the exact scenario the
brief's problem statement describes. *Rejected:* a suffix on the version, for the same reason and
because it corrupts version ordering as well. The identifier collision that namespacing would have
avoided is instead removed by `Explicit` registration, which is an enforced client property rather
than a convention.

**Provenance representation.** *Chosen:* derived from position in the manifest tree. *Rejected:* a
custom manifest field, because `winget validate` rejects unknown fields and it would fail the gate
that makes manifests trustworthy. *Rejected:* a sidecar index, because it duplicates a fact the tree
already carries and the duplicate is the copy that rots. *Rejected:* inferring from the publisher
name, because a mirror of the maintainer's own software published elsewhere would be misclassified
and provenance would become a guess. *Named fallback:* if the pinned release turns out to require a
layout that cannot express two subtrees, provenance moves to a sidecar index and the second rejection
is accepted as a cost — this design does not assume a layout it has not verified.

**Where checks run.** *Chosen:* manifest validation as a repository gate in CI; every host-dependent
check maintainer-invoked, writing a dated record. *Rejected:* running host checks from CI, because
the endpoint is Entra-guarded, so a hosted runner would need a credential in the tenant — new
authentication surface and a stored secret, in order to reach a home server on a residential
connection. *Rejected:* no CI at all, because manifest invalidity is the most common and cheapest
failure and detecting it needs no host. *Rejected:* an external monitoring service polling the
endpoint, because it reintroduces the recurring third-party cost the brief exists to eliminate.

**Client-side information caching.** *Chosen:* advertise no positive `max-age` on `/information`,
keeping the client's default immediate expiry. *Rejected:* a long max-age, because an authentication
or version change would then take effect at an unknown later time, differently per client, and a
client could hold a working view of a source that has been reconfigured. *Rejected:* depending on
`no-store` specifically — the client honours it, but relying on a header the pinned release may not
emit is a weaker guarantee than relying on the absence of one.

**Projection mechanism.** *Chosen:* the served manifest set advances from one commit to another as a
single step. *Rejected:* synchronising files in place, because a client reading mid-sync sees a mixed
set and fails intermittently. *Rejected:* serving directly from a working clone, because it makes the
host's served state a mutable thing with no name, and "which commit is served" stops being a question
that has an answer — which would remove the one field that makes host evidence time-boundable.

**Evidence vocabulary.** *Chosen:* reuse `design/10-design.md`'s Claim, Evidence, Gate and
Environment. *Rejected:* a self-contained vocabulary for this sequence, because two definitions of
*evidence* in one repository is exactly the duplication that guarantees a divergence nobody notices.
*Rejected:* no vocabulary, recording runs as prose, because the brief's definition of done is a list
of recorded observations, and prose is where a record goes to be forgotten.

## Open questions

Two are empirical stop conditions — verifiable, but only by executing, and the answer changes the
slice list rather than the design's shape. One is a maintainer question this design cannot answer.

**Empirical: does WAM interactive authentication complete inside Windows Sandbox?** The brief
requires a *disposable* client to complete registration, refresh, search, show, install, upgrade and
removal against the Entra-guarded source. A sandbox is not Entra-joined and discards its WAM state
each run, and the interactive path creates and foregrounds a window. If it cannot authenticate there,
the brief's disposable-client requirement and its authentication requirement are in tension, and one
gives: either the disposable client becomes a throwaway virtual machine that can hold an account, or
the end-to-end flow is validated against an unguarded staging endpoint and the guard is proven
separately. Slicing should not begin until this is observed, because the two resolutions produce
different slices.

**Empirical: will the tenant admit the fixed client id for a custom resource?** WinGet requests a
token for a Microsoft-owned client id against whatever resource the source advertises. Whether a
single-maintainer tenant can consent that client id to an app registration it owns, and what that
registration must look like, is verifiable and is not verified here. If it cannot, the Entra ID
decision of 2026-08-29 is not reachable by this client and returns to the maintainer.

**Maintainer: is there a recovery objective in time, or is "it can be restored at all" the whole
requirement?** The 2026-08-29 decision settled the backup destination and the split by provenance,
but the third part of that handoff item — the recovery objective — is unstated. It determines how
often a restore must be re-proven and whether backup latency matters. If the answer is "whenever I
notice", saying so retires the question.
