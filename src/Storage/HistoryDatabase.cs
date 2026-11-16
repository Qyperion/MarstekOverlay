using LiteDB;

using Microsoft.Extensions.Logging;

namespace MarstekOverlay.Storage;

/// <summary>The one file everything recorded about the station goes to, a collection per kind of record.
/// When the file cannot be opened the records are kept in memory instead, so the app still runs; they are
/// then gone when it exits.</summary>
public sealed class HistoryDatabase : IDisposable
{
    private readonly LiteDatabase _database;

    public HistoryDatabase(string path, ILogger<HistoryDatabase> logger)
    {
        _database = Open(path, logger);

        // Dates come back as local time otherwise, and a record would shift when the time zone changes.
        Try(
            () => _database.Pragma("UTC_DATE", true),
            exception => logger.LogError(exception, "History in {Path} cannot keep its dates in UTC", path));
    }

    public void Dispose() => _database.Dispose();

    internal ILiteCollection<T> Collection<T>(string name) => _database.GetCollection<T>(name);

    /// <summary>Runs <paramref name="operation"/> on the file so that a failure of the file is reported and
    /// answered with <paramref name="fallback"/> rather than thrown: the stores keep the app going whatever
    /// happens to the file.</summary>
    internal T Try<T>(Func<T> operation, T fallback, Action<Exception> report)
    {
        try
        {
            return operation();
        }
        catch (Exception exception) when (IsFailureOfTheFile(exception))
        {
            report(exception);
            return fallback;
        }
    }

    /// <inheritdoc cref="Try{T}"/>
    internal void Try(Action operation, Action<Exception> report)
    {
        try
        {
            operation();
        }
        catch (Exception exception) when (IsFailureOfTheFile(exception))
        {
            report(exception);
        }
    }

    // A closed database answers a write with LiteException but a query with ObjectDisposedException, which is an
    // InvalidOperationException.
    private static bool IsFailureOfTheFile(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or LiteException or InvalidOperationException;

    private static LiteDatabase Open(string path, ILogger logger)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            return new LiteDatabase(new ConnectionString { Filename = path }, new BsonMapper());
        }
        catch (Exception exception) when (IsFailureOfTheFile(exception))
        {
            // Another program holding the file is the usual reason; a second copy of the app on the same data
            // folder does not get this far.
            logger.LogError(exception, "History in {Path} cannot be opened; this run keeps it in memory only", path);
            return new LiteDatabase(new MemoryStream(), new BsonMapper());
        }
    }
}
