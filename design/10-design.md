# Design — Prove the provisional claims and cut v0.1.0

Designed against `design/00-brief.md`. The product's runtime architecture is settled and is not
redesigned here; `CLAUDE.md` § *Architecture* and `SPECIFICATION.md` remain its description.

What is being designed is the thing the brief actually asks for: a **system that binds every
consumer-facing claim to an execution that licenses it**, so that "provisional" stops being a word
someone has to remember to update. The brief's problem statement is not "the tests are thin" — it is
"a consumer cannot tell which claims are proven and which are aspirational, and neither can I." That
is an evidence-plumbing problem, and it is solved by deciding what evidence exists, which environment
can produce it, which claim it licenses, and what fails when the link is missing.

Three of the brief's factual premises were contradicted by evidence gathered while designing. The
release-identity one changed a definition-of-done item and was put to the maintainer; the brief now
carries the answer. The rest are recorded in *Open questions*.

## Data model

Two models coexist. The **runtime** model — `SubZeroDev.WinGet/Models/` — is unchanged: no new public
API is a binding non-goal, and nothing here needs one. It is touched in exactly one place, and only
in *reachability*: `PackageOperationStatus.Cancelled` is presently unreachable (see *Failure modes*).

The **evidence** model below is new, and is mostly not a set of new files — it is a discipline over
artifacts that already exist. Its entities are few and concrete; the value is in the relation between
them, not in any framework.

### Claim

A statement in consumer-facing prose about what the library supports.

- **Identity** — its *subject*, not its wording. Four subjects exist today: consumer architecture
  support, the managed-assembly package shape, hosting context (elevation, service/SYSTEM), and
  operation coverage (which operations have been executed live).
- **Fields** — subject; the assertion; the strength of the assertion (`executed`, `contract-checked`,
  `unvalidated`); a reference to the Evidence that licenses it.
- **Ownership** — exactly one canonical statement per subject. Other documents reference it. This is
  `CLAUDE.md` § *Single ownership* applied to claims, and it is the property whose absence produced
  the present state: the same architecture claim is written five times across `README.md`,
  `docs/docs/getting-started.md`, `docs/docs/testing.md`, `docs/docs/architecture.md` and
  `SPECIFICATION.md`, and drifts independently.
- **Lifecycle** — authored with a strength; strength changes only when the Evidence edge changes.
  A claim never gains strength because someone believes it, and never silently loses it.
- **Derived** — strength is derived from whether an Evidence record exists and what Environment
  produced it. It is not independently authored. That is the whole mechanism.
- **Persisted** — as Markdown in the repository. `docs/docs/index.md` is generated from `README.md`
  and already drift-gated, so it is not a second copy; the other four are.

### Evidence

The record that a specific Gate ran to completion in a specific Environment at a specific commit.

- **Identity** — the triple (gate, environment, commit).
- **Fields** — outcome; the run that produced it; what was asserted; what was *not* asserted.
- **Ownership** — CI owns it. `design/90-decisions.md` may *reference* a run; it must never restate
  the result, because a restated result is a copy that cannot be re-derived and rots exactly like a
  claim does.
- **Lifecycle** — created by a run, and it **expires against a moving environment**. Evidence that
  the library works against WinGet 1.11 is not evidence that it works against 1.13. Expiry is the
  reason a live gate that runs once is worth less than one that runs repeatedly.
- **Persisted** — as CI run history and run artifacts. Not in prose.

### Gate

An executable check with a binary outcome and no human in the loop.

- **Identity** — its build-target name. The existing set is enumerated in `CLAUDE.md` § *Commands*.
- **Fields** — what it asserts; what it requires of its Environment; whether a failure blocks a merge.
- **Invariant** — *a gate that no workflow invokes does not exist.* The twelve `[Explicit]`
  integration tests are the proof: they are correct, they are maintained, and until the 2026-08-20
  spike no commit had ever run them anywhere but a developer's own machine.
- **Second invariant** — a gate must be able to *fail*. `Coverage` presently computes a number and
  reports it; a number nobody can fall below is a dashboard, not a gate.

### Environment

Where a Gate runs, and therefore which Claims its Evidence can license.

- **Identity** — a name. Three exist: the GitHub-hosted `windows-latest` x64 runner, the maintainer's
  Windows x64 machine, and the maintainer's macOS machine (on which nothing in this repository builds
  or runs).
- **Fields** — OS and architecture; whether WinGet is installed and at what version; whether the
  session is interactive; whether it is elevated; whether it has network reach to the remote catalog.
- **Load-bearing property** — an Environment **licenses a bounded set of claims and no more**. No
  ARM64 environment exists in the loop, so no evidence can license an ARM64 runtime claim, which is
  why the brief makes narrowing that claim a non-goal-protected instruction rather than a task.
- **Derived** — nothing. It is observed and recorded, never assumed. The spike recorded the runner's
  WinGet version, source list, session interactivity and CLSID registration state for precisely this
  reason: an Evidence record whose Environment was not captured cannot be interpreted later.

### Coverage floor

The one number the brief requires to exist.

- **Identity** — a single checked-in value, read by the `Coverage` gate.
- **Derived from** — a measurement, taken after the unit tests the brief calls for have landed, and
  set just below it. It is never chosen aspirationally; an aspirational floor is a gate that fails
  on unrelated work until someone lowers it, which trains everyone to lower it.
- **Lifecycle** — ratchets up only. It is raised in the same commit that shows the measurement
  justifying the raise. It is lowered only by an explicit decision-log entry.
- **Invariant** — **the floor is measured against a run that excludes the `[Explicit]` tests.** If
  live integration coverage were ever folded into the same report, the number would inflate without
  a single new unit test, and the gate would stop meaning what its name says. This is the constraint
  that decides where live tests may run relative to the coverage collector, not a preference.

### Release

- **Identity** — a git tag. The version is **derived from git history by GitVersion**, not from the
  project file; the project file's version governs only the manual NuGet.org path. These two sources
  disagreeing is not hypothetical — it is what produced the current release state (*Open questions*).
- **Lifecycle** — a tag is *published state*. `CLAUDE.md` forbids rewriting published history, which
  makes a tag a one-way door and makes the choice of version number an irreversible decision rather
  than a formality.

## Module boundaries

Three groups. Only the second and third change.

**Product layers** — service, client, COM owner/activation. Owns runtime behaviour; depends on the
WinRT projection; exposes the public surface. Unchanged, and described in `CLAUDE.md` §
*Architecture*. The dependency direction is service → client → COM owner → projection, and it is
already acyclic.

**Pure translation** — the enum mappers and DTO projections that today sit as private members inside
`WinGetClient` and `WinGetSourceClient`. The brief requires unit tests for them, and they are
untestable only because they are private, not because they need COM.

- Owns: total functions from projection values to public model values.
- Depends on: the projection's *enum and struct metadata only*. **It must not depend on
  `WinGetComContext` or `WinGetFactory`.** That is the boundary's entire purpose: loading the
  projection assembly to read an enum value activates nothing, whereas touching the factory
  activates an out-of-proc COM server. A unit test that has to activate WinGet is an integration
  test wearing the wrong attribute.
- Exposed to: the client layer, and the test assembly through the `InternalsVisibleTo` grant that
  already exists.
- Direction: translation ← client, translation ← tests. Nothing depends on the client from here, so
  it is acyclic. Whether this becomes its own type or stays in place with widened visibility is a
  slice-level call; the *boundary* — no path from a mapper to activation — is the design.

**Gates** — the build targets and the workflows that invoke them.

- Owns: assertions and their outcomes. Depends on build artifacts, never on another gate's side
  effects. Exposes: a binary result and a recorded artifact.
- The existing package-contract gate is **hermetic**: it builds packed consumers and asserts asset
  selection without executing a live COM call, so it runs anywhere a Windows SDK does. That property
  is worth more than the convenience of reusing it, so the live packed-consumer smoke test is a
  *separate* gate that reuses the consumer-construction step rather than an assertion bolted onto the
  hermetic one. Direction: consumer construction ← contract assertions, and consumer construction ←
  live smoke. Acyclic, and either can fail without implicating the other.

**Claims** — the prose surface. Depends on Evidence. Nothing depends on it. The documentation gate
already enforces one generated-file relationship and a terminology rule set, which is the shape a
claim-drift rule fits into; the claim surface is small enough that a rule over four files is
proportionate and a generated claims table is not.

## Control flow

**A pull request is opened.** The required check runs the hermetic chain: unit tests, coverage — now
with a floor that can fail — the cross-architecture PE assertions, and the package contract. Nothing
in this path touches a live COM server, a network catalog, or a package feed, so its failures are
always about the change under review. This is the path that must stay fast and deterministic, and
the reason live verification is not added to it.

**Live behaviour is checked.** A separate path activates the real COM server on a real Windows x64
machine: the twelve integration tests, and the packed-consumer smoke test that answers the one
question the hermetic package gate structurally cannot — whether a consumer that resolved the package
the way NuGet resolves it can make a COM call at all. Its outcome is Evidence, and its Environment is
recorded alongside it. Its trigger and its blocking status are the policy question in *Open questions*;
its shape is not affected by that answer.

**A release is cut.** A tag push derives a version from history, packs, and pushes to the package
feed; the manual dispatch path publishes to NuGet.org at the project file's version. Both are gated
on the required check. The ordering hazard here is that the version is derived from a ref that
already exists at the moment of the push — so the tag *is* the decision, and there is no later point
at which it can be reconsidered without rewriting published history.

## Failure modes

**WinGet COM activation.** Detected as an exception from every mode in the factory's chain, surfaced
as `WinGetUnavailableException`. The 2026-08-20 spike established that the chain succeeds on a hosted
runner where neither production CLSID is registered — meaning the fallback that the brief calls the
library's main resilience mechanism is now exercised, not merely present. State left behind: none;
activation failure leaves the owner context constructed but unusable, and the context deliberately
does not cache the failure, so a later call retries.

**A projected interface is unavailable while activation succeeds.** This is the failure shape behind
the spike's single failure, and it is the most consequential finding in this design.
`PackageManager.Version` is declared on `IPackageManager7`, contract version 13 (read from the pinned
projection metadata, 1.29.280). Reaching it requires a `QueryInterface` beyond the default interface
the factory activates against, whereas every call that passed on the runner is reachable through the
interfaces the activation already holds. The leading hypothesis is therefore that the property is
unreachable *through that activation mode*, not that WinGet lacks it — the runner reports
`v1.11.510`, far past contract 13.

It cannot presently be confirmed, because the accessor is wrapped in a catch-everything that returns
null. **The defect is not the null; it is that the null is unfalsifiable.** "The interface is not
implemented on this runtime" and "the call failed for an unrelated reason" are different facts with
different consequences, and the code makes them indistinguishable from outside. The design's position:
narrow the guard to the exception types that actually mean *this member does not exist here*, and let
anything else propagate. That preserves the documented old-runtime behaviour, keeps the public
signature untouched, and converts an unfalsifiable null into a distinguishable outcome. It is a change
to error semantics and therefore `/contract`'s to record, but the semantics are decided here.
A blanket catch anywhere is now suspect for the same reason; two others exist in the same file.

**The remote catalog, and third-party package identity.** Five of the twelve live tests assert
against `Microsoft.VisualStudioCode` or a `git` match in the `winget` source — its publisher, its
display name, its tags, its presence. These are assertions about *Microsoft's catalog*, not about
this library, and they fail on a catalog change, a network fault, or a source-agreement prompt.
The remaining seven depend only on the executing machine's own state. **These are two different risk
classes and must not share a blocking status**, or an upstream catalog edit blocks every merge in
this repository. Splitting them is what makes a live gate safe to require at all.

One of the machine-state tests asserts an implication over a possibly-empty list, so it passes
vacuously when the machine has no available upgrades. Vacuous evidence licenses no claim; the
evidence model above says so, and this is the concrete instance.

**The `winget.exe` shim.** Pins and export/import have no COM equivalent, so they shell out. Failure
is a non-zero exit or unparseable output. The two live CLI tests passed on the runner, so the shim
path has Evidence in the hosted Environment.

**The package feed.** A push of an already-present version is made harmless by the skip-duplicate
flag, so concurrent or repeated runs are benign. The consequential failure is not a rejected push —
it is a **successful push of the wrong version**, which is silent, irreversible, and has already
happened once (*Open questions*). Detection has to be positive: after publishing, confirm the version
that landed is the version intended, rather than inferring it from a green job.

**Version derivation.** GitVersion reads git history. A shallow clone degrades it to a warning in the
non-publishing job and a fatal error in the publishing one — a difference the workflow already
accounts for. The subtler failure is that its *output changes when a tag is added*, without any file
in the repository changing, so a documented example of its behaviour goes stale silently. One is
stale now.

**Runner image drift.** The hosted runner's WinGet version is not pinned by this repository and will
move. This is Evidence expiry made concrete: a live gate that runs on every change converts drift
into a visible failure, whereas one that ran once in August records a fact about August.

**The coverage gate.** Fails below the floor. Its own failure mode is a floor that drifts from what
it was measured against — a large refactor can move the ratio without changing a test. That is
acceptable, and the ratchet policy, not the gate, is what handles it.

**Partial failure across the live suite.** Eleven of twelve passing is the actual observed state, and
it is the state the design must handle rather than treat as an anomaly. A partial pass is Evidence
for the claims its passing tests license and for nothing else. The brief's definition of done demands
all twelve green, so a partial pass is not "mostly done" — but it is also not "no information", and
the failing one is the one carrying the finding.

## Concurrency and ordering

**Inside the library, nothing is concurrent, and the owner thread enforces it.** All COM work is
serialised onto one MTA thread; projected objects never leave it; agility is not assumed. This is
existing, tested behaviour and is not changed here.

**Inside a test run, nothing is concurrent, and the test framework's default enforces it.** NUnit does
not parallelise without an assembly-level opt-in, and this assembly has none. That default is now
load-bearing rather than incidental: each live fixture constructs its own client and therefore its own
owner thread and its own activation. Enabling parallelism would activate several COM contexts at once,
each with an independent activation-mode cache, in an out-of-proc server whose behaviour under that
pattern has never been observed. **Turning it on is a design change, not a speed tweak.**

**Across gates, ordering is by artifact dependency.** The live packed-consumer smoke test cannot run
before the package it consumes exists. The coverage floor is evaluated against the report produced by
the run that just executed — and, per the *Coverage floor* invariant, that run must be the one that
excluded the live tests. If live tests and unit tests ever share a coverage collector, the ordering
stops being a scheduling detail and becomes a correctness bug in the number.

Requesting several targets in one invocation is what keeps shared dependencies from re-running; this
is an existing property of the build tool, documented in `CLAUDE.md` § *Commands*, and the reason
gates are composed into one invocation rather than chained across several.

**Across workflow runs, publishing is idempotent by flag, not by design.** Two runs pushing the same
version is benign. Two runs pushing *different* versions derived from different refs is not ordered
by anything, which is a further reason the release decision belongs to the tag rather than to the run.

## Alternatives considered

**Where live verification runs.** *Chosen:* a live path separate from the required hermetic check,
with its trigger and blocking status decided as policy. *Rejected:* folding the live tests into the
existing required check — it makes every merge in this repository depend on Microsoft's catalog, a
runner's WinGet installation, and network reach, and the first outage teaches everyone to merge past
a red check. *Rejected:* keeping them local-only and recording why under the brief's "or record why"
branch — the spike removed the blocker that branch exists for, and taking it now would write down a
false reason permanently.

**How the packed consumer is smoke-tested.** *Chosen:* a separate gate reusing the existing
consumer-construction step. *Rejected:* adding a live call to the hermetic package-contract gate —
it is currently executable on any Windows machine with an SDK and no WinGet, and that portability is
a real property to spend rather than lose. *Rejected:* a new standalone consumer project checked into
the repository — it would be a second definition of "what a packed consumer looks like", free to
disagree with the one the contract gate already builds, which is the duplication failure this
repository's own rules forbid.

**How the mappers become testable.** *Chosen:* a boundary that guarantees no path from a mapper to
activation, with visibility widened through the existing internals grant. *Rejected:* testing them
through the public API against a mocked client — the mappers translate projection types, so a mock
that avoids the projection tests the mock. *Rejected:* wrapping the projection behind an abstraction
so it can be faked — that is a large refactor of settled, working code to serve a test, and the
projection's enums are already inert data that need no faking.

**What the coverage gate measures.** *Chosen:* one global line floor over the library, ratcheting up,
plus branch coverage reported but not gated initially. *Rejected:* per-class or per-namespace floors —
they encode the current shape of the code and must be edited by every refactor, so they rot into
noise. *Rejected:* excluding the COM-orchestration paths from measurement to make the number look
better — an exclusion makes the number stop describing the library, and the brief's entire complaint
is about numbers and claims that no longer describe the thing.

**`PackageOperationStatus.Cancelled`.** *Chosen:* produce it. The pinned projection metadata was read
directly: **no result-status enum in the WinGet contract has any cancelled member**, so no mapper over
those enums can ever reach it — but WinGet does have a cancelled-by-user error code, and this library
already declares that constant. Cancellation is therefore a real outcome that arrives as an error
code on an otherwise-failed result, not as a status. *Rejected:* deleting the member — it names a real
outcome the library can observe, and deleting it would be a breaking change to an already-published
stable version for no gain. *Rejected:* keeping it unreachable and pinning that with a test — a test
asserting that a public value can never occur documents a defect instead of fixing one.

**Where narrowed claims live.** *Chosen:* one canonical statement per subject, referenced elsewhere,
with a mechanical drift check in the existing documentation gate. *Rejected:* editing all five copies
and relying on care — that is precisely the process that produced five copies saying different things.
*Rejected:* generating the claim text from evidence records — a generator, a schema and a template for
four sentences, and the docs redesign is a binding non-goal.

## Open questions

**1. The release identity is already spent, and the definition of done assumes it is not.**
The brief states that no stable version has been tagged and that the publish targets have never run.
The repository says otherwise: an annotated `v0.1.0` tag exists and is pushed, pointing at commit
`c2cc157` from 2026-07-22; the tag push ran the build workflow to success, and within it the GitHub
Packages release step succeeded. Untagged commits on `main` now publish `0.1.1-<n>`, not the
`0.1.0-<n>` that `GitVersion.yml` and `SPECIFICATION.md` §11 both still describe. So the published
stable `0.1.0` is the code as it stood before the COM-context hardening, the package-consumer
targets and the documentation migration — before nearly everything the brief wants proven.

Two of that criterion's three parts were already satisfied, at the wrong commit, and a tag is
published state this repository does not rewrite. **Answered 2026-08-20: the proven release is cut as
`v0.2.0`**, and `design/00-brief.md` has been updated to say so. `v0.1.0` stays where it is, legible
as history rather than quietly replaced, and no published ref is rewritten. `PublishNuGet` genuinely
has never run; that part of the criterion stands as written.

One limit on the evidence behind this: what the feed actually contains was not inspected, because the
available token lacks the scope to read package versions. The publish *step* is recorded as
successful; the artifact it produced was not fetched back.

**2. Should the live gate block a merge?** The catalog-dependent and machine-state halves of the
live suite have different risk profiles, and the design keeps them separable for that reason — but
whether either blocks a pull request is a policy call about what a red check should mean here. I
recommend requiring the machine-state half and leaving the catalog-dependent half advisory but
visible. Left unanswered, the live gate defaults back to the thing the brief is complaining about:
a suite nobody is obliged to look at.

**3. If the version property turns out to be an upstream contract gap rather than a defect here,
what does the first definition-of-done item become?** The diagnostic that distinguishes the two is
specified above and is cheap. If it comes back "unreachable through the activation mode this library
uses on that environment", the criterion is satisfiable and this is a bug to fix. If it comes back
"WinGet does not serve this property out-of-proc at all", then no packed-consumer smoke test can ever
make it return non-null on a hosted runner, and the criterion needs a different observable — a live
call that proves the COM path works without depending on that one property. I am not rewording a
definition-of-done item on a hypothesis; the question is recorded so it is answered by measurement.
