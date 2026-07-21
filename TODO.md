# TODO — Nuke build port (`feature/nuke-build`)

Committed locally on branch `feature/nuke-build` (commit `746b310`). Not pushed and no PR opened — this environment has no GitHub credentials.

## To finish this

1. Push the branch:

   ```
   git push -u origin feature/nuke-build
   ```

2. Open the PR (GitHub UI, or `gh pr create`).

3. Let CI actually run. This has **not been compiled or executed anywhere** — no Windows host or dotnet SDK was available while writing it. Treat the first real CI run as the actual validation, not the PR review. Expect to fix minor API-signature mismatches if any slipped through (e.g. exact `DotNetPackSettings` / `ReportGeneratorSettings` method names) — see `SPECIFICATION.md` §11 item 8.

## Things worth double-checking on that first run

- **Both CI jobs now use `fetch-depth: 0`** (full git history), not just `publish-github-packages`. Nuke resolves the `[GitVersion]` field in `build/Build.cs` eagerly at startup regardless of which target is requested, and that resolution errors outright on a shallow clone — even in jobs that never touch GitVersion. This is a deliberate behavior change (slightly slower checkout), not an oversight.
- **Fixed a latent bug** while porting: the NuGet.org publish step used to pass `--skip-duplicates` (plural, invalid); the real `dotnet nuget push` flag is singular. Never caught before because that path has never actually run.
- `build/_build.csproj` is deliberately **not** added to `SubZeroDev.WinGet.sln`.
- Nuke install mechanism is the **global tool** (`dotnet tool install --global Nuke.GlobalTool`), invoked as `nuke <Target>` — not Nuke's bootstrapper scripts. This was a tradeoff: Nuke's `[GitHubActions]` auto-generation attribute only knows how to emit bootstrapper-script invocations, so using the global tool meant keeping the workflow YAML hand-authored instead of generated.

## If you'd rather I push/open the PR myself

Authorize the GitHub connector (via `claude mcp` or `/mcp` in an interactive session) — once connected I can push and open the PR directly.
