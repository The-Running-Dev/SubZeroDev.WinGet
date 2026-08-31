using System.Reflection;

using FluentAssertions;

using SubZeroDev.WinGet.Abstractions;

namespace SubZeroDev.WinGet.Tests;

/// <summary>
/// Regression checks for the C13/S6 mapper boundary: no CsWinRT-projected collection is walked
/// with foreach/LINQ, the mapper has no dependency path to the activation layer, and moving the
/// eight translations into <see cref="WinGetProjectionMapper"/> did not change either client's
/// public surface.
/// </summary>
[TestFixture]
public class WinGetProjectionMapperArchitectureTests
{
    private static readonly string MapperSourcePath = FindSourceFile("WinGetProjectionMapper.cs");

    private static readonly string[] ForbiddenTraversalTokens =
    [
        "foreach ",
        "foreach(",
        ".Select(",
        ".Where(",
        ".ToList(",
        ".ToArray(",
        ".Any(",
        ".All(",
        ".First(",
        ".FirstOrDefault(",
        ".Count(",
        ".Aggregate("
    ];

    private static readonly string[] ForbiddenActivationTypeNames =
    [
        "WinGetFactory",
        "WinGetComContext",
        "WinGetCliClient"
    ];

    [Test]
    public void Source_ContainsNoForeachOrLinqTraversal()
    {
        var source = File.ReadAllText(MapperSourcePath);

        foreach (var token in ForbiddenTraversalTokens)
        {
            source.Should().NotContain(token,
                $"every CsWinRT-projected collection in the mapper must be traversed by index, not '{token.Trim()}'");
        }
    }

    [Test]
    public void Source_HasNoDependencyPathToTheActivationLayerOrTheCliShim()
    {
        var source = File.ReadAllText(MapperSourcePath);

        foreach (var typeName in ForbiddenActivationTypeNames)
        {
            source.Should().NotContain(typeName,
                $"the mapper is a pure translation boundary and must not reference '{typeName}'");
        }
    }

    [Test]
    public void MapperClass_IsInternalStatic()
    {
        var type = typeof(WinGetClient).Assembly.GetType("SubZeroDev.WinGet.WinGetProjectionMapper");

        type.Should().NotBeNull();
        type!.IsPublic.Should().BeFalse("the mapper must stay internal");
        type.IsAbstract.Should().BeTrue("static classes are abstract+sealed in metadata");
        type.IsSealed.Should().BeTrue("static classes are abstract+sealed in metadata");
    }

    [Test]
    public void WinGetClient_PublicSurfaceIsExactlyItsInterfaceAndDispose() =>
        GetDeclaredPublicMethodNames(typeof(WinGetClient))
            .Should().BeEquivalentTo(GetExpectedPublicMethodNames(typeof(IWinGetClient)));

    [Test]
    public void WinGetSourceClient_PublicSurfaceIsExactlyItsInterfaceAndDispose() =>
        GetDeclaredPublicMethodNames(typeof(WinGetSourceClient))
            .Should().BeEquivalentTo(GetExpectedPublicMethodNames(typeof(IWinGetSourceClient)));

    private static IEnumerable<string> GetDeclaredPublicMethodNames(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name);

    private static IEnumerable<string> GetExpectedPublicMethodNames(Type contractInterface) =>
        contractInterface.GetMethods().Select(method => method.Name).Append(nameof(IDisposable.Dispose));

    private static string FindSourceFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SubZeroDev.WinGet.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
        }

        return Path.Combine(directory.FullName, "SubZeroDev.WinGet", fileName);
    }
}
