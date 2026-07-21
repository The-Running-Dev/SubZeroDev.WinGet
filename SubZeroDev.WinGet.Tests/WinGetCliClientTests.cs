using FluentAssertions;

using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class WinGetCliClientTests
{
    [Test]
    public void ParsePinList_ParsesTypicalTable()
    {
        const string output = """
            Name               Id             Version Source Pin type
            ---------------------------------------------------------
            Git                Git.Git        2.44.0  winget Pinning
            Mozilla Firefox    Mozilla.Firefox        winget Blocking
            Node.js            OpenJS.NodeJS  20.*    winget Gating
            """;

        var pins = WinGetCliClient.ParsePinList(output);

        pins.Should().HaveCount(3);

        pins[0].Should().Be(new PackagePin("Git.Git", "Git", "2.44.0", PackagePinKind.Pinning, "winget"));
        pins[1].Id.Should().Be("Mozilla.Firefox");
        pins[1].Kind.Should().Be(PackagePinKind.Blocking);
        pins[2].Version.Should().Be("20.*");
        pins[2].Kind.Should().Be(PackagePinKind.Gating);
    }

    [Test]
    public void ParsePinList_WithNoPins_ReturnsEmpty()
    {
        var pins = WinGetCliClient.ParsePinList("There are no pins configured.");

        pins.Should().BeEmpty();
    }

    [Test]
    public void ParsePinList_WithEmptyOutput_ReturnsEmpty()
    {
        WinGetCliClient.ParsePinList(string.Empty).Should().BeEmpty();
    }

    [Test]
    public void ParsePinList_WithoutSourceColumn_DoesNotSwallowPinTypeIntoVersion()
    {
        // Regression: with no Source column, the version slice previously ran to end-of-line
        // and captured the pin-type text as part of the version.
        const string output = """
            Name               Id             Version Pin type
            -------------------------------------------------
            Git                Git.Git        2.44.0  Pinning
            """;

        var pins = WinGetCliClient.ParsePinList(output);

        pins.Should().ContainSingle();
        pins[0].Version.Should().Be("2.44.0");
        pins[0].Kind.Should().Be(PackagePinKind.Pinning);
        pins[0].Source.Should().BeEmpty();
    }

    [Test]
    public void ParsePinList_WithCrLfLineEndings_Parses()
    {
        var output = "Name  Id       Version Source Pin type\r\n" +
                     "---------------------------------------\r\n" +
                     "Git   Git.Git  2.44.0  winget Pinning\r\n";

        var pins = WinGetCliClient.ParsePinList(output);

        pins.Should().ContainSingle().Which.Id.Should().Be("Git.Git");
    }

    [Test]
    public void ParsePinList_SkipsShortAndBlankLines()
    {
        const string output = """
            Name  Id       Version Source Pin type
            ---------------------------------------
            Git   Git.Git  2.44.0  winget Pinning

            short
            """;

        var pins = WinGetCliClient.ParsePinList(output);

        pins.Should().ContainSingle();
    }

    [Test]
    public void ParsePinList_SkipsRowsWithEmptyId()
    {
        // A continuation/wrapped line can have content past the pin-type offset but no id.
        var output = "Name  Id       Version Source Pin type\n" +
                     "---------------------------------------\n" +
                     "Git   Git.Git  2.44.0  winget Pinning\n" +
                     "xx    " + new string(' ', 20) + "someoverflowtexthere\n";

        var pins = WinGetCliClient.ParsePinList(output);

        pins.Should().ContainSingle().Which.Id.Should().Be("Git.Git");
    }

    [Test]
    public void ParsePinList_WithHeaderMissingRequiredColumns_ReturnsEmpty()
    {
        const string output = """
            Something Unrelated
            -------------------
            Data      Here
            """;

        WinGetCliClient.ParsePinList(output).Should().BeEmpty();
    }

    [Test]
    public void ParsePinList_WithSeparatorButNoHeaderAbove_ReturnsEmpty()
    {
        WinGetCliClient.ParsePinList("----------\ndata").Should().BeEmpty();
    }

    [Test]
    public void ParsePinList_WithUnknownPinTypeText_DefaultsToPinning()
    {
        const string output = """
            Name  Id       Version Source Pin type
            ---------------------------------------
            Git   Git.Git  2.44.0  winget Mystery
            """;

        WinGetCliClient.ParsePinList(output).Single().Kind.Should().Be(PackagePinKind.Pinning);
    }

    // The exact CLI argument lists are a contract with winget.exe — flag regressions here
    // would silently break pin/export/import at runtime.

    [Test]
    public void BuildAddPinArguments_Minimal_UsesExactIdAndNonInteractiveFlags()
    {
        WinGetCliClient.BuildAddPinArguments("Git.Git", null, false, false)
            .Should().Equal("pin", "add", "--id", "Git.Git", "--exact", "--accept-source-agreements", "--disable-interactivity");
    }

    [Test]
    public void BuildAddPinArguments_WithVersionBlockingAndInstalled_AppendsAllFlags()
    {
        WinGetCliClient.BuildAddPinArguments("Git.Git", "2.44.*", true, true)
            .Should().Equal(
                "pin", "add", "--id", "Git.Git", "--exact", "--accept-source-agreements", "--disable-interactivity",
                "--version", "2.44.*", "--blocking", "--installed");
    }

    [Test]
    public void BuildRemovePinArguments_WithInstalled_AppendsInstalledFlag()
    {
        WinGetCliClient.BuildRemovePinArguments("Git.Git", true)
            .Should().Equal("pin", "remove", "--id", "Git.Git", "--exact", "--accept-source-agreements", "--disable-interactivity", "--installed");
    }

    [Test]
    public void BuildExportArguments_WithVersionsAndSource_AppendsBoth()
    {
        WinGetCliClient.BuildExportArguments(@"C:\out.json", true, "winget")
            .Should().Equal("export", "--output", @"C:\out.json", "--accept-source-agreements", "--disable-interactivity", "--include-versions", "--source", "winget");
    }

    [Test]
    public void BuildImportArguments_AcceptsBothAgreementKinds_AndOptionalIgnoreFlags()
    {
        WinGetCliClient.BuildImportArguments(@"C:\in.json", true, true)
            .Should().Equal(
                "import", "--import-file", @"C:\in.json", "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity",
                "--ignore-unavailable", "--ignore-versions");
    }
}
