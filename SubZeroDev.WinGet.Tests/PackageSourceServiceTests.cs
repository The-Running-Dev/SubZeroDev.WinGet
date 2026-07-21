using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

[TestFixture]
public class PackageSourceServiceTests
{
    private Mock<IWinGetSourceClient> _sourceClient = null!;

    private PackageSourceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _sourceClient = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        _service = new PackageSourceService(_sourceClient.Object, NullLogger<PackageSourceService>.Instance);
    }

    private static PackageSource MakeSource(string name = "winget") =>
        new("Microsoft.Winget.Source_8wekyb3d8bbwe", name, "Microsoft.PreIndexed.Package", "https://cdn.winget.microsoft.com/cache", DateTimeOffset.UtcNow, PackageSourceOrigin.Predefined, PackageSourceTrustLevel.Trusted, false, 0);

    [Test]
    public async Task GetSourcesAsync_DelegatesToClient()
    {
        var expected = new List<PackageSource> { MakeSource(), MakeSource("msstore") };

        _sourceClient
            .Setup(c => c.GetSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetSourcesAsync();

        result.Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetSourceAsync_DelegatesToClient()
    {
        var expected = MakeSource();

        _sourceClient
            .Setup(c => c.GetSourceAsync("winget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.GetSourceAsync("winget");

        result.Should().Be(expected);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void GetSourceAsync_WithNoName_ThrowsArgumentException(string? name)
    {
        var act = async () => await _service.GetSourceAsync(name!);

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task AddSourceAsync_DelegatesToClient()
    {
        var request = new AddPackageSourceRequest("contoso", "https://contoso.example/source");

        _sourceClient
            .Setup(c => c.AddSourceAsync(request, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.AddSourceAsync(request);

        result.Succeeded.Should().BeTrue();
        _sourceClient.VerifyAll();
    }

    [Test]
    public void AddSourceAsync_WithNoUri_ThrowsArgumentException()
    {
        var act = async () => await _service.AddSourceAsync(new AddPackageSourceRequest("contoso", ""));

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task RemoveSourceAsync_DelegatesToClient()
    {
        _sourceClient
            .Setup(c => c.RemoveSourceAsync("contoso", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.RemoveSourceAsync("contoso", preserveData: true);

        result.Succeeded.Should().BeTrue();
        _sourceClient.VerifyAll();
    }

    [Test]
    public async Task RefreshSourceAsync_DelegatesToClient()
    {
        _sourceClient
            .Setup(c => c.RefreshSourceAsync("winget", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.RefreshSourceAsync("winget");

        result.Succeeded.Should().BeTrue();
    }

    [Test]
    public void UpdateSourceAsync_WithNothingToChange_ThrowsArgumentException()
    {
        var act = async () => await _service.UpdateSourceAsync("winget");

        act.Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task UpdateSourceAsync_DelegatesToClient()
    {
        _sourceClient
            .Setup(c => c.UpdateSourceAsync("winget", true, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        var result = await _service.UpdateSourceAsync("winget", isExplicit: true, priority: 5);

        result.Succeeded.Should().BeTrue();
        _sourceClient.VerifyAll();
    }
}
