using System.Text.RegularExpressions;

using FluentAssertions;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// S15 — a workflow-composition check that proves every hosted live job constitutes its own
/// WinGet runtime (C24) rather than accepting whatever the runner image happens to carry. Reads
/// only the checked-in workflow file and the library project file (S15.5): it activates no COM,
/// starts no process, and contacts no network, so it runs as an ordinary test inside the existing
/// hermetic required check with no new CI wiring. It has no override of any kind (S15.6) — a live
/// job that skips the pinned install fails this check until the check itself is deleted.
/// </summary>
[TestFixture]
public class WorkflowCompositionTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string WorkflowPath = Path.Combine(RepoRoot, ".github", "workflows", "build.yml");
    private static readonly string CsprojPath = Path.Combine(RepoRoot, "SubZeroDev.WinGet", "SubZeroDev.WinGet.csproj");

    // The three live targets named under C8/C9. A job is "invoking a live target" when one of
    // its steps runs `nuke <name>` for one of these — the machine-state, catalog-dependent, and
    // packed-consumer targets this check constrains.
    private static readonly string[] LiveTargets = ["MachineStateTest", "CatalogIntegrationTest", "PackedConsumerSmokeTest"];

    private const string InstallStepName = "Install pinned WinGet";

    // ---- S15.1: the pinned version and the ComInterop reference version must agree ----

    [Test]
    public void PinnedWinGetVersion_MatchesComInteropVersion()
    {
        var pinned = ExtractPinnedWinGetVersion(File.ReadAllText(WorkflowPath));
        var comInterop = ExtractComInteropVersion(File.ReadAllText(CsprojPath));

        pinned.Should().Be(comInterop,
            "the pinned WinGet version the workflow installs must match the " +
            "Microsoft.WindowsPackageManager.ComInterop version the library project declares, per C24");
    }

    [Test]
    public void VersionEqualityCheck_FailsOnAMismatchedFixturePairAndPassesOnTheRealPair()
    {
        var act = () => AssertVersionsEqual("1.29.280", "1.29.281");
        act.Should().Throw<AssertionException>();

        var realPair = () => AssertVersionsEqual(
            ExtractPinnedWinGetVersion(File.ReadAllText(WorkflowPath)),
            ExtractComInteropVersion(File.ReadAllText(CsprojPath)));
        realPair.Should().NotThrow();
    }

    private static void AssertVersionsEqual(string pinned, string comInterop) =>
        pinned.Should().Be(comInterop);

    // ---- S15.2: a job invoking a live target must have a pinned-install step ordered first ----

    [Test]
    public void EveryJobInvokingALiveTarget_HasAPinnedInstallStepOrderedFirst()
    {
        foreach (var job in ParseJobs(File.ReadAllText(WorkflowPath)))
        {
            AssertInstallOrderedBeforeLiveInvocation(job);
        }
    }

    [Test]
    public void InstallOrderingCheck_FailsWhenNoInstallStepIsPresent()
    {
        const string fixture = """
            jobs:
              live-job:
                steps:
                  - name: Run live target
                    run: nuke MachineStateTest --configuration Release
            """;

        var act = () => AssertInstallOrderedBeforeLiveInvocation(ParseJobs(fixture).Single());

        act.Should().Throw<AssertionException>();
    }

    [Test]
    public void InstallOrderingCheck_FailsWhenTheInstallStepIsOrderedAfterTheInvocation()
    {
        const string fixture = """
            jobs:
              live-job:
                steps:
                  - name: Run live target
                    run: nuke MachineStateTest --configuration Release
                  - name: Install pinned WinGet
                    run: Add-AppxPackage @arguments
            """;

        var act = () => AssertInstallOrderedBeforeLiveInvocation(ParseJobs(fixture).Single());

        act.Should().Throw<AssertionException>();
    }

    [Test]
    public void InstallOrderingCheck_PassesWhenTheInstallStepIsOrderedFirst()
    {
        const string fixture = """
            jobs:
              live-job:
                steps:
                  - name: Install pinned WinGet
                    run: Add-AppxPackage @arguments
                  - name: Run live target
                    run: nuke MachineStateTest --configuration Release
            """;

        var act = () => AssertInstallOrderedBeforeLiveInvocation(ParseJobs(fixture).Single());

        act.Should().NotThrow();
    }

    private static void AssertInstallOrderedBeforeLiveInvocation(WorkflowJob job)
    {
        var liveStepIndices = job.Steps
            .Select((step, index) => (step, index))
            .Where(pair => LiveTargets.Any(target => pair.step.Text.Contains($"nuke {target}", StringComparison.Ordinal)))
            .Select(pair => pair.index)
            .ToList();

        if (liveStepIndices.Count == 0)
        {
            return;
        }

        var installIndex = job.Steps.FindIndex(step => step.Name == InstallStepName);

        installIndex.Should().BeGreaterThanOrEqualTo(0,
            $"job '{job.Name}' invokes a live target and must have a step named '{InstallStepName}' " +
            "ordered before it (S15.2)");
        installIndex.Should().BeLessThan(liveStepIndices.Min(),
            $"job '{job.Name}' must run '{InstallStepName}' before invoking a live target (S15.2)");
    }

    // ---- S15.3: each live job records the App Installer version before and after the install ----

    [Test]
    public void EveryJobInvokingALiveTarget_RecordsTheAppInstallerVersionBeforeAndAfter()
    {
        foreach (var job in ParseJobs(File.ReadAllText(WorkflowPath)))
        {
            var invokesLiveTarget = job.Steps.Any(step =>
                LiveTargets.Any(target => step.Text.Contains($"nuke {target}", StringComparison.Ordinal)));

            if (!invokesLiveTarget)
            {
                continue;
            }

            var jobText = string.Join('\n', job.Steps.Select(step => step.Text));

            jobText.Should().Contain("before=$($package.Version)",
                $"job '{job.Name}' invokes a live target and must record the App Installer version " +
                "observed before the pinned install (S15.3)");
            jobText.Should().Contain("after=$($package.Version)",
                $"job '{job.Name}' invokes a live target and must record the App Installer version " +
                "observed after the pinned install (S15.3)");
            jobText.Should().Contain("(not observed)",
                $"job '{job.Name}' must record an absent App Installer observation as unobserved, " +
                "rather than carrying over a value from elsewhere (S15.3)");
        }
    }

    // ---- S15.4: a live-target step must not run regardless of the install step's outcome ----

    [Test]
    public void EveryJobInvokingALiveTarget_NeverRunsItRegardlessOfTheInstallOutcome()
    {
        foreach (var job in ParseJobs(File.ReadAllText(WorkflowPath)))
        {
            AssertLiveStepsDoNotIgnoreInstallOutcome(job);
        }
    }

    [Test]
    public void AlwaysRunCheck_FailsWhenALiveStepRunsRegardlessOfTheInstallOutcome()
    {
        const string fixture = """
            jobs:
              live-job:
                steps:
                  - name: Install pinned WinGet
                    run: Add-AppxPackage @arguments
                  - name: Run live target
                    if: always()
                    run: nuke MachineStateTest --configuration Release
            """;

        var act = () => AssertLiveStepsDoNotIgnoreInstallOutcome(ParseJobs(fixture).Single());

        act.Should().Throw<AssertionException>();
    }

    [Test]
    public void AlwaysRunCheck_PassesWhenTheLiveStepCarriesNoAlwaysCondition()
    {
        const string fixture = """
            jobs:
              live-job:
                steps:
                  - name: Install pinned WinGet
                    run: Add-AppxPackage @arguments
                  - name: Run live target
                    run: nuke MachineStateTest --configuration Release
            """;

        var act = () => AssertLiveStepsDoNotIgnoreInstallOutcome(ParseJobs(fixture).Single());

        act.Should().NotThrow();
    }

    private static void AssertLiveStepsDoNotIgnoreInstallOutcome(WorkflowJob job)
    {
        foreach (var step in job.Steps)
        {
            var invokesLiveTarget = LiveTargets.Any(target => step.Text.Contains($"nuke {target}", StringComparison.Ordinal));
            if (!invokesLiveTarget)
            {
                continue;
            }

            (step.IfCondition?.Contains("always()", StringComparison.Ordinal) ?? false).Should().BeFalse(
                $"job '{job.Name}' step '{step.Name}' invokes a live target and must not run regardless of " +
                $"the pinned install's outcome (S15.4)");
        }
    }

    // ---- helpers: a deliberately minimal workflow-YAML reader, not a general parser. It reads
    // only the shapes this repository's own workflow uses - two-space job indent, six-space step
    // indent - which is exactly the surface S15 constrains, per S15.5 (no third-party dependency). ----

    private sealed record WorkflowStep(string? Name, string? IfCondition, string Text);

    private sealed record WorkflowJob(string Name, List<WorkflowStep> Steps);

    private static List<WorkflowJob> ParseJobs(string yaml)
    {
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        var jobs = new List<WorkflowJob>();

        var jobsStart = Array.FindIndex(lines, line => line == "jobs:");
        if (jobsStart < 0)
        {
            return jobs;
        }

        var i = jobsStart + 1;
        while (i < lines.Length)
        {
            if (IsJobHeader(lines[i], out var jobName))
            {
                var start = i + 1;
                var end = start;
                while (end < lines.Length && !IsJobHeader(lines[end], out _))
                {
                    end++;
                }

                jobs.Add(new WorkflowJob(jobName!, ParseSteps(lines[start..end])));
                i = end;
            }
            else
            {
                i++;
            }
        }

        return jobs;
    }

    private static bool IsJobHeader(string line, out string? name)
    {
        // A top-level job key: exactly two leading spaces, then a bare "name:" with no
        // further content on the line - matches this workflow's own job-boundary shape.
        // Excludes a comment line at the same indent (e.g. one documenting the next job),
        // which would otherwise also end with ':' and be mistaken for a job header.
        if (line.Length > 2 && line[0] == ' ' && line[1] == ' ' && line[2] != ' ' && line[2] != '#' &&
            line.TrimEnd().EndsWith(':'))
        {
            name = line.Trim().TrimEnd(':');
            return true;
        }

        name = null;
        return false;
    }

    private static List<WorkflowStep> ParseSteps(string[] jobLines)
    {
        var boundaries = new List<int>();
        for (var i = 0; i < jobLines.Length; i++)
        {
            if (jobLines[i].StartsWith("      - name: ", StringComparison.Ordinal))
            {
                boundaries.Add(i);
            }
        }

        boundaries.Add(jobLines.Length);

        var steps = new List<WorkflowStep>();
        for (var b = 0; b < boundaries.Count - 1; b++)
        {
            var stepLines = jobLines[boundaries[b]..boundaries[b + 1]];
            var text = string.Join('\n', stepLines);
            var name = stepLines[0]["      - name: ".Length..].Trim();
            var ifLine = stepLines.FirstOrDefault(line => line.TrimStart().StartsWith("if:", StringComparison.Ordinal));
            var ifCondition = ifLine is null ? null : ifLine.Trim()["if:".Length..].Trim();

            steps.Add(new WorkflowStep(name, ifCondition, text));
        }

        return steps;
    }

    private static string ExtractPinnedWinGetVersion(string workflowText)
    {
        var line = workflowText.Replace("\r\n", "\n").Split('\n')
            .FirstOrDefault(l => l.TrimStart().StartsWith("WINGET_VERSION:", StringComparison.Ordinal));

        if (line is null)
        {
            throw new InvalidOperationException($"Could not find 'WINGET_VERSION:' in {WorkflowPath}.");
        }

        return line.Split(':', 2)[1].Trim();
    }

    private static string ExtractComInteropVersion(string csprojText)
    {
        var lines = csprojText.Replace("\r\n", "\n").Split('\n');
        var includeIndex = Array.FindIndex(lines,
            l => l.Contains("Include=\"Microsoft.WindowsPackageManager.ComInterop\"", StringComparison.Ordinal));

        if (includeIndex < 0)
        {
            throw new InvalidOperationException(
                $"Could not find the Microsoft.WindowsPackageManager.ComInterop package reference in {CsprojPath}.");
        }

        for (var i = includeIndex; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], "Version=\"([^\"]+)\"");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        throw new InvalidOperationException(
            $"Could not find a Version attribute for the ComInterop package reference in {CsprojPath}.");
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SubZeroDev.WinGet.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
        }

        return directory.FullName;
    }
}
