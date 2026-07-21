using Microsoft.Extensions.DependencyInjection;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet.Examples;

/// <summary>Examples for every package operation on <see cref="IPackageManagementService"/>.</summary>
public static class PackageExamples
{
    private const string DefaultId = "Microsoft.VisualStudioCode";

    /// <summary>GetWinGetVersion — the version of WinGet the COM API is backed by.</summary>
    public static async Task Version(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var packages = services.GetRequiredService<IPackageManagementService>();

        var version = await packages.GetWinGetVersion(ct);

        Console.WriteLine($"WinGet version: {version ?? "(unavailable — requires winget >= 1.6 / contract 13)"}");
    }

    /// <summary>
    /// Search — one call searches name/id/moniker/tag across every configured source, with
    /// installed state already correlated into each result (no second "list installed" call needed).
    /// </summary>
    public static async Task Search(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var query = args.FirstOrDefault() ?? "vscode";
        var packages = services.GetRequiredService<IPackageManagementService>();

        var results = await packages.Search(query, sourceName: null, ct);

        Console.WriteLine($"{results.Count} result(s) for '{query}':");

        foreach (var p in results)
        {
            var state = p.IsInstalled
                ? p.IsUpdateAvailable ? $"installed {p.InstalledVersion}, update {p.AvailableVersion}" : $"installed {p.InstalledVersion}"
                : $"available {p.AvailableVersion}";

            Console.WriteLine($"  {p.Id,-45} {p.Name,-35} [{p.Source}] {state}");
        }
    }

    /// <summary>Search with sourceName — restrict the search to a single configured source.</summary>
    public static async Task SearchSingleSource(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var query = args.ElementAtOrDefault(0) ?? "vscode";
        var source = args.ElementAtOrDefault(1) ?? "winget";
        var packages = services.GetRequiredService<IPackageManagementService>();

        var results = await packages.Search(query, source, ct);

        Console.WriteLine($"{results.Count} result(s) for '{query}' in source '{source}'.");
    }

    /// <summary>GetInstalled — everything installed on this machine, WinGet-managed or not.</summary>
    public static async Task Installed(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var packages = services.GetRequiredService<IPackageManagementService>();

        var installed = await packages.GetInstalled(ct);

        Console.WriteLine($"{installed.Count} installed package(s); showing first 25:");

        foreach (var p in installed.Take(25))
        {
            // Non-WinGet software surfaces with a raw ARP registry key as its Id — those entries
            // can't be targeted by id for install/upgrade, so flag them rather than offering updates.
            var managed = p.Id.StartsWith(@"ARP\", StringComparison.OrdinalIgnoreCase) ? "unmanaged" : p.Source;

            Console.WriteLine($"  {p.Name,-45} {p.InstalledVersion,-20} [{managed}]");
        }
    }

    /// <summary>GetAvailableUpgrades — the "winget upgrade" list.</summary>
    public static async Task Upgrades(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var packages = services.GetRequiredService<IPackageManagementService>();

        var upgrades = await packages.GetAvailableUpgrades(ct);

        Console.WriteLine($"{upgrades.Count} package(s) with an available upgrade:");

        foreach (var p in upgrades)
        {
            Console.WriteLine($"  {p.Id,-45} {p.InstalledVersion} -> {p.AvailableVersion}");
        }
    }

    /// <summary>GetPackage — exact-id lookup returning install/update state.</summary>
    public static async Task Get(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var id = args.FirstOrDefault() ?? DefaultId;
        var packages = services.GetRequiredService<IPackageManagementService>();

        var package = await packages.GetPackage(id, ct);

        Console.WriteLine(package is null
            ? $"'{id}' was not found in any source."
            : $"{package.Id}: {package.Name} by {package.Publisher} — installed: {package.InstalledVersion ?? "no"}, available: {package.AvailableVersion ?? "n/a"}");
    }

    /// <summary>
    /// GetDetails — the full catalog manifest: description, license, agreements,
    /// documentation links, icons, tags, and every available version.
    /// </summary>
    public static async Task Details(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var id = args.FirstOrDefault() ?? DefaultId;
        var packages = services.GetRequiredService<IPackageManagementService>();

        var details = await packages.GetDetails(id, ct);

        if (details is null)
        {
            Console.WriteLine($"'{id}' was not found in any source.");

            return;
        }

        Console.WriteLine($"{details.Name} ({details.Id})");
        Console.WriteLine($"  Publisher : {details.Publisher} ({details.PublisherUrl})");
        Console.WriteLine($"  License   : {details.License} ({details.LicenseUrl})");
        Console.WriteLine($"  Homepage  : {details.PackageUrl}");
        Console.WriteLine($"  About     : {details.ShortDescription}");
        Console.WriteLine($"  Tags      : {string.Join(", ", details.Tags.Take(10))}");
        Console.WriteLine($"  Agreements: {details.Agreements.Count}, Docs: {details.Documentations.Count}, Icons: {details.Icons.Count}");
        Console.WriteLine($"  Versions  : {string.Join(", ", details.AvailableVersions.Take(8))}{(details.AvailableVersions.Count > 8 ? ", ..." : "")}");
    }

    /// <summary>
    /// Install — full options (version pin, scope, mode, architecture, installer type,
    /// custom arguments) plus live progress. The service layer auto-retries the two documented
    /// recoverable cases (already-installed => success; no-applicable-installer under constraints
    /// => retry unconstrained).
    /// </summary>
    public static async Task Install(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: install <package-id> [version] — installs software on THIS machine.");

            return;
        }

        var packages = services.GetRequiredService<IPackageManagementService>();

        var request = new InstallRequest
        {
            Version = args.ElementAtOrDefault(1),           // null => latest applicable version
            Scope = PackageScope.Any,                        // or User / System
            Mode = PackageOperationMode.Silent,              // or Interactive to show installer UI
            Architecture = PackageArchitecture.Default,      // or X64 / Arm64 / ...
            InstallerType = PackageInstallerKind.Default,    // or force Msi / Msix / Zip / ...
            // PreferredInstallLocation = @"C:\Tools\App",   // if the installer supports it
            // OverrideArguments = "/S /D=C:\\App",          // REPLACES installer args (--override)
            // AdditionalArguments = "/NoDesktopShortcut",   // appended installer args (--custom)
            AcceptPackageAgreements = true,
        };

        var result = await packages.Install(args[0], request, Progress(), ct);

        Report("Install", result);
    }

    /// <summary>Update — upgrade an installed package (auto-retries unknown-version upgrades).</summary>
    public static async Task Update(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: update <package-id> — changes software on THIS machine.");

            return;
        }

        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.Update(args[0], new InstallRequest(), Progress(), ct);

        Report("Update", result);
    }

    /// <summary>Uninstall.</summary>
    public static async Task Uninstall(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: uninstall <package-id> — removes software from THIS machine.");

            return;
        }

        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.Uninstall(args[0], new UninstallRequest { Mode = PackageOperationMode.Silent }, Progress(), ct);

        Report("Uninstall", result);
    }

    /// <summary>Download — fetch the installer file without running it (winget download).</summary>
    public static async Task Download(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: download <package-id> [directory] — downloads the installer (can be large).");

            return;
        }

        var directory = args.ElementAtOrDefault(1) ?? Path.Combine(Path.GetTempPath(), "winget-downloads");
        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.Download(args[0], new DownloadRequest(directory), Progress(), ct);

        Report("Download", result);

        if (result.Succeeded)
        {
            Console.WriteLine($"Installer saved under: {directory}");
        }
    }

    /// <summary>Repair — winget repair; requires the installer technology to support it.</summary>
    public static async Task Repair(IServiceProvider services, string[] args, CancellationToken ct)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("usage: repair <package-id> — runs the package's repair on THIS machine.");

            return;
        }

        var packages = services.GetRequiredService<IPackageManagementService>();

        var result = await packages.Repair(args[0], new RepairRequest(), Progress(), ct);

        Report("Repair", result);
    }

    /// <summary>
    /// The low-level path: IWinGetClient bypasses the service layer's validation/logging/retry
    /// policy — single attempt, raw normalized results. Same registration, one interface lower.
    /// </summary>
    public static async Task LowLevel(IServiceProvider services, string[] args, CancellationToken ct)
    {
        var query = args.FirstOrDefault() ?? "vscode";
        var client = services.GetRequiredService<IWinGetClient>();

        var results = await client.Search(query, limit: 5, sourceName: null, ct);

        Console.WriteLine($"IWinGetClient.Search('{query}', limit 5): {results.Count} result(s), no retry policy applied.");
    }

    private static Progress<PackageOperationProgress> Progress() =>
        new(p => Console.WriteLine($"  [{p.State}] {p.PercentComplete:F0}% {p.StatusMessage}"));

    private static void Report(string operation, PackageOperationResult result)
    {
        Console.WriteLine(result.Succeeded
            ? $"{operation} succeeded.{(result.RebootRequired ? " A reboot is required to finish." : "")}"
            : $"{operation} failed: {result.Status} — {result.ErrorMessage} (0x{result.ExtendedErrorCode ?? 0:X8}, installer code: {result.InstallerErrorCode?.ToString() ?? "n/a"})");
    }
}
