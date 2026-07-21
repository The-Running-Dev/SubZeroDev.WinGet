---
id: testing
title: Building & Testing
sidebar_position: 6
---

# Building & Testing

## Build orchestration with Nuke

CI (and, optionally, local development) runs through a [Nuke](https://nuke.build) build defined in [build/Build.cs](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/build/Build.cs). It's a thin, generic layer over the same `dotnet` commands described below — call targets instead of raw CLI commands:

```shell
./build.ps1 Test Pack                                    # no install needed
# or, via the global tool (`update` is idempotent — safe to re-run):
dotnet tool update --global Nuke.GlobalTool --version 10.1.0
nuke Test Pack       # any combination of targets in one command; shared dependencies run once
```

:::note Requires the .NET 10 SDK
Everything targets .NET 10 — the library, tests, and examples as `net10.0-windows10.0.26100`, and `build/_build.csproj` as plain `net10.0` (Nuke.Common 10.x ships `lib/net10.0` only). The .NET 10 SDK is the only one needed, locally or in CI.

The `build.ps1`/`build.sh` bootstrappers are also **required**, not optional: the Nuke global tool locates a build by searching for them, and without them `nuke <Target>` drops into an interactive setup prompt that fails in CI.
:::

Targets: `Restore`, `Compile`, `Test`, `IntegrationTest` (opt-in, see below), `Coverage`, `Pack`, `PublishNuGet`, `PublishGitHubPackages`, plus a local-only `Clean`. Every target also works standalone (`nuke Compile` restores and builds; `nuke Pack` restores, builds, and packs) since each declares its own dependency chain.

Using plain `dotnet` commands directly (below) still works fine for local development — Nuke doesn't replace them, it's what CI now calls instead of hand-written `dotnet` steps.

## Building

```shell
dotnet build SubZeroDev.WinGet.sln
```

The solution builds three projects: the library, the test suite, and the Examples console app. Platform is pinned to x64 automatically (see [Getting Started](getting-started)). The `build/_build.csproj` Nuke project is intentionally **not** part of this solution.

## Unit tests

```shell
dotnet test SubZeroDev.WinGet.sln
# or: nuke Test
```

100 mocked unit tests — zero COM dependency, run anywhere in ~200ms. They cover the service layer (validation, delegation, retry-policy edges), the CLI argument-building contracts, `winget pin list` output parsing variants, model defaults, DI registration, and the exception type.

## Live integration tests

```shell
dotnet test SubZeroDev.WinGet.sln --filter "FullyQualifiedName~IntegrationTests"
# or: nuke IntegrationTest
```

12 tests marked NUnit `[Explicit]` (excluded from plain `dotnet test`) that exercise the **real** COM API and the **real** winget.exe on the machine: version query, search, installed list, upgrade list, get-by-id hit/miss, full details, source list/get, pin list, and a real export.

They are **deliberately read-only** — no install/upgrade/uninstall — because they run against whatever machine executes them. Live coverage of the mutating operations is a tracked roadmap item pending a disposable, side-effect-free test package.

## Coverage

Collected with coverlet, rendered with ReportGenerator:

```shell
dotnet test SubZeroDev.WinGet.sln --collect:"XPlat Code Coverage"
# or: nuke Coverage   (runs Test first, then renders TestResults/**/coverage.cobertura.xml
#                      into coverage/SummaryGithub.md + coverage/Cobertura.xml via Nuke's
#                      ReportGenerator component, resolved from the PackageDownload
#                      declared in build/_build.csproj - no global tool install needed)
```

Current numbers (2026-07-21): unit-only **28.9% line / 50.2% method**; merged with a live integration run **54.9% line / 70.6% method**. Everything unit-testable sits at or near 100% — the uncovered remainder is COM-operation internals that only execute during real mutating operations.

## Continuous integration

The GitHub Actions workflow ([build.yml](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/.github/workflows/build.yml)) runs on `windows-latest` and has two jobs.

The **`build`** job runs on **every push to `main` and every pull request**. It installs `Nuke.GlobalTool` and calls into `build/Build.cs` with a single invocation — `nuke Test Coverage` — so the shared `Restore → Compile → Test` chain runs once (Nuke only de-duplicates targets within one invocation). A failing test stops the job, and the coverage summary is rendered onto the run page and uploaded as an artifact.

That's all a pull request ever does — **no packing, no publishing**. It's also the required status check.

The **`release`** job (`needs: build`) runs only on a push to `main` or a manual dispatch, and does the packing/publishing (below).

`main` is protected: all changes land via pull request and the `build` check must pass before merge, so "a push to `main`" is always a merged PR. Both jobs check out full git history (`fetch-depth: 0`), but for different reasons: the `release` job genuinely needs it (its publish target dereferences `[GitVersion]`, which is fatal on a shallow clone), while the `build` job uses it only to silence Nuke's harmless eager-injection warning — it never reads the value.

## Publishing

Both publish paths live in the `release` job and only run after `build` passes.

### GitHub Packages (automatic, on every push to main)

Every push to `main` runs `nuke PublishGitHubPackages`. The version is computed by **[GitVersion](https://gitversion.net/)** (resolved by Nuke's GitVersion component) and passed to `dotnet pack`. Authentication uses the automatic `GITHUB_TOKEN` (no secret to configure), and `--skip-duplicate` makes re-pushing an unchanged version harmless. The package lands at `https://nuget.pkg.github.com/The-Running-Dev/index.json`; consumers install from it as shown in [Getting Started](getting-started#installing-from-github-packages).

:::note Version bumps
On `main` without a version tag, GitVersion produces the same `SemVer` across commits, so `--skip-duplicate` means a **new** package only appears when the version actually changes — bump the `.csproj` version, or tag a release. If you want a distinct package on every merge instead, switch GitVersion to continuous-deployment mode.
:::

### NuGet.org (manual)

Off by default. Runs only on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret, passed to `nuke PublishNuGet`. Publishes the version pinned in the `.csproj` (not GitVersion).

## Running CI locally

With [act](https://github.com/nektos/act) installed, the workflow runs directly on your machine (host mode — no Docker needed, and required anyway since the job targets Windows):

```shell
act push -P windows-latest=-self-hosted --artifact-server-path .act-artifacts
```

Host mode reuses your real environment, so anything the workflow installs (the Nuke global tool) persists between runs. The workflow's `Install Nuke` step uses `dotnet tool update`, which is idempotent, so repeated `act` runs don't fail on the already-installed tool.
