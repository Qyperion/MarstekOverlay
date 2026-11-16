using Microsoft.Extensions.Logging;
using Microsoft.Win32;

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security;

namespace MarstekOverlay.Desktop.Services;

/// <summary>The entry that has Windows launch the app at sign-in. The registry is the only place it is kept:
/// a copy in the settings file could disagree with what Windows is actually going to do.</summary>
public sealed class Autostart : IAutostart
{
    // tools/Publish/Publish-App.ps1 writes the same entry, under the same name and in the same form.
    private const string WindowsKeyPath = @"Software\Microsoft\Windows\CurrentVersion";
    private const string RunKeyName = "Run";

    // Where Task Manager marks an entry it was told not to launch; the entry itself stays in Run.
    private const string ApprovalKeyName = @"Explorer\StartupApproved\Run";

    private const string EntryName = "MarstekOverlay";

    private readonly string _runKeyPath;
    private readonly string _approvalKeyPath;
    private readonly ILogger<Autostart> _logger;

    public Autostart(ILogger<Autostart> logger)
        : this(WindowsKeyPath, logger)
    {
    }

    /// <param name="keyPath">The key of the current user that holds <c>Run</c> and
    /// <c>Explorer\StartupApproved\Run</c>, as Windows keeps them under its own.</param>
    internal Autostart(string keyPath, ILogger<Autostart> logger)
    {
        _runKeyPath = $@"{keyPath}\{RunKeyName}";
        _approvalKeyPath = $@"{keyPath}\{ApprovalKeyName}";
        _logger = logger;
    }

    /// <summary>True only while the entry launches this very copy of the app, not one left behind in another
    /// folder, and Task Manager has not disabled it.</summary>
    public bool IsEnabled
    {
        get
        {
            try
            {
                using var run = Registry.CurrentUser.OpenSubKey(_runKeyPath);
                using var approval = Registry.CurrentUser.OpenSubKey(_approvalKeyPath);

                return run?.GetValue(EntryName) is string command
                    && command.Equals(Command, StringComparison.OrdinalIgnoreCase)
                    && !IsDisabledInTaskManager(approval?.GetValue(EntryName) as byte[]);
            }
            catch (Exception exception) when (IsRegistryFailure(exception))
            {
                _logger.LogWarning(exception, "The startup entry could not be read");
                return false;
            }
        }
    }

    private static string Command => $"\"{Environment.ProcessPath}\"";

    /// <param name="approval">What Task Manager keeps for the entry, if anything. As seen on Windows 11: the
    /// first byte is 2 for an enabled entry and 3 for a disabled one, followed by when it was disabled; the
    /// lowest bit is what tells them apart.</param>
    public static bool IsDisabledInTaskManager(byte[]? approval) => approval is [var state, ..] && (state & 1) == 1;

    public bool TrySet(bool isEnabled, [NotNullWhen(false)] out Exception? failure)
    {
        try
        {
            using var run = Registry.CurrentUser.CreateSubKey(_runKeyPath);
            if (isEnabled)
                run.SetValue(EntryName, Command);
            else
                run.DeleteValue(EntryName, throwOnMissingValue: false);

            // A mark left by Task Manager would keep the entry from launching however often it is written anew.
            using var approval = Registry.CurrentUser.OpenSubKey(_approvalKeyPath, writable: true);
            approval?.DeleteValue(EntryName, throwOnMissingValue: false);

            failure = null;
            return true;
        }
        catch (Exception exception) when (IsRegistryFailure(exception))
        {
            _logger.LogWarning(exception, "The startup entry was not changed");
            failure = exception;
            return false;
        }
    }

    private static bool IsRegistryFailure(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException or IOException;
}
