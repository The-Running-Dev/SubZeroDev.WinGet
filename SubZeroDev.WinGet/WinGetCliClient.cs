using System.Diagnostics;
using System.Text;

using SubZeroDev.WinGet.Abstractions;
using SubZeroDev.WinGet.Models;

namespace SubZeroDev.WinGet;

/// <summary>
/// The deliberately-isolated winget.exe shim. Pin management and export/import have no COM
/// equivalent in Microsoft.Management.Deployment (verified against the contract-29 IDL), so
/// these — and only these — operations shell out to the CLI. Everything else in this library
/// uses the COM API.
/// </summary>
public sealed class WinGetCliClient : IWinGetCliClient
{
    private readonly Lazy<string> _wingetPath = new(ResolveWinGetPath);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PackagePin>> GetPins(CancellationToken cancellationToken = default)
    {
        var result = await Run(["pin", "list", "--accept-source-agreements", "--disable-interactivity"], cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            // "pin list" exits non-zero when no pins are configured; treat that as empty.
            return [];
        }

        return ParsePinList(result.Output);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> AddPin(string packageId, string? version = null, bool blocking = false, bool pinInstalledVersion = false, CancellationToken cancellationToken = default)
    {
        return Run(BuildAddPinArguments(packageId, version, blocking, pinInstalledVersion), cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> RemovePin(string packageId, bool pinInstalledVersion = false, CancellationToken cancellationToken = default)
    {
        return Run(BuildRemovePinArguments(packageId, pinInstalledVersion), cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> Export(string filePath, bool includeVersions = false, string? sourceName = null, CancellationToken cancellationToken = default)
    {
        return Run(BuildExportArguments(filePath, includeVersions, sourceName), cancellationToken);
    }

    /// <inheritdoc />
    public Task<CliOperationResult> Import(string filePath, bool ignoreUnavailable = false, bool ignoreVersions = false, CancellationToken cancellationToken = default)
    {
        return Run(BuildImportArguments(filePath, ignoreUnavailable, ignoreVersions), cancellationToken);
    }

    internal static List<string> BuildAddPinArguments(string packageId, string? version, bool blocking, bool pinInstalledVersion)
    {
        var arguments = new List<string> { "pin", "add", "--id", packageId, "--exact", "--accept-source-agreements", "--disable-interactivity" };

        if (version is not null)
        {
            arguments.AddRange(["--version", version]);
        }

        if (blocking)
        {
            arguments.Add("--blocking");
        }

        if (pinInstalledVersion)
        {
            arguments.Add("--installed");
        }

        return arguments;
    }

    internal static List<string> BuildRemovePinArguments(string packageId, bool pinInstalledVersion)
    {
        var arguments = new List<string> { "pin", "remove", "--id", packageId, "--exact", "--accept-source-agreements", "--disable-interactivity" };

        if (pinInstalledVersion)
        {
            arguments.Add("--installed");
        }

        return arguments;
    }

    internal static List<string> BuildExportArguments(string filePath, bool includeVersions, string? sourceName)
    {
        var arguments = new List<string> { "export", "--output", filePath, "--accept-source-agreements", "--disable-interactivity" };

        if (includeVersions)
        {
            arguments.Add("--include-versions");
        }

        if (sourceName is not null)
        {
            arguments.AddRange(["--source", sourceName]);
        }

        return arguments;
    }

    internal static List<string> BuildImportArguments(string filePath, bool ignoreUnavailable, bool ignoreVersions)
    {
        var arguments = new List<string> { "import", "--import-file", filePath, "--accept-source-agreements", "--accept-package-agreements", "--disable-interactivity" };

        if (ignoreUnavailable)
        {
            arguments.Add("--ignore-unavailable");
        }

        if (ignoreVersions)
        {
            arguments.Add("--ignore-versions");
        }

        return arguments;
    }

    private async Task<CliOperationResult> Run(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _wingetPath.Value,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
        {
            throw new WinGetUnavailableException($"Failed to start winget.exe at '{_wingetPath.Value}'.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already exited.
            }

            throw;
        }

        return new CliOperationResult(
            process.ExitCode == 0,
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }

    /// <summary>
    /// Parses "winget pin list" tabular output. Column offsets are derived from the header row
    /// at runtime (they vary per invocation with content width), never hardcoded.
    /// </summary>
    internal static List<PackagePin> ParsePinList(string output)
    {
        var pins = new List<PackagePin>();
        var lines = output.Replace("\r", string.Empty).Split('\n');

        var separatorIndex = Array.FindIndex(lines, line => line.Length > 0 && line.All(c => c == '-'));

        if (separatorIndex < 1)
        {
            return pins;
        }

        var header = lines[separatorIndex - 1];

        var idStart = header.IndexOf("Id", StringComparison.Ordinal);
        var versionStart = header.IndexOf("Version", StringComparison.Ordinal);
        var sourceStart = header.IndexOf("Source", StringComparison.Ordinal);
        var pinTypeStart = header.IndexOf("Pin type", StringComparison.Ordinal);

        if (idStart < 0 || versionStart < 0 || pinTypeStart < 0)
        {
            return pins;
        }

        foreach (var line in lines.Skip(separatorIndex + 1))
        {
            if (line.Length <= pinTypeStart)
            {
                continue;
            }

            var name = line[..idStart].Trim();
            var id = line[idStart..versionStart].Trim();
            // The Version column ends where the next present column begins — Source when the
            // table has one, otherwise Pin type (never end-of-line, which would swallow it).
            var versionEnd = sourceStart > versionStart ? sourceStart : pinTypeStart;
            var version = line[versionStart..versionEnd].Trim();
            var source = sourceStart > 0 && sourceStart < pinTypeStart ? line[sourceStart..pinTypeStart].Trim() : string.Empty;
            var kindText = line[pinTypeStart..].Trim();

            if (id.Length == 0)
            {
                continue;
            }

            var kind = kindText switch
            {
                "Blocking" => PackagePinKind.Blocking,
                "Gating" => PackagePinKind.Gating,
                _ => PackagePinKind.Pinning
            };

            pins.Add(new PackagePin(id, name, version, kind, source));
        }

        return pins;
    }

    /// <summary>
    /// Resolves winget.exe. The App Execution Alias works for interactive users; service/SYSTEM
    /// contexts have no alias, so fall back to globbing the WindowsApps package directory and
    /// picking the highest version (the same resolution strategy Winget-AutoUpdate uses at scale).
    /// </summary>
    private static string ResolveWinGetPath()
    {
        var aliasPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WindowsApps", "winget.exe");

        if (File.Exists(aliasPath))
        {
            return aliasPath;
        }

        var windowsApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "WindowsApps");

        if (Directory.Exists(windowsApps))
        {
            var candidate = Directory
                .EnumerateDirectories(windowsApps, "Microsoft.DesktopAppInstaller_*__8wekyb3d8bbwe")
                .Select(dir => Path.Combine(dir, "winget.exe"))
                .Where(File.Exists)
                .Select(path => (Path: path, Version: FileVersionInfo.GetVersionInfo(path).FileVersion))
                .OrderByDescending(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.Path)
                .FirstOrDefault();

            if (candidate is not null)
            {
                return candidate;
            }
        }

        throw new WinGetUnavailableException(
            "winget.exe was not found on this machine. Ensure WinGet (App Installer) is installed.");
    }
}
