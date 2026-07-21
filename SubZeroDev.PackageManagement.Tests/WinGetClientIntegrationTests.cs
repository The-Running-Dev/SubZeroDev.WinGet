using FluentAssertions;

namespace SubZeroDev.PackageManagement.Tests;

/// <summary>
/// Exercises the real WinGet COM API on the machine running the tests. Excluded from normal
/// `dotnet test` runs (NUnit's [Explicit]) since it requires an actual Windows machine with
/// WinGet installed and makes live catalog/network calls. Deliberately read-only — no
/// Install/Upgrade/Uninstall calls belong here, since those would mutate the test machine.
/// Run explicitly with:
///   dotnet test --filter "FullyQualifiedName~WinGetClientIntegrationTests"
/// </summary>
[TestFixture]
[Explicit("Requires a real Windows machine with WinGet installed; makes live catalog calls.")]
public class WinGetClientIntegrationTests
{
    private WinGetClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _client = new WinGetClient();
    }

    [Test]
    public async Task SearchAsync_ForVsCode_ReturnsAtLeastOneMatch()
    {
        var results = await _client.SearchAsync("Visual Studio Code", limit: 10, CancellationToken.None);

        results.Should().NotBeEmpty();
        results.Should().Contain(p => p.Id == "Microsoft.VisualStudioCode");
    }

    [Test]
    public async Task GetInstalledPackagesAsync_ReturnsTheLocalMachinesInstalledPackages()
    {
        var results = await _client.GetInstalledPackagesAsync(CancellationToken.None);

        results.Should().NotBeEmpty();
        results.Should().OnlyContain(p => p.IsInstalled);
    }

    [Test]
    public async Task GetPackageAsync_ForAKnownId_ReturnsMatchingDetails()
    {
        var package = await _client.GetPackageAsync("Microsoft.VisualStudioCode", CancellationToken.None);

        package.Should().NotBeNull();
        package!.Id.Should().Be("Microsoft.VisualStudioCode");
        package.Name.Should().Contain("Visual Studio Code");
    }

    [Test]
    public async Task GetPackageAsync_ForAnUnknownId_ReturnsNull()
    {
        var package = await _client.GetPackageAsync("Not.A.Real.Package.Id.12345", CancellationToken.None);

        package.Should().BeNull();
    }
}
