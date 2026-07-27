using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Abstractions;

/// <summary>
/// WinGet source (catalog) management — the "winget source" equivalent, via the COM API.
/// Add/remove typically require an elevated caller; WinGet returns AccessDenied otherwise.
/// </summary>
/// <remarks>
/// Not <see cref="IDisposable"/> by design; see the remarks on <see cref="IWinGetClient"/> for
/// the ownership rules. Injected instances belong to the container; only direct construction of
/// <see cref="WinGetSourceClient"/> owns a disposable COM owner thread.
/// </remarks>
public interface IWinGetSourceClient
{
    Task<IReadOnlyList<PackageSource>> GetSources(CancellationToken cancellationToken = default);

    Task<PackageSource?> GetSource(string name, CancellationToken cancellationToken = default);

    Task<SourceOperationResult> AddSource(AddPackageSourceRequest request, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a source. With <paramref name="preserveData"/> true, only the registration is
    /// removed and cached data is kept (the "winget source reset" behavior for one source).
    /// </summary>
    Task<SourceOperationResult> RemoveSource(string name, bool preserveData = false, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Forces a refresh of the source's cached index (the "winget source update" equivalent).</summary>
    Task<SourceOperationResult> RefreshSource(string name, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>Edits a source's Explicit and/or Priority properties. Null leaves a property unchanged.</summary>
    Task<SourceOperationResult> UpdateSource(string name, bool? isExplicit = null, int? priority = null, CancellationToken cancellationToken = default);
}
