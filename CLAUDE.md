# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```shell
dotnet build SubZeroDev.WinGet.sln
dotnet test  SubZeroDev.WinGet.sln                    # 100 mocked unit tests, no COM, ~200ms
dotnet test  SubZeroDev.WinGet.sln --filter "FullyQualifiedName~IntegrationTests"   # 12 live tests
dotnet test  SubZeroDev.WinGet.sln --filter "Name=Install_WithAnyAlreadyInstalledCode_NormalizesToSuccess"
```

Integration tests are NUnit `[Explicit]`, so a plain `dotnet test` already excludes them — no filter needed to skip them. They are read-only by design and hit the machine's real WinGet catalog and real `winget.exe`.

CI drives everything through Nuke ([build/Build.cs](build/Build.cs)). Same targets locally:

```shell
./build.ps1 Test Pack     # bootstrapper — no tool install needed
```

Targets: `Restore`, `Compile`, `Test`, `IntegrationTest`, `Coverage`, `Pack`, `PublishNuGet`, `PublishGitHubPackages`, plus local-only `Clean`. Request multiple targets in **one** invocation (`nuke Test Coverage`) — Nuke de-duplicates shared dependencies only within a single invocation, so two separate calls re-run `Restore`/`Compile`/`Test`.

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
- **Activation layer** — `Com/WinGetFactory.cs`, internal. Every COM object is created through a three-step fallback chain: WinRT projection activation → `CoCreateInstance` with `CLSCTX_LOCAL_SERVER` → `CoCreateInstance` with `CLSCTX_ALLOW_LOWER_TRUST_REGISTRATION` (the mitigation for elevated hosts). The first mode that succeeds is cached so all objects share one activation context; if all three fail, `WinGetUnavailableException` is thrown.

`services.AddPackageManagement()` registers all five interfaces as singletons over one shared factory.

Every lookup goes through a **composite catalog** (`CreateCompositePackageCatalog`) merging remote sources with local install state — that's what makes `IsUpdateAvailable` meaningful and what makes upgrade/uninstall/repair resolve to the `CatalogPackage` WinGet associates with the installed app. If the composite fails to connect, sources are probed individually and the composite is rebuilt from the reachable subset.

`SPECIFICATION.md` is the full design document, including the COM findings below; `docs/` holds Docusaurus-ready consumer documentation.

## Constraints that will bite you

**Never convert COM collection loops to `foreach`/LINQ.** Enumerating a CsWinRT-projected `IReadOnlyList<T>` throws `InvalidCastException` (interop 1.29.280). All traversal of COM-returned collections uses indexed `for` loops on purpose — the ones in `WinGetClient.cs` and `WinGetSourceClient.cs` are load-bearing, not style. The `foreach` loops that do exist iterate plain .NET collections.

**No `Async` suffix on method names.** Deliberate convention (commit `f2115a7`); `Task`-returning methods are named `Search`, `Install`, `GetPins`.

**No COM/WinRT types in the public surface.** Callers only ever see plain C# records, enums, and interfaces from `Models/` and `Abstractions/`.

**No console output parsing** for anything the COM API can do. Pins and export/import have no COM equivalent at all (verified against the winget-cli IDL), so those — and only those — shell out to `winget.exe` behind `IWinGetCliClient`. Don't widen that exception.

**`ExtendedErrorCode` is an `Exception`, not an `int`** — the HRESULT comes from `.HResult` on it. `FindPackagesOptions.Selectors` are OR'd while `Filters` are AND'd, which is why multi-field search is one call with several Selectors. Install and uninstall have *different* WinRT progress structs, hence separate progress mappers.

**Any project that runs this library's code needs a direct `PackageReference` to `Microsoft.WindowsPackageManager.ComInterop`** — a `ProjectReference` is not enough. The interop package's targets copy the native `Microsoft.Management.Deployment.dll` only into the directly-referencing project's output. Without it: `COMException 0x80040154 (REGDB_E_CLASSNOTREG)` at runtime.

**Platform is pinned to x64.** The interop DLL is not AnyCPU; both `.csproj` files map `AnyCPU` → `x64` so plain `dotnet build`/`test` works.

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

The `build` job (tests + coverage, one `nuke Test Coverage` invocation) runs on every push to `main` and every PR and is the required status check. **PRs never pack or publish.** The `release` job (`needs: build`) handles publishing:

- **GitHub Packages** — automatic. Push to `main` publishes a prerelease `0.1.0-<n>`; pushing a `v*` tag publishes stable `0.1.0`. Auth via the built-in `GITHUB_TOKEN`.
- **NuGet.org** — manual `workflow_dispatch` with `push_to_nuget` checked, requires the `NUGET_API_KEY` secret. Publishes the `.csproj`-pinned `<Version>`.

**GitVersion derives the version from git history, not the `.csproj` `<Version>`.** `GitVersion.yml` sets the base via `next-version`. An untagged commit is *always* a prerelease — a stable release requires a tag. The workflow's `push` trigger must keep `tags: ['v*']`; with only `branches:` declared GitHub does not run the workflow for tag pushes at all, and a tag would silently publish nothing.

## Known gaps

Elevation behavior for mutating operations is untested, and Windows Service / SYSTEM hosting is unverified — the activation fallback chain and the WindowsApps `winget.exe` glob exist for those cases but nothing has run under `LocalSystem`. ARM64 is declared but never built on hardware. Live coverage of mutating operations is pending a disposable test package. Interop is pinned at 1.29.280 with no compatibility matrix. See `SPECIFICATION.md` §11.
