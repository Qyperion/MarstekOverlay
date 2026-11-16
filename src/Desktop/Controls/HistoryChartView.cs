using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Monitoring.History;

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace MarstekOverlay.Desktop.Controls;

/// <summary>The charts of the details window on one time axis, each with a scale down its left side: the load
/// with the band its samples kept within, the charge, and the battery power, which goes up from zero while
/// charging and down while discharging. The pointer picks a moment, which is marked on every line and handed
/// out through <see cref="Pointer"/> for its values to be worded. The pointer is drawn on a layer of its own, so
/// that following the mouse does not build the lines again.</summary>
public sealed class HistoryChartView : ChartView
{
    public static readonly DependencyProperty PointerProperty = DependencyProperty.Register(
        nameof(Pointer),
        typeof(DateTimeOffset?),
        typeof(HistoryChartView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (owner, _) => ((HistoryChartView)owner).DrawPointerLayer()));

    public static readonly DependencyProperty GridBrushProperty = Register<HistoryChartView, Brush>(nameof(GridBrush));
    public static readonly DependencyProperty LabelBrushProperty = Register<HistoryChartView, Brush>(nameof(LabelBrush));
    public static readonly DependencyProperty PointerBrushProperty = Register<HistoryChartView, Brush>(nameof(PointerBrush));
    public static readonly DependencyProperty LoadRangeBrushProperty = Register<HistoryChartView, Brush>(nameof(LoadRangeBrush));
    public static readonly DependencyProperty ChargingBrushProperty = Register<HistoryChartView, Brush>(nameof(ChargingBrush));
    public static readonly DependencyProperty ChargingFillBrushProperty = Register<HistoryChartView, Brush>(nameof(ChargingFillBrush));
    public static readonly DependencyProperty DischargingBrushProperty = Register<HistoryChartView, Brush>(nameof(DischargingBrush));
    public static readonly DependencyProperty DischargingFillBrushProperty = Register<HistoryChartView, Brush>(nameof(DischargingFillBrush));
    public static readonly DependencyProperty LoadTitleProperty = Register<HistoryChartView, string>(nameof(LoadTitle));
    public static readonly DependencyProperty ChargeTitleProperty = Register<HistoryChartView, string>(nameof(ChargeTitle));
    public static readonly DependencyProperty BatteryTitleProperty = Register<HistoryChartView, string>(nameof(BatteryTitle));

    private const double ScaleWidth = 46;
    private const double RightMargin = 10;
    private const double TitleHeight = 20;
    private const double TimeAxisHeight = 20;
    private const double LabelGap = 6;
    private const double LowestPlot = 24;

    // The load is what the window is opened for; the charge runs along a scale that needs no height to be read.
    private const double LoadShare = 0.4;
    private const double ChargeShare = 0.25;
    private const int PlotCount = 3;

    private const double LineThickness = 1.75;
    private const double GridThickness = 1;
    private const double PointerMarkRadius = 3.5;
    private const double LabelFontSize = 11;

    // How much of the axis one mark needs for its label to stay clear of the next.
    private const double TimeMarkRoom = 90;
    private const double ValueMarkRoom = 34;
    private const int MostValueMarks = 5;

    private const string ValueFormat = "0";

    private static readonly Typeface LabelTypeface = new(
        SystemFonts.MessageFontFamily,
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    private readonly DrawingVisual _pointerLayer = new();
    private Pen? _pointerPen;

    public HistoryChartView() => AddVisualChild(_pointerLayer);

    /// <summary>The moment under the pointer; nothing while it is elsewhere.</summary>
    public DateTimeOffset? Pointer
    {
        get => (DateTimeOffset?)GetValue(PointerProperty);
        set => SetValue(PointerProperty, value);
    }

    public Brush? GridBrush
    {
        get => (Brush?)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush? LabelBrush
    {
        get => (Brush?)GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public Brush? PointerBrush
    {
        get => (Brush?)GetValue(PointerBrushProperty);
        set => SetValue(PointerBrushProperty, value);
    }

    /// <summary>Fills the band between the lowest and the highest sample behind every point of the load line.</summary>
    public Brush? LoadRangeBrush
    {
        get => (Brush?)GetValue(LoadRangeBrushProperty);
        set => SetValue(LoadRangeBrushProperty, value);
    }

    public Brush? ChargingBrush
    {
        get => (Brush?)GetValue(ChargingBrushProperty);
        set => SetValue(ChargingBrushProperty, value);
    }

    public Brush? ChargingFillBrush
    {
        get => (Brush?)GetValue(ChargingFillBrushProperty);
        set => SetValue(ChargingFillBrushProperty, value);
    }

    public Brush? DischargingBrush
    {
        get => (Brush?)GetValue(DischargingBrushProperty);
        set => SetValue(DischargingBrushProperty, value);
    }

    public Brush? DischargingFillBrush
    {
        get => (Brush?)GetValue(DischargingFillBrushProperty);
        set => SetValue(DischargingFillBrushProperty, value);
    }

    public string? LoadTitle
    {
        get => (string?)GetValue(LoadTitleProperty);
        set => SetValue(LoadTitleProperty, value);
    }

    public string? ChargeTitle
    {
        get => (string?)GetValue(ChargeTitleProperty);
        set => SetValue(ChargeTitleProperty, value);
    }

    public string? BatteryTitle
    {
        get => (string?)GetValue(BatteryTitleProperty);
        set => SetValue(BatteryTitleProperty, value);
    }

    protected override int VisualChildrenCount => 1;

    protected override double PlotWidth(Size size) => PlotsAt(size) is { } plots ? plots.Load.Width : 0;

    protected override Visual GetVisualChild(int index) =>
        index == 0 ? _pointerLayer : throw new ArgumentOutOfRangeException(nameof(index));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var x = e.GetPosition(this).X;
        DateTimeOffset? moment = Chart is { } chart && PlotsAt(RenderSize) is { } plots && x >= plots.Load.Left && x <= plots.Load.Right
            ? chart.From + (x - plots.Load.Left) / plots.Load.Width * (chart.To - chart.From)
            : null;

        SetCurrentValue(PointerProperty, moment);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        SetCurrentValue(PointerProperty, null);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        DrawLines(drawing);
        DrawPointerLayer();
    }

    private void DrawLines(DrawingContext drawing)
    {
        PaintWholeArea(drawing);

        if (PlotsAt(RenderSize) is not { } plots)
            return;

        var (load, charge, battery) = plots;

        foreach (var plot in plots.All)
            drawing.DrawRectangle(BandBrush, null, plot);

        DrawTitle(drawing, LoadTitle, load);
        DrawTitle(drawing, ChargeTitle, charge);
        DrawTitle(drawing, BatteryTitle, battery);

        if (DrawnChart is not { } chart)
            return;

        var loadCeiling = chart.LoadCeiling.Watts;
        var batteryFloor = chart.BatteryScale.Floor.Watts;
        var batteryCeiling = chart.BatteryScale.Ceiling.Watts;

        foreach (var plot in plots.All)
            DrawTimeOnBattery(drawing, chart, plot);

        DrawScale(drawing, load, ScaleFromZero, loadCeiling);
        DrawScale(drawing, charge, ScaleFromZero, FullChargePercent);
        DrawScale(drawing, battery, batteryFloor, batteryCeiling);
        DrawTimeAxis(drawing, chart, plots);

        DrawRanges(drawing, chart, chart.Load, load, ScaleFromZero, loadCeiling, LoadRangeBrush, LineThickness);
        DrawRuns(drawing, chart, Averages(chart.Load), load, ScaleFromZero, loadCeiling, LoadBrush, LoadFillBrush, LineThickness, load.Bottom);
        DrawRuns(drawing, chart, chart.Charge, charge, ScaleFromZero, FullChargePercent, ChargeBrush, ChargeFillBrush, LineThickness, charge.Bottom);
        DrawBatteryPower(drawing, chart, battery);
    }

    private void DrawPointerLayer()
    {
        using var drawing = _pointerLayer.RenderOpen();

        if (Pointer is { } moment && DrawnChart is { } chart && PlotsAt(RenderSize) is { } plots && chart.ReadAt(moment) is { } reading)
            DrawPointer(drawing, chart, reading, plots);
    }

    /// <returns>Nothing while the element is too small to hold the plots.</returns>
    private static Plots? PlotsAt(Size size)
    {
        var width = size.Width - ScaleWidth - RightMargin;
        var height = size.Height - PlotCount * TitleHeight - TimeAxisHeight;

        if (width <= 0 || height < PlotCount * LowestPlot)
            return null;

        var loadHeight = Math.Round(height * LoadShare);
        var chargeHeight = Math.Round(height * ChargeShare);
        var load = new Rect(ScaleWidth, TitleHeight, width, loadHeight);
        var charge = new Rect(ScaleWidth, load.Bottom + TitleHeight, width, chargeHeight);
        var battery = new Rect(ScaleWidth, charge.Bottom + TitleHeight, width, height - loadHeight - chargeHeight);

        return new Plots(load, charge, battery);
    }

    // Each direction is drawn in its own colour, cut off at zero. The cuts leave out the line's own thickness
    // around zero, where a battery at rest would otherwise be drawn half in one colour and half in the other:
    // at rest it shows as the bare zero line.
    private void DrawBatteryPower(DrawingContext drawing, HistoryChart chart, Rect plot)
    {
        var (floor, ceiling) = (chart.BatteryScale.Floor.Watts, chart.BatteryScale.Ceiling.Watts);
        var zero = PlaceInScale(0, floor, ceiling, plot, LineThickness);
        var above = new Rect(plot.Left, plot.Top, plot.Width, Math.Max(zero - LineThickness / 2 - plot.Top, 0));
        var below = new Rect(plot.Left, zero + LineThickness / 2, plot.Width, Math.Max(plot.Bottom - zero - LineThickness / 2, 0));

        DrawGridLine(drawing, LabelBrush, new Point(plot.Left, zero), new Point(plot.Right, zero));
        DrawWithin(above, ChargingBrush, ChargingFillBrush);
        DrawWithin(below, DischargingBrush, DischargingFillBrush);

        void DrawWithin(Rect part, Brush? stroke, Brush? fill)
        {
            drawing.PushClip(new RectangleGeometry(part));
            DrawRuns(drawing, chart, chart.Battery, plot, floor, ceiling, stroke, fill, LineThickness, zero);
            drawing.Pop();
        }
    }

    private void DrawTitle(DrawingContext drawing, string? title, Rect plot)
    {
        if (title is null || Label(title) is not { } text)
            return;

        text.MaxTextWidth = plot.Width;
        text.MaxLineCount = 1;
        text.Trimming = TextTrimming.CharacterEllipsis;

        drawing.DrawText(text, new Point(plot.Left, plot.Top - TitleHeight + (TitleHeight - text.Height) / 2));
    }

    private void DrawScale(DrawingContext drawing, Rect plot, double floor, double ceiling)
    {
        var room = Math.Clamp((int)(plot.Height / ValueMarkRoom), 1, MostValueMarks);

        foreach (var value in ChartAxis.ValueTicks(floor, ceiling, room))
        {
            var y = PlaceInScale(value, floor, ceiling, plot, LineThickness);
            DrawGridLine(drawing, GridBrush, new Point(plot.Left, y), new Point(plot.Right, y));

            if (Label(value.ToString(ValueFormat, CultureInfo.CurrentCulture)) is { } text)
                drawing.DrawText(text, new Point(plot.Left - LabelGap - text.Width, y - text.Height / 2));
        }
    }

    private void DrawTimeAxis(DrawingContext drawing, HistoryChart chart, Plots plots)
    {
        var room = Math.Max((int)(plots.Load.Width / TimeMarkRoom), 1);
        var labelsEndAt = double.NegativeInfinity;

        foreach (var tick in ChartAxis.TimeTicks(chart.From, chart.To, room, TimeZoneInfo.Local))
        {
            var x = PlaceInTime(chart, tick.At, plots.Load);

            foreach (var plot in plots.All)
                DrawGridLine(drawing, GridBrush, new Point(x, plot.Top), new Point(x, plot.Bottom));

            if (Label(tick.IsDayStart ? Phrases.Date(tick.At) : Phrases.TimeOfDay(tick.At)) is not { } text)
                continue;

            var left = Math.Clamp(x - text.Width / 2, 0, Math.Max(RenderSize.Width - text.Width, 0));
            if (left < labelsEndAt + LabelGap)
                continue;

            drawing.DrawText(text, new Point(left, plots.Battery.Bottom + (TimeAxisHeight - text.Height) / 2));
            labelsEndAt = left + text.Width;
        }
    }

    private void DrawPointer(DrawingContext drawing, HistoryChart chart, ChartReading reading, Plots plots)
    {
        var (load, charge, battery) = plots;
        var x = PlaceInTime(chart, reading.At, load);

        if (PointerPen() is { } pen)
        {
            foreach (var plot in plots.All)
                drawing.DrawLine(pen, new Point(x, plot.Top), new Point(x, plot.Bottom));
        }

        if (reading.Load is { } watts)
            Mark(LoadBrush, PlaceInScale(watts.Watts, ScaleFromZero, chart.LoadCeiling.Watts, load, LineThickness));

        if (reading.SocPercent is { } soc)
            Mark(ChargeBrush, PlaceInScale(soc, ScaleFromZero, FullChargePercent, charge, LineThickness));

        if (reading.BatteryPower is { } flow)
        {
            Mark(
                flow.Watts < 0 ? DischargingBrush : ChargingBrush,
                PlaceInScale(flow.Watts, chart.BatteryScale.Floor.Watts, chart.BatteryScale.Ceiling.Watts, battery, LineThickness));
        }

        void Mark(Brush? fill, double y) => drawing.DrawEllipse(fill, null, new Point(x, y), PointerMarkRadius, PointerMarkRadius);
    }

    private Pen? PointerPen()
    {
        if (PointerBrush is not { } brush)
            return null;

        if (_pointerPen?.Brush != brush)
            _pointerPen = FrozenPen(brush, GridThickness);

        return _pointerPen;
    }

    private static void DrawGridLine(DrawingContext drawing, Brush? brush, Point from, Point to)
    {
        if (brush is null)
            return;

        // Snapped to whole pixels: a hairline that straddles two of them comes out twice as wide and half as bright.
        var half = GridThickness / 2;
        var guidelines = new GuidelineSet([from.X - half, to.X + half], [from.Y - half, to.Y + half]);
        guidelines.Freeze();

        drawing.PushGuidelineSet(guidelines);
        drawing.DrawLine(FrozenPen(brush, GridThickness), from, to);
        drawing.Pop();
    }

    private FormattedText? Label(string text) =>
        LabelBrush is { } brush
            ? new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                LabelTypeface,
                LabelFontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip)
            : null;

    private readonly record struct Plots(Rect Load, Rect Charge, Rect Battery)
    {
        public Rect[] All => [Load, Charge, Battery];
    }
}
