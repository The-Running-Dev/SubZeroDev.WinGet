---
id: testing
title: Building & Testing
sidebar_position: 6
---

# Building & Testing

## Building

```shell
dotnet build SubZeroDev.WinGet.sln
```

The solution builds three projects: the library, the test suite, and the Examples console app. Platform is pinned to x64 automatically (see [Getting Started](getting-started)).

## Unit tests

```shell
dotnet test SubZeroDev.WinGet.sln
```

100 mocked unit tests — zero COM dependency, run anywhere in ~200ms. They cover the service layer (validation, delegation, retry-policy edges), the CLI argument-building contracts, `winget pin list` output parsing variants, model defaults, DI registration, and the exception type.

## Live integration tests

```shell
dotnet test SubZeroDev.WinGet.sln --filter "FullyQualifiedName~IntegrationTests"
```

12 tests marked NUnit `[Explicit]` (excluded from plain `dotnet test`) that exercise the **real** COM API and the **real** winget.exe on the machine: version query, search, installed list, upgrade list, get-by-id hit/miss, full details, source list/get, pin list, and a real export.

They are **deliberately read-only** — no install/upgrade/uninstall — because they run against whatever machine executes them. Live coverage of the mutating operations is a tracked roadmap item pending a disposable, side-effect-free test package.

## Coverage

Collected with coverlet, rendered with ReportGenerator:

```shell
dotnet test SubZeroDev.WinGet.sln --collect:"XPlat Code Coverage"
```

Current numbers (2026-07-21): unit-only **28.9% line / 50.2% method**; merged with a live integration run **54.9% line / 70.6% method**. Everything unit-testable sits at or near 100% — the uncovered remainder is COM-operation internals that only execute during real mutating operations.

## Continuous integration

Every push/PR to `main` runs the GitHub Actions workflow ([build.yml](https://github.com/The-Running-Dev/SubZeroDev.WinGet/blob/main/.github/workflows/build.yml)) on `windows-latest`:

1. Restore → Build (Release)
2. **Test** — a failing test stops the job; nothing gets packed or published
3. Coverage summary rendered onto the run page
4. `dotnet pack` → NuGet package uploaded as a run artifact

`main` is protected: all changes land via pull request, and the `build` check must pass before merge.

## Publishing

Two publish targets, both driven by the same workflow:

### GitHub Packages (automatic, on release)

Publishing a **GitHub Release** runs the `publish-github-packages` job. It depends on the `build` job, so a release only publishes if the build and tests pass. The package version is computed by **[GitVersion](https://gitversion.net/)** — installed as a .NET tool (`GitVersion.Tool`) and run against the release tag — then injected into `dotnet pack` via `-p:Version`. Authentication uses the automatic `GITHUB_TOKEN` (no secret to configure), and `--skip-duplicate` makes re-runs harmless.

To cut a release:

1. Decide the version and create a GitHub Release with a matching tag (e.g. `v0.2.0` — GitVersion accepts the `v` prefix).
2. Publish the release. The job packs at that version and pushes to `https://nuget.pkg.github.com/The-Running-Dev/index.json`.

Consumers install from the feed as shown in [Getting Started](getting-started#installing-from-github-packages).

### NuGet.org (manual)

Off by default. Runs only on a manual `workflow_dispatch` with the `push_to_nuget` input checked, and requires a `NUGET_API_KEY` repository secret. Publishes the version pinned in the `.csproj` (not GitVersion) — this path is intentionally left unchanged.

## Running CI locally

With [act](https://github.com/nektos/act) installed, the workflow runs directly on your machine (host mode — no Docker needed, and required anyway since the job targets Windows):

```shell
act push -P windows-latest=-self-hosted --artifact-server-path .act-artifacts
```
