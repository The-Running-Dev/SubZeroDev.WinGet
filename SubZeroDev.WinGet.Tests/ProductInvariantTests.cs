using System.Reflection;

using FluentAssertions;

using SubZeroDev.WinGet.Abstractions;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// S13 — product invariants that must fail the build when they regress, rather than waiting on a
/// reviewer to notice: the public surface baseline (S13.1, S13.2, S13.6), the declared dependency
/// direction between layers (S13.3), and the CLI shim's exclusive ownership of pin/export/import
/// and process creation (S13.4). Reads only repository source and compiled metadata; activates no
/// COM, starts no process, contacts no network (S13.5).
/// </summary>
[TestFixture]
public class ProductInvariantTests
{
    private static readonly string LibraryRoot = FindDirectory("SubZeroDev.WinGet");
    private static readonly string BaselinePath = Path.Combine(
        FindDirectory("SubZeroDev.WinGet.Tests"), "PublicSurfaceBaseline.txt");

    // ---- S13.1 / S13.6: the checked-in baseline is the whole public surface, exactly ----

    [Test]
    public void PublicSurface_MatchesCheckedInBaselineExactly()
    {
        var baseline = ReadBaseline(BaselinePath);
        var actual = PublicSurfaceScanner.Scan(LibraryRoot);

        var (added, removed) = Diff(actual, baseline);

        (added.Count == 0 && removed.Count == 0).Should().BeTrue(BuildDiffMessage(added, removed));
    }

    // ---- S13.2: the comparison itself catches an added member, and only an added member ----

    [Test]
    public void SurfaceComparison_FailsOnAnAddedMemberAndPassesOnTheUnmodifiedSurface()
    {
        var baseline = PublicSurfaceScanner.Scan(LibraryRoot);
        var withOneAddedMember = new List<string>(baseline) { "WinGetClient.cs|public Task<bool> NotARealMember();" };

        Diff(withOneAddedMember, baseline).added.Should().Equal("WinGetClient.cs|public Task<bool> NotARealMember();");
        Diff(withOneAddedMember, baseline).removed.Should().BeEmpty();

        Diff(baseline, baseline).added.Should().BeEmpty();
        Diff(baseline, baseline).removed.Should().BeEmpty();
    }

    // A plain LINQ Except() is a set operation and silently ignores a change in how many times an
    // identical line repeats (e.g. two records sharing one property declaration verbatim), so the
    // comparison is done by count per distinct entry instead - an added or removed instance of an
    // otherwise-duplicated line is exactly as detectable as a uniquely-worded one.
    private static (List<string> added, List<string> removed) Diff(IReadOnlyList<string> actual, IReadOnlyList<string> baseline)
    {
        var actualCounts = actual.GroupBy(entry => entry, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var baselineCounts = baseline.GroupBy(entry => entry, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var added = new List<string>();
        var removed = new List<string>();

        foreach (var key in actualCounts.Keys.Union(baselineCounts.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            var actualCount = actualCounts.GetValueOrDefault(key);
            var baselineCount = baselineCounts.GetValueOrDefault(key);

            added.AddRange(Enumerable.Repeat(key, Math.Max(0, actualCount - baselineCount)));
            removed.AddRange(Enumerable.Repeat(key, Math.Max(0, baselineCount - actualCount)));
        }

        return (added, removed);
    }

    // ---- S13.3: the declared dependency direction has no back edge ----

    private static readonly string[] ServiceTypeNames = ["PackageManagementService", "PackageSourceService"];
    private static readonly string[] ClientTypeNames = ["WinGetClient", "WinGetSourceClient", "WinGetCliClient"];
    private static readonly string[] ActivationTypeNames =
        ["WinGetFactory", "WinGetComContext", "WinGetActivationModeSelector"];

    [Test]
    public void ClientLayer_ReferencesNoServiceType()
    {
        foreach (var file in new[] { "WinGetClient.cs", "WinGetSourceClient.cs", "WinGetCliClient.cs" })
        {
            AssertSourceReferencesNone(Path.Combine(LibraryRoot, file), ServiceTypeNames);
        }
    }

    [Test]
    public void ActivationLayer_ReferencesNoClientOrServiceType()
    {
        foreach (var file in new[]
                 {
                     Path.Combine("Com", "WinGetFactory.cs"),
                     Path.Combine("Com", "WinGetComContext.cs"),
                     Path.Combine("Com", "WinGetActivationModeSelector.cs")
                 })
        {
            AssertSourceReferencesNone(Path.Combine(LibraryRoot, file), [.. ClientTypeNames, .. ServiceTypeNames]);
        }
    }

    [Test]
    public void Mapper_ReferencesNoneOfTheThreeLayers()
    {
        AssertSourceReferencesNone(
            Path.Combine(LibraryRoot, "WinGetProjectionMapper.cs"),
            [.. ClientTypeNames, .. ServiceTypeNames, .. ActivationTypeNames]);
    }

    [Test]
    public void DependencyDirectionCheck_DetectsAScriptedBackEdge()
    {
        const string scriptedBackEdge = "internal sealed class WinGetFactory { private readonly WinGetClient _client; }";

        var act = () => AssertSourceTextReferencesNone(scriptedBackEdge, ["WinGetClient"], "fixture");

        act.Should().Throw<AssertionException>();
    }

    private static void AssertSourceReferencesNone(string filePath, IReadOnlyList<string> forbiddenTypeNames) =>
        AssertSourceTextReferencesNone(File.ReadAllText(filePath), forbiddenTypeNames, filePath);

    private static void AssertSourceTextReferencesNone(string source, IReadOnlyList<string> forbiddenTypeNames, string label)
    {
        foreach (var typeName in forbiddenTypeNames)
        {
            source.Should().NotContain(typeName,
                $"'{label}' must not depend on '{typeName}' — the declared dependency direction has no back edge");
        }
    }

    // ---- S13.4: IWinGetCliClient's exact members, and sole ownership of process creation ----

    [Test]
    public void IWinGetCliClient_DeclaresExactlyThePinExportAndImportMembers()
    {
        var declared = typeof(IWinGetCliClient).GetMethods().Select(method => method.Name);

        declared.Should().BeEquivalentTo(
            ["GetPins", "AddPin", "RemovePin", "Export", "Import"]);
    }

    [Test]
    public void ProcessCreation_OccursOnlyInTheCliShim()
    {
        var forbidden = new[] { "ProcessStartInfo", "Process.Start" };

        foreach (var file in Directory.EnumerateFiles(LibraryRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == "WinGetCliClient.cs")
            {
                continue;
            }

            var source = File.ReadAllText(file);

            foreach (var token in forbidden)
            {
                source.Should().NotContain(token,
                    $"process creation must occur only in WinGetCliClient.cs, not in {Path.GetFileName(file)}");
            }
        }
    }

    // ---- helpers ----

    private static List<string> ReadBaseline(string path) =>
        File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .OrderBy(line => line, StringComparer.Ordinal)
            .ToList();

    private static string BuildDiffMessage(List<string> added, List<string> removed) =>
        "the public surface must match the checked-in baseline exactly." + Environment.NewLine +
        "Added: " + (added.Count == 0 ? "(none)" : string.Join(Environment.NewLine + "  ", added)) + Environment.NewLine +
        "Removed: " + (removed.Count == 0 ? "(none)" : string.Join(Environment.NewLine + "  ", removed));

    private static string FindDirectory(string name)
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

        return Path.Combine(directory.FullName, name);
    }
}
