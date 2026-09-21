# Agent contract

**Read `AGENTS.shared.md` (home install: `C:/Users/Ben/.agent-kit/AGENTS.shared.md`) completely before this file.** It holds the rules every repository using the kit shares.


This file is binding for every agent session in this repo, regardless of tool or model. `AGENTS.md`
is a pointer to this file — the kit's default arrangement has that reversed, but this repository's
direction (`CLAUDE.md` holding the content) predates the kit install and is the smaller change to
keep.

## Project identity

**SubZeroDev.WinGet** is a C# client library for the WinGet COM API
(`Microsoft.Management.Deployment`) — the same in-process API `winget.exe` itself is built on. It
covers search, install, upgrade, uninstall, download, and repair of packages; source management;
pinning; and export/import of package sets, with no console-output parsing and no COM/WinRT types
on the public surface (see `README.md` and `SPECIFICATION.md` for the full design).

It does not own: a CLI or GUI front end (the library is consumed by other projects), or the WinGet
COM API's own behaviour (bugs there are upstream, not this repo's).

`.claude/kit.json` records the installed kit commit. The Commands/Architecture/
Constraints/Retry-policy/CI/Known-gaps sections below are this repository's own pre-existing
guidance, kept verbatim from before the kit install.

## Source of truth

`design/` now holds this repository's real design chain — not the kit's seed skeleton — so
`AGENTS.shared.md` § *Source of truth*'s precedence order (`00-brief.md` → `20-contract.md` →
`10-design.md` → `30-slices.md` → `90-decisions.md`) governs it directly.

Lessons learned the hard way live in [`agent.md`](agent.md) — read it after this file.

## Local additions to the shared contract

Everything in `AGENTS.shared.md` governs this repository as written — the command names, model
tiers, session boundaries, hard rules, verification rules, and git/delivery rules there are not
restated here. What follows are the points where this repository narrows one of its options or
adds something the shared file has no occasion to state.

- **No local "High volume" model tier.** The shared table's two tiers (Deep reasoning,
  Implementation) apply as given; this repository does not keep a separate `haiku`/`low` row now
  that Implementation folds in that work.
- **Writing a design-state record.** This repository's `design/state/` holds only the work mirror
  `tools/Update-WorkMirror.ps1` maintains — there are no unit records and no `Live`/`Archival`
  tracking. So the shared sequence's decision-record, unit-record, and `StatedIn` steps do not
  apply here: write the decision-log entry (*Decision logging*, below), then run
  `tools/Update-DesignProjection.ps1` for real, then `tools/Test-DesignState.ps1`. Restore the
  fuller sequence if unit-record tracking is adopted.
- **The direct-push exception for design-state records cannot be exercised here.** The `Main`
  branch ruleset requires the `build` and `machine-state` status checks, so a push straight to
  `main` is rejected regardless of how cleanly the shared exception's conditions are met (*CI and
  releasing*, below, states the same fact from the CI side). A work-mirror refresh rides along
  with the next substantive pull request instead of getting one of its own.

## Decision logging

Any choice a future reader would ask "why?" about goes in `design/90-decisions.md` as:

```
### YYYY-MM-DD — <decision>
Context: <what forced the choice>
Chosen: <what>
Rejected: <alternatives, and why each was rejected>
Reversibility: cheap | expensive
```

The rejected alternatives are the point. Without them the next session relitigates the same choice.

---

# Repository specifics

Everything below predates the kit install and is kept verbatim from this repository's own
`CLAUDE.md`.

## Commands

```shell
dotnet build SubZeroDev.WinGet.sln
dotnet test  SubZeroDev.WinGet.sln                    # mocked unit tests, no COM, ~200ms
./build.ps1 MachineStateTest                   # 7 local-machine live tests
./build.ps1 CatalogIntegrationTest             # 6 remote-catalog live tests
./build.ps1 IntegrationTest                    # all 13 live tests
dotnet test  SubZeroDev.WinGet.sln --filter "Name=Install_WithAnyAlreadyInstalledCode_NormalizesToSuccess"
```

Integration tests are NUnit `[Explicit]`, so a plain `dotnet test` already excludes them — no filter needed to skip them. They are read-only by design and hit the machine's real WinGet catalog and real `winget.exe`.

CI drives everything through Nuke ([build/Build.cs](build/Build.cs)). Same targets locally:

```shell
./build.ps1 Test Coverage ArchitectureTest PackageTest     # bootstrapper — no tool install needed
```

Targets: `Restore`, `Compile`, `Test`, `MachineStateTest`, `CatalogIntegrationTest`, `IntegrationTest`, `Coverage`, `ArchitectureTest`, `PackageTest`, `Pack`, `PublishNuGet`, `PublishGitHubPackages`, plus local-only `Clean`. PR CI invokes `nuke Test Coverage ArchitectureTest PackageTest --configuration Release`. Request multiple targets in **one** invocation — Nuke de-duplicates shared dependencies only within a single invocation.

Examples console app, one runnable example per public API:

```shell
cd SubZeroDev.WinGet.Examples && dotnet run          # lists all examples
dotnet run -- search terminal                        # read-only examples run live
```

Run the CI workflow locally with [act](https://github.com/nektos/act) (host mode, no Docker — the job targets Windows):

```shell
act push -P windows-latest=-self-hosted --artifact-server-path .act-artifacts
```

## Architecture

Three layers over `Microsoft.Management.Deployment` (the WinGet COM/WinRT API, contract 29 — the same in-process API `winget.exe` is built on):

- **Service layer** — `PackageManagementService`, `PackageSourceService`. Validation, `ILogger<T>` logging, result normalization, and the auto-retry policy. What consumers should normally use.
- **Client layer** — `WinGetClient` (packages), `WinGetSourceClient` (sources), `WinGetCliClient` (the CLI shim). Thin, **single-attempt** translations to/from COM. No retry logic lives here.
- **COM owner/activation layer** — `Com/WinGetComContext.cs` owns the WinGet projection on one MTA thread; `Com/WinGetFactory.cs` activates it through WinRT projection → `CoCreateInstance` with `CLSCTX_LOCAL_SERVER` → `CoCreateInstance` with `CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION`. Do not let projected objects escape the owner thread or use arbitrary `Task.Run` around them: agility is not assumed.

`services.AddPackageManagement()` registers all five interfaces as singletons over one shared COM owner context. Service and CLI awaits use `ConfigureAwait(false)`; COM-client flows intentionally retain the owner context.

Every lookup goes through a **composite catalog** (`CreateCompositePackageCatalog`) merging remote sources with local install state — that's what makes `IsUpdateAvailable` meaningful and what makes upgrade/uninstall/repair resolve to the `CatalogPackage` WinGet associates with the installed app. If the composite fails to connect, sources are probed individually and the composite is rebuilt from the reachable subset.

`SPECIFICATION.md` is the full design document, including the COM findings below; `docs/` holds Docusaurus-ready consumer documentation.

## Constraints that will bite you

**Never convert COM collection loops to `foreach`/LINQ.** Enumerating a CsWinRT-projected `IReadOnlyList<T>` throws `InvalidCastException` (interop 1.29.280). All traversal of COM-returned collections uses indexed `for` loops on purpose — the ones in `WinGetClient.cs` and `WinGetSourceClient.cs` are load-bearing, not style. The `foreach` loops that do exist iterate plain .NET collections.

**No `Async` suffix on method names.** Deliberate convention (commit `f2115a7`); `Task`-returning methods are named `Search`, `Install`, `GetPins`.

**No COM/WinRT types in the public surface.** Callers only ever see plain C# records, enums, and interfaces from `Models/` and `Abstractions/`.

**No console output parsing** for anything the COM API can do. Pins and export/import have no COM equivalent at all (verified against the winget-cli IDL), so those — and only those — shell out to `winget.exe` behind `IWinGetCliClient`. Don't widen that exception.

**`ExtendedErrorCode` is an `Exception`, not an `int`** — the HRESULT comes from `.HResult` on it. `FindPackagesOptions.Selectors` are OR'd while `Filters` are AND'd, which is why multi-field search is one call with several Selectors. Install and uninstall have *different* WinRT progress structs, hence separate progress mappers.

**A packaged consumer needs only `SubZeroDev.WinGet`, with an explicit x64 or ARM64 architecture.** Its `buildTransitive` target supplies the matching native `Microsoft.Management.Deployment.dll` and WinMD. A repository `ProjectReference` consumer remains different: retain a direct ComInterop reference on its executable/test host because package build assets do not apply.

**Platform contract:** the managed library is IL-only AnyCPU, while executable/test hosts explicitly select x64 or ARM64. The package target rejects unresolved AnyCPU consumers. This shape is provisional until a Windows x64 packed-consumer runtime smoke test; ARM64 hardware execution remains unvalidated.

**Two SDKs, on purpose.** Library, tests, and examples target `net8.0-windows10.0.26100`. `build/_build.csproj` targets `net10.0` because Nuke.Common 10.x ships `lib/net10.0` only. `build/_build.csproj` is deliberately **not** in the solution. Plain `dotnet build`/`test` needs only .NET 8; driving the build through Nuke needs both.

**`build.ps1` / `build.sh` are required, not optional.** The Nuke global tool locates a build by searching for them; without them `nuke <Target>` drops into an interactive setup prompt that fails in CI.

**`winget-cli/`, `UniGetUI/`, `Winget-AutoUpdate/`** are gitignored reference clones used for research. They are not part of this repo — don't edit them or count them as source.

## Retry policy

Lives only in the service layer; each rule fires at most once. Bypass it by calling `IWinGetClient` directly.

| Condition | Action |
|---|---|
| Install fails with an "already installed" code (`0x8A150061`, `0x8A15010D`, `0x8A15010E`, `0x8A15004F`) | Normalize to success |
| Install/Upgrade fails `NoApplicableInstallers`/`NoApplicableUpgrade` **and** the request constrained architecture/installer-type/scope | Retry unconstrained |
| Upgrade fails `UPGRADE_VERSION_UNKNOWN` (`0x8A150050`) | Retry with `AllowUpgradeToUnknownVersion` |

Well-known HRESULTs are published as `int` constants in `WinGetErrorCodes` (already `unchecked`-cast for direct comparison with `ExtendedErrorCode`).

## CI and releasing

`main` is protected — all changes land via PR, so "a push to main" always means a merged PR. Do not add AI attribution (`Co-Authored-By`, generated-with footers) to commits or PR bodies in this repo.

The `build` job (`nuke Test Coverage ArchitectureTest PackageTest --configuration Release`) runs on every push to `main` and every PR and is the required status check. It includes package-contract packing but never publishes. The `release` job (`needs: build`) handles publishing:

- **GitHub Packages** — automatic. Push to `main` publishes a prerelease `0.1.0-<n>`; pushing a `v*` tag publishes stable `0.1.0`. Auth via the built-in `GITHUB_TOKEN`.
- **NuGet.org** — manual `workflow_dispatch` with `push_to_nuget` checked, requires the `NUGET_API_KEY` secret. Publishes the `.csproj`-pinned `<Version>`.

**GitVersion derives the version from git history, not the `.csproj` `<Version>`.** `GitVersion.yml` sets the base via `next-version`. An untagged commit is *always* a prerelease — a stable release requires a tag. The workflow's `push` trigger must keep `tags: ['v*']`; with only `branches:` declared GitHub does not run the workflow for tag pushes at all, and a tag would silently publish nothing.

## Known gaps

Elevation behavior for mutating operations is untested, and Windows Service / SYSTEM hosting is unverified. Owner-context unit tests do not replace read-only Windows x64 integration or UI responsiveness validation, and ARM64 has not run on hardware. Live coverage of mutating operations is pending a disposable test package. Interop is pinned at 1.29.280 with no compatibility matrix. See `SPECIFICATION.md` §11 and its dated amendment.
