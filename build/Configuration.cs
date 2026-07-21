using System.ComponentModel;

using Nuke.Common.Tooling;

/// <summary>
/// Build configuration (Debug/Release) as a Nuke <see cref="Enumeration"/>, so it can be bound
/// from the <c>--configuration</c> parameter and implicitly converted to the string the dotnet
/// CLI expects. This is the standard type Nuke's own templates generate alongside Build.cs;
/// Build.cs references it, so the build project does not compile without it.
/// </summary>
[TypeConverter(typeof(TypeConverter<Configuration>))]
public class Configuration : Enumeration
{
    public static Configuration Debug = new() { Value = nameof(Debug) };

    public static Configuration Release = new() { Value = nameof(Release) };

    public static implicit operator string(Configuration configuration) => configuration.Value;
}
