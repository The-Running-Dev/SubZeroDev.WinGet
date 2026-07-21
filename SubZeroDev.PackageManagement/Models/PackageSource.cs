namespace SubZeroDev.PackageManagement.Models;

public enum PackageSourceOrigin
{
    Predefined,
    User,
    Unknown
}

public enum PackageSourceTrustLevel
{
    None,
    Trusted
}

/// <summary>
/// A configured WinGet source (the "winget source list" equivalent).
/// </summary>
public sealed record PackageSource(
    string Id,
    string Name,
    string Type,
    string Argument,
    DateTimeOffset? LastUpdated,
    PackageSourceOrigin Origin,
    PackageSourceTrustLevel TrustLevel,
    bool IsExplicit,
    int Priority);

/// <summary>
/// Options for registering a new WinGet source.
/// </summary>
public sealed record AddPackageSourceRequest(string Name, string Uri)
{
    /// <summary>"Microsoft.PreIndexed.Package" or "Microsoft.Rest".</summary>
    public string Type { get; init; } = "Microsoft.PreIndexed.Package";

    public PackageSourceTrustLevel TrustLevel { get; init; } = PackageSourceTrustLevel.None;

    public string? CustomHeader { get; init; }

    /// <summary>Excludes the source from discovery unless explicitly specified.</summary>
    public bool IsExplicit { get; init; }

    /// <summary>Higher values are sorted first.</summary>
    public int Priority { get; init; }
}

/// <summary>
/// The outcome of a source add/remove/edit/refresh operation.
/// </summary>
public sealed record SourceOperationResult(bool Succeeded, string? ErrorMessage, int? ExtendedErrorCode)
{
    public static SourceOperationResult Success() => new(true, null, null);

    public static SourceOperationResult Failure(string errorMessage, int? extendedErrorCode = null) =>
        new(false, errorMessage, extendedErrorCode);
}
