using FluentAssertions;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// S8.2 — a source-level negative dependency check proving the required `build` job stays
/// hermetic under C7: it must never invoke a live nuke target, activate WinGet COM (via
/// `Get-AppxPackage`/`Add-AppxPackage`), or execute `winget.exe`. Reads only the checked-in
/// workflow file - no COM, no process, no network - so it runs inside the existing hermetic
/// `Test` target with no new CI wiring, the same as `WorkflowCompositionTests`.
/// </summary>
[TestFixture]
public class HermeticJobTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string WorkflowPath = Path.Combine(RepoRoot, ".github", "workflows", "build.yml");

    private const string HermeticJobName = "build";

    // The live nuke targets and the COM/process/network signals a hermetic job must never
    // reference. `IntegrationTest` is included because it is the local aggregate of the other
    // two live-test targets (C8) and so is just as disqualifying as naming them individually.
    private static readonly string[] ForbiddenSignals =
    [
        "nuke MachineStateTest",
        "nuke CatalogIntegrationTest",
        "nuke IntegrationTest",
        "nuke PackedConsumerSmokeTest",
        "Get-AppxPackage",
        "Add-AppxPackage",
        "winget.exe",
    ];

    [Test]
    public void HermeticJob_ReferencesNoLiveTargetOrComOrProcessSignal()
    {
        AssertJobStaysHermetic(ParseJobs(File.ReadAllText(WorkflowPath)).Single(j => j.Name == HermeticJobName));
    }

    [Test]
    public void HermeticJobCheck_FailsWhenTheJobInvokesALiveTarget()
    {
        const string fixture = """
            jobs:
              build:
                steps:
                  - name: Test, coverage, architecture, and package
                    run: nuke Test Coverage ArchitectureTest PackageTest --configuration Release
                  - name: Sneak in a live check
                    run: nuke MachineStateTest --configuration Release
            """;

        var act = () => AssertJobStaysHermetic(ParseJobs(fixture).Single(j => j.Name == HermeticJobName));

        act.Should().Throw<AssertionException>();
    }

    [Test]
    public void HermeticJobCheck_FailsWhenTheJobActivatesComDirectly()
    {
        const string fixture = """
            jobs:
              build:
                steps:
                  - name: Test, coverage, architecture, and package
                    run: nuke Test Coverage ArchitectureTest PackageTest --configuration Release
                  - name: Probe WinGet
                    run: Get-AppxPackage Microsoft.DesktopAppInstaller
            """;

        var act = () => AssertJobStaysHermetic(ParseJobs(fixture).Single(j => j.Name == HermeticJobName));

        act.Should().Throw<AssertionException>();
    }

    [Test]
    public void HermeticJobCheck_PassesOnTheRealWorkflow()
    {
        var act = () => AssertJobStaysHermetic(
            ParseJobs(File.ReadAllText(WorkflowPath)).Single(j => j.Name == HermeticJobName));

        act.Should().NotThrow();
    }

    private static void AssertJobStaysHermetic(WorkflowJob job)
    {
        var jobText = string.Join('\n', job.Steps.Select(step => step.Text));

        foreach (var signal in ForbiddenSignals)
        {
            jobText.Should().NotContain(signal,
                $"the required '{HermeticJobName}' job must remain hermetic under C7 and must not " +
                $"reference '{signal}' (S8.2)");
        }
    }

    // ---- helpers: the same deliberately minimal workflow-YAML reader as WorkflowCompositionTests
    // (S15) - two-space job indent, six-space step indent. Duplicated rather than shared so this
    // check has no dependency on that file's private surface. ----

    private sealed record WorkflowStep(string? Name, string Text);

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

            steps.Add(new WorkflowStep(name, text));
        }

        return steps;
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
