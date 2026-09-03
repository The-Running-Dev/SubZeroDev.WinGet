---
id: testing
title: Building & Testing
sidebar_position: 7
---

# Building & Testing

## Build orchestration with Nuke

CI (and, optionally, local development) runs through a [Nuke](https://nuke.build) build defined in [build/Build.cs](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/build/Build.cs). It's a thin, generic layer over the same `dotnet` commands described below — call targets instead of raw CLI commands:

```shell
./build.ps1 Test Coverage ArchitectureTest PackageTest   # no install needed
# or, via the global tool (`update` is idempotent — safe to re-run):
dotnet tool update --global Nuke.GlobalTool --version 10.1.0
nuke Test Coverage ArchitectureTest PackageTest       # CI-equivalent targets; shared dependencies run once
```

:::note SDKs
The product — library, tests, examples — targets `net8.0-windows10.0.26100`, so a plain `dotnet build`/`dotnet test` needs only the **.NET 8 SDK**. The Nuke build project (`build/_build.csproj`) targets `net10.0` because Nuke.Common 10.x ships `lib/net10.0` only, so driving the build **through Nuke** additionally needs the **.NET 10 SDK**. CI installs both (net8 builds/runs the product; net10 runs Nuke, which shells out to the net8 targets).

The `build.ps1`/`build.sh` bootstrappers are also **required**, not optional: the Nuke global tool locates a build by searching for them, and without them `nuke <Target>` drops into an interactive setup prompt that fails in CI.
:::

Targets: `Restore`, `Compile`, `Test`, `MachineStateTest`, `CatalogIntegrationTest`, `IntegrationTest` (all live targets are opt-in, see below), `Coverage`, `ArchitectureTest`, `PackageTest`, `Pack`, `PublishNuGet`, `PublishGitHubPackages`, plus a local-only `Clean`. `ArchitectureTest` verifies AnyCPU library and x64/ARM64 executable PE output. `PackageTest` inspects the packed assets and validates direct and two-hop package consumers through restore/build/publish without executing COM. Every target also works standalone.

Using plain `dotnet` commands directly (below) still works fine for local development — Nuke doesn't replace them, it's what CI now calls instead of hand-written `dotnet` steps.

## Building

```shell
dotnet build SubZeroDev.WinGet.sln
```

The solution builds three projects: the library, the test suite, and the Examples console app. The library is IL-only AnyCPU while executable/test fixtures select x64 or ARM64 explicitly; this managed package layout is provisional pending Windows x64 runtime validation. The `build/_build.csproj` Nuke project is intentionally **not** part of this solution.

## Unit tests

```shell
dotnet test SubZeroDev.WinGet.sln
# or: nuke Test
```

Mocked unit tests — zero COM dependency, run anywhere in ~200ms. They cover the service layer (validation, delegation, retry-policy edges), the CLI argument-building contracts, `winget pin list` output parsing variants, model defaults, DI registration, and the exception type.

## Live integration tests

```shell
./build.ps1 MachineStateTest        # 7 assertions about this machine's installed/local state
./build.ps1 CatalogIntegrationTest  # 5 assertions with a remote-catalog witness
./build.ps1 IntegrationTest         # all 12 checks, composed from the two risk classes
```

12 tests marked NUnit `[Explicit]` (excluded from plain `dotnet test`) exercise the **real** COM API and the **real** winget.exe on the machine. Stable NUnit categories split them into seven `MachineState` checks and five `CatalogIntegration` checks; each risk-specific target verifies its selected count before it executes. The catalog class contains the assertions whose witness is a `Microsoft.VisualStudioCode` or `git` result in the remote catalog.

They are **deliberately read-only** — no install/upgrade/uninstall — because they run against whatever machine executes them.

<!-- claim:operation-coverage
strength: unvalidated
evidence: MachineStateTest, CatalogIntegrationTest
-->
Read-only package and source operations — search, get, list installed, list available upgrades, get manifest details (agreements, documentation, icons), list/get sources, and a pinned-version download — are executed live by the twelve tests above and by `WinGetProjectionMapper`'s ten live-licensed translations they exercise. Mutating operations — install, upgrade, uninstall, repair, source add/remove/refresh, pin add/remove, export/import — remain unvalidated against the real API. Live coverage of the mutating operations is a tracked roadmap item pending a disposable, side-effect-free test package.

## Coverage

Collected with coverlet, rendered with ReportGenerator:

```shell
dotnet test SubZeroDev.WinGet.sln --collect:"XPlat Code Coverage"
# or: nuke Coverage   (runs Test first, then renders TestResults/**/coverage.cobertura.xml
#                      into coverage/SummaryGithub.md + coverage/Cobertura.xml via Nuke's
#                      ReportGenerator component, resolved from the PackageDownload
#                      declared in build/_build.csproj - no global tool install needed)
```

`Coverage` gates on a checked-in **47.9% line** floor (591 of 1232 unit-only lines, one tenth of a percentage point below the measured 48.0% result the floor was set from). That floor is a **lower bound on unit-only coverage** collected by `Test` alone — it excludes every NUnit `[Explicit]` live test by construction, so a live run can never inflate it. It is not a measure of the library's proven, tested, or verified behavior: ten `WinGetProjectionMapper` translations are deliberately licensed by live evidence rather than a unit test (see [operation coverage](#live-integration-tests) above and [Architecture](architecture.md#hosting-caveats)), and their correctness is invisible to this number either way.

Method coverage isn't quoted here: ReportGenerator 5.5.x gates that metric behind sponsorship, so this repo's tooling can't produce it.

## Continuous integration

The GitHub Actions workflow ([build.yml](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/.github/workflows/build.yml)) runs on `windows-latest` and has two jobs.

The **`build`** job runs on **every push to `main` and every pull request**. It installs `Nuke.GlobalTool` and invokes `nuke Test Coverage ArchitectureTest PackageTest --configuration Release`; shared dependencies run once because Nuke de-duplicates targets within an invocation. `ArchitectureTest` cross-builds and checks PE headers; `PackageTest` packs and validates direct/two-hop consumer restore, build, and publish contracts without live COM activation. A failing check stops the build, and the coverage summary is rendered onto the run page and uploaded as an artifact.

That's all a pull request ever does — **no release packaging or publishing**. `PackageTest` deliberately performs isolated contract packing; only the release job produces and publishes release artifacts. It is also the required status check.

The **`release`** job (`needs: build`) runs only on a push to `main` or a manual dispatch, and does the packing/publishing (below).

`main` is protected: all changes land via pull request and the `build` check must pass before merge, so "a push to `main`" is always a merged PR. Both jobs check out full git history (`fetch-depth: 0`), but for different reasons: the `release` job genuinely needs it (its publish target dereferences `[GitVersion]`, which is fatal on a shallow clone), while the `build` job uses it only to silence Nuke's harmless eager-injection warning — it never reads the value.

## Publishing

Both publish paths live in the `release` job and only run after `build` passes.

### GitHub Packages (automatic)

Two triggers run `nuke PublishGitHubPackages`, and which one fired determines whether the published version is a prerelease or stable:

| Trigger | Published version |
|---|---|
| Push to `main` (i.e. a merged PR) | `0.1.0-<commits-since-source>` — a **prerelease**, distinct for every merge |
| Push of a `v*` tag | `0.1.0` — **stable** |

The version is computed by **[GitVersion](https://gitversion.net/)** (resolved by Nuke's GitVersion component) and passed to `dotnet pack`. Authentication uses the automatic `GITHUB_TOKEN` (no secret to configure), and `--skip-duplicate` makes re-pushing an existing version harmless. The package lands at `https://nuget.pkg.github.com/The-Running-Dev/index.json`; consumers install from it as shown in [Getting Started](getting-started.md#installing-from-github-packages).

Consumers need `--prerelease` to install the untagged prerelease builds; tagged releases install normally.

### Cutting a stable release

```shell
git tag v0.1.0
git push origin v0.1.0
```

The workflow's `push` trigger includes `tags: ['v*']` specifically so this works — with only `branches:` declared, GitHub does **not** run a workflow for tag pushes at all, and the tag would silently publish nothing.

Afterwards, bump `next-version` in [`GitVersion.yml`](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/GitVersion.yml) (and the `.csproj` `<Version>` used by the NuGet.org path) to the next version you're working toward.

:::note Where the version comes from
GitVersion derives the version from **git history, not the `.csproj` `<Version>`**. `GitVersion.yml` sets the base with `next-version`; without it GitVersion inferred `0.0.1` and published `0.0.1-<n>`. Per-branch `deployment-mode` overrides were tested and make no difference — an untagged commit is always a prerelease, which is why a stable release needs a tag.
:::

### NuGet.org (manual)

Off by default. Runs only on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret, passed to `nuke PublishNuGet`. Publishes the version pinned in the `.csproj` (not GitVersion).

## Running CI locally

With [act](https://github.com/nektos/act) installed, the workflow runs directly on your machine (host mode — no Docker needed, and required anyway since the job targets Windows):

```shell
act push -P windows-latest=-self-hosted --artifact-server-path .act-artifacts
```

Host mode reuses your real environment, so anything the workflow installs (the Nuke global tool) persists between runs. The workflow's `Install Nuke` step uses `dotnet tool update`, which is idempotent, so repeated `act` runs don't fail on the already-installed tool.
