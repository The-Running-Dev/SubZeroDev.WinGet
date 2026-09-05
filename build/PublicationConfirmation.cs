#nullable enable
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Xml.Linq;
using Nuke.Common.IO;

// S11/C21: a publish target succeeds only after the exact intended version is positively
// observed back from the feed it just pushed to - a push exit code, or --skip-duplicate
// reporting nothing to do, is not evidence the version is actually there. The comparison
// itself (Evaluate/Assert) is kept as a small directly-testable unit, mirroring CoverageGate;
// querying a live feed over HTTP is not unit tested, for the same reason C26 does not put a
// unit test on anything that can only be exercised against a real external system.
static class PublicationConfirmation
{
    public readonly record struct Result(
        bool Confirmed,
        string Destination,
        string Tag,
        string Commit,
        string IntendedVersion,
        string? ObservedVersion,
        string RunUrl);

    // Exact match only (C21): a differently-cased or prefixed version does not confirm the
    // intended one. Pure comparison over already-observed data - no push and no fetch happens
    // in here, which is what makes re-evaluating the same inputs safe to repeat (S11.6).
    public static Result Evaluate(
        string destination,
        string tag,
        string commit,
        string runUrl,
        string intendedVersion,
        IReadOnlyCollection<string> observedVersions)
    {
        var observed = observedVersions.FirstOrDefault(
            v => string.Equals(v, intendedVersion, StringComparison.Ordinal));
        return new Result(observed is not null, destination, tag, commit, intendedVersion, observed, runUrl);
    }

    public static void Assert(Result result)
    {
        if (!result.Confirmed)
        {
            throw new InvalidOperationException(
                $"{result.Destination} does not show {result.IntendedVersion} for tag/ref " +
                $"'{result.Tag}' at commit {result.Commit} (run {result.RunUrl}). " +
                $"Observed version: {result.ObservedVersion ?? "(none)"}.");
        }
    }

    // S11.3: names the tag/ref, commit, destination, intended and observed version, and run
    // identity. Result never carries a token or API key, so there is nothing here that could
    // expose one. Only a genuinely confirmed result is reported as "Publication confirmed:" -
    // that exact prefix is what the workflow greps into the job summary (build.yml), so an
    // unconfirmed result must never produce that line. Assert already carries the observed/
    // intended mismatch into the thrown exception for the unconfirmed case.
    public static void Report(Result result)
    {
        if (!result.Confirmed)
        {
            return;
        }

        Console.WriteLine(
            $"Publication confirmed: {result.Destination} {result.IntendedVersion} " +
            $"(tag/ref {result.Tag}, commit {result.Commit}, run {result.RunUrl}).");
    }

    // Reads the version a `dotnet pack` run actually produced, rather than trusting a second,
    // independently-typed copy of it - the nupkg's own nuspec is the one place that fact is
    // already written down.
    public static string ReadPackedVersion(AbsolutePath package)
    {
        using var archive = ZipFile.OpenRead(package);
        var nuspecEntry = archive.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        var nuspec = XDocument.Load(nuspecEntry.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;
        return nuspec.Root.Element(ns + "metadata")!.Element(ns + "version")!.Value;
    }

    sealed record FlatContainerIndex(string[] Versions);

    // S11.1: NuGet.org's flat-container package-base-address resource lists every version ever
    // pushed for an id; it is public and needs no authentication.
    public static async Task<IReadOnlyCollection<string>> FetchNuGetOrgVersions(
        HttpClient client, string packageId, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.nuget.org/v3-flatcontainer/{packageId.ToLowerInvariant()}/index.json";
        using var response = await client.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<string>();
        }

        response.EnsureSuccessStatusCode();
        var index = await response.Content.ReadFromJsonAsync<FlatContainerIndex>(cancellationToken);
        return index?.Versions ?? Array.Empty<string>();
    }

    sealed record GitHubPackageVersion(string Name);

    // S11.2: GitHub Packages' NuGet feed does not expose flat-container version listing, so
    // this reads the version list from the GitHub REST API instead, using the same token the
    // push already required. `owner` names either an org or a user and the caller does not
    // know which - this repository's own owner is an org, so that route is tried first, and a
    // 404 there falls back to the user route.
    public static async Task<IReadOnlyCollection<string>> FetchGitHubPackagesVersions(
        HttpClient client, string owner, string packageId, string token, CancellationToken cancellationToken = default)
    {
        async Task<IReadOnlyCollection<string>?> TryList(string ownerKind)
        {
            var url = $"https://api.github.com/{ownerKind}/{Uri.EscapeDataString(owner)}" +
                      $"/packages/nuget/{Uri.EscapeDataString(packageId)}/versions";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("SubZeroDev.WinGet-build", "1.0"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var versions = await response.Content.ReadFromJsonAsync<GitHubPackageVersion[]>(cancellationToken);
            return versions?.Select(v => v.Name).ToArray() ?? Array.Empty<string>();
        }

        return await TryList("orgs") ?? await TryList("users") ??
            throw new InvalidOperationException(
                $"GitHub Packages has no '{packageId}' package under owner '{owner}' as either an org or a user.");
    }

    // S11.6: a fresh push is not guaranteed to be immediately queryable back, so this polls a
    // bounded number of times rather than failing on the first miss or requiring a human to
    // notice a false negative and re-run confirmation later. The fetch itself is a read-only
    // GET, so calling this again for the same intended version is always safe.
    public static async Task<Result> Confirm(
        string destination,
        string tag,
        string commit,
        string runUrl,
        string intendedVersion,
        Func<CancellationToken, Task<IReadOnlyCollection<string>>> fetchVersions,
        TimeSpan? pollInterval = null,
        int maxAttempts = 5,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(15);
        var result = new Result(false, destination, tag, commit, intendedVersion, null, runUrl);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var observed = await fetchVersions(cancellationToken);
            result = Evaluate(destination, tag, commit, runUrl, intendedVersion, observed);
            if (result.Confirmed || attempt == maxAttempts)
            {
                break;
            }

            await Task.Delay(interval, cancellationToken);
        }

        Report(result);
        return result;
    }
}
