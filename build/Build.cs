// This is the generic build orchestration for SubZeroDev.WinGet, written with Nuke
// (https://nuke.build). It replaces the dotnet-CLI steps that used to live directly in
// .github/workflows/build.yml: everything here is invoked as `nuke <Target>` from CI
// (see the workflow file) but works identically from a local shell once the Nuke.GlobalTool
// is installed:
//
//     dotnet tool install --global Nuke.GlobalTool --version 10.1.0
//     nuke Test Pack
//
// Targets are plain and composable - `nuke Test Coverage Pack` computes one execution
// plan and runs each shared dependency (Restore, Compile) exactly once, so CI invokes
// whichever combination of leaf targets a job needs in a single command instead of
// re-running earlier steps per invocation.
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.ReportGenerator;
using Nuke.Common.Utilities.Collections;
using System.Reflection.PortableExecutable;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.ReportGenerator.ReportGeneratorTasks;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Build configuration. Defaults to 'Debug' locally and 'Release' on any CI server.")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("NuGet.org API key. Required by PublishNuGet.")]
    [Secret]
    readonly string NugetApiKey;

    [Parameter("Token used to authenticate against the GitHub Packages NuGet feed (the built-in GITHUB_TOKEN is sufficient). Required by PublishGitHubPackages.")]
    [Secret]
    readonly string GithubToken;

    [Parameter("Owner (org or user) of the GitHub Packages feed to publish to, e.g. 'The-Running-Dev'. Required by PublishGitHubPackages.")]
    readonly string GithubRepositoryOwner;

    // Only PublishGitHubPackages actually reads this (via GitVersion.SemVer). Nuke injects
    // [GitVersion]-attributed fields eagerly at startup regardless of the requested target,
    // and on a shallow clone that injection can't compute a version - but that is a
    // *warning*, not a fatal error: Test/Compile/Pack run fine without it. It only becomes
    // fatal when a target dereferences this field, i.e. PublishGitHubPackages. So full
    // history is strictly required only by that target's job; the build job checks out
    // fetch-depth: 0 as well purely to silence the harmless warning and keep both jobs'
    // checkouts identical.
    [GitVersion] readonly GitVersion GitVersion;

    [Solution] readonly Solution Solution;

    AbsolutePath LibraryProject => RootDirectory / "SubZeroDev.WinGet" / "SubZeroDev.WinGet.csproj";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";
    AbsolutePath CoverageDirectory => RootDirectory / "coverage";

    const string MachineStateCategory = "MachineState";
    const string CatalogIntegrationCategory = "CatalogIntegration";
    const string PackageId = "SubZeroDev.WinGet";

    // S11.3: identity fields for a confirmation record. Only meaningful inside a GitHub Actions
    // job - PublishGitHubPackages/PublishNuGet are release-job-only targets, never run locally
    // against a real feed, so a placeholder here would never actually reach a report.
    static string RunUrl =>
        $"{Environment.GetEnvironmentVariable("GITHUB_SERVER_URL")}/{Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")}" +
        $"/actions/runs/{Environment.GetEnvironmentVariable("GITHUB_RUN_ID")} " +
        $"(attempt {Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT")})";

    static string CommitSha =>
        Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "(unknown - not running in GitHub Actions)";

    static string RefName =>
        Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? "(unknown - not running in GitHub Actions)";

    // S9.1: measured from the unit-only Coverage report on main at commit f063212
    // (591 covered / 1232 valid lines = 47.97077922...%, which rounds to 48.0% at one
    // decimal place), 0.1 percentage point below that one-decimal measured result, per C12.
    const decimal CoverageFloorPercent = 47.9m;

    // `dotnet test --list-tests` does not honor `--filter` - it lists every discovered test in
    // the assembly regardless of the filter given alongside it, which PR #75's first hosted run
    // proved (0 selected became "every test in the assembly", not the filtered subset this method
    // used to assume). Counting `[Category("...")]` occurrences in the checked-in source instead
    // is deterministic and independent of that tooling behaviour; the guard's purpose - fail
    // before any live effect if a test's category tag drifts from what C8 asserts - still holds,
    // and the actual test run immediately after this check applies `--filter` for real execution,
    // where filtering (as opposed to listing) is the well-tested, supported path.
    void AssertLiveTestCount(string category, int expectedCount)
    {
        var pattern = $"[Category(\"{category}\")]";
        var selectedCount = Directory
            .EnumerateFiles(RootDirectory / "SubZeroDev.WinGet.Tests", "*.cs", SearchOption.AllDirectories)
            .Sum(file => File.ReadAllLines(file).Count(line => line.Trim() == pattern));

        if (selectedCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"{category} must select exactly {expectedCount} live tests before execution; " +
                $"found {selectedCount} occurrences of {pattern} under SubZeroDev.WinGet.Tests.");
        }
    }

    // Local-dev convenience only - CI always starts from a fresh checkout, so this is
    // never part of the target chains the workflow invokes.
    Target Clean => _ => _
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            TestResultsDirectory.CreateOrCleanDirectory();
            CoverageDirectory.CreateOrCleanDirectory();
        });

    Target Restore => _ => _
        .Executes(() => DotNetRestore(s => s
            .SetProjectFile(Solution)
            .SetProperty("EnableWindowsTargeting", "true")));

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() => DotNetBuild(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .SetProperty("EnableWindowsTargeting", "true")
            .EnableNoRestore()));

    Target ArchitectureTest => _ => _
        .DependsOn(Restore)
        .After(Coverage)
        .Executes(() =>
        {
            foreach (var platform in new[] { "x64", "ARM64" })
            {
                DotNetBuild(s => s
                    .SetProjectFile(Solution)
                    .SetConfiguration(Configuration)
                    .SetProperty("Platform", platform)
                    .SetProperty("EnableWindowsTargeting", "true")
                    .EnableNoRestore());
            }

            foreach (var platform in new[] { "x64", "ARM64" })
            {
                PeArchitecture.AssertAnyCpu(
                    RootDirectory / "SubZeroDev.WinGet" / "bin" / platform /
                    Configuration / "net8.0-windows10.0.26100" / "SubZeroDev.WinGet.dll");
            }

            foreach (var (platform, machine) in new[]
                     {
                         ("x64", Machine.Amd64),
                         ("ARM64", Machine.Arm64)
                     })
            {
                PeArchitecture.AssertMachine(
                    RootDirectory / "SubZeroDev.WinGet.Tests" / "bin" / platform /
                    Configuration / "net8.0-windows10.0.26100" / "SubZeroDev.WinGet.Tests.dll",
                    machine);
                PeArchitecture.AssertMachine(
                    RootDirectory / "SubZeroDev.WinGet.Examples" / "bin" / platform /
                    Configuration / "net8.0-windows10.0.26100" / "SubZeroDev.WinGet.Examples.dll",
                    machine);

                foreach (var projectDirectory in new[]
                         {
                             "SubZeroDev.WinGet",
                             "SubZeroDev.WinGet.Tests",
                             "SubZeroDev.WinGet.Examples"
                         })
                {
                    PeArchitecture.AssertMachine(
                        RootDirectory / projectDirectory / "bin" / platform /
                        Configuration / "net8.0-windows10.0.26100" /
                        "Microsoft.Management.Deployment.dll",
                        machine);
                }
            }
        });

    // Package-level contract test. This intentionally stops at restore/build/publish:
    // live COM activation remains in the opt-in IntegrationTest target and requires
    // a Windows machine with WinGet installed.
    Target PackageTest => _ => _
        .DependsOn(Pack)
        .After(ArchitectureTest)
        .Executes(() => PackageVerification.Run(
            RootDirectory, ArtifactsDirectory, Configuration));

    // Deliberately separate from PackageTest: this consumes the same locally packed package,
    // but executes its consumer and therefore activates the live WinGet COM server.
    Target PackedConsumerSmokeTest => _ => _
        .DependsOn(Pack)
        .Executes(() => PackageVerification.RunPackedConsumerSmoke(
            ArtifactsDirectory, Configuration));

    // Mirrors the original CI "Test" step exactly: NUnit's [Explicit] attribute already
    // excludes the 12 live integration tests from a plain test run, so no filter is needed.
    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() => DotNetTest(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .EnableNoRestore()
            .EnableNoBuild()
            .SetLoggers("trx")
            .SetResultsDirectory(TestResultsDirectory)
            .SetProcessAdditionalArguments("--collect:\"XPlat Code Coverage\"")));

    // Opt-in only, never part of the default CI target chain. It composes both live risk
    // classes locally after their count guards, and needs a real WinGet install.
    Target IntegrationTest => _ => _
        .DependsOn(MachineStateTest, CatalogIntegrationTest);

    // S8.4/S8.6: a trx logger plus a detailed console logger give the hosted job a retained,
    // per-test record - not just a pass/fail job outcome - so a partial failure keeps evidence
    // for every assertion that did pass instead of collapsing to one binary result.
    Target MachineStateTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            AssertLiveTestCount(MachineStateCategory, 7);
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetFilter($"Category={MachineStateCategory}")
                .SetLoggers("trx")
                .SetResultsDirectory(TestResultsDirectory / "MachineState")
                .SetProcessAdditionalArguments("--logger \"console;verbosity=detailed\""));
        });

    Target CatalogIntegrationTest => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            AssertLiveTestCount(CatalogIntegrationCategory, 6);
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild()
                .SetFilter($"Category={CatalogIntegrationCategory}")
                .SetLoggers("trx")
                .SetResultsDirectory(TestResultsDirectory / "CatalogIntegration")
                .SetProcessAdditionalArguments("--logger \"console;verbosity=detailed\""));
        });

    // S9: gates on the unit-only report ReportGenerator just produced. Test is the only
    // dependency (S9.4) - MachineStateTest/CatalogIntegrationTest never collect coverage,
    // so a live run cannot contribute files or counts to what this evaluates.
    Target Coverage => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            ReportGenerator(s => s
                .SetReports(TestResultsDirectory / "**/coverage.cobertura.xml")
                .SetTargetDirectory(CoverageDirectory)
                .SetReportTypes(ReportTypes.MarkdownSummaryGithub, ReportTypes.Cobertura));

            var (coveredLines, validLines) = CoverageGate.ReadLineCounts(CoverageDirectory / "Cobertura.xml");
            CoverageGate.Assert(coveredLines, validLines, CoverageFloorPercent);
        });

    // S9.2/S9.6: exercises the exact comparison CoverageGate.Assert performs, independent of a
    // real test run - a boundary case that passes, one line below it that fails, and the
    // full-range extremes. Restoring report-only behaviour (e.g. the comparison itself always
    // passing) makes AssertFails fail here, which is what S9.6 requires of the negative case;
    // this does not, by itself, catch the Coverage target ceasing to call CoverageGate.Assert
    // at all, since that wiring lives in the Coverage target above, not in this gate math. Kept
    // out of the default CI target chain, matching the other hand-rolled Build.cs checks
    // (ArchitectureTest/PackageTest) - it needs no live Windows toolchain and is cheap to run
    // before pushing a Coverage change.
    Target CoverageGateTest => _ => _
        .Executes(() =>
        {
            void AssertPasses(int covered, int valid, decimal floor)
            {
                if (!CoverageGate.Evaluate(covered, valid, floor).Passed)
                {
                    throw new InvalidOperationException(
                        $"Expected {covered}/{valid} to pass at floor {floor}, but it failed.");
                }
            }

            void AssertFails(int covered, int valid, decimal floor)
            {
                if (CoverageGate.Evaluate(covered, valid, floor).Passed)
                {
                    throw new InvalidOperationException(
                        $"Expected {covered}/{valid} to fail at floor {floor}, but it passed.");
                }
            }

            // Exactly at the boundary passes; one line below it fails.
            AssertPasses(479, 1000, 47.9m);
            AssertFails(478, 1000, 47.9m);

            // Range extremes.
            AssertPasses(1000, 1000, 47.9m);
            AssertFails(0, 1000, 47.9m);

            // The checked-in floor itself, against the exact counts it was measured from.
            AssertPasses(591, 1232, CoverageFloorPercent);
        });

    // Packs at the version pinned in SubZeroDev.WinGet.csproj. Reached transitively by
    // PublishNuGet (the manual NuGet.org release). The GitHub Packages release path does
    // its own GitVersion-derived pack in PublishGitHubPackages instead.
    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            DotNetPack(s => s
                .SetProject(LibraryProject)
                .SetConfiguration(Configuration)
                .SetProperty("EnableWindowsTargeting", "true")
                .EnableNoRestore()
                .EnableNoBuild()
                .SetOutputDirectory(ArtifactsDirectory));
        });

    // S11.1/S11.2/C21: a successful push, or --skip-duplicate reporting nothing to do, is not
    // itself evidence the intended version is live - both targets confirm it back from the
    // feed they just pushed to before the target is allowed to succeed.
    Target PublishNuGet => _ => _
        .DependsOn(Pack)
        .Requires(() => NugetApiKey)
        .Executes(async () =>
        {
            var package = ArtifactsDirectory.GlobFiles("*.nupkg").Single();
            var version = PublicationConfirmation.ReadPackedVersion(package);

            DotNetNuGetPush(s => s
                .SetTargetPath(package)
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NugetApiKey)
                // Original workflow used the (invalid) plural "--skip-duplicates" here;
                // this is the correct singular flag - see PR description.
                .EnableSkipDuplicate());

            using var http = new HttpClient();
            var result = await PublicationConfirmation.Confirm(
                destination: "NuGet.org",
                tag: RefName,
                commit: CommitSha,
                runUrl: RunUrl,
                intendedVersion: version,
                fetchVersions: ct => PublicationConfirmation.FetchNuGetOrgVersions(http, PackageId, ct));
            PublicationConfirmation.Assert(result);
        });

    // Deliberately independent of Compile/Pack: like the original publish-github-packages
    // job, this does its own from-scratch `dotnet pack` (implicit restore+build) at the
    // GitVersion-computed version, since it runs as its own CI job/checkout.
    Target PublishGitHubPackages => _ => _
        .Requires(() => GithubToken)
        .Requires(() => GithubRepositoryOwner)
        .Executes(async () =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            DotNetPack(s => s
                .SetProject(LibraryProject)
                .SetConfiguration(Configuration)
                .SetProperty("EnableWindowsTargeting", "true")
                .SetVersion(GitVersion.SemVer)
                .SetOutputDirectory(ArtifactsDirectory));

            var package = ArtifactsDirectory.GlobFiles("*.nupkg").Single();
            var version = PublicationConfirmation.ReadPackedVersion(package);

            DotNetNuGetPush(s => s
                .SetTargetPath(package)
                .SetSource($"https://nuget.pkg.github.com/{GithubRepositoryOwner}/index.json")
                .SetApiKey(GithubToken)
                .EnableSkipDuplicate());

            using var http = new HttpClient();
            var result = await PublicationConfirmation.Confirm(
                destination: "GitHub Packages",
                tag: RefName,
                commit: CommitSha,
                runUrl: RunUrl,
                intendedVersion: version,
                fetchVersions: ct => PublicationConfirmation.FetchGitHubPackagesVersions(
                    http, GithubRepositoryOwner, PackageId, GithubToken, ct));
            PublicationConfirmation.Assert(result);
        });

    // S11.4: exercises PublicationConfirmation.Evaluate/Assert directly, independent of a real
    // feed - a positive match, a missing version, and a mismatched one - the same shape as
    // CoverageGateTest above. Kept out of the default CI target chain for the same reason.
    Target PublicationConfirmationTest => _ => _
        .Executes(() =>
        {
            void AssertConfirms(string intended, params string[] observed)
            {
                var result = PublicationConfirmation.Evaluate(
                    "TestFeed", "v0.2.0", "abc123", "https://example.invalid/run/1", intended, observed);
                if (!result.Confirmed)
                {
                    throw new InvalidOperationException(
                        $"Expected {intended} to be confirmed by [{string.Join(", ", observed)}], but it was not.");
                }

                PublicationConfirmation.Assert(result);
            }

            void AssertDoesNotConfirm(string intended, params string[] observed)
            {
                var result = PublicationConfirmation.Evaluate(
                    "TestFeed", "v0.2.0", "abc123", "https://example.invalid/run/1", intended, observed);
                if (result.Confirmed)
                {
                    throw new InvalidOperationException(
                        $"Expected {intended} not to be confirmed by [{string.Join(", ", observed)}], but it was.");
                }

                try
                {
                    PublicationConfirmation.Assert(result);
                    throw new InvalidOperationException(
                        $"Expected Assert to throw for {intended} against [{string.Join(", ", observed)}].");
                }
                catch (InvalidOperationException)
                {
                    // Expected: Assert throws when the intended version was not confirmed.
                }
            }

            // A push followed by an exact-match observation confirms.
            AssertConfirms("0.2.0", "0.1.0", "0.2.0");

            // A push followed by a missing observation - the feed has not shown the version
            // at all - does not confirm.
            AssertDoesNotConfirm("0.2.0", "0.1.0", "0.1.1");
            AssertDoesNotConfirm("0.2.0");

            // A push followed by a mismatched observation - a prerelease suffix or different
            // casing - does not confirm; only an exact string match does.
            AssertDoesNotConfirm("0.2.0", "0.2.0-1");
            AssertDoesNotConfirm("0.2.0", "0.2.0-rc1");
        });
}
