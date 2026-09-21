using System.Globalization;
using System.Xml.Linq;
using Nuke.Common.IO;

// #120: Build.cs's AssertLiveTestCount only counts [Category("...")] occurrences in the checked-in
// source before execution (C8) - it catches an under-selecting filter but has no path that reads
// what VSTest actually executed. A [Ignore]-d test still lets `dotnet test` exit 0 (Skipped is not
// a failing outcome), so a partial run reports green. This gate closes that gap by reading the trx
// VSTest wrote and comparing its passed count against the expected count, after the pre-run source
// guard runs and execution completes. Kept as a small directly-testable unit, mirroring CoverageGate.
static class LiveTestCountGate
{
    public readonly record struct Result(
        bool Passed,
        int PassedCount,
        int ExpectedCount);

    public static Result Evaluate(int passedCount, int expectedCount) =>
        new(passedCount == expectedCount, passedCount, expectedCount);

    public static void Assert(string category, int passedCount, int expectedCount)
    {
        var result = Evaluate(passedCount, expectedCount);
        if (!result.Passed)
        {
            throw new InvalidOperationException(
                $"{category} selected {expectedCount} live tests, but only {result.PassedCount} " +
                "executed and passed. A skipped, ignored, or otherwise not-executed test leaves " +
                "`dotnet test` exiting 0; this gate is what fails a partial run instead.");
        }
    }

    // VSTest's trx <ResultSummary><Counters passed="N" .../> already excludes anything
    // Skipped/NotExecuted/Inconclusive, so comparing it directly against the expected count is
    // sufficient - a genuinely failed test also fails `dotnet test`'s own exit code before this
    // runs; this gate exists for the runs that stay green regardless, e.g. an ignored test.
    public static int ReadPassedCount(AbsolutePath trxPath)
    {
        var root = XDocument.Load(trxPath).Root
            ?? throw new InvalidOperationException($"{trxPath} has no root element.");
        var ns = root.GetDefaultNamespace();

        var counters = root.Element(ns + "ResultSummary")?.Element(ns + "Counters")
            ?? throw new InvalidOperationException($"{trxPath} has no ResultSummary/Counters element.");

        return int.Parse(
            counters.Attribute("passed")?.Value
                ?? throw new InvalidOperationException($"{trxPath} Counters has no 'passed' attribute."),
            CultureInfo.InvariantCulture);
    }

    // The most recently written trx under a live target's results directory. A fresh CI checkout
    // only ever produces one; a developer re-running the target locally without cleaning first
    // could leave more than one behind, so this picks the run that just happened.
    public static AbsolutePath FindLatestTrx(AbsolutePath resultsDirectory) =>
        resultsDirectory.GlobFiles("*.trx")
            .OrderByDescending(path => File.GetLastWriteTimeUtc(path))
            .FirstOrDefault()
        ?? throw new InvalidOperationException($"No .trx file found under {resultsDirectory}.");
}
