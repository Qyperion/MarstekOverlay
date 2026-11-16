using MarstekOverlay.Desktop.Settings;

using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Core;
using Serilog.Events;

using System.Globalization;
using System.IO;

namespace MarstekOverlay.Desktop.Services;

/// <summary>The log file and the ones it has rolled over into. The writer keeps its file open, so it is owned
/// here and lets go of the files for as long as it takes to delete entries from them. That takes seconds for
/// a full log, and entries written meanwhile, from the UI thread too, are held back rather than kept waiting.</summary>
public sealed class DiagnosticLog : ILogEventSink, IDisposable
{
    private readonly Lock _gate = new();
    private readonly string _path;
    private readonly bool _isEnabled;

    private Logger? _writer;
    private List<LogEvent>? _heldBack;
    private bool _isDisposed;

    public DiagnosticLog(StorageSettings storage, IOptions<LoggerOptions> options)
    {
        _path = storage.Resolve(options.Value.LogFilePath);
        _isEnabled = options.Value.IsEnabledFileLog;
        _writer = Open();
    }

    /// <summary>Where the log is written; null when it is switched off.</summary>
    public string? FilePath => _isEnabled ? _path : null;

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            if (_heldBack is { } heldBack)
                heldBack.Add(logEvent);
            else
                _writer?.Write(logEvent);
        }
    }

    /// <summary>Deletes the entries written before <paramref name="moment"/> from every file of the log; a file
    /// left with none is deleted itself. A file that cannot be changed is left as it was, and the others are
    /// cleared all the same. One deletion at a time; it may run on any thread.</summary>
    public LogCleanup DeleteBefore(DateTimeOffset moment)
    {
        lock (_gate)
        {
            if (_heldBack is not null)
                throw new InvalidOperationException("The log is being cleared already.");

            _writer?.Dispose();
            _writer = null;
            _heldBack = [];
        }

        try
        {
            return DeleteFromFiles(moment);
        }
        finally
        {
            lock (_gate)
            {
                var heldBack = _heldBack ?? [];
                _heldBack = null;

                if (!_isDisposed)
                {
                    _writer = Open();
                    foreach (var logEvent in heldBack)
                        _writer?.Write(logEvent);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _isDisposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    // The levels are sifted by the logger this is a sink of.
    private Logger? Open() => _isEnabled
        ? new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                _path,
                outputTemplate: LogConstants.OutputTemplate,
                formatProvider: CultureInfo.InvariantCulture,
                fileSizeLimitBytes: LogConstants.FileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: LogConstants.RetainedFileCountLimit)
            .CreateLogger()
        : null;

    private LogCleanup DeleteFromFiles(DateTimeOffset moment)
    {
        var cleared = 0;
        Exception? failure = null;

        try
        {
            foreach (var file in LogFiles.Find(_path))
            {
                try
                {
                    LogFiles.DeleteBefore(file, moment);
                    cleared++;
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    failure ??= exception;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure ??= exception;
        }

        return new LogCleanup(failure, IsPartial: failure is not null && cleared > 0);
    }
}
