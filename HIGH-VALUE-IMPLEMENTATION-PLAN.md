# High-Value Implementation Plan

This plan executes the three highest-value remaining changes in dependency
order:

1. correct managed architecture output;
2. make the NuGet package self-sufficient for supported consumers;
3. prevent library awaits and synchronous COM work from freezing caller
   synchronization contexts.

The detailed package checklist remains in
[PACKAGING-TARGETS-PLAN.md](PACKAGING-TARGETS-PLAN.md). This document is the
cross-workstream execution plan and source of truth for ordering.

## Phase 0 — Verified documentation and constraints

### Allowed APIs and patterns

- [x] Use `<PlatformTarget>$(Platform)</PlatformTarget>` for explicit x64 and
  ARM64 executable/test project builds, as identified in
  [ROADMAP.md](ROADMAP.md#phase-1--correctness-fixes).
- [x] Use `buildTransitive/<tfm>/SubZeroDev.WinGet.targets` for modern
  PackageReference consumers, following
  [NuGet's MSBuild targets guidance](https://learn.microsoft.com/nuget/concepts/msbuild-props-and-targets).
- [x] Use `GeneratePathProperty="true"` on the library's direct ComInterop
  reference to locate pack-time payloads without assuming a global-packages
  directory.
- [x] Preserve the upstream ComInterop behavior: RID-first architecture
  selection, static native payload, WinMD copy, and
  `WindowsMetadataReference`.
- [x] Use `ConfigureAwait(false)` only outside COM-owner flows, and keep
  projected-object continuations on their dedicated owner context; use the
  [.NET synchronization-context documentation](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/executioncontext-synchronizationcontext)
  as rationale and verify behavior with non-pumping-context tests.
- [x] Use `Task.Run` only for coherent synchronous COM segments and remember
  that its cancellation token prevents scheduling but does not interrupt an
  already-running COM call.

### Verified constraints

- [x] The library now packs as an IL-only AnyCPU managed assembly; executable
  and test hosts are explicitly x64/ARM64 and have PE regression checks.
- [x] One package carries x64 and ARM64 native payloads plus the WinMD under
  its canonical `buildTransitive` target; this does not establish ARM64
  hardware runtime support.
- [x] ProjectReference-based tests and examples still need direct ComInterop
  references; packaged `buildTransitive` assets do not apply to them.
- [x] Projected WinRT/COM objects are not proven agile. Do not move an already
  created projected object across a `Task.Run` boundary without live
  validation.
- [x] The ComInterop package is MIT-licensed and ships `NOTICE.txt`; a
  self-contained package must include appropriate third-party attribution.

### Anti-pattern guards

- [ ] Do not claim ARM64 package support from a cross-build alone.
- [ ] Do not assume `<PlatformTarget>$(Platform)</PlatformTarget>` creates a
  dual-architecture package.
- [ ] Do not hardcode NuGet cache paths or synthesize RIDs.
- [ ] Do not remove direct ComInterop references from ProjectReference
  consumers.
- [ ] Do not ship only the native DLL while omitting WinMD/metadata behavior.
- [ ] Do not treat `ConfigureAwait(false)` as a substitute for moving
  synchronous work that occurs before the first await.
- [ ] Do not wrap isolated property reads on existing COM objects in
  `Task.Run` unless object agility is established.
- [ ] Do not replace indexed WinRT collection loops with `foreach` or LINQ.

## Phase 1 — Correct project architecture and choose package architecture

### Implementation

- [x] Change the tests and examples to
  `<PlatformTarget>$(Platform)</PlatformTarget>`.
- [x] Add a regression check proving executable/test x64 builds emit PE machine
  `0x8664` and ARM64 builds emit `0xAA64`.
- [x] Spike an AnyCPU managed library build using an explicit producer
  configuration: keep `Platform=x64` so the upstream ComInterop build target
  can resolve its producer-only native asset, override the library's
  `PlatformTarget=AnyCPU`, and verify the resulting library is IL-only AnyCPU.
- [x] If the library compiles as IL-only AnyCPU, adopt that package shape
  provisionally while keeping consumers explicitly x64/ARM64 for native target
  selection and using one managed library in `lib/<tfm>`.
- [ ] Keep the AnyCPU selection provisional until a Windows x64 runtime smoke
  test passes against the packed consumer experience; revert to the documented
  architecture-specific package shape if that test exposes an incompatibility.
- [ ] If AnyCPU is not viable, stop and implement the documented
  architecture-specific package shape:
  `ref/<tfm>` plus `runtimes/win-{x64,arm64}/lib/<tfm>`.
- [ ] Keep the ARM64 hardware-validation caveat until a real ARM64 execution
  test passes.

### Verification

- [x] Build tests and examples for x64 and ARM64.
- [x] Assert AnyCPU for the library if selected, and x64/ARM64 for executable
  fixtures; otherwise inspect both architecture-specific library assets.
- [x] Pack once and inspect the managed asset layout.
- [x] Confirm an ARM64 consumer never receives an x64 managed assembly.

## Phase 2 — Ship self-contained package consumer targets

Follow [PACKAGING-TARGETS-PLAN.md](PACKAGING-TARGETS-PLAN.md), with Strategy A
as the evidence-favored implementation.

### Implementation

- [x] Set `GeneratePathProperty="true"` on the library's ComInterop reference.
- [x] Pack the x64 and ARM64 static native DLLs, WinMD, canonical
  `buildTransitive` target, and `THIRD-PARTY-NOTICES.txt`.
- [x] Select exact `win-x64`/`win-arm64` RIDs first, then explicit
  `PlatformTarget`, then explicit `Platform`.
- [x] Fail clearly for unresolved AnyCPU or unsupported platforms.
- [x] Add the selected native DLL and WinMD through
  `ReferenceCopyLocalPaths`, plus `WindowsMetadataReference` with
  `Implementation="Microsoft.Management.Deployment.dll"`.
- [x] Guard against duplicate items when a consumer intentionally retains a
  direct ComInterop reference.
- [x] Keep direct ComInterop references in the library, tests, and examples.
  The library needs the dependency to compile and package its payloads; the ProjectReference
  test and example hosts need it because package build assets do not apply to their outputs.

### Verification

- [x] Inspect exact `.nupkg` entries and embedded `.nuspec`.
- [x] Create an isolated clean consumer referencing only the locally packed
  `SubZeroDev.WinGet`.
- [x] Create a two-hop fixture (`app -> wrapper package -> SubZeroDev.WinGet`)
  proving the canonical `buildTransitive` target reaches an indirect consumer.
- [x] Restore with a non-default global-packages directory.
- [x] Build and publish x64 and ARM64; validate managed and native PE types.
- [x] Verify target import and absence of duplicate output items.
- [x] Verify incremental rebuilds do not duplicate or stale-copy assets, and
  `dotnet clean` removes copied assets.
- [ ] Run a read-only `GetWinGetVersion` smoke test on Windows x64.
- [x] Add a Nuke `PackageTest` target and run it in PR CI before release.

## Phase 3 — Fix synchronization-context capture

### Implementation

- [x] Add `ConfigureAwait(false)` to service and CLI awaits; COM-client awaits
  intentionally capture the dedicated owner context.
- [x] Forward caller-provided `IProgress<T>` instances unchanged.
- [x] Pass cancellation into WinRT task adapters or retain an explicit
  cancellation registration; do not assume
  `Task.Run` can interrupt synchronous COM calls.

### Verification

- [x] Add a non-pumping `SynchronizationContext` test using delayed fake
  clients.
- [x] Cover package search, a retrying package operation, and every async
  package-source service path.
- [x] Verify the library has no unreviewed raw awaits.

## Phase 4 — Move synchronous COM work off caller contexts

### Implementation

- [ ] Choose and document COM ownership before offloading: query/verify agility
  for every projected type that would cross threads on Windows; if it is not
  proven, introduce a dedicated COM dispatcher that owns PackageManager
  activation and all synchronous projected-object access.
- [x] Do not retain a caller-created or arbitrary-worker-created shared
  `Lazy<PackageManager>` while dispatching its use to unrelated pool threads.
- [x] Introduce the smallest testable dispatcher/offload seam that enforces the
  selected ownership model.
- [x] Move package-manager activation, catalog enumeration/options setup, and
  synchronous result materialization off the caller thread within that model.
- [x] Apply the same rule to source add/remove/refresh setup and result
  materialization where currently caller-bound.
- [x] Keep WinRT async operations asynchronous; do not block pool threads with
  `.GetAwaiter().GetResult()`.

### Verification

- [x] Unit-test the offload seam for thread change, cancellation-before-start,
  result propagation, and exception propagation.
- [x] Test repeated and concurrent calls so ownership is not proved only for a
  single invocation.
- [ ] Run read-only Windows x64 integration tests.
- [ ] Run a single-thread synchronization-context/UI responsiveness harness
  for search, details, and source operations.
- [ ] Verify cancellation registrations are disposed and caller-supplied
  `IProgress<T>` callbacks retain their chosen delivery context.
- [ ] Record COM agility assumptions and the activation path exercised.

## Phase 5 — Documentation, roadmap, and final checks

- [x] Update consumer installation documentation with the package-contract
  evidence while retaining the Windows runtime caveat.
- [x] Update architecture/threading documentation with the verified execution
  model and cancellation limits.
- [x] Update `CLAUDE.md`, `README.md`, `ROADMAP.md`, and the relevant `docs/`
  pages.
- [x] Add a dated implementation/amendment note to `SPECIFICATION.md`; preserve
  original normative requirements and make new decisions visibly attributable.
- [ ] Check off only work actually validated.
- [ ] Run unit tests, integration tests available on Windows, `PackageTest`,
  Docusaurus build, `git diff --check`, and final `.nupkg` inspection.

## Definition of done

- [ ] Explicit x64 and ARM64 project builds emit correctly marked assemblies.
- [ ] A clean supported application references only `SubZeroDev.WinGet` and
  receives the correct managed, native, and WinMD assets.
- [ ] PR CI prevents a broken package from reaching the automatic release job.
- [ ] Library awaits do not capture caller synchronization contexts.
- [ ] Synchronous COM work no longer blocks the caller context within the
  validated execution model.
- [ ] x64 runtime behavior is proven; ARM64 claims remain limited to the
  validation actually performed.
