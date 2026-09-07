# Contract — A self-hosted, owner-controlled WinGet source

Derived from [`10-design.md`](10-design.md), which is designed against
[`00-brief.md`](00-brief.md). This is the second, parallel sequence those documents describe; it does
not amend, renumber, or weaken the v0.2 sequence in `design/00-brief.md` and its siblings, and it is
hand-authored rather than produced by `/contract`, for the reason recorded on 2026-08-29 in
`design/90-decisions.md` under *The parallel design sequence is hand-authored*.

**Nothing in this sequence is implemented.** No manifest, deployment configuration, validation script,
or operations document exists in the tree yet. Every enforcement below is therefore stated as the
enforcement a slice must materialise, and the honesty rule the v0.2 contract established applies with
more force here than there: **no row may be trusted until the slice that materialises it has landed
and said so.** A row marked `authored` is held by prose and review alone; a row marked `planned` names
a check nobody has written. There are no `enforced` rows in this document today, and a future revision
that has not moved one there has moved none.

## Identifier space

Ids in this sequence carry an `F` prefix — `FC<n>` for a contract invariant here, `FS<n>` for a slice
in `30-slices.md` when it exists. The brief already forbids an `S<n>` slice id in this sequence; the
same collision exists for contract rows, because `C1`–`C28` are live in `design/20-contract.md` and
are referenced from the tracker, from `build/Test-Documentation.ps1`'s configuration, and from test
names. A bare `C29` here would be indistinguishable from the next v0.2 row by grep, by an issue body,
and by a reader. Ids are never renumbered and never reused; a row struck by a later decision leaves a
gap.

## Invariants

| Id | Assertion | Owner | Enforcement |
|---|---|---|---|
| FC1 | No module in this sequence depends on `SubZeroDev.WinGet`, and the library depends on none of them. Nothing here adds public library API, alters the service/client/COM dependency direction, or changes the library's build, packaging, or release pipeline. | Repository boundary | `planned` — a repository check asserting no reference in either direction between this sequence's directories and the library's projects. Today `authored` only: the property holds because no such file exists yet, which is not the same as being defended. |
| FC2 | The runtime is external and adopted by a pinned release identity. This repository does not vendor, fork, wrap, or re-implement it, and contains no runtime source. A release's artifact is verified against upstream before adoption, and the previously pinned release stays recoverable until the host is observed working on the new one. | Deployment configuration | `planned` — the deployment configuration names one pinned release and its artifact verification; a check asserts the pin is a release identity rather than a floating tag. Artifact verification and rollback retention are procedural and `authored`. |
| FC3 | Installer binaries and private keys never enter Git, under any justification. Manifests always do, permanently. | Manifest store | `planned` — a repository gate rejecting binary installer payloads and key material on the diff. This is a brief non-goal, so the gate blocks rather than warns. |
| FC4 | A package's identity is WinGet's own `(PackageIdentifier, PackageVersion)`. This system mints no second identifier, and a mirrored package carries the upstream `PackageIdentifier` verbatim — no namespace, no prefix, no version suffix. | Manifest store | `planned` — a manifest-tree check comparing each mirrored manifest's identifier against the upstream identifier it records as its origin. The identifier collision this creates is answered by FC18, not by renaming. |
| FC5 | Provenance is `first-party` or `mirror`, derived solely from the manifest's position in the manifest tree — one subtree per class, membership being the whole of the fact. It is not a manifest field and not a sidecar record. | Manifest store | `planned` — the tree layout is the mechanism; a check asserts every manifest sits under exactly one provenance subtree. **Named fallback:** if the pinned release requires a layout that cannot express two subtrees, provenance moves to a sidecar index and the duplication cost is accepted — recorded as a decision, never taken silently. |
| FC6 | An artifact is identified by the SHA-256 of its bytes — not its URL, filename, or version. A manifest records a hash *measured from* the artifact; nothing about an artifact is derived from its manifest. Verification compares the recorded hash against bytes retrieved from the **served** location. Comparing it against the file the manifest author holds is comparing a claim with itself and licenses nothing. | Validation scripts | `planned` — the served-artifact hash check. Which location counts as served is the endpoint the manifest publishes, not a local path. |
| FC7 | Every installer carries a SHA-256 in its manifest. A code signature is verified wherever the installer format and publisher make one available; where they do not, that absence is a recorded statement rather than a silence, and the set of such cases is listed. | Validation scripts and operations documentation | `planned` — signature verification in the same check as FC6, with an explicit unsigned-artifact list as part of its record. |
| FC8 | First-party installer bytes reach the offsite backup **before** the manifest referencing them merges. Mirrored bytes are deliberately not backed up; upstream is their backup, and the cost of a pulled or re-signed upstream is accepted and recorded. | Publication procedure | `authored` — ordering held by the operations documentation and by review. A check can observe that a merged first-party manifest's artifact is present in the backup, but cannot observe that it arrived first; the ordering itself is procedural. |
| FC9 | There is no recovery objective in time. Restorability alone is the requirement: a restore is proven to work at least once, and nothing is owed about how long one takes. Retrieval latency is not a design constraint and must not be reintroduced as one. A backup destination with slow or archival retrieval is licensed. | Backup arrangement | `authored` — this is a constraint on what may later be *added*, so its enforcement is the review that rejects a reintroduced latency requirement, and the 2026-08-30 decision *Restorability is the whole recovery requirement* it would have to overturn. |
| FC10 | Publication is ordered and never reordered: bytes to the artifact store → to the offsite backup if first-party → manifest merged → served set advanced → refresh observed. Withdrawal is the exact inverse, bytes last. Every step is safe to repeat; none is safe to reorder. The accepted half-state is an artifact no manifest references, which is inert; the rejected one is a manifest with no artifact, which is a broken package on a live source. | Publication procedure | `authored` — the order is procedural and review-enforced. FC6's check detects the rejected half-state after the fact, which is a detection rather than a prevention, and the design says so. |
| FC11 | The served manifest set advances from one commit to another as a single step. No file-by-file synchronisation of a live served set, because a client reading mid-synchronisation sees a manifest set that never existed as a commit. *How* atomicity is achieved is a deployment-configuration choice this contract does not decide; *that* it is required is decided here. | Deployment configuration | `planned` — the mechanism is chosen by the slice that configures the host, and that slice states which property of its choice makes the advance atomic. No check can observe atomicity from outside; what a check can observe is FC12's served-commit answer. |
| FC12 | A manifest revision is rolled back by publishing an earlier state forward as a new commit, never by rewriting history or moving a branch backwards. *Which commit is served* therefore always has an answer, which is the field every piece of host evidence is bounded by. | Publication procedure | `authored` — held by procedure and by the repository's existing no-force-push rule. The served-commit field is asserted by the endpoint observation under FC13. |
| FC13 | A publication ends at an **observed** refresh — the source asked what it now serves — not at the projection advance. An assumed refresh licenses nothing. Whether the pinned release watches its directory or must be restarted is a property of that release, and the observation is identical either way. | Validation scripts | `planned` — the endpoint refresh observation, recording the served commit it read back. |
| FC14 | The host holds no source of truth. Its served state is a projection of a named repository commit plus the artifact store, and the runtime's own working state is disposable. The only host state that cannot be reconstructed from Git and the artifact store is the certificate private key and the Entra registration in the tenant; both are named so their absence is visible, and neither is in Git. | Deployment configuration | `authored` — held by the deployment configuration's shape and by review. Its falsification is any host state a rebuild cannot reproduce, which is found by rebuilding, not by a check. |
| FC15 | Claim, Evidence, Gate, and Environment are `design/10-design.md`'s definitions, reused and not redefined. Evidence is keyed `(Gate, Environment, Commit)`, a gate has a binary outcome, and evidence licenses only the bounded environment it records. This sequence adds three environments: the **host** (pinned runtime release, certificate issuer and remaining validity, served manifest commit), a **client** (Entra-joined or not, WAM account cached for the fixed client id or not, interactive or not, this source registered `Explicit` or not), and the **backup** (destination, and the restore procedure as written at the time of the proof). | Shared vocabulary | `authored` — reuse is a reference rather than a copy, so the only failure mode is a second definition appearing here, which review rejects. The environments' observed facts are recorded by the checks under FC17. |
| FC16 | Evidence recorded against one pinned runtime release, one certificate, one served commit, or one backup arrangement does not license another. A changed backup destination, a changed pinned release, or an edited restore procedure leaves the system with **no current restore evidence**, and that absence is visible without anyone remembering a schedule. No proof is re-run on a clock, because there is no recovery objective (FC9). | Shared vocabulary | `authored` — this is the licensing rule already adopted, applied to this sequence's environments. What makes it checkable is that each record names its environment's facts, which FC17's records are required to carry. |
| FC17 | Manifest validation runs as a repository gate in CI. Every host-dependent check is maintainer-invoked and writes a dated record naming its gate, environment facts, outcome, assertions, and explicit non-assertions. No credential for the maintainer's tenant is stored in CI, and no external service polls the endpoint. | Validation scripts | `planned` — the CI gate is one workflow job over the manifest tree; the host-dependent checks are scripts whose record shape is stated under *Types*. The negative half — no stored tenant credential, no third-party poller — is `authored`, and a check cannot prove an absence. |
| FC18 | The source is registered with WinGet's `Explicit` property, so an operation that does not name it never consults it. Non-competition is **demonstrated**, not assumed: an unqualified operation over a package this source carries, shown not to consult it, bounded to the client it ran on. The accepted consequence is that an unqualified `winget upgrade` never consults this source — the mirror is a deliberate fallback, not an automatic one. | Client registration and validation scripts | `planned` — the non-competition observation, which must name a package this source actually carries. A check that names a package the source does not carry is vacuous and licenses nothing (`design/10-design.md` § *Error semantics*, vacuous assertion). |
| FC19 | `/information` is served `no-store`. A positive `max-age` is forbidden in every case, because it would let a client hold a working view of a reconfigured or revoked source until a chosen later time, differently per client. **Named fallback:** if the pinned release and the HTTPS termination arrangement between them cannot set that header, the client's sixty-second default is accepted, stated in the operations documentation as the bound on how long a client may believe a stale source, and recorded — not discovered. Serving no header is not equivalent to `no-store`: an absent `Cache-Control` yields sixty seconds of caching, not none. | Deployment configuration | `planned` — a check reading the response headers the endpoint actually serves, which is the only thing that distinguishes the chosen state from its fallback. Configuration alone does not settle it, because the header may be stripped or added by the termination arrangement. |
| FC20 | An unauthenticated request is refused, and that refusal is observed **from an environment with no cached WAM account**. A check run from a signed-in session never asked the question and licenses nothing. A source that has never rejected anything is not known to be guarded. | Validation scripts | `planned` — the refusal check, whose environment record must state that no account was cached for the fixed client id. This is the discipline `AGENTS.md` § *Verification* states for validators: the check is not trusted until removing the guard makes it fail. |
| FC21 | Unattended use from this source is not provided. The WinGet client's authenticator refuses in its constructor when the process runs as SYSTEM, before any request is attempted, so a service or scheduled task in that context cannot reach this source at all. No host configuration makes it available, and no document may present unattended upgrade from this source as available or as a later improvement. | Operations documentation | `authored` — a documentation statement, review-enforced. It is stated as an invariant rather than a note because the failure it prevents is someone spending a slice on a capability the client refuses by construction. |
| FC22 | Exactly one consumer-facing statement records that `SubZeroDev.WinGet` cannot connect to this source, with its reason. Its strength is `contract-checked` — it rests on the COM IDL declaring no authentication type the library exposes, not on an execution. It is owned by this sequence's own documentation, outside the v0.2 canonical claim map. | This sequence's documentation | `planned` — policed by this sequence's own check or not at all. The v0.2 documentation gate does not cover it, and that cost was accepted in the 2026-08-30 decision *The library-limitation statement is owned by this sequence*; a slice that adds the statement without a check has left it unpoliced and must say so. |
| FC23 | Certificate evidence is the **remaining validity of the certificate as served**, never the exit status of the renewal job. A renewal that reports success proves only that it ran. Renewal must not require the runtime to stop; if the pinned release cannot take a renewed certificate without a restart, renewal is a scheduled brief outage stated in the operations documentation rather than discovered as an unexplained nightly failure. | Validation scripts and operations documentation | `planned` — the served-certificate validity check. This is the one path with no maintainer in it, which is why its evidence may not come from the actor performing it. |
| FC24 | Slicing does not begin until it is observed whether WAM interactive authentication completes inside the disposable client the brief requires. A negative observation does not license a weaker disposable client, a substituted validation, or a quiet redefinition of "disposable": it returns to the maintainer as a tension between the brief's disposable-client requirement and its authentication requirement, and one of them gives. | Stop condition | `authored` — a stop condition, satisfied by an observation recorded under FC15's client environment or not at all. Unsatisfied as at 2026-09-07. |
| FC25 | Slicing does not begin until it is observed whether the maintainer's tenant will admit the fixed Microsoft-owned client id for a resource the maintainer's own app registration advertises. A negative observation returns the 2026-08-29 Entra ID decision to the maintainer; it does not license anonymous public read, a custom-header shared secret, or any other substitute, each of which that decision already rejected on its own grounds. | Stop condition | `authored` — a stop condition, satisfied by an observation recorded under FC15's host environment or not at all. Unsatisfied as at 2026-09-07. |

FC24 and FC25 are the two stop conditions `10-design.md` § *Open questions* names. They gate
`30-slices.md`, not this document: a contract states what must hold, and both rows state what must
hold about *how the answer is taken* as much as about the answer. Writing them down now is what stops
a later session from resolving either by assumption, which is the failure both are most exposed to.

## Types

No runtime type is introduced anywhere in this sequence. Every entry below is a discipline over Git
objects, bytes on a host, and recorded runs.

### Package

`10-design.md` § *Data model* owns the fields. What this contract adds is the lifecycle's terminal
semantics: *published* requires all three of merged, projected, and **observed served** — a manifest
that is merged and projected but not yet observed is not published, and no record may describe it as
such. *Superseded* removes nothing. *Withdrawn* is manifest-first and bytes-last (FC10), so a
withdrawn package's bytes may briefly outlive its manifest, and that direction is deliberate.

### Artifact

Identity is the SHA-256 of the bytes (FC6). The recorded fields are hash, size, and either a verified
code signature or the recorded statement that none is available (FC7). An artifact has no name in this
system — the manifest names it, and a manifest is not part of its identity.

### Manifest tree

The layout is the sole carrier of provenance (FC5), which makes it contract-bearing rather than a
filing convenience. Two subtrees, one per provenance class; every manifest under exactly one. The
WinGet manifest schema itself is external and is not restated, constrained, or extended here — a
custom field would fail `winget validate`, which is the gate that makes a manifest trustworthy at all.

### Validation record

The written output of a maintainer-invoked check (FC17). It is an Evidence record in
`design/10-design.md`'s sense, keyed `(Gate, Environment, Commit)`, carrying `Outcome`, `Run`,
`Assertions`, and `NonAssertions`, with `Assertions` non-empty and both sets explicit. The `Commit` is
the repository commit the check ran against; where the check observes the host, the environment
additionally carries the **served** commit, which is a different field and may differ — that
difference is the whole point of FC13, so a record that conflates them licenses nothing.

Its serialised form is a slice-level choice. What is fixed here is that it is a file in the
repository, dated, and that its absence is as readable as its presence.

### Source configuration

Identity is the endpoint URL (`10-design.md` § *Data model*). The repository half is declarative and
in Git; the host half — private key, running process, Entra registration — is named but never in Git
(FC14). What a client believes is derived from `/information` at request time under FC19, never from
what the repository says.

### Claim, Evidence, Gate, Environment

Not defined here. `design/10-design.md` §§ *Claim*, *Evidence*, *Gate*, *Environment* own them, and
FC15 states the reuse. This sequence's only Claim is FC22's.

## Persisted schemas

| Store | Key | Required constraints | Existing-data and migration story |
|---|---|---|---|
| Manifest store (Git) | `(PackageIdentifier, PackageVersion)` | Valid against the external WinGet manifest schema; under exactly one provenance subtree (FC5); records a SHA-256 measured from the artifact (FC6); a mirror's identifier is the upstream one verbatim (FC4). No installer bytes, no key material (FC3). | None — the store does not exist yet. It is created empty and grows one manifest at a time; there is no import of existing manual installer copies, and none is planned. |
| Artifact store (host) | SHA-256 of the bytes | Serves every artifact the manifest store references. Bytes are immutable under their hash; a replacement is a new artifact with a new hash and a new manifest version, never an overwrite. | None. Manually copied installers predating this system are not migrated in; a package enters by being published through FC10's ordering or not at all. |
| Offsite backup | SHA-256 of the bytes | First-party bytes only (FC8), written before the referencing manifest merges. Mirrored bytes deliberately absent — their absence is the decision, not a gap. Restorable at least once, with no objective in time (FC9). | None. A first-party installer lost before this store exists is not recoverable, which the 2026-08-29 decision already records as the cost of the split. |
| Host projection | Served commit | One named repository commit at a time, advanced atomically (FC11), rolled forward never backward (FC12), and the advance observed (FC13). Reconstructible in full from Git plus the artifact store (FC14). | None. The host is rebuildable by construction, so there is no prior host state to carry forward. |
| Validation records | `(Gate, Environment, Commit)` | Shape per *Types* § *Validation record*. Retired by its environment moving rather than by age (FC16). | None. No historical observation of this system exists, and none may be constructed retrospectively from memory. |
| Irreplaceable host state | Named, not stored | The certificate private key and the Entra registration. Named in the deployment configuration so their absence is visible; never in Git (FC14). | None. If either is lost, it is reissued or re-registered — which is a new environment under FC16, retiring the evidence bounded by the old one. |

There is no database, generated index, sidecar provenance record, or evidence manifest in scope.
Adding one is a design change, and in the sidecar's case it is specifically the named fallback under
FC5 rather than a free choice.

## Public surface

### Library

**None.** This is the sequence's first non-goal and FC1's assertion, and it is stated here as a surface
because the absence is the commitment: `SubZeroDev.WinGet` gains no `AuthenticationArguments`, no
`AuthenticationMode`, no Entra ID surface, and no new member of any kind under this contract. A
consumer of the library sees no difference whatsoever.

The accepted consequence is that the library cannot connect to this source, and a consumer that
registers it through the library fails at the first authenticated request. FC22 requires that be
stated with its reason, because an unexplained limitation reads as a bug and gets reported as one.

### Repository assets

The surface this sequence does add is a directory of declarative assets, and the parts of it that are
contract-bearing rather than incidental are: the **manifest tree layout**, because it carries
provenance (FC5); the **pinned runtime release identity**, because FC2 and FC16 both key on it; the
**stable invocation names of the validation scripts**, because a record naming a check that has been
renamed is a record nobody can re-run; and the **record shape** under *Types*. Everything else —
internal script structure, documentation prose, configuration file organisation — is free.

### Endpoint

The endpoint's protocol surface is the WinGet REST source API, which is external and owned upstream.
This contract commits to exactly two properties of it, both of which are deployment obligations rather
than protocol extensions: `/information` carries `no-store` or the recorded fallback (FC19), and
unauthenticated requests are refused (FC20).

## Error semantics

Thirteen failure modes are named in `10-design.md` § *Failure modes*. They are stated here as outcomes
with a required action, because for several of them the wrong action is the one that looks like a fix.

### Publication and stores

| Variant | Occurs when | Retryable | Required action |
|---|---|---|---|
| Invalid manifest | The repository gate rejects a manifest before merge. | Yes, on a corrected manifest. | Fail the gate. Nothing is left behind but an already-promoted artifact no manifest references, which is inert and is the accepted direction of leak under FC10. |
| Manifest references an unserved artifact | A merged manifest's recorded hash names bytes the served location does not return. | No without a corrected publication. | Investigate as a violated publish ordering (FC10). Do not publish the manifest again unchanged, and do not remove the manifest quietly — the ordering failure is the finding. |
| Served bytes do not match the recorded hash | FC6's check, or `winget.exe` itself, finds the served bytes hashing to something other than the manifest's record. | No. | Investigate as a corrupted store or an unrecorded replacement. **Never recompute the hash into the manifest** — that converts an integrity failure into a silent acceptance. |
| Offsite backup is unrestorable | A restore attempt fails, or returns bytes matching no manifest. | Depends on the cause. | Record no restore evidence for that backup environment. Bytes matching no manifest are a recovered artifact with no package: visible, and the better of the two half-states. With no recovery objective (FC9), taking a long time is not a failure; being unable to do it at all is the only one. |
| Upstream pulls or re-signs a mirrored package | An upstream package disappears or changes signature. | Not applicable — it is not a failure of this system. | None. The served copy keeps working; this is the case the mirror exists for. What is lost is re-obtainability, which is the accepted, recorded cost of not backing mirrors up (FC8). |

### Host and runtime

| Variant | Occurs when | Retryable | Required action |
|---|---|---|---|
| Host unreachable | Connection, power, DNS, or a stopped process. | Yes, once the host returns. | Record the failed precondition and no licensed claim. There is no second host and nothing fails over; the client sees a source failure and nothing partial is left behind. The brief requires a stopped service be provoked and recorded deliberately rather than first met in production. |
| Certificate expired or untrusted | FC23's served-validity check ahead of time, or every client at once after the fact. | Yes, on a renewed or re-trusted certificate. | TLS fails before any request, so no partial state exists. Provoke and record this once deliberately; a renewal job's success is not evidence that the served certificate is valid. |
| Stale runtime view after a projection advance | The served set was advanced but the runtime still answers with the previous manifest set. | Yes. | Detected only by FC13's observation. The publication is not complete and must not be recorded as such. Do not infer the refresh from the advance. |
| Runtime upgrade breaks the source | A newly pinned release fails on the disposable client, or on the host. | Yes, on the retained previous release. | Roll back to the retained pinned release. The manifest and artifact stores are untouched by a runtime rollback, and that separation is what makes it safe to perform under pressure. A rollback copy nobody has ever restored is a rollback plan, not a rollback (FC2). |

### Client and authentication

| Variant | Occurs when | Retryable | Required action |
|---|---|---|---|
| No cached account and no interactive session | A client with no WAM account for the fixed client id, in a non-interactive session. | Yes, from an interactive session, or once an account is cached. | A client-environment fact, not a host misconfiguration. Record it as the environment it is, and do not reconfigure the host in response. |
| Running as SYSTEM | Any process in the SYSTEM context. | No, anywhere, ever. | Nothing. The client's authenticator refuses in its constructor before issuing a request. This is not fixable at the host, the tenant, or the source, and FC21 forbids presenting it as a later improvement. |
| Tenant has not admitted the fixed client id | The tenant refuses consent for the Microsoft-owned client id against the advertised resource. | Yes, once consented. | Host and tenant configuration. If it proves impossible rather than unconfigured, FC25 fires and the Entra decision returns to the maintainer — it does not license a substitute. |
| Advertised resource wrong or changed | The endpoint advertises a resource the tenant does not match. | Yes. | Host configuration. This is the failure `no-store` makes correctable within one request rather than one cache lifetime, which is FC19's whole justification. |
| Unauthenticated request is **not** refused | The refusal check, run from an environment with no cached account, receives a response instead of a rejection. | No. | Treat as a guard failure, not a check failure. Stop publishing to the source until refused. A check that ran from a signed-in session and passed proves nothing and must be re-run from an unauthenticated environment (FC20). |
| Source stops being explicit | A re-registration, restored source list, or hand-edited configuration drops the `Explicit` flag. | Yes, per client. | Silent by construction — the failure is a command that *succeeds against the wrong source*, and a mirror carrying an upstream identifier verbatim is exactly the package that resolves from the wrong place. Detected only by FC18's demonstration, and bounded to the client it ran on. |
| Library asked to use this source | A consumer registers this source through `SubZeroDev.WinGet`. | No. | It fails at the first authenticated request. This is a design-time fact, not a runtime defect: the COM API declares no authentication type the library exposes. FC22's statement carries the reason so it is not reported as a bug. |

### Checks themselves

| Variant | Occurs when | Retryable | Required action |
|---|---|---|---|
| Vacuous assertion | A check's assertion passes over an empty input set — no package carried, no artifact served, no client registered. | Yes, in an environment with a witness. | Record no evidence for that assertion and make the missing witness visible. FC18 is the row most exposed to this, since a non-competition demonstration over a package this source does not carry passes trivially. |
| Evidence from a stale environment | A record is cited whose environment facts no longer describe the system — a changed pinned release, certificate, backup destination, or restore procedure. | Not applicable. | Treat the system as having **no** current evidence for that assertion (FC16). Do not narrow the citation to the part that still holds; the licensing rule is over the environment, not over the sentence. |

## Unresolved

Two, and both are `10-design.md` § *Open questions* carried forward as FC24 and FC25 rather than
restated: whether WAM interactive authentication completes inside the disposable client, and whether
the tenant will admit the fixed client id for a maintainer-owned resource. Neither is answerable by
reading anything; both require executing.

FC24 and FC25 state that a negative answer to either escalates past the slice list — to a tension
between two of the brief's own requirements in the first case, and to the maintainer in the second.
That reading was adjudicated on 2026-09-07 against a looser summary sentence in `10-design.md`, which
was corrected in the same change; the decision is logged, so the escalation is not something a later
session can narrow by rereading the design.
