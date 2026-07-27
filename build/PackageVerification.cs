#nullable enable
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Nuke.Common.IO;

static class PackageVerification
{
    const string Framework = "net8.0-windows10.0.26100";
    const string PackageId = "SubZeroDev.WinGet";
    const string NativeName = "Microsoft.Management.Deployment.dll";
    const string WinMdName = "Microsoft.Management.Deployment.winmd";
    const string UnresolvedArchitectureError =
        "SubZeroDev.WinGet could not select Microsoft.Management.Deployment.dll because the consumer architecture is unresolved.";
    const string UnsupportedArchitectureError =
        "SubZeroDev.WinGet does not support the architecture selected by PlatformTarget 'x86'.";
    const string UnsupportedRidError =
        "SubZeroDev.WinGet does not support the architecture selected by RuntimeIdentifier 'win-x86'.";

    public static void Run(
        AbsolutePath rootDirectory,
        AbsolutePath artifactsDirectory,
        string configuration)
    {
        var producedPackages = artifactsDirectory.GlobFiles("*.nupkg").ToArray();
        Require(producedPackages.Length == 1,
            $"PackageTest requires exactly one freshly produced .nupkg in {artifactsDirectory}; " +
            $"found {producedPackages.Length}: {string.Join(", ", producedPackages.Select(x => Path.GetFileName(x)))}");
        var package = producedPackages.Single();
        var identity = ReadPackageIdentity(package);

        var testRoot = artifactsDirectory / "package-test";
        testRoot.CreateOrCleanDirectory();
        var feed = testRoot / "feed";
        var packages = testRoot / "packages";
        feed.CreateDirectory();
        packages.CreateDirectory();
        File.Copy(package, feed / Path.GetFileName(package), overwrite: true);

        InspectPackage(rootDirectory, package, testRoot, identity);
        WriteNuGetConfig(testRoot, feed);

        foreach (var selection in new[] { X64Platform, Arm64Platform })
        {
            var direct = testRoot / $"platform-{selection.Name.ToLowerInvariant()}";
            WriteConsumer(direct, DirectReference(identity), platform: selection.Name);
            VerifySupportedConsumer(direct, selection, package, packages, testRoot, identity, configuration);
        }

        foreach (var selection in new[] { X64Rid, Arm64Rid })
        {
            var ridOnly = testRoot / $"rid-{selection.Rid}";
            WriteConsumer(ridOnly, DirectReference(identity), runtimeIdentifier: selection.Rid);
            VerifySupportedConsumer(ridOnly, selection, package, packages, testRoot, identity, configuration);
        }

        var precedence = testRoot / "rid-precedence";
        WriteConsumer(precedence, DirectReference(identity), platform: "x64", runtimeIdentifier: Arm64Rid.Rid);
        RunDotNet(precedence, packages, "restore", "-p:EnableWindowsTargeting=true");
        RunDotNet(precedence, packages, "msbuild", "-t:PackageTestAssertSelection",
            "-p:PackageTestExpectedArchitecture=arm64", "-p:EnableWindowsTargeting=true");

        var wrapper = testRoot / "wrapper";
        WriteWrapper(wrapper, identity);
        RunDotNet(wrapper, packages, "pack", "--configuration", configuration,
            "--output", feed, "-p:EnableWindowsTargeting=true");

        var transitive = testRoot / "transitive";
        WriteConsumer(transitive, WrapperReference(), platform: "x64");
        VerifySupportedConsumer(transitive, X64Platform, package, packages, testRoot, identity, configuration);

        VerifyExpectedFailure(testRoot / "anycpu", packages, identity,
            platform: "AnyCPU", runtimeIdentifier: null, UnresolvedArchitectureError);
        VerifyExpectedFailure(testRoot / "unsupported", packages, identity,
            platform: "x86", runtimeIdentifier: null, UnsupportedArchitectureError);
        VerifyExpectedFailure(testRoot / "unsupported-rid", packages, identity,
            platform: null, runtimeIdentifier: "win-x86", UnsupportedRidError);

        var compatible = testRoot / "direct-cominterop";
        WriteConsumer(compatible, DirectReference(identity) + Environment.NewLine + ComInteropReference(),
            platform: "x64");
        SetProjectProperty(compatible, "MicrosoftManagementDeployment-Platform", "x64");
        RunDotNet(compatible, packages, "restore", "-p:EnableWindowsTargeting=true");
        RunDotNet(compatible, packages, "build", "--no-restore", "--configuration", configuration,
            "-p:EnableWindowsTargeting=true");
        AssertMsBuildItems(compatible, packages, expectedArchitecture: null);
        AssertRootAssets(compatible / "bin" / configuration / Framework, Machine.Amd64,
            ExpectedNativeFromGlobalPackages(packages, "win-x64"));
        AssertSingleRootAsset(compatible / "bin" / configuration / Framework, NativeName);
        AssertSingleRootAsset(compatible / "bin" / configuration / Framework, WinMdName);
        (testRoot / "archive-extracts").DeleteDirectory();
        (testRoot / "selected-payloads").DeleteDirectory();
    }

    static PackageIdentity ReadPackageIdentity(AbsolutePath package)
    {
        using var archive = ZipFile.OpenRead(package);
        var nuspecEntry = archive.Entries.Single(x => x.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        var nuspec = XDocument.Load(nuspecEntry.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;
        var metadata = nuspec.Root.Element(ns + "metadata")!;
        var identity = new PackageIdentity(
            metadata.Element(ns + "id")!.Value,
            metadata.Element(ns + "version")!.Value);
        Require(identity.Id == PackageId, $"Packed package id is '{identity.Id}', expected '{PackageId}'.");
        return identity;
    }

    static void InspectPackage(
        AbsolutePath rootDirectory,
        AbsolutePath package,
        AbsolutePath testRoot,
        PackageIdentity identity)
    {
        using var archive = ZipFile.OpenRead(package);
        var entries = archive.Entries
            .Select(x => x.FullName.Replace('\\', '/'))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var required = new[]
        {
            "_rels/.rels",
            $"{PackageId}.nuspec",
            "README.md",
            $"lib/{Framework}/{PackageId}.dll",
            $"lib/{Framework}/{PackageId}.xml",
            $"buildTransitive/{Framework}/{PackageId}.targets",
            $"buildTransitive/{Framework}/native/win-x64/{NativeName}",
            $"buildTransitive/{Framework}/native/win-arm64/{NativeName}",
            $"buildTransitive/{Framework}/{WinMdName}",
            "THIRD-PARTY-NOTICES.txt",
            "[Content_Types].xml"
        };

        foreach (var expected in required)
            Require(entries.Count(x => x == expected) == 1,
                $"Package must contain exactly one '{expected}'.");
        Require(entries.Length == required.Length + 1 &&
                entries.Count(x => x.StartsWith("package/services/metadata/core-properties/",
                                         StringComparison.Ordinal) &&
                                   x.EndsWith(".psmdcp", StringComparison.Ordinal)) == 1,
            $"Package entries differ from the exact contract:{Environment.NewLine}" +
            string.Join(Environment.NewLine, entries));

        Require(!entries.Any(x => x.Contains("/native/win-x64/native/", StringComparison.Ordinal) ||
                                  x.Contains("/native/win-arm64/native/", StringComparison.Ordinal)),
            "Package contains a nested native directory.");
        Require(!entries.Any(x => x.EndsWith(NativeName, StringComparison.Ordinal) &&
                                  x != $"buildTransitive/{Framework}/native/win-x64/{NativeName}" &&
                                  x != $"buildTransitive/{Framework}/native/win-arm64/{NativeName}"),
            "Package contains a native DLL outside the two canonical locations.");
        Require(!entries.Any(x => x.EndsWith(WinMdName, StringComparison.Ordinal) &&
                                  x != $"buildTransitive/{Framework}/{WinMdName}"),
            "Package contains WinMD outside the canonical location.");

        var extracts = testRoot / "archive-extracts";
        extracts.CreateOrCleanDirectory();
        PeArchitecture.AssertAnyCpu(Extract(archive, $"lib/{Framework}/{PackageId}.dll",
            extracts / $"{PackageId}.dll"));
        AssertArchiveMachine(archive,
            $"buildTransitive/{Framework}/native/win-x64/{NativeName}", Machine.Amd64,
            extracts / $"win-x64-{NativeName}");
        AssertArchiveMachine(archive,
            $"buildTransitive/{Framework}/native/win-arm64/{NativeName}", Machine.Arm64,
            extracts / $"win-arm64-{NativeName}");

        var nuspec = XDocument.Load(archive.GetEntry($"{identity.Id}.nuspec")!.Open());
        XNamespace ns = nuspec.Root!.Name.Namespace;
        var dependency = nuspec.Descendants(ns + "dependency")
            .Single(x => (string?)x.Attribute("id") == "Microsoft.WindowsPackageManager.ComInterop");
        Require((string?)dependency.Attribute("version") == "1.29.280",
            "Nuspec has the wrong ComInterop dependency version.");
        var excluded = ((string?)dependency.Attribute("exclude") ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Require(excluded.ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(new[] { "Build", "Analyzers" }),
            "Nuspec ComInterop dependency must exclude exactly Build and Analyzers.");

        AssertArchiveHash(archive,
            $"buildTransitive/{Framework}/native/win-x64/{NativeName}",
            FindComInteropAsset(rootDirectory, "win-x64", NativeName),
            extracts / $"hash-win-x64-{NativeName}");
        AssertArchiveHash(archive,
            $"buildTransitive/{Framework}/native/win-arm64/{NativeName}",
            FindComInteropAsset(rootDirectory, "win-arm64", NativeName),
            extracts / $"hash-win-arm64-{NativeName}");
        AssertArchiveHash(archive,
            $"buildTransitive/{Framework}/{WinMdName}",
            FindComInteropAsset(rootDirectory, null, WinMdName),
            extracts / $"hash-{WinMdName}");
    }

    static void VerifySupportedConsumer(
        AbsolutePath project,
        ConsumerSelection selection,
        AbsolutePath package,
        AbsolutePath packages,
        AbsolutePath testRoot,
        PackageIdentity identity,
        string configuration)
    {
        RunDotNet(project, packages, "restore", "-p:EnableWindowsTargeting=true");
        AssertIsolatedRestoreAndImport(project, packages, identity);
        RunDotNet(project, packages, "build", "--no-restore", "--configuration", configuration,
            "-p:EnableWindowsTargeting=true");
        AssertMsBuildItems(project, packages,
            selection.PayloadRelativePath.Contains("win-arm64", StringComparison.Ordinal)
                ? "arm64"
                : "x64");
        var output = project / "bin" / configuration / Framework /
                     (selection.Rid is null ? "" : selection.Rid);
        var expectedNative = ExpectedNativeFromPackage(package, selection.PayloadRelativePath, testRoot);
        AssertRootAssets(output, selection.Machine, expectedNative);
        var native = output / NativeName;
        var firstTimestamp = File.GetLastWriteTimeUtc(native);
        var firstHash = Hash(native);

        RunDotNet(project, packages, "build", "--no-restore", "--configuration", configuration,
            "-p:EnableWindowsTargeting=true");
        Require(File.GetLastWriteTimeUtc(native) == firstTimestamp && Hash(native).SequenceEqual(firstHash),
            $"Incremental build rewrote or changed {native}.");
        File.Delete(native);
        RunDotNet(project, packages, "build", "--no-restore", "--configuration", configuration,
            "-p:EnableWindowsTargeting=true");
        AssertRootAssets(output, selection.Machine, expectedNative);

        var publish = project / "publish";
        RunDotNet(project, packages, "publish", "--no-restore", "--configuration", configuration,
            "--output", publish, "-p:EnableWindowsTargeting=true");
        AssertRootAssets(publish, selection.Machine, expectedNative);

        RunDotNet(project, packages, "clean", "--configuration", configuration,
            "-p:EnableWindowsTargeting=true");
        Require(!File.Exists(output / NativeName), $"Clean left '{output / NativeName}'.");
        Require(!File.Exists(output / WinMdName), $"Clean left '{output / WinMdName}'.");
    }

    static void AssertRootAssets(AbsolutePath directory, Machine machine, AbsolutePath expectedNative)
    {
        RequireFile(directory / PackageId + ".dll");
        PeArchitecture.AssertAnyCpu(directory / $"{PackageId}.dll");
        RequireFile(directory / NativeName);
        PeArchitecture.AssertMachine(directory / NativeName, machine);
        Require(Hash(directory / NativeName).SequenceEqual(Hash(expectedNative)),
            $"{directory / NativeName} does not match the selected packaged native asset.");
        RequireFile(directory / WinMdName);
        AssertSingleRootAsset(directory, NativeName);
        AssertSingleRootAsset(directory, WinMdName);
        Require(!Directory.EnumerateFiles(directory, NativeName, SearchOption.AllDirectories)
                .Any(x => Path.GetDirectoryName(x) != directory),
            $"{NativeName} was also copied into a nested output directory.");
    }

    static void AssertSingleRootAsset(AbsolutePath directory, string name)
    {
        Require(Directory.EnumerateFiles(directory, name, SearchOption.TopDirectoryOnly).Count() == 1,
            $"Expected exactly one root-level {name} in {directory}.");
    }

    static void AssertIsolatedRestoreAndImport(
        AbsolutePath project,
        AbsolutePath packages,
        PackageIdentity identity)
    {
        var generated = Directory.EnumerateFiles(project / "obj", "*.nuget.g.targets",
                SearchOption.AllDirectories)
            .Single();
        var expectedImport = Path.GetFullPath(packages / identity.Id.ToLowerInvariant() /
            identity.Version.ToLowerInvariant() / "buildTransitive" / Framework /
            $"{identity.Id}.targets");
        var document = XDocument.Load(generated);
        var imports = document.Descendants()
            .Where(x => x.Name.LocalName == "Import")
            .Select(x => (string?)x.Attribute("Project"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Replace("$(NuGetPackageRoot)",
                Path.GetFullPath(packages) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            .Select(Path.GetFullPath)
            .ToArray();
        Require(imports.Count(x => PathEquals(x, expectedImport)) == 1,
            $"{generated} must import exactly '{expectedImport}' once.{Environment.NewLine}" +
            string.Join(Environment.NewLine, imports));
        Require(Path.GetFullPath(generated).StartsWith(Path.GetFullPath(project), StringComparison.Ordinal),
            "Generated targets escaped the isolated fixture.");

        var assetsPath = Directory.EnumerateFiles(project / "obj", "project.assets.json",
            SearchOption.AllDirectories).Single();
        using var assets = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var packageFolders = assets.RootElement.GetProperty("packageFolders")
            .EnumerateObject().Select(x => Path.GetFullPath(x.Name)).ToArray();
        // packageFolders is the global packages folder followed by every configured fallback
        // folder, so extra entries here mean the fixture's NuGet.Config did not clear fallbacks
        // (see WriteNuGetConfig) rather than that the restore targeted the wrong folder.
        Require(packageFolders.Length == 1 &&
                PathEquals(packageFolders[0].TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(packages).TrimEnd(Path.DirectorySeparatorChar)),
            $"Restore did not exclusively use isolated NUGET_PACKAGES '{packages}'. Found: " +
            string.Join(", ", packageFolders) +
            $".{Environment.NewLine}Entries beyond the first are NuGet fallback folders; the " +
            "fixture NuGet.Config is expected to clear them.");
    }

    static void VerifyExpectedFailure(
        AbsolutePath project,
        AbsolutePath packages,
        PackageIdentity identity,
        string? platform,
        string? runtimeIdentifier,
        string expected)
    {
        WriteConsumer(project, DirectReference(identity), platform, runtimeIdentifier);
        RunDotNet(project, packages, "restore", "-p:EnableWindowsTargeting=true");
        var result = RunDotNet(project, packages, expectSuccess: false, "build", "--no-restore",
            "--configuration", "Release", "-p:EnableWindowsTargeting=true");
        var selection = runtimeIdentifier ?? platform ?? "(unresolved)";
        Require(result.ExitCode != 0, $"Architecture '{selection}' unexpectedly built successfully.");
        Require(result.Output.Contains(expected, StringComparison.Ordinal),
            $"Architecture '{selection}' did not emit the actionable error.{Environment.NewLine}{result.Output}");
    }

    static void WriteConsumer(
        AbsolutePath directory,
        string references,
        string? platform = null,
        string? runtimeIdentifier = null)
    {
        directory.CreateDirectory();
        var platformProperty = platform is null ? "" : $"<PlatformTarget>{platform}</PlatformTarget>";
        var ridProperty = runtimeIdentifier is null
            ? ""
            : $"<RuntimeIdentifier>{runtimeIdentifier}</RuntimeIdentifier>";
        File.WriteAllText(directory / "Consumer.csproj", $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>{{Framework}}</TargetFramework>
                {{platformProperty}}
                {{ridProperty}}
              </PropertyGroup>
              <ItemGroup>
                {{references}}
              </ItemGroup>
              <Target Name="PackageTestAssertSelection"
                      DependsOnTargets="SubZeroDevWinGetValidateConsumerArchitecture">
                <ItemGroup>
                  <_PackageTestNative Include="@(ReferenceCopyLocalPaths)"
                                      Condition="'%(Filename)%(Extension)' == 'Microsoft.Management.Deployment.dll'" />
                  <_PackageTestWinMd Include="@(WindowsMetadataReference)"
                                     Condition="'%(Filename)%(Extension)' == 'Microsoft.Management.Deployment.winmd'" />
                </ItemGroup>
                <Error Condition="'$(PackageTestExpectedArchitecture)' != '' And '$(SubZeroDevWinGetArchitecture)' != '$(PackageTestExpectedArchitecture)'"
                       Text="Package selected '$(SubZeroDevWinGetArchitecture)', expected '$(PackageTestExpectedArchitecture)'." />
                <Error Condition="'@(_PackageTestNative->Count())' != '1'"
                       Text="Expected exactly one Microsoft.Management.Deployment.dll ReferenceCopyLocalPaths item; found @(_PackageTestNative->Count())." />
                <Error Condition="'@(_PackageTestWinMd->Count())' != '1'"
                       Text="Expected exactly one Microsoft.Management.Deployment.winmd WindowsMetadataReference item; found @(_PackageTestWinMd->Count())." />
              </Target>
            </Project>
            """);
        File.WriteAllText(directory / "Program.cs",
            "using SubZeroDev.WinGet;\nSystem.Console.WriteLine(typeof(PackageManagementService).FullName);\n");
    }

    static void SetProjectProperty(AbsolutePath directory, string name, string value)
    {
        var project = directory / "Consumer.csproj";
        var document = XDocument.Load(project);
        var property = document.Descendants(name).SingleOrDefault();
        if (property is null)
        {
            var group = document.Root!.Elements("PropertyGroup").First();
            group.Add(new XElement(name, value));
        }
        else
        {
            property.Value = value;
        }
        document.Save(project);
    }

    static void AssertMsBuildItems(
        AbsolutePath project,
        AbsolutePath packages,
        string? expectedArchitecture)
    {
        var arguments = new List<string>
        {
            "msbuild",
            "-t:PackageTestAssertSelection",
            "-p:EnableWindowsTargeting=true"
        };
        if (expectedArchitecture is not null)
            arguments.Add($"-p:PackageTestExpectedArchitecture={expectedArchitecture}");
        RunDotNet(project, packages, arguments.ToArray());
    }

    static void WriteWrapper(AbsolutePath directory, PackageIdentity identity)
    {
        directory.CreateDirectory();
        File.WriteAllText(directory / "Wrapper.csproj", $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{{Framework}}</TargetFramework>
                <!-- The wrapper itself is a supported package consumer while it is packed. -->
                <PlatformTarget>x64</PlatformTarget>
                <PackageId>PackageTest.Wrapper</PackageId>
                <Version>1.0.0</Version>
              </PropertyGroup>
              <ItemGroup>
                {{DirectReference(identity)}}
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(directory / "Wrapper.cs", "namespace PackageTest; public sealed class Wrapper { }\n");
    }

    static void WriteNuGetConfig(AbsolutePath directory, AbsolutePath feed)
    {
        File.WriteAllText(directory / "NuGet.Config", $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{{feed}}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
              <!--
                A <clear /> under packageSources does NOT clear fallback folders — they are a
                separate section, and outer config can register them. Visual Studio does exactly
                that on GitHub's windows-latest image
                ("C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages"), and NuGet
                records every fallback folder in project.assets.json's packageFolders, which is
                what AssertIsolatedRestoreAndImport inspects.

                This <clear /> covers config-registered fallbacks only; a fallback supplied via
                the NUGET_FALLBACK_PACKAGES environment variable is NOT affected by it (verified
                empirically). Run() drops that variable from the child environment to cover the
                other half.
              -->
              <fallbackPackageFolders>
                <clear />
              </fallbackPackageFolders>
              <packageSourceMapping>
                <packageSource key="local">
                  <package pattern="SubZeroDev.WinGet" />
                  <package pattern="PackageTest.Wrapper" />
                </packageSource>
                <packageSource key="nuget.org">
                  <package pattern="*" />
                </packageSource>
              </packageSourceMapping>
            </configuration>
            """);
    }

    static string DirectReference(PackageIdentity identity) =>
        $"<PackageReference Include=\"{identity.Id}\" Version=\"{identity.Version}\" />";

    static string WrapperReference() =>
        "<PackageReference Include=\"PackageTest.Wrapper\" Version=\"1.0.0\" />";

    static string ComInteropReference() =>
        "<PackageReference Include=\"Microsoft.WindowsPackageManager.ComInterop\" Version=\"1.29.280\" />";

    static AbsolutePath ExpectedNativeFromPackage(
        AbsolutePath package,
        string payloadRelativePath,
        AbsolutePath testRoot)
    {
        using var archive = ZipFile.OpenRead(package);
        return Extract(archive,
            payloadRelativePath,
            testRoot / "selected-payloads" /
            payloadRelativePath.Replace('/', '-'));
    }

    static AbsolutePath ExpectedNativeFromGlobalPackages(AbsolutePath packages, string architecture)
    {
        var path = packages / "microsoft.windowspackagemanager.cominterop" / "1.29.280" /
                   "bin" / architecture / "native" / "static" / NativeName;
        RequireFile(path);
        return path;
    }

    static AbsolutePath FindComInteropAsset(
        AbsolutePath rootDirectory,
        string? architecture,
        string file)
    {
        var packageRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (string.IsNullOrWhiteSpace(packageRoot))
            packageRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var basePath = (AbsolutePath)packageRoot /
                       "microsoft.windowspackagemanager.cominterop" / "1.29.280";
        var path = architecture is null
            ? basePath / "lib" / "uap10.0" / file
            : basePath / "bin" / architecture / "native" / "static" / file;
        RequireFile(path);
        return path;
    }

    static void AssertArchiveMachine(
        ZipArchive archive,
        string entry,
        Machine machine,
        AbsolutePath destination)
    {
        PeArchitecture.AssertMachine(Extract(archive, entry, destination), machine);
    }

    static void AssertArchiveHash(
        ZipArchive archive,
        string entry,
        AbsolutePath expected,
        AbsolutePath destination)
    {
        var extracted = Extract(archive, entry, destination);
        Require(Hash(extracted).SequenceEqual(Hash(expected)),
            $"Packaged '{entry}' does not match '{expected}'.");
    }

    static AbsolutePath Extract(ZipArchive archive, string entry, AbsolutePath? destination = null)
    {
        Require(destination is not null,
            $"Archive extraction for '{entry}' must stay inside the scoped package-test directory.");
        var extractionPath = destination!;
        Directory.CreateDirectory(Path.GetDirectoryName(extractionPath)!);
        archive.GetEntry(entry)?.ExtractToFile(extractionPath, overwrite: true);
        RequireFile(extractionPath);
        return extractionPath;
    }

    static byte[] Hash(AbsolutePath path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    static ProcessResult RunDotNet(
        AbsolutePath workingDirectory,
        AbsolutePath packages,
        params string[] arguments) =>
        RunDotNet(workingDirectory, packages, expectSuccess: true, arguments);

    static ProcessResult RunDotNet(
        AbsolutePath workingDirectory,
        AbsolutePath packages,
        bool expectSuccess,
        params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment["NUGET_PACKAGES"] = packages;
        // The fixture's NuGet.Config clears config-registered fallback folders, but a fallback
        // set through this variable is immune to that <clear /> and would still land in
        // project.assets.json's packageFolders, breaking the isolation assertion on any machine
        // that happens to set it. Dropping it here closes that gap.
        start.Environment.Remove("NUGET_FALLBACK_PACKAGES");
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start dotnet.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
        Console.WriteLine($"dotnet {string.Join(' ', arguments)}");
        Console.WriteLine(output);
        if (expectSuccess && process.ExitCode != 0)
            throw new InvalidOperationException($"dotnet failed ({process.ExitCode}).{Environment.NewLine}{output}");
        return new ProcessResult(process.ExitCode, output);
    }

    static void RequireFile(AbsolutePath path) =>
        Require(File.Exists(path), $"Required file does not exist: {path}");

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    sealed record ProcessResult(int ExitCode, string Output);

    sealed record PackageIdentity(string Id, string Version);

    sealed record ConsumerSelection(
        string Name,
        string? Rid,
        Machine Machine,
        string PayloadRelativePath);

    static readonly ConsumerSelection X64Platform = new(
        "x64",
        null,
        Machine.Amd64,
        $"buildTransitive/{Framework}/native/win-x64/{NativeName}");

    static readonly ConsumerSelection Arm64Platform = new(
        "ARM64",
        null,
        Machine.Arm64,
        $"buildTransitive/{Framework}/native/win-arm64/{NativeName}");

    static readonly ConsumerSelection X64Rid = new(
        "win-x64",
        "win-x64",
        Machine.Amd64,
        $"buildTransitive/{Framework}/native/win-x64/{NativeName}");

    static readonly ConsumerSelection Arm64Rid = new(
        "win-arm64",
        "win-arm64",
        Machine.Arm64,
        $"buildTransitive/{Framework}/native/win-arm64/{NativeName}");

    static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
