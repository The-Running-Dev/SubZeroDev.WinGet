namespace SubZeroDev.WinGet.Models;

public enum PackageOperationState
{
    Queued,
    Downloading,
    Installing,
    Uninstalling,
    Repairing,
    PostOperation,
    Completed,
    Failed
}

/// <summary>
/// A single progress update for an in-flight package operation.
/// </summary>
public sealed record PackageOperationProgress(
    PackageOperationState State,
    double PercentComplete,
    string? StatusMessage,
    ulong BytesDownloaded = 0,
    ulong BytesRequired = 0);
