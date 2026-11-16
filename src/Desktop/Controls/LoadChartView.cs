using System.Windows;
using System.Windows.Media;

namespace MarstekOverlay.Desktop.Controls;

/// <summary>The chart of the panel: two bands that share the time axis, the load above and the charge below.
/// Each band has a scale of its own, so the two lines never cross and neither can be read against the other's
/// scale. The charge band is too low for the whole scale to show anything, so it spans only what the charge
/// kept within over the period. Time on the battery is shaded through both.</summary>
public sealed class LoadChartView : ChartView
{
    private const double ChargeBandHeight = 16;
    private const double BandGap = 3;
    private const double LineThickness = 1.5;
    private const double LowestLoadBandHeight = 8;

    // Anything lower leaves no room for the load band above the charge band, and the chart would draw nothing.
    public LoadChartView() => MinHeight = LowestLoadBandHeight + BandGap + ChargeBandHeight;

    protected override double PlotWidth(Size size) => size.Width;

    protected override void OnRender(DrawingContext drawing)
    {
        var size = RenderSize;
        PaintWholeArea(drawing);

        if (size.Width <= 0 || size.Height <= ChargeBandHeight + BandGap)
            return;

        var loadBand = new Rect(0, 0, size.Width, size.Height - ChargeBandHeight - BandGap);
        var chargeBand = new Rect(0, size.Height - ChargeBandHeight, size.Width, ChargeBandHeight);

        drawing.DrawRectangle(BandBrush, null, loadBand);
        drawing.DrawRectangle(BandBrush, null, chargeBand);

        if (DrawnChart is not { } chart)
            return;

        DrawTimeOnBattery(drawing, chart, new Rect(size));
        var (chargeFloor, chargeCeiling) = chart.ChargeScale;

        DrawRuns(drawing, chart, Averages(chart.Load), loadBand, ScaleFromZero, chart.LoadCeiling.Watts, LoadBrush, LoadFillBrush, LineThickness, loadBand.Bottom);
        DrawRuns(drawing, chart, chart.Charge, chargeBand, chargeFloor, chargeCeiling, ChargeBrush, ChargeFillBrush, LineThickness, chargeBand.Bottom);
    }
}
