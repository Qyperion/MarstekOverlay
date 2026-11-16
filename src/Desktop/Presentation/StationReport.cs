using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Monitoring;

using System.Text;

namespace MarstekOverlay.Desktop.Presentation;

/// <summary>Everything a snapshot holds, as plain text: the full picture of what a station reports.</summary>
public static class StationReport
{
    private const string Indent = "  ";

    public static string Describe(StationSnapshot snapshot)
    {
        var report = new StringBuilder();

        foreach (var section in StationSections.Describe(snapshot))
        {
            report.AppendLine(Phrases.Format(Strings.Report_SectionHeading, section.Title, section.Method));

            if (section.Note is { } note)
                report.Append(Indent).AppendLine(note);

            foreach (var row in section.Rows)
                report.Append(Indent).AppendLine(Phrases.Labelled(row.Label, row.Value ?? StationSections.NoValue));

            report.AppendLine();
        }

        if (snapshot.Errors.Count > 0)
        {
            report.AppendLine(Strings.Report_Errors);
            foreach (var error in snapshot.Errors)
                report.Append(Indent).AppendLine(ErrorPresenter.Describe(error));
        }

        return report.ToString().TrimEnd();
    }
}
