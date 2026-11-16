using Microsoft.Extensions.Logging;

namespace MarstekOverlay.TestSupport;

public sealed record LoggedEntry(LogLevel Level, string Message, Exception? Exception);

public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LoggedEntry> _entries = [];

    public IReadOnlyList<LoggedEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Add(new LoggedEntry(logLevel, formatter(state, exception), exception));
}
