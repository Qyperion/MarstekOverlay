using MarstekOverlay.Desktop.Localization;

using System.IO;
using System.Security;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Why a file or the registry was left as it was, in the language of the app. The message of the
/// exception is in the language of Windows, so it goes to the log only.</summary>
public static class FailureReasons
{
    public static string OfFile(Exception exception) => exception switch
    {
        UnauthorizedAccessException or SecurityException => Strings.Failure_NoAccess,
        IOException => Strings.Failure_FileUnavailable,
        _ => Strings.Failure_Unexpected,
    };

    // An IOException of the registry is a key deleted under the app, not a file in use.
    public static string OfRegistry(Exception exception) => exception switch
    {
        UnauthorizedAccessException or SecurityException => Strings.Failure_NoAccess,
        _ => Strings.Failure_Unexpected,
    };
}
