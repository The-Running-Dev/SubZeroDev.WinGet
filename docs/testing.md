---
id: testing
title: Building & Testing
sidebar_position: 6
---

# Building & Testing

## Build orchestration with Nuke

CI (and, optionally, local development) runs through a [Nuke](https://nuke.build) build defined in [build/Build.cs](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/build/Build.cs). It's a thin, generic layer over the same `dotnet` commands described below — install it once, then call targets instead of raw CLI commands:

```shell
dotnet tool install --global Nuke.GlobalTool --version 10.1.0
nuke Test Pack       # any combination of targets in one command; shared dependencies run once
```

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
#                      ReportGenerator component - no separate tool install needed)
```

Current numbers (2026-07-21): unit-only **28.9% line / 50.2% method**; merged with a live integration run **54.9% line / 70.6% method**. Everything unit-testable sits at or near 100% — the uncovered remainder is COM-operation internals that only execute during real mutating operations.

## Continuous integration

Every push/PR to `main` runs the GitHub Actions workflow ([build.yml](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/.github/workflows/build.yml)) on `windows-latest`. The workflow installs `Nuke.GlobalTool` and calls into `build/Build.cs`:

1. `nuke Test` — Restore → Compile run first automatically; a failing test stops the job, so nothing gets packed or published
2. `nuke Coverage` — renders the coverage summary onto the run page
3. `nuke Pack` → NuGet package uploaded as a run artifact

`main` is protected: all changes land via pull request, and the `build` check must pass before merge. Both CI jobs now check out full git history (`fetch-depth: 0`), not just the release job — Nuke resolves the `[GitVersion]` field in `Build.cs` eagerly at startup regardless of which target is requested, and that resolution fails outright on a shallow clone even in the job that never calls the GitVersion-dependent target.

## Publishing

Two publish targets, both driven by the same workflow:

### GitHub Packages (automatic, on release)

Publishing a **GitHub Release** runs the `publish-github-packages` job, which calls `nuke PublishGitHubPackages`. It depends on the `build` job, so a release only publishes if the build and tests pass. The package version is computed by **[GitVersion](https://gitversion.net/)** — resolved automatically by Nuke's GitVersion component (no manual tool install step anymore) — then passed to `dotnet pack` via `-p:Version`. Authentication uses the automatic `GITHUB_TOKEN` (no secret to configure), and `--skip-duplicate` makes re-runs harmless.

To cut a release:

1. Decide the version and create a GitHub Release with a matching tag (e.g. `v0.2.0` — GitVersion accepts the `v` prefix).
2. Publish the release. The job packs at that version and pushes to `https://nuget.pkg.github.com/The-Running-Dev/index.json`.

Consumers install from the feed as shown in [Getting Started](getting-started#installing-from-github-packages).

### NuGet.org (manual)

Off by default. Runs only on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret, passed to `nuke PublishNuGet`. Publishes the version pinned in the `.csproj` (not GitVersion) — this path is intentionally left unchanged. (The previous workflow passed the invalid `--skip-duplicates`, plural, to this specific push; it was never caught because this path has never actually run — see [SPECIFICATION.md](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/SPECIFICATION.md). Fixed to the correct singular flag as part of the Nuke port.)

## Running CI locally

With [act](https://github.com/nektos/act) installed, the workflow runs directly on your machine (host mode — no Docker needed, and required anyway since the job targets Windows):

```shell
act push -P windows-latest=-self-hosted --artifact-server-path .act-artifacts
```
