# TODO — Nuke build port (`feature/nuke-build`)

Branch is pushed. The Nuke build has now been **compiled and run on a real Windows host**
(2026-07-21) — `nuke Test Coverage Pack --configuration Release` completes green through the
global tool, the same invocation path CI uses.

## Fixed during validation

The original port had never been compiled ("treat the first real CI run as validation").
Four defects surfaced, all environmental/packaging rather than API-signature mistakes — the
`Build.cs` target code written from Nuke's docs was correct:

1. **`Nuke.Common` 10.1.0 ships `lib/net10.0` only**, but `build/_build.csproj` targeted
   `net8.0` → every Nuke type unresolvable (43 compile errors). Build project moved to
   `net10.0`; CI's `setup-dotnet` now installs both `10.0.x` (for Nuke) and `8.0.x` (the
   runtime the library and tests execute on).
2. **`build/Configuration.cs` was missing** — `Build.cs` references the `Configuration`
   type that Nuke's templates normally generate alongside it. Added.
3. **Bootstrappers were missing.** The global tool locates a build by searching for
   `build.ps1`/`build.sh`; without them `nuke <Target>` opens an interactive setup prompt
   that hard-fails in CI (`Failed to read input in non-interactive mode`). Added both, plus
   converted the legacy `.nuke` marker *file* into the modern `.nuke/` directory. This
   reverses the port's documented "global tool, no bootstrapper scripts" decision — that
   combination simply does not work.
4. **NuGet-backed tools weren't declared.** `Coverage` failed with *"Missing package
   reference/download"*, and `[GitVersion]` injection silently degraded to a warning.
   Added `PackageDownload` entries for `ReportGenerator` and `GitVersion.Tool`.

Also corrected in the docs: the claim that `[GitVersion]` injection "fails outright on a
shallow clone" — it is a warning, and only `PublishGitHubPackages` truly needs full history.

## Remaining

1. Open the PR for this branch and let GitHub Actions run — the one thing still unverified is
   `setup-dotnet` provisioning both SDKs on `windows-latest`.
2. The two publish targets (`PublishNuGet`, `PublishGitHubPackages`) remain unexercised by
   design: they push packages. See SPECIFICATION.md §11 items 6–7.

Delete this file once the PR merges.
