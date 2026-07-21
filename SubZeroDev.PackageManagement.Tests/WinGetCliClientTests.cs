using FluentAssertions;

using SubZeroDev.PackageManagement.Models;

namespace SubZeroDev.PackageManagement.Tests;

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
}
