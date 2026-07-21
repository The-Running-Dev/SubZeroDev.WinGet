using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// Source-management surface for consumers (validation and structured logging over
/// <see cref="IWinGetSourceClient"/>).
/// </summary>
public interface IPackageSourceService
{
    Task<IReadOnlyList<PackageSource>> GetSourcesAsync(CancellationToken cancellationToken = default);

    Task<PackageSource?> GetSourceAsync(string name, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> AddSourceAsync(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> RemoveSourceAsync(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> RefreshSourceAsync(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> UpdateSourceAsync(string name, bool? isExplicit = null, int? priority = null, CancellationToken cancellationToken = default);
}
