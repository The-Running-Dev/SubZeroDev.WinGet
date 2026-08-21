using FluentAssertions;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// Exercises the real WinGet COM API on the machine running the tests. Excluded from normal
/// `dotnet test` runs (NUnit's [Explicit]) since it requires an actual Windows machine with
/// WinGet installed. The <c>MachineState</c> and <c>CatalogIntegration</c> categories declare
/// whether each assertion depends on local machine state or a remote catalog witness. Deliberately read-only — no
/// Install/Upgrade/Uninstall/Repair/Import calls belong here, since those would mutate the
/// test machine.
/// Run explicitly with <c>nuke MachineStateTest</c>, <c>nuke CatalogIntegrationTest</c>, or
/// <c>nuke IntegrationTest</c> for both categories.
/// </summary>
[TestFixture]
[Explicit("Requires a real Windows machine with WinGet installed; makes live catalog calls.")]
[NonParallelizable]
public class WinGetClientIntegrationTests
{
    private WinGetClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new WinGetClient();
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    [Category("MachineState")]
    public async Task GetWinGetVersion_ReturnsAVersion()
    {
        var version = await _client.GetWinGetVersion();

        version.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    [Category("CatalogIntegration")]
    public async Task Search_ForVsCode_ReturnsAtLeastOneMatch()
    {
        var results = await _client.Search("Visual Studio Code", limit: 10);

        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Id == "Microsoft.VisualStudioCode");
    }

    [Test]
    [Category("CatalogIntegration")]
    public async Task Search_RestrictedToWingetSource_OnlyReturnsThatSource()
    {
        var results = await _client.Search("git", limit: 10, sourceName: "winget");

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(p => p.Source == "winget");
    }

    [Test]
    [Category("MachineState")]
    public async Task GetInstalledPackages_ReturnsTheLocalMachinesInstalledPackages()
    {
        var results = await _client.GetInstalledPackages();

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(p => p.IsInstalled);
    }

    [Test]
    [Category("MachineState")]
    public async Task GetAvailableUpgrades_OnlyReturnsPackagesWithUpdates()
    {
        var results = await _client.GetAvailableUpgrades();

        results.Should().OnlyContain(p => p.IsUpdateAvailable && p.IsInstalled);
    }

    [Test]
    [Category("CatalogIntegration")]
    public async Task GetPackage_ForAKnownId_ReturnsMatchingDetails()
    {
        var package = await _client.GetPackage("Microsoft.VisualStudioCode");

        package.Should().NotBeNull();
        package!.Id.Should().Be("Microsoft.VisualStudioCode");
        package.Name.Should().Contain("Visual Studio Code");
    }

    [Test]
    [Category("CatalogIntegration")]
    public async Task GetPackage_ForAnUnknownId_ReturnsNull()
    {
        var package = await _client.GetPackage("Not.A.Real.Package.Id.12345");

        package.Should().BeNull();
    }

    [Test]
    [Category("CatalogIntegration")]
    public async Task GetPackageDetails_ForAKnownId_ReturnsManifestMetadata()
    {
        var details = await _client.GetPackageDetails("Microsoft.VisualStudioCode");

        details.Should().NotBeNull();
        details!.Id.Should().Be("Microsoft.VisualStudioCode");
        details.Publisher.Should().Contain("Microsoft");

        // The pre-indexed winget source populates ShortDescription; full Description is often
        // empty there (verified live) — accept either.
        (details.ShortDescription ?? details.Description).Should().NotBeNullOrWhiteSpace();
        details.AvailableVersions.Should().NotBeEmpty();
        details.Tags.Should().NotBeEmpty();
    }
}

/// <summary>
/// Live read-only checks for source management. Same [Explicit] rationale as above; source
/// add/remove/refresh are not exercised because they mutate machine state (and require
/// elevation).
/// </summary>
[TestFixture]
[Explicit("Requires a real Windows machine with WinGet installed.")]
[NonParallelizable]
public class WinGetSourceClientIntegrationTests
{
    private WinGetSourceClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new WinGetSourceClient();
    }

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    [Category("MachineState")]
    public async Task GetSources_ReturnsTheConfiguredSources()
    {
        var sources = await _client.GetSources();

        sources.Should().NotBeEmpty();
        sources.Should().Contain(s => s.Name == "winget");
    }

    [Test]
    [Category("MachineState")]
    public async Task GetSource_ForWinget_ReturnsIt()
    {
        var source = await _client.GetSource("winget");

        source.Should().NotBeNull();
        source!.Name.Should().Be("winget");
        source.Type.Should().NotBeNullOrWhiteSpace();
    }
}

/// <summary>
/// Live read-only checks for the CLI shim. Pin add/remove and import are not exercised
/// because they mutate machine state; export writes only to a temp file which is cleaned up.
/// </summary>
[TestFixture]
[Explicit("Requires a real Windows machine with winget.exe available.")]
[NonParallelizable]
public class WinGetCliClientIntegrationTests
{
    private WinGetCliClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new WinGetCliClient();
    }

    [Test]
    [Category("MachineState")]
    public async Task GetPins_ReturnsWithoutError()
    {
        var act = async () => await _client.GetPins();

        await act.Should().NotThrowAsync();
    }

    [Test]
    [Category("MachineState")]
    public async Task Export_WritesAnImportableJsonFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"winget-export-{Guid.NewGuid():N}.json");

        try
        {
            var result = await _client.Export(filePath);

            result.Succeeded.Should().BeTrue(result.Output + result.Error);
            File.Exists(filePath).Should().BeTrue();
            (await File.ReadAllTextAsync(filePath)).Should().Contain("Packages");
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
