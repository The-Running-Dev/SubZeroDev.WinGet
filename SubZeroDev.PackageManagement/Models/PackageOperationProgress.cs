namespace SubZeroDev.PackageManagement.Models;

public enum PackageOperationState
{
    Queued,
    Downloading,
    Installing,
    PostInstall,
    Completed,
    Failed
}

/// <summary>
/// A single progress update for an in-flight install/upgrade/uninstall operation.
/// </summary>
public sealed record PackageOperationProgress(
    PackageOperationState State,
    double PercentComplete,
    string? StatusMessage);
