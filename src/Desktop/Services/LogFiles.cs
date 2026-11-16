using MarstekOverlay.Desktop.Settings;

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace MarstekOverlay.Desktop.Services;

/// <summary>The files of a log as they lie on disk, and the entries in them.</summary>
internal static class LogFiles
{
    private const string ReplacementExtension = ".tmp";

    /// <summary>The file at <paramref name="path"/> and the ones it has rolled over into: a file that reached its
    /// size limit is followed by one with "_001" and so on added to the name.</summary>
    public static List<string> Find(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
            return [];

        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var isLogFile = new Regex($@"^{Regex.Escape(name)}(_\d{{3,}})?{Regex.Escape(extension)}$", RegexOptions.IgnoreCase);

        return [.. Directory.EnumerateFiles(directory, $"{name}*{extension}").Where(file => isLogFile.IsMatch(Path.GetFileName(file)))];
    }

    /// <summary>Deletes the entries written before <paramref name="moment"/>; a file left with none is deleted
    /// itself. What is kept is written aside and put in place in one step, so a failure leaves the file whole.</summary>
    /// <exception cref="IOException">The file is held by something else.</exception>
    /// <exception cref="UnauthorizedAccessException">The file may not be changed.</exception>
    public static void DeleteBefore(string file, DateTimeOffset moment)
    {
        var lines = File.ReadAllLines(file);

        // The lines of an exception carry no time and stay with the entry above them.
        var firstKept = Array.FindIndex(lines, line => WrittenAt(line) >= moment);

        if (firstKept < 0)
        {
            File.Delete(file);
        }
        else if (firstKept > 0)
        {
            var replacement = file + ReplacementExtension;

            try
            {
                File.WriteAllLines(replacement, lines[firstKept..]);
                File.Move(replacement, file, overwrite: true);
            }
            finally
            {
                File.Delete(replacement);
            }
        }
    }

    private static DateTimeOffset? WrittenAt(string line) =>
        line.Length > LogConstants.TimestampFormat.Length
        && line[0] == '['
        && DateTime.TryParseExact(
            line.AsSpan(1, LogConstants.TimestampFormat.Length),
            LogConstants.TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var writtenAt)
            ? new DateTimeOffset(writtenAt)
            : null;
}
