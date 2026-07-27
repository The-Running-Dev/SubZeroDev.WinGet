# Package Consumer Targets — Implementation Plan

Goal: make a consuming application work with only a direct reference to
`SubZeroDev.WinGet`. Consumers must no longer need their own direct
`Microsoft.WindowsPackageManager.ComInterop` reference just to receive
`Microsoft.Management.Deployment.dll`.

This plan implements the highest-value open item in
[ROADMAP.md](ROADMAP.md#phase-3--packaging-and-distribution). It is intentionally
checkbox-driven so each phase can be executed and reviewed independently.

## Phase 0 — Documentation and package discovery

### Allowed mechanisms

- [ ] Re-read NuGet's
  [MSBuild props and targets guidance](https://learn.microsoft.com/nuget/concepts/msbuild-props-and-targets)
  before implementation. Package-owned targets may be placed under `build/` for
  direct consumers or `buildTransitive/` when their behavior must flow through
  another package.
- [ ] Re-read the
  [NuGet pack MSBuild reference](https://learn.microsoft.com/nuget/reference/msbuild-targets)
  and use `Pack="true"` with an explicit `PackagePath` when adding the targets
  file to `SubZeroDev.WinGet.csproj`.
- [ ] Re-read Microsoft's
  [native files packaging guidance](https://learn.microsoft.com/nuget/create-packages/native-files-in-net-packages)
  before deciding whether native binaries belong under `runtimes/<rid>/native`
  or should be surfaced through an MSBuild item.
- [ ] Treat `win-x64` and `win-arm64` as opaque portable RIDs from the
  [.NET RID catalog](https://learn.microsoft.com/dotnet/core/rid-catalog);
  do not construct or parse RIDs.
- [ ] Inspect the pinned
  `Microsoft.WindowsPackageManager.ComInterop` 1.29.280 package and record its
  exact target and payload paths in the implementation PR.
- [ ] Confirm from `obj/project.assets.json` and generated
  `obj/*.nuget.g.targets` that the upstream package's `build/` target does not
  flow transitively to a clean consumer.

Current upstream behavior to preserve:

- `Microsoft.WindowsPackageManager.ComInterop.targets` selects architecture
  from `RuntimeIdentifier` first and then `Platform`.
- Its common target defaults to static factory linkage and adds the selected
  `bin/win-<architecture>/native/static/Microsoft.Management.Deployment.dll`
  through `ReferenceCopyLocalPaths`.
- The upstream package contains x86, x64, and ARM64 payloads, but this library's
  declared support remains x64 and ARM64.

### Anti-pattern guards

- [ ] Do not mutate restore inputs such as `TargetFramework`,
  `PackageReference`, or `PackageVersion` from a packaged `.targets` file.
- [ ] Do not assume a downstream `$(PkgMicrosoft_WindowsPackageManager_ComInterop)`
  property exists unless the clean consumer's generated restore files prove it.
- [ ] Do not hardcode the default global-packages directory.
- [ ] Do not silently select x64 for AnyCPU or ARM64 builds.
- [ ] Do not redistribute Microsoft binaries until their license permits it and
  the package layout has been reviewed.

### Phase verification

- [ ] Save the relevant generated import and asset evidence from a clean
  consumer build in the implementation PR description.
- [ ] Record the selected implementation strategy and why the rejected
  strategies failed or were less reliable.

## Phase 1 — Prove the target strategy on Windows

Compare these strategies in a disposable clean consumer before changing public
documentation:

- [ ] **A — Self-contained payload:** package the supported native DLLs inside
  `SubZeroDev.WinGet` and select the correct package-owned file.
- [ ] **B — Dependency payload:** locate the restored ComInterop dependency and
  add its selected DLL to `ReferenceCopyLocalPaths`.
- [ ] **C — Upstream import:** import or reuse the upstream target without
  duplicating projection, WinMD, or copy behavior.

Use the upstream ComInterop `.targets` files as the behavioral reference. Prefer
normal build/publish items or `ReferenceCopyLocalPaths` over an unconditional
late `Copy` target. If an SDK item is used, copy incrementally with
`CopyToOutputDirectory="PreserveNewest"` and
`CopyToPublishDirectory="PreserveNewest"`.

The winning strategy must:

- [ ] Work from a clean Windows x64 console application containing only a
  `PackageReference` to the locally packed `SubZeroDev.WinGet`.
- [ ] Restore and build with a non-default NuGet global-packages directory.
- [ ] Place the correct-architecture `Microsoft.Management.Deployment.dll` in
  both build output and publish output.
- [ ] Preserve the managed CsWinRT projection and WinMD behavior.
- [ ] Produce a clear build error for unsupported or unresolved platforms
  instead of copying a mismatched binary.
- [ ] Avoid duplicate target imports, duplicate copy items, and duplicate
  output entries.
- [ ] Pass a read-only runtime smoke test such as `GetWinGetVersion` on Windows
  x64.
- [ ] Define the ARM64 validation boundary: cross-build and inspect the ARM64
  output in CI; keep the existing "not validated on hardware" caveat until a
  real ARM64 runtime smoke test passes.

## Phase 2 — Package the supported implementation

- [ ] Add the canonical target file with a unique, idempotent target/item name.
- [ ] Choose `build/`, `buildTransitive/`, or a thin wrapper plus one canonical
  implementation based on the Phase 1 evidence.
- [ ] Add the target and any permitted payloads to
  `SubZeroDev.WinGet/SubZeroDev.WinGet.csproj` with explicit package paths.
- [ ] Keep architecture selection aligned with the upstream precedence:
  `RuntimeIdentifier`, then explicit platform.
- [ ] Support x64 and ARM64 only unless the public platform contract is changed
  in a separate decision.
- [ ] Preserve incremental build behavior; do not copy unchanged files on every
  build.
- [ ] Ensure both `dotnet build` and `dotnet publish` receive the required DLL.

### Phase verification

- [ ] Run `dotnet pack -c Release`.
- [ ] Inspect the `.nupkg` as a ZIP and assert the exact targets and payload
  paths.
- [ ] Inspect the embedded `.nuspec` dependency and asset metadata.
- [ ] Confirm `dotnet nuget verify` is used only for signature verification, not
  as a substitute for package-layout tests.

## Phase 3 — Add a packaging regression harness

The existing pull-request workflow runs `Test` and `Coverage` but never packs.
Because merges to `main` publish automatically, package verification must become
a pre-merge gate.

- [ ] Add a clean consumer fixture or generated test project that references
  only the locally produced `SubZeroDev.WinGet` package.
- [ ] Restore it from an isolated local feed and an isolated global-packages
  directory.
- [ ] Assert the generated NuGet target import.
- [ ] Assert the x64 build and publish outputs contain the x64 native DLL.
- [ ] Cross-build ARM64 and assert the output contains the ARM64 native DLL.
- [ ] Assert unsupported/ambiguous platform configurations fail with the
  intended actionable message.
- [ ] Add a Nuke packaging-test target or equivalent repeatable entry point.
- [ ] Run that target in the pull-request build job before any release can run.
- [ ] Keep live COM activation tests Windows-only and read-only.

## Phase 4 — Remove the consumer workaround

Only begin this phase after the clean-consumer package test and Windows x64
runtime smoke test pass.

- [ ] Remove the direct ComInterop reference from
  `SubZeroDev.WinGet.Examples/SubZeroDev.WinGet.Examples.csproj`.
- [ ] Decide whether the test project's direct reference remains necessary for
  ProjectReference-based integration tests; do not remove it merely because the
  installed-package scenario works.
- [ ] Build and run the examples through the packed-package consumer fixture,
  not only through `ProjectReference`.
- [ ] Verify the library retains its own direct ComInterop dependency for
  compilation and correct NuGet dependency metadata.

## Phase 5 — Update documentation and specification

- [ ] Replace the README's "one integration rule" warning with the validated
  package behavior and any remaining platform caveat.
- [ ] Update `docs/getting-started.md` so the minimal consumer truly contains
  one package reference.
- [ ] Update `docs/troubleshooting.md` with the new diagnostics and remove the
  obsolete direct-reference remedy.
- [ ] Update `docs/testing.md` with the packaging regression target and Windows
  smoke-test boundary.
- [ ] Update `SPECIFICATION.md` to distinguish the original transitive-copy
  failure from the shipped fix and its validation evidence.
- [ ] Check off and annotate the Phase 3 roadmap item only after all acceptance
  tests pass.
- [ ] Run the Docusaurus production build so broken documentation links fail
  before merge.

## Phase 6 — Final verification and release readiness

- [ ] Run the full mocked unit suite on Windows.
- [ ] Run the packaging regression matrix for x64, ARM64 cross-build, build, and
  publish.
- [ ] Run the read-only x64 COM smoke test from the clean package consumer.
- [ ] Run `git diff --check`.
- [ ] Inspect the final release `.nupkg` and embedded `.nuspec`.
- [ ] Confirm no direct consumer ComInterop reference remains in the minimal
  installation instructions.
- [ ] Confirm the PR build gates package correctness before the automatic
  GitHub Packages release job.
- [ ] Record ARM64 hardware validation as unresolved until it is actually run.

## Definition of done

- [ ] A clean supported Windows application works with only
  `PackageReference Include="SubZeroDev.WinGet"`.
- [ ] Build and publish select the correct native DLL for x64 and ARM64.
- [ ] Package behavior is regression-tested before merge.
- [ ] A read-only x64 runtime smoke test proves COM activation from the packed
  consumer.
- [ ] Documentation no longer instructs every consumer to add the dependency
  workaround.
- [ ] No support claim exceeds the validation actually performed.
