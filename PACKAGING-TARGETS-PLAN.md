# Package Consumer Targets — Implementation Plan

Goal: make a consuming application work with only a direct reference to
`SubZeroDev.WinGet`. Consumers must no longer need their own direct
`Microsoft.WindowsPackageManager.ComInterop` reference just to receive
`Microsoft.Management.Deployment.dll`.

This plan implements the formerly highest-value open item in
[ROADMAP.md](ROADMAP.md#phase-3--packaging-and-distribution). It is intentionally
checkbox-driven so each phase can be executed and reviewed independently.

## Prerequisite — the ARM64 half needs a managed-layout decision

The original package contained an x64-marked managed assembly (verified PE
machine type `0x8664`). The shipped package instead uses an IL-only AnyCPU
managed assembly plus explicit x64/ARM64 consumer selection. This is
package-contract evidence; Windows x64 runtime and ARM64 hardware execution
are still required before expanding runtime claims.

- [x] Complete Phase 1 of
  [HIGH-VALUE-IMPLEMENTATION-PLAN.md](HIGH-VALUE-IMPLEMENTATION-PLAN.md) before
  starting Phase 2 here: prove an AnyCPU managed library is viable, or pack
  distinct managed assemblies under
  `runtimes/win-{x64,arm64}/lib/<tfm>` with a common `ref/<tfm>` reference
  assembly.
- [x] Change tests and examples to
  `<PlatformTarget>$(Platform)</PlatformTarget>` so their explicit ARM64 builds
  are no longer emitted as x64.

The selected managed layout is IL-only AnyCPU, with executable/test consumers explicitly x64 or
ARM64. This makes package-contract validation possible for both supported architectures; it does
not establish Windows x64 runtime activation or ARM64 hardware execution.

## Where each phase can run

Package *layout* work does not require Windows. `dotnet build` fails on macOS
with `NETSDK1100`, but `dotnet pack -p:EnableWindowsTargeting=true` succeeds and
produces an inspectable `.nupkg`, which covers Phase 0's package discovery and
Phase 2's layout assertions. Clean-consumer restore/build/publish and layout/PE
assertions may also run cross-platform with `EnableWindowsTargeting=true`.
COM activation and architecture-native runtime smoke tests require Windows.

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
- [x] Inspect the pinned
  `Microsoft.WindowsPackageManager.ComInterop` 1.29.280 package and record its
  exact target and payload paths in the implementation PR. **Done** — layout
  recorded under "Verified upstream layout" below.
- [ ] Confirm from `obj/project.assets.json` and generated
  `obj/*.nuget.g.targets` that the upstream package's `build/` target does not
  flow transitively to a clean consumer. **Partly answered** — the mechanism is
  confirmed (no `buildTransitive/` folder upstream; our own packed nuspec
  excludes build assets, see below). Still worth capturing the generated files
  from a real clean consumer as PR evidence.

### Verified upstream layout (1.29.280)

| Path | Contents |
|---|---|
| `build/Microsoft.WindowsPackageManager.ComInterop.common.targets` | Linkage default, WinMD reference, `ReferenceCopyLocalPaths` items, the two error targets |
| `build/net8.0-windows10.0.26100.0/…ComInterop.targets` | Architecture selection, then imports the common targets |
| `build/native/…ComInterop.targets` | Same for native consumers |
| `bin/win-{x86,x64,arm64}/native/{static,dynamic}/Microsoft.Management.Deployment.dll` | The native payloads |
| `lib/net8.0-windows10.0.26100.0/Microsoft.Management.Deployment.CsWinRTProjection.dll` | Managed projection — this *does* flow transitively |
| `lib/uap10.0/Microsoft.Management.Deployment.winmd` | WinMD, added via `ReferenceCopyLocalPaths` by the targets |

There is **no `buildTransitive/` folder** — that single fact is the whole root
cause. NuGet auto-imports `build/` only for a direct reference.

### Our own packaged dependency metadata excludes build assets

`dotnet pack` emits the ComInterop dependency as:

```xml
<dependency id="Microsoft.WindowsPackageManager.ComInterop" version="1.29.280" exclude="Build,Analyzers" />
```

That is NuGet's default `PrivateAssets` (`contentfiles;analyzers;build`) for a
`PackageReference`, and `buildTransitive` belongs to the `build` asset group. So
build assets from that dependency are suppressed for our consumers *even if
Microsoft shipped a `buildTransitive/` folder tomorrow*. Any strategy that
relies on upstream MSBuild logic reaching the consumer must also set
`PrivateAssets="analyzers;contentfiles"` on our reference.

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
- [x] Do not redistribute Microsoft binaries until their license permits it and
  the package layout has been reviewed. **License resolved** — the upstream
  nuspec declares `<license type="expression">MIT</license>`, so redistribution
  is permitted with attribution. Redistribution therefore still needs a layout
  review and an attribution decision (the upstream package ships a `NOTICE.txt`
  for its own third-party components), but it is no longer legally blocked.

### Phase verification

- [ ] Save the relevant generated import and asset evidence from a clean
  consumer build in the implementation PR description.
- [ ] Record the selected implementation strategy and why the rejected
  strategies failed or were less reliable.

## Phase 1 — Prove the target strategy on Windows

Compare these strategies in a disposable clean consumer before changing public
documentation:

- [ ] **A — Self-contained payload:** package the supported native DLLs inside
  `SubZeroDev.WinGet` and select the correct package-owned file. The Phase 0
  evidence favors this one: MIT permits redistribution, and it depends on
  neither the upstream package's location on disk nor its asset flow. Cost is
  duplicating the native payload and pinning the interop version into our
  package — record that trade-off rather than assuming it away.
- [ ] **B — Dependency payload:** locate the restored ComInterop dependency and
  add its selected DLL to `ReferenceCopyLocalPaths`. Note that
  `$(Pkg…)` path properties are generated only for *direct* references with
  `GeneratePathProperty="true"`, so a consumer's transitive ComInterop has no
  such property; deriving the package root from an already-resolved item (the
  managed projection assembly does flow) is more reliable than composing a path
  from `$(NuGetPackageRoot)`.
- [ ] **C — Upstream import:** import or reuse the upstream target without
  duplicating projection, WinMD, or copy behavior. **Requires a second change:**
  our packed nuspec currently carries `exclude="Build,Analyzers"` on the
  ComInterop dependency, so upstream build assets never reach the consumer until
  our `PackageReference` sets `PrivateAssets="analyzers;contentfiles"`. Even
  then, upstream ships no `buildTransitive/`, so importing its targets means
  reaching into the restored package directory by path anyway.

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
  real ARM64 runtime smoke test passes. *Gated on the managed-layout prerequisite
  above; an ARM64 native copy beside an x64 managed assembly proves nothing.*

## Phase 2 — Package the supported implementation

- [x] Add the canonical target file with a unique, idempotent target/item name.
- [x] Place the canonical implementation under `buildTransitive/<tfm>` so it
  supports both direct and two-hop PackageReference consumers.
- [x] Add the target and permitted payloads to
  `SubZeroDev.WinGet/SubZeroDev.WinGet.csproj` with explicit package paths.
- [x] Keep architecture selection aligned with the upstream precedence:
  `RuntimeIdentifier`, then explicit platform.
- [x] Support x64 and ARM64 only unless the public platform contract is changed
  in a separate decision. *ARM64 requires the managed-layout prerequisite; if
  it has not landed, ship x64 and have the targets fail loudly on ARM64 rather
  than shipping a payload the managed assembly cannot pair with.*
- [x] Preserve incremental build behavior; do not copy unchanged files on every
  build.
- [x] Ensure both `dotnet build` and `dotnet publish` receive the required DLL.

### Phase verification

- [x] Run `dotnet pack -c Release` through `PackageTest`.
- [x] Inspect the `.nupkg` as a ZIP and assert the exact targets and payload
  paths.
- [x] Inspect the embedded `.nuspec` dependency and asset metadata.
- [ ] Confirm `dotnet nuget verify` is used only for signature verification, not
  as a substitute for package-layout tests.

## Phase 3 — Add a packaging regression harness

The pull-request workflow now runs `Test`, `Coverage`, `ArchitectureTest`, and
`PackageTest`. `PackageTest` performs isolated contract packing but does not create or publish
release artifacts. Because merges to `main` publish automatically, that package verification is
a pre-merge gate.

- [x] Add a clean consumer fixture or generated test project that references
  only the locally produced `SubZeroDev.WinGet` package.
- [x] Add a two-hop fixture (`app -> wrapper package -> SubZeroDev.WinGet`) so
  `buildTransitive` behavior is proved rather than inferred from a direct
  consumer.
- [x] Restore it from an isolated local feed and an isolated global-packages
  directory.
- [x] Assert the generated NuGet target import.
- [x] Assert the x64 build and publish outputs contain the x64 native DLL.
- [x] Cross-build ARM64 and assert the output contains the ARM64 native DLL;
  assert the selected `SubZeroDev.WinGet.dll` remains IL-only AnyCPU, while the
  ARM64 executable is PE-marked `0xAA64`. This establishes package-contract
  selection only; x64 runtime activation and ARM64 hardware execution remain open.
- [x] Assert unsupported/ambiguous platform configurations fail with the
  intended actionable message.
- [x] Assert an incremental rebuild does not duplicate or stale-copy assets and
  `dotnet clean` removes copied package assets.
- [x] Add a Nuke packaging-test target or equivalent repeatable entry point.
- [x] Run that target in the pull-request build job before any release can run.
- [ ] Keep live COM activation tests Windows-only and read-only.

## Phase 4 — Remove the consumer workaround

The clean-consumer package contract is verified in `PackageTest`; the Windows
x64 runtime smoke test remains open, so public documentation retains that
runtime boundary.

- [x] Keep direct ComInterop references in the tests and examples while they use
  `ProjectReference`; packaged `buildTransitive` assets do not participate in a
  project-reference build.
- [ ] Build and run the examples through the packed-package consumer fixture,
  not only through `ProjectReference`.
- [x] Verify the library retains its own direct ComInterop dependency for
  compilation and correct NuGet dependency metadata.

## Phase 5 — Update documentation and specification

Every place that currently states the workaround as a rule — all seven were
located by grep, do not trust this list without re-running it:

- [x] Replace the README's former direct-reference warning
  ([README.md:83](README.md)) with the validated package behavior and any
  remaining platform caveat. Note the README is the package's
  `PackageReadmeFile`, so stale text here ships inside the `.nupkg`.
- [x] Remove the second README mention in the GitHub Packages install section
  ([README.md:100](README.md)) — it repeats the workaround parenthetically and
  is easy to miss.
- [x] Update `docs/getting-started.md` so the minimal consumer truly contains
  one package reference — both the `dotnet add package` line (:19) and the
  former direct-reference section.
- [x] Update `docs/troubleshooting.md` (:11) with the new diagnostics and remove
  the obsolete direct-reference remedy.
- [x] Update `docs/examples.md` (:61), which lists the direct reference as
  something every consuming executable needs.
- [x] Update `CLAUDE.md` (:63), which states the same rule under "Constraints
  that will bite you" — stale guidance there misleads future contributors and
  agents, not just consumers.
- [x] Update `docs/testing.md` with the packaging regression target and Windows
  smoke-test boundary.
- [x] Update `SPECIFICATION.md` to distinguish the original transitive-copy
  failure from the shipped fix and its validation evidence.
- [x] Check off and annotate the Phase 3 roadmap item with package-contract
  evidence while retaining runtime and hardware caveats.
- [x] Run the Docusaurus production build so broken documentation links fail
  before merge.

## Phase 6 — Final verification and release readiness

- [ ] Run the full mocked unit suite on Windows.
- [ ] Run the packaging regression matrix for x64, ARM64 cross-build, build, and
  publish.
- [ ] Run the read-only x64 COM smoke test from the clean package consumer.
- [x] Run `git diff --check`.
- [ ] Inspect the final release `.nupkg` and embedded `.nuspec`.
- [x] Confirm no direct consumer ComInterop reference remains in the minimal
  installation instructions.
- [ ] Confirm the PR build gates package correctness before the automatic
  GitHub Packages release job.
- [ ] Record ARM64 hardware validation as unresolved until it is actually run.

## Definition of done

- [ ] A clean supported Windows application works with only
  `PackageReference Include="SubZeroDev.WinGet"`.
- [ ] Build and publish select the correct native DLL and a valid managed layout
  for each architecture: either one IL-only AnyCPU managed library or
  RID-specific x64/ARM64 managed assets. Do not tick ARM64 on the strength of a
  native-DLL copy alone.
- [ ] Package behavior is regression-tested before merge.
- [ ] A read-only x64 runtime smoke test proves COM activation from the packed
  consumer.
- [x] Documentation no longer instructs every consumer to add the dependency
  workaround.
- [ ] No support claim exceeds the validation actually performed.
