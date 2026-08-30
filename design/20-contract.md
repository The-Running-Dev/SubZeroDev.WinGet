# Contract — Evidence-bound support claims

This contract governs the evidence system designed in [10-design.md](10-design.md). The product's
existing public declarations remain authoritative for shape; this document states the semantics those
declarations, build targets, workflows, and Markdown files cannot express. It introduces no new public
library API.

## Invariants

| Id | Assertion | Owner | Enforcement |
|---|---|---|---|
| C1 | For each claim subject — consumer architecture, managed-assembly package shape, hosting context, live operation coverage, and the runtime version floor — exactly one authored Markdown statement is canonical. Every other mention references that statement rather than restating its assertion or strength. | Documentation | Planned code enforcement in the documentation gate. |
| C2 | A claim's strength is exactly one of `executed`, `contract-checked`, or `unvalidated`, and is justified by referenced evidence from an environment capable of licensing that strength. Wording alone never raises it. | Documentation | Planned code enforcement in the documentation gate; the evidence-to-strength judgement remains reviewable. |
| C3 | Evidence is identified by gate, environment, and commit. It records outcome, run identity, asserted facts, and explicit non-assertions. A passing subset licenses only the claims asserted by that subset. | Live-verification workflow | Planned workflow and artifact enforcement. |
| C4 | Environment facts are observed, not assumed. Live evidence records OS, architecture, WinGet version, interactivity, elevation state, configured-source state, and remote-catalog reachability where each fact affects interpretation. | Live-verification workflow | Planned workflow enforcement. |
| C5 | Evidence never licenses a broader environment than the one recorded. In particular, Windows x64 execution is not ARM64 runtime evidence, package-contract checks are not runtime evidence, and interactive-user execution is not service, SYSTEM, or elevation evidence. | Documentation and release review | Planned documentation-gate enforcement for the named support claims. |
| C6 | A gate exists only when a workflow invokes it, can fail on a violated assertion, and records a binary outcome for the exact commit tested. A target that merely reports a value is not a gate. | Build and workflows | Planned workflow enforcement. |
| C7 | The required pull-request check remains hermetic: it may compile, run unit tests, collect and enforce unit-only coverage, inspect PE architecture, and verify packed-package structure, but it must not activate WinGet COM, query a network catalog, invoke a package feed, or execute `winget.exe`. | `build` job | Enforced in workflow composition; negative dependency checks are planned. |
| C8 | `MachineStateTest` selects exactly seven machine-state tests, `CatalogIntegrationTest` selects exactly five catalog-dependent tests, and both fail before execution if their stable risk metadata selects another count. The machine-state target and `PackedConsumerSmokeTest` share one required pull-request status; the catalog target has a separately failing, non-required status. `IntegrationTest` is only their local aggregate. | Live-verification workflow | Planned test metadata, workflow, and count assertions. |
| C9 | `PackedConsumerSmokeTest` is separate from `PackageTest`. Both reuse one consumer-construction implementation, but only the smoke target executes the packed consumer and activates COM. | Build | Planned code enforcement. |
| C10 | `Coverage` evaluates one build-owned, checked-in decimal line-coverage floor over the library, with no file, class, namespace, or COM-orchestration exclusions. The value has one decimal place, and enforcement compares exact covered- and valid-line counts against its ratio rather than comparing rounded percentages. The report may include branch coverage, but only line coverage is gated initially. | Build | Planned code enforcement. |
| C11 | Coverage input comes from the unit-test run that excludes every NUnit `[Explicit]` test. Live-test execution must never contribute to the report compared with the floor. | Build | `[Explicit]` exclusion exists; floor and input assertions are planned. |
| C12 | The checked-in coverage floor is set from a measured unit-only result after the required unit tests land, is raised only with the measurement that justifies it, and is lowered only with a decision-log entry. | Build and decision log | Planned code enforcement for comparison; ratchet provenance is review-enforced. |
| C13 | `WinGetProjectionMapper` owns every pure projection enum, option, result, DTO, and collection translation used by the package and source clients. It has no call path to `WinGetFactory`, `WinGetComContext`, COM activation, network access, or `winget.exe`. Loading projection enum and struct metadata does not by itself make its tests integration tests. | Client translation boundary | Planned unit and architecture enforcement. |
| C14 | Every CsWinRT-projected collection is traversed by index. Exposing translation functions to tests must not replace indexed loops with `foreach` or LINQ. | Client translation boundary | Enforced by production code; regression enforcement is planned. |
| C15 | A failed operation whose extended HRESULT is `WinGetErrorCodes.InstallCancelledByUser` produces `PackageOperationStatus.Cancelled`, regardless of the projection status enum's lack of a cancelled member. Caller-token cancellation remains an `OperationCanceledException`; the two outcomes are not normalized together. | Client translation boundary | Planned code and unit-test enforcement. |
| C16 | `GetWinGetVersion` returns `null` only when the runtime does not expose the version member through the supported activation path. Cancellation and every unrelated failure remain distinguishable and propagate according to *Error semantics*. | Client layer | Planned code and live-test enforcement. |
| C17 | `WinGetActivationModeSelector` tries projection, local server, then local server with lower-trust registration. The first successful mode is reused for later activations in that factory; a cached mode's later failure propagates without reselection; total initial failure becomes `WinGetUnavailableException` with every attempt failure retained; a failure is not cached as a successful mode. | COM activation layer | The activation order exists in production code; extracting and unit-testing the selector is planned. |
| C18 | COM work remains serialized on one owned MTA thread, and the live test assembly remains non-parallel. Adding NUnit parallelization is a contract change. | COM owner and tests | Enforced by production code and current test configuration. |
| C19 | A live run with partial success records evidence for each passing assertion and failure for each failing assertion. It does not become an all-suite pass, and vacuous assertions over empty inputs license no claim. | Live-verification workflow | Planned test and artifact enforcement. |
| C20 | ARM64 is described only as build and package-contract support until ARM64 hardware evidence exists. Elevation, Windows Service, SYSTEM hosting, and mutating-operation live coverage remain `unvalidated`. | Documentation | Planned documentation-gate enforcement. |
| C21 | A release is identified by its pushed tag and exact commit. `v0.2.0` is the next stable release; `v0.1.0` is not moved. A publishing run is successful only after the intended version is positively confirmed on each intended feed. | Release workflow | Tag immutability is repository policy; positive feed confirmation is planned. |
| C22 | No work governed by this contract adds public library surface, changes the service/client/COM dependency direction, or broadens the documented CLI-shim exception beyond pins and export/import. | Product | Review-enforced; regression enforcement is planned. |
| C23 | The machine-state live status is not made required until the narrowed version-member classifier has run on the GitHub-hosted Windows x64 environment constituted by C24 and `PackedConsumerSmokeTest` has observed a non-null version. A `null` result or unrelated failure stops that rollout; it does not license a fallback observable, weaker assertion, or required status. | Live-verification workflow | Planned workflow and branch-protection sequencing. |
| C24 | The hosted live-verification jobs constitute their own WinGet runtime: they install one explicitly pinned build — the version the library's `Microsoft.WindowsPackageManager.ComInterop` reference pins — and record the App Installer version observed both before and after that install. A floating latest is not a pinned build, because C4's observed environment fact must be reproducible. Constituting the runtime is not a fallback observable: the assertion under C23 remains a non-null `GetWinGetVersion` from the packed consumer. | Live-verification workflow | Planned workflow enforcement. |
| C25 | `GetWinGetVersion` requires a runtime exposing WinGet COM contract 13, first declared in WinGet 1.12; below that it returns `null` under C16 rather than failing. That floor is a Claim with subject `runtime-version-floor` and is scoped to this member alone — no library-wide minimum WinGet version is asserted, because live evidence exists of the rest of the surface working below it. | Documentation | Planned documentation-gate enforcement. |

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
file, per-file children, or exclusions. Its initial numeric value is materialised only after the
required unit tests produce the measurement prescribed by C12.

### Projection mapper

The operation and request translations materialised in S5 are declared in
[`WinGetProjectionMapper.cs`](../SubZeroDev.WinGet/WinGetProjectionMapper.cs). The remaining internal
scaffold moves there in S6. C13-C15 and the state-dependent facts under *Product result types* remain
authoritative.

```csharp
internal static class WinGetProjectionMapper
{
    internal static List<PackageInfo> ToPackages(IReadOnlyList<MatchResult> matches);
    internal static PackageInfo ToPackageInfo(CatalogPackage package);
    internal static PackageDetails ToPackageDetails(CatalogPackage package);
    internal static List<string> CopyStrings(IReadOnlyList<string>? source);
    internal static List<PackageAgreementInfo> CopyAgreements(IReadOnlyList<PackageAgreement>? source);
    internal static List<PackageDocumentation> CopyDocumentations(IReadOnlyList<Documentation>? source);
    internal static List<PackageIconInfo> CopyIcons(IReadOnlyList<Icon>? source);
    internal static PackageSource ToPackageSource(PackageCatalogInfo info);
    internal static int GetPriority(PackageCatalogInfo info);
    internal static DateTimeOffset? ToNullableDate(DateTimeOffset value);
}
```

The mapper is stateless. Its callers supply already-created projection values; it never constructs a
client, factory, context, or live COM object. Every projected collection parameter is traversed by
index.

### Activation-mode selector

The following internal scaffold moves under the library's COM module. Materialisation replaces it
with pointers to the declarations and preserves C17-C18.

```csharp
internal enum ActivationMode
{
    Projection,
    LocalServer,
    LocalServerLowerTrust
}

internal sealed class WinGetActivationModeSelector
{
    internal T Create<T>(Func<ActivationMode, T> attempt) where T : class;
}
```

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
| Canonical claims in authored Markdown | `Claim.Subject` | `README.md` owns `consumer-architecture` and `managed-assembly-shape`; `docs/docs/troubleshooting.md` owns `hosting-context`; `docs/docs/testing.md` owns `operation-coverage`. Each subject has exactly one canonical statement, a valid strength, and evidence sufficient for that strength; all non-canonical mentions are links or explicit references. | Existing duplicated prose is narrowed or replaced with references in place. There is no format migration or generated claims file. `docs/docs/index.md` remains generated from `README.md` and is not a second authored Claim. |
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

The operation and request translations materialised in S5, plus `CopyStrings` and `ToNullableDate`
materialised in S6, are declared in
[`WinGetProjectionMapper.cs`](../SubZeroDev.WinGet/WinGetProjectionMapper.cs) and called from
[`WinGetClient.cs`](../SubZeroDev.WinGet/WinGetClient.cs) and
[`WinGetSourceClient.cs`](../SubZeroDev.WinGet/WinGetSourceClient.cs). The remaining scaffold under
*Projection mapper* — `ToPackages`, `ToPackageInfo`, `ToPackageDetails`, `CopyAgreements`,
`CopyDocumentations`, `CopyIcons`, `ToPackageSource`, `GetPriority` — is still declared as private
translations in `WinGetClient.cs` and `WinGetSourceClient.cs`. Their parameter types are undetermined
and are stated under *Unresolved*; until that is settled they may not be materialised here. Tests
reach the materialised mapper members only through the existing `InternalsVisibleTo` grant in
[`SubZeroDev.WinGet.csproj`](../SubZeroDev.WinGet/SubZeroDev.WinGet.csproj); they do not reach them
through a constructed client, factory, context, or live COM object.

The activation-selection seam is the scaffold under *Activation-mode selector*. Tests call it with
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

The unmaterialised build-target scaffold is:

```csharp
Target MachineStateTest { get; }
Target CatalogIntegrationTest { get; }
Target PackedConsumerSmokeTest { get; }
```

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
| Publication mismatch | The push command succeeds or skips a duplicate, but the observed feed version is not the intended release. | No for the already-spent version identity. | Fail release verification and do not claim that feed published the Release. |

Gate failures are process failures with structured test or workflow output; no string-only success or
failure record satisfies C3.

### Documentation gate

Claim validation failures are blocking documentation findings. A warning cannot represent a broken
C1, C2, C5, or C20 invariant. Existing link, anchor, generated-file, and terminology semantics remain
unchanged.

## Unresolved

### The eight remaining mapper signatures

`design/10-design.md` does not determine the parameter types of the eight translations still declared
as private members of [`WinGetClient.cs`](../SubZeroDev.WinGet/WinGetClient.cs) and
[`WinGetSourceClient.cs`](../SubZeroDev.WinGet/WinGetSourceClient.cs) — `ToPackages`, `ToPackageInfo`,
`ToPackageDetails`, `CopyAgreements`, `CopyDocumentations`, `CopyIcons`, `ToPackageSource`, and
`GetPriority`.

Each currently takes a concrete WinRT-projected type — `MatchResult`, `CatalogPackage`,
`PackageAgreement`, `Documentation`, `Icon`, or `PackageCatalogInfo`. Those types cannot be
constructed outside live COM activation, so C13 and S6.5 cannot both hold for them: the members
cannot be owned by `WinGetProjectionMapper` *and* exercised by unit tests that construct no client,
factory, context, or live COM object.

The design doc forecloses the resolution rather than leaving it open. *Module boundaries* states the
mapper is "an internal organization boundary, not a fakeable projection abstraction", and
*Alternatives considered* rejects mocking the projection because "a fake projection tests the fake".
`design/30-slices.md` S6 repeats it as an out-of-scope line.

Resolving this therefore requires a `design/10-design.md` amendment, not a contract edit. Until one
lands, S6.1 and S6.5 are unsatisfiable for these eight members and no slice may narrow, reinterpret,
or partially satisfy them. The scaffold under *Projection mapper* remains the declared intent; it is
not licence to materialise the members without the tests the invariants require.
