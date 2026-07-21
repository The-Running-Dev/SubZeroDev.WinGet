using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// Source-management surface for consumers (validation and structured logging over
/// <see cref="IWinGetSourceClient"/>).
/// </summary>
public interface IPackageSourceService
{
    Task<IReadOnlyList<PackageSource>> GetSources(CancellationToken cancellationToken = default);

    Task<PackageSource?> GetSource(string name, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> AddSource(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> RemoveSource(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> RefreshSource(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> UpdateSource(string name, bool? isExplicit = null, int? priority = null, CancellationToken cancellationToken = default);
}
