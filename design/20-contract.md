# Contract — Evidence-bound support claims

This contract governs the evidence system designed in [10-design.md](10-design.md). The product's
existing public declarations remain authoritative for shape; this document states the semantics those
declarations, build targets, workflows, and Markdown files cannot express. It introduces no new public
library API.

## Invariants

| Id | Assertion | Owner | Enforcement |
|---|---|---|---|
| C1 | For each claim subject — consumer architecture, managed-assembly package shape, hosting context, live operation coverage, and the runtime version floor — exactly one authored Markdown statement is canonical. Every other mention references that statement rather than restating its assertion or strength. | Documentation | Enforced in `build/Test-Documentation.ps1`'s `Test-DocumentationClaim`: a Claim block outside its `$Claims.Owners`-configured file, or a second block for the same subject, fails `ClaimDuplicateOwner`; a strength-assertion phrase in a document that owns no Claim subject fails `ClaimRestatement`. |
| C2 | A claim's strength is exactly one of `executed`, `contract-checked`, or `unvalidated`, and is justified by referenced evidence from an environment capable of licensing that strength. Wording alone never raises it. | Documentation | Enforced in `build/Test-Documentation.ps1`'s `Test-DocumentationClaim`: an unrecognized strength fails `ClaimInvalidStrength`; a non-`unvalidated` strength with no evidence fails `ClaimMissingEvidence`; evidence naming no `$Claims.EvidenceTokens` entry ranked at or above the claimed strength fails `ClaimInsufficientEvidence`. Which strength a *new* evidence token is capable of licensing remains a reviewed config decision, not inferred. |
| C3 | Evidence is identified by gate, environment, and commit. It records outcome, run identity, asserted facts, and explicit non-assertions. A passing subset licenses only the claims asserted by that subset. | Live-verification workflow | Planned workflow and artifact enforcement. |
| C4 | Environment facts are observed, not assumed. Live evidence records OS, architecture, WinGet version, interactivity, elevation state, configured-source state, and remote-catalog reachability where each fact affects interpretation. | Live-verification workflow | Enforced in the `machine-state` and `catalog` job evidence steps in `.github/workflows/build.yml`. |
| C5 | Evidence never licenses a broader environment than the one recorded. In particular, Windows x64 execution is not ARM64 runtime evidence, package-contract checks are not runtime evidence, and interactive-user execution is not service, SYSTEM, or elevation evidence. | Documentation and release review | The `consumer-architecture` and `managed-assembly-shape` Claims (README.md) hold this at `contract-checked` strength precisely because ARM64 has no execution evidence, and `hosting-context` (troubleshooting.md) holds `unvalidated`; C1/C2's structural checks enforce that strength and its evidence. Distinguishing which cited evidence token covers which named environment *within* one Claim's assertion is authored and review-enforced, not a dedicated mechanical check. |
| C6 | A gate exists only when a workflow invokes it, can fail on a violated assertion, and records a binary outcome for the exact commit tested. A target that merely reports a value is not a gate. | Build and workflows | Planned workflow enforcement. |
| C7 | The required pull-request check remains hermetic: it may compile, run unit tests, collect and enforce unit-only coverage, inspect PE architecture, and verify packed-package structure, but it must not activate WinGet COM, query a network catalog, invoke a package feed, or execute `winget.exe`. | `build` job | Enforced in workflow composition and by `HermeticJobTests.cs`'s negative dependency check. |
| C8 | `MachineStateTest` selects exactly seven machine-state tests, `CatalogIntegrationTest` selects exactly six catalog-dependent tests, and both fail before execution if their stable risk metadata selects another count. The machine-state target and `PackedConsumerSmokeTest` share one required pull-request status; the catalog target has a separately failing, non-required status. `IntegrationTest` is only their local aggregate. | Live-verification workflow | Count assertions enforced by `Build.cs`'s `AssertLiveTestCount`; the `machine-state`/`catalog` job split is enforced in workflow composition. The machine-state status is not yet marked required in branch protection — that setting change is pending (see C23). The catalog-dependent count is still enforced at five pending S14, which adds the sixth test and raises the assertion (`design/90-decisions.md`, 2026-08-31). |
| C9 | `PackedConsumerSmokeTest` is separate from `PackageTest`. Both reuse one consumer-construction implementation, but only the smoke target executes the packed consumer and activates COM. | Build | Planned code enforcement. |
| C10 | `Coverage` evaluates one build-owned, checked-in decimal line-coverage floor over the library, with no file, class, namespace, or COM-orchestration exclusions. The value has one decimal place, and enforcement compares exact covered- and valid-line counts against its ratio rather than comparing rounded percentages. The report may include branch coverage, but only line coverage is gated initially. | Build | Enforced in `CoverageGate` (`build/CoverageGate.cs`), called from the `Coverage` target in `build/Build.cs`. |
| C11 | Coverage input comes from the unit-test run that excludes every NUnit `[Explicit]` test. Live-test execution must never contribute to the report compared with the floor. Because the C26 translations are licensed by live evidence rather than by unit tests, the floor is a lower bound on *unit-only* coverage and is deliberately blind to live evidence; it is not a measure of the library's proven behaviour and no document may present it as one. | Build and documentation | `[Explicit]` exclusion, the floor, and the input assertion are enforced (`Coverage` depends only on `Test`, which is the only target that collects coverage — `MachineStateTest`/`CatalogIntegrationTest` do not); the documentation reading is enforced in `build/Test-Documentation.ps1`'s `Test-DocumentationCoverageFloorScope`, over `$Claims.Scope`, failing `CoverageFloorScope` on an undisclaimed proven/tested/verified claim next to the floor. |
| C12 | The checked-in coverage floor is set from a measured unit-only result after the required unit tests land, is raised only with the measurement that justifies it, and is lowered only with a decision-log entry. | Build and decision log | Enforced in comparison by `CoverageGate.Assert` (`build/CoverageGate.cs`); ratchet provenance is review-enforced. |
| C13 | Membership of `WinGetProjectionMapper` is decided by purity alone — no activation path, no I/O. It owns every such projection enum, option, result, DTO, and collection translation used by the package and source clients, and a projected parameter type never excludes a translation from it. It has no call path to `WinGetFactory`, `WinGetComContext`, COM activation, network access, or `winget.exe`. Loading projection enum and struct metadata does not by itself make its tests integration tests. | Client translation boundary | All members, including the eight moved in S6, are declared in `WinGetProjectionMapper.cs`; a source-level regression check in `SubZeroDev.WinGet.Tests` proves the absent dependency paths. |
| C14 | Every CsWinRT-projected collection is traversed by index. Exposing translation functions to tests must not replace indexed loops with `foreach` or LINQ. | Client translation boundary | Enforced by production code; regression enforcement is planned. |
| C15 | A failed operation whose extended HRESULT is `WinGetErrorCodes.InstallCancelledByUser` produces `PackageOperationStatus.Cancelled`, regardless of the projection status enum's lack of a cancelled member. Caller-token cancellation remains an `OperationCanceledException`; the two outcomes are not normalized together. | Client translation boundary | Planned code and unit-test enforcement. |
| C16 | `GetWinGetVersion` returns `null` only when the runtime does not expose the version member through the supported activation path. Cancellation and every unrelated failure remain distinguishable and propagate according to *Error semantics*. | Client layer | Planned code and live-test enforcement. |
| C17 | `WinGetActivationModeSelector` tries projection, local server, then local server with lower-trust registration. The first successful mode is reused for later activations in that factory; a cached mode's later failure propagates without reselection; total initial failure becomes `WinGetUnavailableException` with every attempt failure retained; a failure is not cached as a successful mode. | COM activation layer | Enforced by production code and by the four unit tests in `WinGetActivationModeSelectorTests.cs`, which cover mode ordering with first success, cached-mode reuse, cached-failure propagation without reselection, and total-failure aggregation with a later retry. |
| C18 | COM work remains serialized on one owned MTA thread, and the live test assembly remains non-parallel. Adding NUnit parallelization is a contract change. | COM owner and tests | Enforced by production code and current test configuration. |
| C19 | A live run with partial success records evidence for each passing assertion and failure for each failing assertion. It does not become an all-suite pass, and vacuous assertions over empty inputs license no claim. | Live-verification workflow | Planned test and artifact enforcement. |
| C20 | ARM64 is described only as build and package-contract support until ARM64 hardware evidence exists. Elevation, Windows Service, SYSTEM hosting, and mutating-operation live coverage remain `unvalidated`. | Documentation | The `hosting-context` Claim (troubleshooting.md) and the mutating-operation half of the `operation-coverage` Claim (testing.md) hold `unvalidated`; the `consumer-architecture`/`managed-assembly-shape` Claims hold `contract-checked` rather than `executed` because of ARM64. C1/C2 enforce those strengths and their evidence structurally (`Test-DocumentationClaim`); which specific fact within one Claim's prose each strength covers remains authored and review-enforced. |
| C21 | A release is identified by its pushed tag and exact commit. `v0.2.0` is the next stable release; `v0.1.0` is not moved. A publishing run is successful only after the intended version is positively confirmed on each intended feed. | Release workflow | Tag immutability is repository policy; positive feed confirmation is planned. |
| C22 | No work governed by this contract adds public library surface, changes the service/client/COM dependency direction, or broadens the documented CLI-shim exception beyond pins and export/import. | Product | Review-enforced; regression enforcement is planned. |
| C23 | The machine-state live status is not made required until the narrowed version-member classifier has run on the GitHub-hosted Windows x64 environment constituted by C24 and `PackedConsumerSmokeTest` has observed a non-null version. A `null` result or unrelated failure stops that rollout; it does not license a fallback observable, weaker assertion, or required status. | Live-verification workflow | Precondition satisfied: run 33671049835 (commit `44afa873`, 2026-09-02) observed a non-null WinGet version (1.29.280) from the packed consumer on GitHub-hosted Windows x64. The branch-protection change that makes the `machine-state` status required is a repository-settings action outside this repository's own tooling and remains for the maintainer to apply. |
| C24 | The hosted live-verification jobs constitute their own WinGet runtime: they install one explicitly pinned build — the version the library's `Microsoft.WindowsPackageManager.ComInterop` reference pins — and record the App Installer version observed both before and after that install. A floating latest is not a pinned build, because C4's observed environment fact must be reproducible. Constituting the runtime is not a fallback observable: the assertion under C23 remains a non-null `GetWinGetVersion` from the packed consumer. | Live-verification workflow | Planned workflow enforcement. |
| C25 | `GetWinGetVersion` requires a runtime exposing WinGet COM contract 13, first declared in WinGet 1.12; below that it returns `null` under C16 rather than failing. That floor is a Claim with subject `runtime-version-floor`, canonically owned by `docs/docs/getting-started.md`, and is scoped to this member alone — no library-wide minimum WinGet version is asserted, because live evidence exists of the rest of the surface working below it. | Documentation | Ownership, `executed` strength, and its evidence (`PackedConsumerSmokeTest`, `Test`) are enforced structurally by C1/C2 (`Test-DocumentationClaim`). That no other document asserts a library-wide minimum WinGet version is authored (SPECIFICATION.md §11 item 3, README.md) and review-enforced, not a dedicated mechanical check. |
| C26 | Whether a mapper member carries a unit test is decided separately from C13, by whether its inputs are constructible without live COM activation. A translation taking a projected instance — `MatchResult`, `CatalogPackage`, `PackageCatalogInfo`, `PackageAgreement`, `Documentation`, `Icon`, or `InstallResult` — is owned under C13 and licensed under C3 by the live run that exercised it. That licence is an explicit obligation with named assertions; a live run that merely happens to touch such a member licenses nothing. No fake projection, stub-backed read accessor, or reflection substitute may stand in for it, and its absence of a unit test is not a coverage gap to be closed. | Client translation boundary and live-verification workflow | The ten members are named under *Types* § *Projection mapper*; their named live assertions are planned. |
| C27 | A green hermetic required check asserts nothing about any C26 live-licensed translation. A regression in one is detectable only by the live run whose assertion names it, and its blocking consequence is exactly that of that run's risk class under C8 — never elevated by the hermetic check, and never substituted for by it. | Live-verification workflow | Planned workflow enforcement. |

Only rows whose enforcement says **enforced** may be trusted without inspecting the planned slice that
materialises the rest. A green workflow before those slices land is not evidence that a planned row is
already checked.

## Types

### Product result types

[`PackageOperationStatus` and `PackageOperationResult`](../SubZeroDev.WinGet/Models/PackageOperationResult.cs)
remain the public declarations. `Cancelled` is meaningful only for a terminal operation outcome caused
by WinGet's cancelled-by-user HRESULT. It is not a synonym for caller-token cancellation, and a
cancelled result must preserve the extended HRESULT rather than normalising it away.

[`WinGetErrorCodes`](../SubZeroDev.WinGet/WinGetErrorCodes.cs) declares the HRESULT constants. The
cancelled mapping compares the `HResult` carried by the projection's `Exception` with
`InstallCancelledByUser`; installer-specific numeric codes do not participate in that classification.

### Claim

No runtime type. A Claim is an authored Markdown record with:

- `Subject`: one of `consumer-architecture`, `managed-assembly-shape`, `hosting-context`,
  `operation-coverage`, or `runtime-version-floor`;
- `Assertion`: the consumer-facing statement;
- `Strength`: one of `executed`, `contract-checked`, or `unvalidated`;
- `Evidence`: zero or more evidence references; `executed` and `contract-checked` require sufficient
  evidence, while `unvalidated` may have none.

The subject is the stable identity; rewording the assertion does not create a second Claim.

### Evidence

No repository runtime type. Evidence is a CI-owned record keyed by `(Gate, Environment, Commit)` with
`Outcome`, `Run`, `Assertions`, and `NonAssertions`. `Assertions` is non-empty; both sets are explicit,
including an explicitly empty `NonAssertions` set. Absence from `Assertions` never means implicit
success.

### Gate

Build-gate declarations live in [`build/Build.cs`](../build/Build.cs); workflow invocation lives in
[`.github/workflows/build.yml`](../.github/workflows/build.yml). A Gate has a stable invocation name,
one risk class (`hermetic`, `machine-state`, or `catalog-dependent`), a binary outcome, and a declared
set of assertions. A Gate is not a new library type.

### Environment

No repository runtime type. An Environment is an observed record containing the fields required by C4.
Unknown facts stay unknown; they are never filled from runner-image documentation or a prior run.

### Coverage floor

One private build-owned decimal value in [`Build`](../build/Build.cs), checked in with one decimal
place and constrained to the inclusive range `0..100`. It has no command-line override, separate data
file, per-file children, or exclusions. Its value was materialised in S9 from the unit-only
measurement prescribed by C12; the comparison itself is
[`CoverageGate.Assert`](../build/CoverageGate.cs), called from the `Coverage` target.

### Projection mapper

Declared in [`WinGetProjectionMapper.cs`](../SubZeroDev.WinGet/WinGetProjectionMapper.cs). C13 decides
what belongs there; C26 decides, separately, what can carry a unit test. Because those two lines cut
across each other rather than coinciding, the resulting partition is the fact the declarations cannot
carry, and it is stated here rather than inferred from which members happen to have tests.

**Unit-testable, and unit-tested.** Every enum, option, result-shaping, and plain-collection
translation. Their inputs are ordinary .NET and public model values, so a unit test constructing no
client, factory, context, or COM object exercises the real member and not a substitute for it.

**Live-licensed.** Ten translations take a projected instance and have no constructible input outside
an activated server. C13 owns them and C26 licenses them by live evidence: `FindVersionId(CatalogPackage,
string)` and `GetInstallerErrorCode(InstallResult)`, landed in S5, plus the eight moved under C13 in S6
— `ToPackages`, `ToPackageInfo`, `ToPackageDetails`, `CopyAgreements`, `CopyDocumentations`, `CopyIcons`,
`ToPackageSource`, and `GetPriority`. All ten are declared in
[`WinGetProjectionMapper.cs`](../SubZeroDev.WinGet/WinGetProjectionMapper.cs) without a unit test, which
is the state C26 describes rather than an omission to correct.

C14, C15, and the state-dependent facts under *Product result types* remain authoritative. The mapper
is stateless. Its callers supply already-created projection values; it never constructs a client,
factory, context, or live COM object. Every projected collection parameter is traversed by index.

### Activation-mode selector

`WinGetActivationMode` and `WinGetActivationModeSelector` are declared in
[`Com/WinGetActivationModeSelector.cs`](../SubZeroDev.WinGet/Com/WinGetActivationModeSelector.cs).
C17 and C18 state the semantics the declarations cannot: the mode order, first-success caching, the
non-reselection of a failed cached mode, and the aggregation of every attempt failure.

One selector belongs to one `WinGetFactory`. The callback performs exactly one real activation attempt
for the supplied mode. The selector owns ordering, synchronization, first-success caching, cached-mode
reuse, and failed-attempt aggregation; the factory remains the only owner of projection constructors,
CLSIDs, IIDs, and raw activation flags.

### Release

No runtime type. A Release is the immutable pair `(Tag, Commit)` plus one feed-confirmation record per
intended destination. GitVersion output and the project-file version are inputs to publishing, not the
identity of an already-published Release.

## Persisted schemas

| Store | Key | Required constraints | Existing-data and migration story |
|---|---|---|---|
| Canonical claims in authored Markdown | `Claim.Subject` | `README.md` owns `consumer-architecture` and `managed-assembly-shape`; `docs/docs/troubleshooting.md` owns `hosting-context`; `docs/docs/testing.md` owns `operation-coverage`; `docs/docs/getting-started.md` owns `runtime-version-floor` and references the other four. Each subject has exactly one canonical statement, a valid strength, and evidence sufficient for that strength; all non-canonical mentions are links or explicit references. | Existing duplicated prose is narrowed or replaced with references in place. There is no format migration or generated claims file. `docs/docs/index.md` remains generated from `README.md` and is not a second authored Claim. |
| CI run and artifact history | `(Gate, Environment, Commit)` | Exact commit and run identity; binary outcome; explicit assertions and non-assertions; environment facts required by C4; partial results retained. | None. Historical runs that lack these fields remain historical observations but do not satisfy this contract. |
| Coverage floor | Singleton | One private decimal value in `build/Build.cs`, with one decimal place, compared by exact covered- and valid-line counts against unit-only whole-library line coverage; no exclusions or command-line override; ratchet rule C12. | Introduced once after the required unit-test measurement. No prior value is inferred from prose or an old report. |
| Git tags | Tag name | One tag resolves to one commit; published tags are never moved or reused. | None. Existing `v0.1.0` remains unchanged; `v0.2.0` is additive. |
| Feed confirmation | `(Release, Feed)` | Observed published version equals the intended Release version; command success without retrieval is insufficient. | None. The prior `v0.1.0` workflow result is not rewritten into confirmation that was never captured. |

There is no database, collection migration, compatibility reader, or generated evidence manifest in
scope. Adding one is a design change.

## Public surface

### Library

[`IWinGetClient.GetWinGetVersion`](../SubZeroDev.WinGet/Abstractions/IWinGetClient.cs) and the service
forwarder keep their existing signature. Callers may rely on:

- a non-null, non-whitespace value meaning the activated WinGet backend supplied a version;
- `null` meaning only that the supported runtime/activation path does not expose that member;
- caller cancellation remaining cancellation; and
- unrelated activation, interop, or projection failures not being converted to `null`.

The method acquires no logger, fallback to `winget --version`, default substitute, or new result type.

Operation methods keep their existing declarations. When C15 applies they return a failed
`PackageOperationResult` with `Status == Cancelled`, `Succeeded == false`,
`ExtendedErrorCode == WinGetErrorCodes.InstallCancelledByUser`, and no retry by the service layer.

### Internal translation boundary

The mapper's members, including the eight moved under C13 in S6, are declared in
[`WinGetProjectionMapper.cs`](../SubZeroDev.WinGet/WinGetProjectionMapper.cs) and called from
[`WinGetClient.cs`](../SubZeroDev.WinGet/WinGetClient.cs) and
[`WinGetSourceClient.cs`](../SubZeroDev.WinGet/WinGetSourceClient.cs); neither client retains a
duplicate pure mapper.

Tests reach a mapper member only through the existing `InternalsVisibleTo` grant in
[`SubZeroDev.WinGet.csproj`](../SubZeroDev.WinGet/SubZeroDev.WinGet.csproj), and never through a
constructed client, factory, context, or live COM object. For a C26 live-licensed member a unit test
does not reach it at all: there is no input to supply that is not itself a substitute for the
projection, so the absence of such a test is the contract holding, not failing.

The activation-selection seam is the declaration pointed at under *Activation-mode selector*. Tests call it with
inert return values and scripted exceptions. They do not construct or fake a projected WinGet type,
and they do not mutate CLSID, IID, or activation-flag tables.

### Build and workflow commands

Existing target declarations remain in [`build/Build.cs`](../build/Build.cs):

- `Test` is unit-only and produces the coverage input;
- `Coverage` produces the report and must enforce C10–C12;
- `ArchitectureTest` and `PackageTest` remain hermetic;
- `MachineStateTest`, `CatalogIntegrationTest`, and `PackedConsumerSmokeTest` are the stable live
  invocation names;
- `IntegrationTest` remains a local aggregate of `MachineStateTest` and `CatalogIntegrationTest` and
  has no CI blocking consequence of its own;
- `PublishGitHubPackages` and `PublishNuGet` retain their existing credentials and destinations, then
  gain positive confirmation under C21.

The documentation gate remains
[`build/Test-Documentation.ps1`](../build/Test-Documentation.ps1) with its existing parameters and
warning policy. Claim validation is a blocking rule: duplicates, invalid strengths, missing evidence
references, unsupported strength, and non-canonical restatement all make the gate fail. It does not
fetch external evidence during a documentation check; the committed Claim carries the reference the
gate validates structurally.

The pull-request workflow invokes `MachineStateTest` and `PackedConsumerSmokeTest` together in the
machine-state job and `CatalogIntegrationTest` separately. Both jobs retain normal failing outcomes;
only the machine-state job becomes required, and only after C23 is satisfied.

## Error semantics

### Product client

| Variant | Raised or returned when | Retryable | Caller action |
|---|---|---|---|
| `OperationCanceledException` | The caller's cancellation token cancels before or during work. | No automatic retry. | Treat as caller cancellation. Do not convert it to `PackageOperationStatus.Cancelled`. |
| `WinGetUnavailableException` | Every supported COM activation mode fails, or an existing CLI-only operation cannot start `winget.exe`. | A later independent call may retry after the environment changes; the current call is terminal. | Report WinGet/App Installer unavailable with the preserved inner diagnostics. |
| Version member unavailable | Activation succeeds, and reading the version member throws `InvalidCastException` with `HResult == 0x80004002` (`E_NOINTERFACE`). Returned as `null`, not thrown. | No automatic retry in the same environment. | Treat version as unavailable; do not infer that WinGet itself is unavailable. |
| Unexpected version access failure | Version access fails for any reason outside the unavailable-member classifier. The original typed exception propagates. | Determined by the concrete exception; the client adds no retry. | Diagnose the concrete failure. It must remain distinguishable from `null`. |
| Cancelled operation result | A failed operation carries `InstallCancelledByUser`. Returned as the result defined under *Public surface*. | No. | Treat as terminal user cancellation while retaining the HRESULT. |
| Other operation result | Projection status and extended error do not meet the cancelled rule. | Existing service retry rules only. | Use the existing normalized status and HRESULT; this contract adds no retry class. |

No other exception or HRESULT means that the version member is unavailable. A bare `catch`, message
match, normalization of every `InvalidCastException`, or normalization of any `COMException` to
`null` violates C16.

### Verification gates

| Variant | Occurs when | Retryable | Caller action |
|---|---|---|---|
| Assertion failure | Code or an artifact violates a gate assertion. | No without a new commit or corrected artifact. | Fail the gate for that commit. |
| Machine-state environment failure | WinGet, required local state, or the hosted runner cannot satisfy a machine-state precondition. | Yes after the environment changes. | Record the failed precondition and no licensed claim. Do not relabel it as product success. |
| Catalog-dependent failure | Network reachability, source agreement, or upstream catalog content prevents or contradicts a catalog assertion. | Yes after the upstream environment changes. | Fail the catalog-dependent gate and retain any separately passing evidence. Do not give it the machine-state gate's blocking consequence. |
| Vacuous assertion | A test's implication passes only because its input set is empty. | Yes in an environment with a witness. | Record no evidence for that assertion; the gate must make the missing witness visible. |
| Live-licensed translation regression | A C26 translation returns a wrong projection reading. | No without a corrected commit. | Only the live run whose named assertion covers that member detects it. Read a green hermetic check as asserting nothing about it, and give it exactly its risk class's blocking consequence under C8. |
| Publication mismatch | The push command succeeds or skips a duplicate, but the observed feed version is not the intended release. | No for the already-spent version identity. | Fail release verification and do not claim that feed published the Release. |

Gate failures are process failures with structured test or workflow output; no string-only success or
failure record satisfies C3.

### Documentation gate

Claim validation failures are blocking documentation findings. A warning cannot represent a broken
C1, C2, C5, or C20 invariant. Existing link, anchor, generated-file, and terminology semantics remain
unchanged.

## Unresolved

None.

The one previous entry — the eight remaining mapper signatures — is resolved and does not return.
`design/10-design.md` § *Module boundaries* now separates ownership from unit-testability, which
determines both halves the entry was blocked on: the eight belong to `WinGetProjectionMapper` by
purity under C13, and their licence is live evidence rather than a unit test under C26. Their
parameter types were never in doubt in the tree; what the design had not determined was whether a
projected parameter evicts a translation from the boundary, and it now determines that it does not.
