using MarstekOverlay.Monitoring.History;

namespace MarstekOverlay.Desktop.Presentation;

/// <param name="Hint">The figures the chart itself has no room for.</param>
public sealed record OverlayChart(HistoryChart Chart, string Hint);
