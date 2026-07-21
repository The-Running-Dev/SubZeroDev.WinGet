using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Tests;

/// <summary>Failure outcomes and parameter-variant paths for <see cref="PackageSourceService"/>.</summary>
[TestFixture]
public class PackageSourceServiceEdgeTests
{
    private Mock<IWinGetSourceClient> _sourceClient = null!;

    private PackageSourceService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _sourceClient = new Mock<IWinGetSourceClient>(MockBehavior.Strict);
        _service = new PackageSourceService(_sourceClient.Object, NullLogger<PackageSourceService>.Instance);
    }

    [Test]
    public async Task AddSource_ReturnsFailureUnchanged()
    {
        var failure = SourceOperationResult.Failure("AccessDenied", unchecked((int)0x80070005));

        _sourceClient
            .Setup(c => c.AddSource(It.IsAny<AddPackageSourceRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        var result = await _service.AddSource(new AddPackageSourceRequest("contoso", "https://contoso.example"));

        result.Should().Be(failure);
    }

    [Test]
    public async Task RemoveSource_PassesPreserveDataThrough()
    {
        _sourceClient
            .Setup(c => c.RemoveSource("contoso", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        (await _service.RemoveSource("contoso", preserveData: true)).Succeeded.Should().BeTrue();

        _sourceClient.VerifyAll();
    }

    [Test]
    public async Task RefreshSource_ReturnsFailureUnchanged()
    {
        var failure = SourceOperationResult.Failure("CatalogError");

        _sourceClient
            .Setup(c => c.RefreshSource("winget", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failure);

        (await _service.RefreshSource("winget")).Should().Be(failure);
    }

    [Test]
    public async Task UpdateSource_WithExplicitOnly_Delegates()
    {
        _sourceClient
            .Setup(c => c.UpdateSource("winget", true, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        (await _service.UpdateSource("winget", isExplicit: true)).Succeeded.Should().BeTrue();
    }

    [Test]
    public async Task UpdateSource_WithPriorityOnly_Delegates()
    {
        _sourceClient
            .Setup(c => c.UpdateSource("winget", null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SourceOperationResult.Success());

        (await _service.UpdateSource("winget", priority: 10)).Succeeded.Should().BeTrue();
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void RemoveRefreshUpdate_WithNoName_ThrowArgumentException(string? name)
    {
        var operations = new Func<Task>[]
        {
            () => _service.RemoveSource(name!),
            () => _service.RefreshSource(name!),
            () => _service.UpdateSource(name!, isExplicit: true),
        };

        foreach (var operation in operations)
        {
            operation.Should().ThrowAsync<ArgumentException>();
        }
    }
}
