# Slices — Prove the provisional claims and cut v0.2.0

The riskiest remaining assumption is that the packed consumer can obtain a non-null WinGet version
on GitHub-hosted Windows after the version-member classifier is narrowed. S1 makes the result
falsifiable; S2 runs that exact packed-consumer call before any live status becomes required. If S2
does not observe a non-null version, the sequence stops at the brief/environment decision required by
`design/20-contract.md` C23. Later slices do not weaken the observable or substitute
`winget --version`.

`/track` should be run after this document is reviewed. Do not open issues from this command.

## How this document is kept

`## Outstanding` is the authoritative specification for work that has not landed. `/slices` appends
new slices at the next unused number; slice and criterion ids are never renumbered or reused.

Once a slice's issue is closed, retire its full body to the `## Landed` index, preserving its id,
name, issue, and the commit at which its body was last authoritative. The body remains recoverable
from git history, while `/track` ignores landed slices when checking criterion drift.

## Outstanding

## S1 — Version absence becomes falsifiable
Delivers: A .NET consumer can distinguish a runtime that lacks the WinGet version member from a
real activation, interop, projection, or cancellation failure instead of receiving `null` for all of
them.
Touches: `SubZeroDev.WinGet/WinGetClient.cs`, `SubZeroDev.WinGet.Tests/`
Depends on: none
Acceptance:
  - S1.1 When the activated backend supplies a non-null, non-whitespace version,
    `IWinGetClient.GetWinGetVersion` and the service forwarder return that value unchanged.
  - S1.2 An `InvalidCastException` with `HResult == 0x80004002` (`E_NOINTERFACE`) while reading the
    version member returns `null`.
  - S1.3 An `InvalidCastException` with any other HRESULT, any `COMException`, and any activation or
    projection failure propagate as their original typed failures rather than returning `null`.
  - S1.4 Caller-token cancellation remains `OperationCanceledException` and is not classified as an
    unavailable version member.
  - S1.5 The existing public signatures remain unchanged, and the implementation adds neither a
    `winget --version` fallback nor client-layer logging.
  - S1.6 Reinstating the former blanket catch makes at least one new classifier regression test fail.
Out of scope: constructing or executing a packed consumer, changing CI status policy, or changing any
    public declaration.

---

## S2 — A packed consumer proves the version path on hosted Windows
Delivers: The maintainer can run one named gate that installs the built package into a real consumer
and proves that consumer can obtain a WinGet version on GitHub-hosted Windows x64.
Touches: `build/Build.cs`, `.github/workflows/spike-com-activation.yml`,
         `.github/workflows/build.yml`, packed-consumer artifacts under the build output
Depends on: S1
Acceptance:
  - S2.1 `PackedConsumerSmokeTest` constructs its consumer through the same implementation used by
    `PackageTest`, restores from the produced `.nupkg`, and uses no `ProjectReference` to the library.
  - S2.2 On Windows x64 with WinGet installed, the packed consumer calls `GetWinGetVersion` and fails
    unless the returned value is non-null and non-whitespace.
  - S2.3 `PackageTest` remains hermetic: it validates package structure without executing the
    consumer, activating COM, invoking `winget.exe`, or contacting a feed.
  - S2.4 A GitHub-hosted Windows x64 run after S1 records the exact commit and run identity and
    observes a non-null version from the packed consumer.
  - S2.5 The recorded run includes the observed OS, architecture, WinGet version, interactivity, and
    elevation state; an absent prerequisite is a failed gate and licenses no runtime claim.
  - S2.6 A `null` result or unrelated failure leaves the machine-state status non-required and stops
    the sequence for the brief/environment decision in C23; no fallback observable or weaker
    assertion is introduced.
Out of scope: classifying the twelve project-reference integration tests, making a status required,
    ARM64 runtime execution, or any mutating WinGet operation.

---

## S3 — Cancelled operations return the cancelled result
Delivers: A caller whose WinGet operation is cancelled by the user receives the existing cancelled
terminal result and can distinguish it from both caller-token cancellation and other failures.
Touches: `SubZeroDev.WinGet/WinGetClient.cs`,
         `SubZeroDev.WinGet/PackageManagementService.cs`, `SubZeroDev.WinGet.Tests/`
Depends on: none
Acceptance:
  - S3.1 A failed operation carrying an extended exception whose HRESULT is
    `WinGetErrorCodes.InstallCancelledByUser` returns `Status == PackageOperationStatus.Cancelled`,
    `Succeeded == false`, and preserves that HRESULT in `ExtendedErrorCode`.
  - S3.2 The same projection status with any other HRESULT retains its existing mapped status and
    failure data.
  - S3.3 The service layer performs no automatic retry for a returned cancelled result.
  - S3.4 Cancellation requested through the caller's token still throws `OperationCanceledException`
    rather than returning a cancelled result.
  - S3.5 No public declaration or existing retry class changes, and reverting the HRESULT
    classification makes the new result test fail.
Out of scope: adding a new cancellation value, changing caller-token semantics, or covering live
    mutating operations.

---

## S4 — Activation fallback is deterministic under unit test
Delivers: The maintainer can prove the production activation order, fallback, caching, and failure
behaviour without activating WinGet or constructing projected COM objects in a unit test.
Touches: `SubZeroDev.WinGet/Com/WinGetFactory.cs`,
         `SubZeroDev.WinGet/Com/WinGetActivationModeSelector.cs`,
         `SubZeroDev.WinGet.Tests/`
Depends on: none
Acceptance:
  - S4.1 With scripted failures, the selector attempts `Projection`, `LocalServer`, then
    `LocalServerLowerTrust`, stops at the first success, and returns that attempt's value.
  - S4.2 After the first success, a later create invokes only the cached mode; it does not retry the
    earlier modes.
  - S4.3 If the cached mode later fails, that failure propagates and no mode is reselected for that
    factory.
  - S4.4 If all three initial attempts fail, the caller receives `WinGetUnavailableException` with
    every attempt failure retained, and a later independent call may attempt the sequence again.
  - S4.5 Concurrent first callers serialize mode selection so that one successful mode becomes the
    factory invariant and no caller observes a mixed activation sequence.
  - S4.6 `WinGetFactory` remains the sole owner of projection constructors, CLSIDs, IIDs, and raw
    activation flags; selector tests use inert values and scripted exceptions only.
Out of scope: changing activation order or flags, adding retry after a cached-mode failure, or
    replacing the owned MTA context.

---

## S5 — Operation and request translations have one tested owner
Delivers: The maintainer can exercise package-operation and request translations directly as unit
tests, while production callers continue to receive the same results without activating WinGet.
Touches: `SubZeroDev.WinGet/WinGetClient.cs`,
         `SubZeroDev.WinGet/WinGetProjectionMapper.cs`, `SubZeroDev.WinGet.Tests/`
Depends on: S3
Acceptance:
  - S5.1 `WinGetProjectionMapper` is the sole declaration owner for `FindVersionId`, operation-result
    creation, installer-error extraction, the four operation-status mappings, and the scope, mode,
    architecture, and installer-kind mappings declared in `design/20-contract.md`.
  - S5.2 Production client paths call those mapper members directly and no duplicate private
    translation remains in `WinGetClient`.
  - S5.3 Table-driven unit tests cover every declared projection enum member and an unknown value;
    each input produces the exact public enum value required by the current declarations.
  - S5.4 Operation-result tests cover success, ordinary failure, installer error preservation, and
    the cancelled-by-user result from S3.
  - S5.5 Version selection returns the requested version when present and `null` when absent without
    enumerating a projected collection through `foreach` or LINQ.
  - S5.6 The mapper has no call path to `WinGetFactory`, `WinGetComContext`, network access, or
    `winget.exe`, and no public API is added.
Out of scope: DTO and source projection, live COM tests, service retry changes, or collection
    traversal outside the moved operation/request translations.

---

## S6 — DTO and collection projections have one tested owner
Delivers: The maintainer can verify package, details, agreement, documentation, icon, and source
projections with deterministic unit tests, including their empty and ordered collection behaviour.
Touches: `SubZeroDev.WinGet/WinGetClient.cs`, `SubZeroDev.WinGet/WinGetSourceClient.cs`,
         `SubZeroDev.WinGet/WinGetProjectionMapper.cs`, `SubZeroDev.WinGet.Tests/`
Depends on: S5
Acceptance:
  - S6.1 Every remaining pure DTO, source, date, and collection translation declared for
    `WinGetProjectionMapper` in `design/20-contract.md` is owned there and called by both production
    clients; the clients retain no duplicate pure mapper.
  - S6.2 Given representative projection values, package summaries, package details, agreements,
    documentation entries, icons, and source records preserve the declared fields and input order in
    plain .NET model values.
  - S6.3 Null optional projected collections produce the existing empty/null public result required
    by the model, while non-empty collections preserve every item exactly once.
  - S6.4 Every CsWinRT-projected collection in the mapper is traversed by index; an automated
    regression check rejects `foreach` or LINQ traversal over those parameters.
  - S6.5 Unit tests reach the mapper through the existing internals grant and do not construct a
    client, factory, owner context, network connection, or `winget.exe` process.
  - S6.6 An architecture check verifies that the completed mapper has no dependency path to
    `WinGetFactory`, `WinGetComContext`, or the CLI shim, and the public surface is unchanged.
Out of scope: changing DTO shape, adding a projection abstraction, changing service behaviour, or
    executing live COM.

---

## S7 — The live suite has stable risk-class entry points
Delivers: A maintainer can run the machine-dependent and remote-catalog-dependent live checks
separately and know that a clean result did not silently omit tests.
Touches: `SubZeroDev.WinGet.Tests/WinGetClientIntegrationTests.cs`, `build/Build.cs`,
         repository test documentation that names the local invocation
Depends on: none
Acceptance:
  - S7.1 Stable test metadata classifies exactly seven existing tests as machine-state and exactly
    five as catalog-dependent; selection does not depend on fixture or method name substrings.
  - S7.2 `MachineStateTest` and `CatalogIntegrationTest` assert their expected selected counts before
    execution and fail if a test is added, removed, or misclassified without updating the contract.
  - S7.3 `IntegrationTest` composes both risk-specific targets and selects all twelve live tests
    exactly once; it remains a local aggregate with no CI blocking consequence of its own.
  - S7.4 The machine-state set contains no assertion whose witness is a fixed remote package identity
    or catalog content; the five such assertions run only in the catalog-dependent set.
  - S7.5 The live assembly remains non-parallel, and `PackedConsumerSmokeTest` is not counted as a
    thirteenth project-reference integration test.
  - S7.6 The stale documented fixture-name filter is replaced by the stable target invocations, and
    an intentionally under-selecting fixture filter no longer represents a supported clean run.
Out of scope: workflow jobs, branch-protection settings, evidence artifacts, or changing any live
    test into a mutating operation.

---

## S8 — Pull requests record live evidence without contaminating the hermetic check
Delivers: For every pull request, the maintainer can see separately whether local WinGet behaviour,
the packed consumer, and Microsoft's current catalog passed, with the machine-state result enforced
without turning catalog drift into a merge blocker.
Touches: `.github/workflows/build.yml`, `build/Build.cs`,
         `SubZeroDev.WinGet.Tests/WinGetClientIntegrationTests.cs`, CI evidence artifacts,
         repository branch-protection configuration
Depends on: S2, S7
Acceptance:
  - S8.1 Pull requests run `MachineStateTest` and `PackedConsumerSmokeTest` together in one
    machine-state job and run `CatalogIntegrationTest` in a separate catalog job; both jobs retain
    normal failing outcomes.
  - S8.2 The existing hermetic job remains required and has an automated negative dependency check
    proving it does not activate COM, run either live-test class, execute `winget.exe`, or contact a
    package feed.
  - S8.3 After S2's non-null hosted observation, branch protection requires the machine-state job in
    addition to the hermetic job and does not require the catalog job.
  - S8.4 Each live job records the exact commit, run identity, binary outcome, non-empty asserted
    facts, explicit non-assertions, and the environment facts required by C4 in a retained artifact.
  - S8.5 A GitHub-hosted Windows x64 run records all twelve project-reference integration tests green
    at their asserted seven/five counts and records the packed-consumer smoke separately.
  - S8.6 Partial success retains evidence for each passing assertion without becoming an all-suite
    pass; an empty input that makes an implication vacuously true is surfaced and licenses no claim.
  - S8.7 A catalog outage or changed witness fails only the catalog job and does not erase separately
    passing machine-state evidence.
Out of scope: ARM64 runtime claims, elevation, service/SYSTEM hosting, mutating-operation coverage, or
    swallowing a live failure to make a workflow green.

---

## S9 — Unit coverage has an exact ratcheting floor
Delivers: A maintainer gets a unit-only coverage gate that fails a regression at a checked-in floor
instead of merely reporting a percentage that no workflow enforces.
Touches: `build/Build.cs`, coverage test fixtures under `build/` or `tools/`,
         `.github/workflows/build.yml`
Depends on: S4, S6
Acceptance:
  - S9.1 After the S4–S6 unit tests land, the slice records the measured unit-only covered-line and
    valid-line counts and declares one private build-owned decimal floor exactly 0.1 percentage point
    below the one-decimal measured result.
  - S9.2 `Coverage` compares the exact integer counts against that decimal ratio; a synthetic report
    one line below the boundary fails and reports the counts and floor, while a report at the boundary
    passes.
  - S9.3 The gated report covers the whole library with no file, class, namespace, or COM-orchestration
    exclusions and is produced only by the unit run that excludes every `[Explicit]` test.
  - S9.4 Live targets cannot contribute files or counts to the report evaluated by the floor.
  - S9.5 The floor has one decimal place, is constrained to `0..100`, and has no command-line override
    or separate data file.
  - S9.6 Restoring report-only coverage makes the new negative gate test fail; lowering the checked-in
    floor requires a decision-log entry, while an increase carries its justifying measurement.
Out of scope: branch-coverage enforcement, per-file thresholds, exclusions that improve the displayed
    ratio, or an aspirational floor chosen before measurement.

---

## S10 — Support claims point to the evidence that licenses them
Delivers: A package consumer can tell which architecture, package-shape, hosting, and operation claims
were executed, contract-checked, or remain unvalidated without comparing contradictory documents.
Touches: `README.md`, `docs/docs/index.md`, `docs/docs/getting-started.md`,
         `docs/docs/architecture.md`, `docs/docs/testing.md`,
         `docs/docs/troubleshooting.md`, `SPECIFICATION.md`,
         `build/Test-Documentation.ps1`, documentation-gate tests
Depends on: S2, S8, S9
Acceptance:
  - S10.1 The five claim subjects have exactly the canonical owners assigned by C1, and each canonical
    statement names one valid strength plus the evidence sufficient for that strength.
  - S10.2 Windows x64 execution evidence is not presented as ARM64 evidence; ARM64 is stated only as
    build/package-contract support, while elevation, service/SYSTEM hosting, and mutating-operation
    live coverage remain `unvalidated`.
  - S10.3 Getting Started, Architecture, and the specification link or explicitly refer to the
    canonical statements instead of restating their assertion or strength; the generated homepage
    remains a projection of the README rather than a second authored owner.
  - S10.4 The documentation gate fails separate negative fixtures for a duplicate owner, invalid
    strength, missing evidence reference, evidence insufficient for the claimed strength, and a
    non-canonical restatement.
  - S10.5 The documentation gate passes the repository's real documents, including the regenerated
    homepage, only after every support claim matches evidence produced by the exact referenced run.
  - S10.6 Existing link, anchor, generated-file, warning, and terminology checks retain their current
    behaviour.
  - S10.7 The `runtime-version-floor` subject has a canonical owner stating that `GetWinGetVersion`
    requires WinGet 1.12 or newer and returns `null` below it, per C25; no document asserts a
    library-wide minimum WinGet version.
Out of scope: the documentation redesign, generated claim manifests, changing routes or information
    architecture, or promoting a claim from wording alone.

---

## S11 — Publishing succeeds only after both feeds confirm the intended version
Delivers: The maintainer can publish a release and have each publishing target fail unless its feed
can be queried back for the exact version, so a skipped duplicate or green push command is not
mistaken for delivery.
Touches: `build/Build.cs`, `.github/workflows/build.yml`, `GitVersion.yml`,
         `SubZeroDev.WinGet/SubZeroDev.WinGet.csproj`, `SPECIFICATION.md`, publication test fixtures
Depends on: S9
Acceptance:
  - S11.1 `PublishGitHubPackages` confirms through the intended GitHub Packages feed that the exact
    GitVersion-derived package version is visible after push; push success or `--skip-duplicate`
    without that observation fails the target.
  - S11.2 `PublishNuGet` confirms through NuGet.org that the exact project-version package is visible
    after push; command success without that observation fails the target.
  - S11.3 Each confirmation records the release tag/ref, exact commit, destination, intended version,
    observed version, and workflow run identity without exposing credentials.
  - S11.4 Publication tests make both targets fail for a successful push followed by a missing or
    mismatched feed version and pass only for an exact match.
  - S11.5 GitVersion and project version inputs both prepare `0.2.0`, the workflow publishes from the
    exact checked-out ref, and stale comments or specification claims about `v0.1.0` and previously
    exercised publishing paths are corrected to the verified history.
  - S11.6 Re-running confirmation for the same intended version is safe; changing the version or tag
    to escape a partial publish is not automated.
Out of scope: creating or pushing `v0.2.0`, moving `v0.1.0`, deleting a feed version, or publishing
    from a commit that has not passed the release prerequisites.

---

## S12 — The proven release is cut and observed on both feeds
Delivers: A package consumer can install the immutable `v0.2.0` release from either intended feed,
and the maintainer has run evidence tying both published packages to the exact tested commit.
Touches: git tag `v0.2.0`, GitHub Actions run history and evidence artifacts,
         GitHub Packages, NuGet.org, release references in canonical repository documentation
Depends on: S8, S10, S11
Acceptance:
  - S12.1 The exact release commit has green required hermetic and machine-state statuses, a recorded
    green catalog result for all five catalog tests, and the evidence artifacts required by C3–C4.
  - S12.2 One immutable `v0.2.0` tag is created at that commit and pushed once; `v0.1.0` still resolves
    to its original published commit.
  - S12.3 The tag workflow executes `PublishGitHubPackages`, and positive feed confirmation observes
    `SubZeroDev.WinGet` version `0.2.0` tied to the tagged commit.
  - S12.4 `PublishNuGet` executes against the same tagged commit, and positive NuGet.org confirmation
    observes `SubZeroDev.WinGet` version `0.2.0`.
  - S12.5 The release record references the tag, commit, both workflow runs, both feed confirmations,
    asserted facts, and explicit non-assertions; no document claims success before both confirmations
    exist.
  - S12.6 If only one feed confirms the package, that feed remains published state but the slice and
    release criterion remain incomplete; neither tag is moved and no replacement version is invented.
Out of scope: `v0.2.1`, moving or deleting published tags/packages, ARM64 runtime claims, or any
    validation excluded by the brief.

## Landed

| Slice | Issue | Landed at |
|---|---|---|
