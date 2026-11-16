using System.IO;

namespace MarstekOverlay.Desktop.Settings;

/// <summary>The folder the exe lies in: where <c>appsettings.json</c> is looked for and relative paths start.
/// Not always <see cref="AppContext.BaseDirectory"/>: an exe published as one file that unpacks itself has its
/// base directory in the temporary folder it unpacked into.</summary>
public static class ExeFolder
{
    public static string Location { get; } =
        Path.GetDirectoryName(Environment.ProcessPath) is { Length: > 0 } folder ? folder : AppContext.BaseDirectory;
}
