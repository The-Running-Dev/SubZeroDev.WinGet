using System.Globalization;
using System.Xml.Linq;
using Nuke.Common.IO;

// S9: the checked-in coverage floor (C10-C12). Kept separate from Build.cs so the comparison
// itself - the part a regression can silently drop - is one small, directly testable unit
// rather than buried inside the Coverage target's Executes() block.
static class CoverageGate
{
    public readonly record struct Result(
        bool Passed,
        int CoveredLines,
        int ValidLines,
        decimal FloorPercent,
        decimal ActualPercent);

    // C10: enforcement compares exact covered-/valid-line counts against the floor ratio,
    // never rounded percentages. Cross-multiplying keeps the comparison exact - decimal
    // arithmetic has no rounding error for a floor with one decimal place and an integer
    // line count, unlike dividing first and comparing floating-point percentages.
    public static Result Evaluate(int coveredLines, int validLines, decimal floorPercent)
    {
        if (validLines <= 0)
        {
            throw new InvalidOperationException(
                $"Coverage report has {validLines} valid lines; there is nothing to gate.");
        }

        var passed = coveredLines * 100m >= floorPercent * validLines;
        var actualPercent = coveredLines * 100m / validLines;
        return new Result(passed, coveredLines, validLines, floorPercent, actualPercent);
    }

    public static void Assert(int coveredLines, int validLines, decimal floorPercent)
    {
        var result = Evaluate(coveredLines, validLines, floorPercent);
        if (!result.Passed)
        {
            throw new InvalidOperationException(
                $"Unit-only line coverage {result.CoveredLines}/{result.ValidLines} " +
                $"({result.ActualPercent.ToString("F4", CultureInfo.InvariantCulture)}%) is below " +
                $"the checked-in floor of {result.FloorPercent.ToString("F1", CultureInfo.InvariantCulture)}%.");
        }
    }

    // Reads the exact integer counts ReportGenerator's Cobertura output carries on its root
    // <coverage> element - lines-covered/lines-valid - rather than its line-rate attribute,
    // which is already a rounded ratio and would defeat C10's exact-count comparison.
    public static (int CoveredLines, int ValidLines) ReadLineCounts(AbsolutePath coberturaXmlPath)
    {
        var root = XDocument.Load(coberturaXmlPath).Root
            ?? throw new InvalidOperationException($"{coberturaXmlPath} has no root element.");

        int ReadAttribute(string name) =>
            int.Parse(
                root.Attribute(name)?.Value
                    ?? throw new InvalidOperationException($"{coberturaXmlPath} has no '{name}' attribute."),
                CultureInfo.InvariantCulture);

        return (ReadAttribute("lines-covered"), ReadAttribute("lines-valid"));
    }
}
