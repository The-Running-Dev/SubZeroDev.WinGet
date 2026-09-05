# Design — Prove the provisional claims and cut v0.2.0

Designed against `design/00-brief.md`. The product runtime architecture is settled and is not
redesigned here; `CLAUDE.md` § *Architecture* and `SPECIFICATION.md` remain its description.

What is being designed is the system the brief asks for: a **binding between every consumer-facing
support claim and an execution that licenses it**. The problem is not merely thin tests. It is that a
maintainer or consumer cannot distinguish an executed claim from an aspirational one. The design
therefore decides what evidence exists, which environment may produce it, which claim it licenses,
and which gate fails when that relation is absent.

The first pass exposed six loose boundaries; the second closed the live-gate policy and invocation
surface, the two internal test seams, the coverage-floor representation, the version-member absence
classifier, and the canonical document map, and left one empirical stop condition open. This revision
closes that stop condition — the hosted runner's `null` was an absent interface on a runtime older
than the member, so the environment is now constituted rather than accepted — and separates the two
lines the translation seam had conflated: what the mapper *owns* and what unit tests can *license*.

## Data model

The existing runtime model is unchanged. No new public API is introduced. The only runtime semantic
changes are that `PackageOperationStatus.Cancelled` becomes reachable for WinGet's cancelled-by-user
HRESULT, and a version lookup returns `null` only for a missing version interface rather than for every
failure.

The evidence model is a discipline over existing product, build, CI, and Markdown artifacts. It does
not introduce a database, evidence service, or generated claim manifest.

### Claim

A Claim is a consumer-facing statement about one support subject.

- **Identity** — the subject, not the sentence used to express it.
- **Fields** — subject; assertion; strength (`executed`, `contract-checked`, or `unvalidated`);
  references to the Evidence that licenses that strength.
- **Ownership** — exactly one authored canonical statement per subject. Generated copies and links do
  not become further owners.
- **Lifecycle** — the assertion may be reworded, but its strength changes only when the evidence edge
  changes. Wording alone never promotes it.
- **Persistence** — authored Markdown in the repository.

There are exactly three strengths, and *provisional* is not among them. The brief's requirement that
the managed-assembly package shape be "confirmed or reverted — not left provisional" is therefore
discharged by the type rather than by a separate rule: that Claim must come to rest on a strength, and
whichever strength it reaches names the evidence that licensed it.

The document map is fixed without changing the documentation information architecture:

| Subject | Canonical owner | Why |
|---|---|---|
| Consumer architecture | `README.md` | It is the package-facing entry point and is also projected into the documentation homepage. |
| Managed-assembly package shape | `README.md` | Package consumers encounter this constraint before any repository-specific technical document. |
| Hosting context | `docs/docs/troubleshooting.md` | Elevation, service, and SYSTEM limitations are operational caveats with user actions. |
| Live operation coverage | `docs/docs/testing.md` | This page owns what was executed, where, and what remains unvalidated. |
| Runtime version floor | `docs/docs/getting-started.md` | It refines the prerequisite that page already owns — that WinGet must be installed — into which build one member needs. |

The two README-owned subjects remain distinct Claim records even if adjacent prose expresses them.
`docs/docs/index.md` is generated from the README and is not a second owner. Getting Started owns the
runtime version floor and references the other four; Architecture and the specification reference the
canonical statements rather than restating their strength.

The runtime version floor is scoped to the one member evidence exists about. It is not a library-wide
minimum supported WinGet version, because live execution below that floor demonstrably passes the rest
of the suite, and a floor the passing tests contradict is the wording-over-evidence failure this whole
design exists to prevent.

### Evidence

Evidence records that a Gate ran in an Environment against an exact commit.

- **Identity** — `(gate, environment, commit)`.
- **Fields** — binary outcome; run identity; assertions actually exercised; explicit non-assertions;
  observed environment facts.
- **Ownership** — CI run history and artifacts. Prose and the decision log may reference a run but do
  not copy its result.
- **Lifecycle** — immutable for that run, but time-bounded when the environment moves. A result
  against one WinGet or runner image does not silently license later images.
- **Partial results** — a passing assertion licenses only its own claim. Eleven passing tests do not
  become an all-suite pass, but neither are their results discarded.

Evidence is not only a test outcome. A member whose input cannot be constructed outside live COM
activation is licensed by the live run that exercised it, and that licence is as real as a unit test's
— it is narrower, because it is bounded to the environment the run recorded.

### Gate

A Gate is an executable check that can fail, records a binary outcome for one commit, and is invoked
by a workflow. A build target that no workflow invokes, or that only reports a number, is not a gate.

The stable live invocation surface is:

| Target | Risk class | Meaning |
|---|---|---|
| `MachineStateTest` | Machine state | Executes the seven read-only tests whose truth depends on the runner's installed/local state rather than a particular remote catalog entry. |
| `CatalogIntegrationTest` | Catalog dependent | Executes the six tests whose witnesses are identities or content in Microsoft's remote catalog. |
| `PackedConsumerSmokeTest` | Machine state | Builds through the existing packed-consumer construction path, then executes `GetWinGetVersion` from that consumer. |
| `IntegrationTest` | Local aggregate | Preserves the existing developer entry point by composing the two integration-test risk classes; it is not itself assigned a CI blocking consequence. |

Selection is by stable test risk metadata, not fixture-name substrings. Each target asserts its
expected selected count before its result can license a claim, so an under-selecting filter fails.
The packed smoke remains a separate target because it consumes an artifact; it does not become a
thirteenth project-reference integration test.

The required pull-request statuses are the existing hermetic check and a separate machine-state live
status that invokes `MachineStateTest` and `PackedConsumerSmokeTest` in one build invocation. The
catalog-dependent target runs for the same pull requests and records a normal pass or failure, but it
is not a required branch-protection status. Advisory means non-required, not swallowed or rewritten
as success.

### Environment

An Environment is observed execution context, not runner-image documentation.

- **Identity** — a named runner or maintainer machine.
- **Fields** — OS; architecture; WinGet version; interactivity; elevation; configured sources;
  remote-catalog reachability where relevant.
- **Licensing rule** — evidence licenses only the bounded environment it records. Windows x64 is not
  ARM64 runtime evidence; an interactive user is not service or SYSTEM evidence; contract checks are
  not runtime evidence.

The current loop contains the GitHub-hosted Windows x64 runner, the maintainer's Windows x64 machine,
and the maintainer's macOS machine, on which the product does not build or run. No ARM64 execution
environment exists, so ARM64 remains build/package-contract evidence only.

A hosted runner's WinGet version is not one of its documented components, and the image has shipped
builds predating parts of the COM surface. The hosted live jobs therefore **constitute** their own
runtime rather than inheriting it: they install one explicitly pinned build — the same version the
library's interop reference pins — and record the observed App Installer version both before and after
that install. Pinning is what makes the recorded environment fact reproducible; a floating latest would
redefine the gate's environment on an upstream release cadence, which is the unobserved-environment
failure in a new costume. Constituting the runtime changes the environment, never the assertion.

### Coverage floor

The coverage floor is one build-owned checked-in decimal line percentage, not a command-line
parameter and not a separate data file. It is expressed to one decimal place. Enforcement compares
the exact integer covered-line and valid-line counts against that decimal ratio; it does not compare
two independently rounded display values.

The initial value is measured after the required unit tests land and set one tenth of a percentage
point below that observed unit-only result. It ratchets upward only. Lowering it requires a decision
log entry. The input excludes every `[Explicit]` test and covers the whole library without file,
namespace, class, or COM-orchestration exclusions.

Because some translations are licensed by live evidence rather than by unit tests, the floor measures
less than the library's proven behaviour. That is the intended reading: the floor is a lower bound on
*unit-only* coverage, deliberately blind to live evidence so live execution can never inflate it.

### Release

A Release is the immutable pair `(tag, commit)` plus positive confirmation from each intended feed.
GitVersion output and the project-file version are publishing inputs, not release identity.
`v0.1.0` remains at its published commit. The proven release is `v0.2.0`; once pushed, that tag is the
same one-way door and is never moved.

## Module boundaries

The dependency direction is:

`service → client → {pure translation, COM owner}`

`COM owner → factory → activation-mode selector → {projection activation, raw COM activation}`

`workflow → gate → artifact/evidence → canonical claim`

No edge points back toward a consumer, and the graph is acyclic.

**Product layers** — service, client, and COM owner/activation retain their existing responsibilities.
The service owns validation, logging, normalization, and retry policy. Clients own single-attempt
translation between the public model and WinGet. The owner context serializes all projected-object
access on one MTA thread. No COM/WinRT object crosses that boundary.

**Pure translation** — a dedicated internal `WinGetProjectionMapper` owns every pure projection enum,
option, result, DTO, and collection translation used by both package and source clients.

- It depends only on projection metadata and public model values.
- It has no path to `WinGetFactory`, `WinGetComContext`, network access, or `winget.exe`.
- Production clients call it directly; tests reach it through the existing internals grant.
- Projected collections remain indexed inside this boundary. Moving a translation does not license
  `foreach` or LINQ over CsWinRT collections.

This selects one declaration owner rather than widening private helpers across two client types. It
is an internal organization boundary, not a fakeable projection abstraction.

**Ownership and unit-testability are two lines, not one.** Membership of this boundary is decided by
whether a translation is pure — no activation path, no I/O. Whether that translation can carry a
*unit* test is decided by something else entirely: whether its inputs can be constructed without live
COM activation. Enum, option, result-shaping, and plain-collection translations satisfy both, and are
unit-tested. Translations taking a projected instance — a match result, a catalog package, a catalog
info, an agreement, a documentation, an icon, an install result — satisfy the first and cannot satisfy
the second, because those types have no constructible form outside an activated server. They remain
inside the boundary, and their correctness is licensed by live evidence under the same Evidence rules
as any other execution.

Conflating the two lines is what made the seam unsatisfiable: it demanded a unit test for members
whose only honest witness is a live run, and the available escapes — a fake projection, or a
read-accessor wrapper unit-tested against a stub — would have proved that the fake reads correctly
rather than that the projection does. That is precisely the aspirational-evidence confusion the brief
exists to remove, so it is refused here rather than in each slice.

**Activation-mode selection** — a dedicated internal `WinGetActivationModeSelector` owns the ordered
mode list, first-success choice, cache, synchronization, and aggregation of failed attempts.
`WinGetFactory` continues to own real projected and raw COM construction and supplies one attempt
callback to the selector.

The selector is testable with inert values and scripted failures; unit tests do not construct a
WinGet projected type. They prove projection-first ordering, each fallback, first-success caching,
cached-mode reuse, total-failure aggregation, and serialization. The factory remains the only module
that knows CLSIDs, IIDs, or raw activation flags.

**Gates** — build targets own assertions and binary outcomes. Workflows own cadence, required-status
policy, environment constitution, and evidence persistence. The existing package-contract gate remains
hermetic. The live packed smoke reuses its consumer-construction implementation but has an independent
assertion and outcome.

**Claims** — canonical prose depends on Evidence. No product or gate depends on prose. The existing
documentation gate enforces ownership, strength vocabulary, evidence references, and the absence of
non-canonical restatements; it does not fetch external evidence during a documentation check.

## Control flow

**A pull request is opened.** The existing required hermetic check runs unit tests, exact coverage-floor
enforcement, architecture assertions, and package-contract verification. Separately, the required
machine-state status constitutes its runtime, then runs the seven machine-state tests plus the
packed-consumer smoke. A third, non-required status runs the six catalog-dependent tests. All three
record results against the same commit; one cannot substitute for another.

**Live behaviour is interpreted.** A live target first records its environment — including the pinned
WinGet build and the versions observed either side of installing it — and its selected-test count, then
executes. Missing prerequisites are failures with no licensed claim, not product success. Passing
assertions become Evidence only for their named subjects, and a passing live run is what licenses the
projected-parameter translations. A catalog outage may leave valid machine-state evidence intact. An
empty input that makes an implication vacuously true records no evidence for that assertion and makes
the absent witness visible.

**A release is cut.** The hermetic check gates publishing directly: it is the only check the release
job depends on, and it is the only one that runs for a tag. The machine-state and catalog jobs are
scoped to pull requests, and squash merge gives `main` a tip whose SHA no live job has ever seen, so
**no tagged commit can carry its own live evidence** and none is waited for.

The live half of a release's evidence is therefore carried by *tree identity* rather than commit
identity: the release cites the pull-request run whose pre-squash head has a tree identical to the
tagged commit's, and the citation is admissible only while that identity is demonstrated — a diff
between the two commits touching no product, build, or workflow path. Anything else is a gap in the
evidence, not a weaker grade of it. This is a substitution with a real cost, stated here rather than
improvised per release: a machine-state regression introduced between the cited run and the tag is
invisible to every gate that fires, and the required status that would have caught it is required
only on the pull request. Closing that gap is a workflow-composition change, not a documentation one.

Publishing then derives the intended version, pushes, and retrieves or queries each intended feed to
confirm that exact version. A green push command or skipped duplicate without retrieval is not a
successful publication. Only after both GitHub Packages and NuGet.org positively confirm `v0.2.0`
does the release satisfy the brief.

## Failure modes

**COM activation fails.** Each real mode is attempted in order. The selector aggregates failures and
the client surface receives `WinGetUnavailableException`. No failed mode is cached as success. A later
independent call may retry after the environment changes; no projected object is left outside the
owner context.

**A cached activation mode later fails.** The cached mode is the context invariant and is not silently
reselected for one object. The failure propagates. Mixing activation contexts would make projected
object ownership unknowable and is worse than failing the call.

**The version interface is absent.** [CsWinRT obtains added WinRT interfaces through
`QueryInterface`](https://github.com/microsoft/CsWinRT/blob/master/docs/interop.md); COM reports an
absent interface as `E_NOINTERFACE`, and [.NET represents HRESULT `0x80004002` as
`InvalidCastException`](https://learn.microsoft.com/en-us/dotnet/api/system.invalidcastexception?view=net-8.0).
`GetWinGetVersion` therefore converts only an `InvalidCastException` carrying exactly `0x80004002` to
`null`. Cancellation, activation errors, other COM failures, and every other exception propagate
unchanged. Message matching and a blanket `COMException` guard are forbidden.

This is now a diagnosed condition rather than a suspected one. The version member is declared in COM
contract 13, first present in WinGet 1.12; the hosted image carried a build declaring contract 12,
which cannot answer the query, so the narrowed classifier correctly reported absence. That is an
environment fact, not a product defect, and it is why the hosted jobs constitute their runtime. The
consumer-visible half of the same fact is the runtime version floor Claim.

The stop condition survives in narrowed form. The machine-state status is not made required until the
narrowed classifier has run against the *constituted* environment and the packed consumer has observed
a non-null version. If it still returns `null` there, the packed-consumer criterion is unsatisfied and
the work returns to the brief for an environment decision. It must not silently replace
`GetWinGetVersion`, weaken non-null, or call `winget --version` as a fallback.

**A remote catalog or witness changes.** Catalog-dependent tests fail their non-required status and
retain any separately passing machine-state evidence. A package identity, publisher, tag, or source
agreement is upstream data, not product state. A machine-state assertion over an empty list is not
evidence merely because an implication returned true.

**A live-licensed translation regresses.** The projected-parameter translations have no unit test to
fail, so a regression in them surfaces only in a live run — and never in the hermetic required check.
This is an accepted, stated cost of the boundary rather than an oversight: the alternative was a test
that could not fail for the right reason. Two things bound it. The live coverage for these members is
an explicit obligation with named assertions, not a hope that some existing test happens to touch
them; and their live status is exactly as advisory or required as its risk class, so a machine-state
regression blocks a merge while a catalog-coupled one does not.

**The CLI shim fails.** Pins and export/import have no COM equivalent. A non-zero exit, missing
executable, or unparseable output remains the existing typed failure. The shim does not expand beyond
those operations.

**The coverage floor fails.** Exact covered and valid line counts fall below the checked-in decimal
ratio. The gate fails. A refactor changing the ratio is not an automatic reason to lower the floor;
lowering requires an explicit decision.

**A claim drifts.** Duplicate canonical owners, invalid strength, missing or insufficient evidence,
and non-canonical restatement are blocking documentation failures. A warning is not enough because a
green documentation gate is part of the evidence chain.

**Publication partially succeeds.** Each feed confirmation is independent. If one feed contains the
intended release and the other does not, the successful feed remains published state but the release
criterion is incomplete. Re-running is safe only for the same intended version; changing the tag to
escape a partial publish is forbidden.

**Runner image drift.** A later runner may change WinGet version, sources, or interface availability.
Constituting the runtime absorbs the version axis of that drift and makes the rest visible as a
recorded before/after fact. Historical evidence remains an observation about its recorded environment
and does not automatically license the new one.

## Concurrency and ordering

Inside the library, COM work is serialized on one owned MTA thread. Projected objects never leave it.
The activation selector uses the factory's existing synchronization boundary so one caller resolves
the mode and later callers observe the same cached choice.

Inside a live test process, tests remain non-parallel. Each fixture currently owns a client/context;
parallel opt-in would activate several independent COM contexts against one out-of-proc server and is
a contract change, not a test-speed optimization.

Across gates, runtime constitution precedes every live assertion in the same job, so no test observes
the pre-install runtime. The packed-consumer smoke runs only after its package and consumer
construction exist. Coverage is evaluated only after its unit-only report exists. Live execution never
contributes to that report. Several targets requested in one build invocation share dependencies once,
but sharing a dependency never merges their outcomes or risk classes.

Across workflow runs, two publications of the same version are idempotent at the push step. Two
different versions derived from different refs are not ordered by the build system; the pushed tag
therefore remains the release decision. Feed confirmation follows publishing and cannot be inferred
from it.

## Alternatives considered

**Live status policy and invocation.** *Chosen:* required machine-state and packed-consumer checks,
plus a separately failing but non-required catalog status; stable risk metadata; count assertions;
and `IntegrationTest` retained only as a local aggregate. *Rejected:* one all-live required status,
because an upstream catalog edit would block unrelated merges. *Rejected:* making all live checks
advisory, because that recreates a suite nobody must keep green. *Rejected:* fixture-name filters,
because the existing under-selection proved they can report a clean partial run.

**Packed-consumer execution.** *Chosen:* a separate target reusing consumer construction. *Rejected:*
adding live COM to the hermetic package-contract gate, because it spends determinism and portability.
*Rejected:* a checked-in second consumer project, because it would duplicate the existing definition
of a packed consumer.

**Translation seam.** *Chosen:* one dedicated internal pure mapper used by both clients and tests.
*Rejected:* widening helpers in place across two client types, because ownership and the no-activation
boundary would remain split. *Rejected:* mocking the projection, because a fake projection tests the
fake. *Rejected:* adding a public mapper interface, because no consumer needs it and new public surface
is a binding non-goal.

**What the translation seam guarantees.** *Chosen:* the boundary is defined by purity, and the
unit-test obligation is defined separately by input constructibility, so a projected-parameter
translation is owned here and licensed by live evidence. *Rejected:* scoping the boundary itself to
constructible-input translations, because it would evict members already living inside it whose only
fault is their parameter type, split one translation concern across two owners for a test-tooling
reason, and leave the evicted members with neither an owner nor a stated licence. *Rejected:* an
internal read-accessor over the projected types, faked in unit tests, because the test would prove the
accessor reads a stub correctly and assert nothing about the projection — aspirational evidence
wearing a unit test's clothes. *Rejected:* leaving the seam unsatisfiable and proceeding, because the
coverage floor is measured from the unit run that the blocked work feeds, so the block propagates.

**Activation-selection seam.** *Chosen:* extract only the mode-selection state machine and inject one
attempt callback from the factory. *Rejected:* injecting fake projected `PackageManager` objects,
because constructing or faking those types crosses back into the integration boundary. *Rejected:*
making CLSID/IID tables mutable for tests, because that tests raw activation configuration rather than
selection. *Rejected:* reflection over private cache state, because it couples tests to representation
without controlling failures.

**Coverage declaration.** *Chosen:* one build-local decimal percentage with one-decimal precision and
an exact integer-ratio comparison. *Rejected:* a command-line parameter, because a green run could
lower its own contract. *Rejected:* a separate data file, because one scalar does not justify a schema
and reader. *Rejected:* comparing rounded display percentages, because rounding can move a boundary.

**Version-member absence.** *Chosen:* only `InvalidCastException` with HRESULT `0x80004002` means
unavailable. *Rejected:* every `COMException`, because unrelated interop failures would become null.
*Rejected:* message matching, because localized text is not an error contract. *Rejected:* removing
the guard, because old runtimes genuinely may omit the interface — as the hosted image proved.
*Rejected:* `winget --version` fallback, because it changes the meaning from COM-backend version and
broadens the CLI exception.

**The hosted runtime.** *Chosen:* constitute it — install one pinned build equal to the interop pin
and record the version either side. *Rejected:* accepting the hosted limit and weakening the criterion,
because it would spend the brief's headline definition-of-done item on a runner detail one install
fixes. *Rejected:* validating only on the maintainer's machine, because it gives up the CI gate this
design is built around and makes the evidence manual and unrepeatable. *Rejected:* installing the
latest available build, because an unreproducible recorded version is the unobserved-environment
failure this design exists to close. *Rejected:* matrixing the oldest contract-13 release alongside the
pin, because it doubles the surface that can go red on the status the rollout is trying to make
required; it remains the natural second leg later.

**Canonical claims.** *Chosen:* package-facing architecture and managed-shape claims in the README,
hosting caveats in Troubleshooting, execution coverage in Testing, and the runtime version floor in
Getting Started. *Rejected:* making the specification canonical for consumer support, because package
consumers should not need an internal design document to interpret installation support. *Rejected:*
generating claim prose from evidence, because a generator and schema for five statements exceed the
problem and approach the excluded docs redesign. *Rejected:* placing the version floor in
Troubleshooting beside the hosting caveats, because it would split one prerequisite — which WinGet you
need — across two documents, and the symptom it explains is better prevented than diagnosed.
*Rejected:* placing it in the README, because a per-member runtime floor is below the altitude of the
package-facing entry point and would compete with the two claims already owned there.

**Cancellation result.** *Chosen:* produce `PackageOperationStatus.Cancelled` from WinGet's
cancelled-by-user HRESULT while retaining the HRESULT. *Rejected:* deleting the public value, because
it names a real outcome and removal is breaking. *Rejected:* merging it with caller-token
cancellation, because returned terminal state and thrown cancellation have different caller actions.

## Open questions

No maintainer policy question remains in this design revision, and the empirical stop condition the
previous revision carried has been answered: the hosted `null` was an absent interface on a runtime
older than the member, and the environment branch was taken.

One execution remains unobserved rather than undecided. Against the constituted runtime, the packed
consumer must return a non-null version before the machine-state status becomes required. That is a
gate sequencing rule under *Failure modes*, not a question for the maintainer — it becomes one only if
the observation fails, at which point it is an environment decision and not an implementation choice.
