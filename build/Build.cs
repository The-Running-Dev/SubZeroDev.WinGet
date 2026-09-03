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
using System.Diagnostics;
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

    void AssertLiveTestCount(string category, int expectedCount)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("test");
        startInfo.ArgumentList.Add(Solution);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(Configuration);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--list-tests");
        startInfo.ArgumentList.Add("--filter");
        startInfo.ArgumentList.Add($"Category={category}");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to list live tests.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to list {category} tests before execution:{Environment.NewLine}{output}{error}");
        }

        var selectedCount = output.Split(Environment.NewLine)
            .Count(line => line.StartsWith("    SubZeroDev.WinGet.Tests.", StringComparison.Ordinal));
        if (selectedCount != expectedCount)
        {
            // Diagnostic-only: this method has never run against a hosted CI invocation before
            // S8 wired MachineStateTest/CatalogIntegrationTest into build.yml, so a mismatch here
            // needs the raw --list-tests output to root-cause rather than a second blind guess.
            throw new InvalidOperationException(
                $"{category} must select exactly {expectedCount} live tests before execution; selected {selectedCount}." +
                $"{Environment.NewLine}Raw `dotnet test --list-tests --filter Category={category}` output:" +
                $"{Environment.NewLine}{output}");
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
            AssertLiveTestCount(CatalogIntegrationCategory, 5);
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

    Target Coverage => _ => _
        .DependsOn(Test)
        .Executes(() => ReportGenerator(s => s
            .SetReports(TestResultsDirectory / "**/coverage.cobertura.xml")
            .SetTargetDirectory(CoverageDirectory)
            .SetReportTypes(ReportTypes.MarkdownSummaryGithub, ReportTypes.Cobertura)));

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

    Target PublishNuGet => _ => _
        .DependsOn(Pack)
        .Requires(() => NugetApiKey)
        .Executes(() => ArtifactsDirectory.GlobFiles("*.nupkg")
            .ForEach(package => DotNetNuGetPush(s => s
                .SetTargetPath(package)
                .SetSource("https://api.nuget.org/v3/index.json")
                .SetApiKey(NugetApiKey)
                // Original workflow used the (invalid) plural "--skip-duplicates" here;
                // this is the correct singular flag - see PR description.
                .EnableSkipDuplicate())));

    // Deliberately independent of Compile/Pack: like the original publish-github-packages
    // job, this does its own from-scratch `dotnet pack` (implicit restore+build) at the
    // GitVersion-computed version, since it runs as its own CI job/checkout.
    Target PublishGitHubPackages => _ => _
        .Requires(() => GithubToken)
        .Requires(() => GithubRepositoryOwner)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            DotNetPack(s => s
                .SetProject(LibraryProject)
                .SetConfiguration(Configuration)
                .SetProperty("EnableWindowsTargeting", "true")
                .SetVersion(GitVersion.SemVer)
                .SetOutputDirectory(ArtifactsDirectory));

            ArtifactsDirectory.GlobFiles("*.nupkg").ForEach(package => DotNetNuGetPush(s => s
                .SetTargetPath(package)
                .SetSource($"https://nuget.pkg.github.com/{GithubRepositoryOwner}/index.json")
                .SetApiKey(GithubToken)
                .EnableSkipDuplicate()));
        });
}
