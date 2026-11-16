using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MarstekOverlay.Desktop.Services;

/// <summary>The one copy of the app that runs on a data folder. Copies on the same folder would share its
/// history file, overwrite each other's settings and together ask the station more often than it answers.
/// The claim is a named event: whoever created it is the copy that runs, and a later copy sets it to have
/// that one show its panel.</summary>
public sealed class SingleInstance : IDisposable
{
    // Closing the history file and the log takes well under a second when nothing is wrong.
    private static readonly TimeSpan LongestExit = TimeSpan.FromSeconds(5);

    private readonly EventWaitHandle _shown;
    private readonly string _exitingName;

    private RegisteredWaitHandle? _waiting;
    private EventWaitHandle? _exiting;

    private SingleInstance(EventWaitHandle shown, string exitingName)
    {
        _shown = shown;
        _exitingName = exitingName;
    }

    /// <summary>Claims the data folder for this copy of the app. A copy that is closing on the folder is waited
    /// for, as it would no longer show its panel.</summary>
    /// <returns>Null when another copy already runs on the folder; that copy has been asked to show its
    /// panel.</returns>
    public static SingleInstance? Claim(string rootDirectory) => Claim(rootDirectory, LongestExit);

    /// <param name="longestExit">How long a copy that is closing is waited for before it is taken to be
    /// running after all.</param>
    internal static SingleInstance? Claim(string rootDirectory, TimeSpan longestExit)
    {
        var name = NameFor(rootDirectory);
        var exitingName = ExitingNameFor(name);

        // Looked for before the claim: once that copy has let go of the claim, it no longer has this either.
        if (EventWaitHandle.TryOpenExisting(exitingName, out var exiting))
        {
            using (exiting)
                exiting.WaitOne(longestExit);
        }

        var shown = new EventWaitHandle(false, EventResetMode.AutoReset, name, out var isFirst);
        if (isFirst)
            return new SingleInstance(shown, exitingName);

        using (shown)
            shown.Set();

        return null;
    }

    /// <summary>The name of the event for a data folder. One folder written differently gets the same name;
    /// the folder itself cannot be the name, which may not hold a backslash. Local to the session, since the
    /// panel a later copy asks for is on the desktop of that session.</summary>
    public static string NameFor(string rootDirectory)
    {
        var folder = Path.GetFullPath(rootDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();

        return $@"Local\MarstekOverlay-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder)))}";
    }

    /// <summary>Has <paramref name="show"/> run in <paramref name="context"/> each time a later copy of the
    /// app asks for the panel.</summary>
    public void WhenAskedToShow(SynchronizationContext context, Action show)
    {
        _waiting?.Unregister(null);
        _waiting = ThreadPool.RegisterWaitForSingleObject(
            _shown,
            (_, _) => context.Post(_ => show(), null),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Tells a copy started from now on that this one is closing: it waits for the claim to be let go
    /// instead of asking this copy for its panel.</summary>
    public void AnnounceExit()
    {
        _waiting?.Unregister(null);
        _waiting = null;
        _exiting ??= new EventWaitHandle(false, EventResetMode.ManualReset, _exitingName);
    }

    public void Dispose()
    {
        _waiting?.Unregister(null);
        _shown.Dispose();

        // Only after the claim is let go, so that a copy waiting for it finds the folder free.
        _exiting?.Set();
        _exiting?.Dispose();
        _exiting = null;
    }

    private static string ExitingNameFor(string name) => $"{name}-Exiting";
}
