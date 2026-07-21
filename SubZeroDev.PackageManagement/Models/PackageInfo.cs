namespace SubZeroDev.PackageManagement.Models;

/// <summary>
/// A package as known to WinGet, merging catalog data with local install state.
/// </summary>
public sealed record PackageInfo(
    string Id,
    string Name,
    string? Publisher,
    string? InstalledVersion,
    string? AvailableVersion,
    bool IsInstalled,
    bool IsUpdateAvailable,
    string Source);
