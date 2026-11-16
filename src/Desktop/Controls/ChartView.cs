using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.History;

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;

namespace MarstekOverlay.Desktop.Controls;

/// <summary>What every drawing of a <see cref="HistoryChart"/> is made of: lines with their fills, broken wherever
/// nothing is known, the band a line's samples kept within and the shading of the time on the battery.</summary>
public abstract class ChartView : FrameworkElement
{
    public static readonly DependencyProperty ChartProperty = Register<ChartView, HistoryChart>(nameof(Chart));
    public static readonly DependencyProperty BandBrushProperty = Register<ChartView, Brush>(nameof(BandBrush));
    public static readonly DependencyProperty LoadBrushProperty = Register<ChartView, Brush>(nameof(LoadBrush));
    public static readonly DependencyProperty LoadFillBrushProperty = Register<ChartView, Brush>(nameof(LoadFillBrush));
    public static readonly DependencyProperty ChargeBrushProperty = Register<ChartView, Brush>(nameof(ChargeBrush));
    public static readonly DependencyProperty ChargeFillBrushProperty = Register<ChartView, Brush>(nameof(ChargeFillBrush));
    public static readonly DependencyProperty OnBatteryBrushProperty = Register<ChartView, Brush>(nameof(OnBatteryBrush));

    public static readonly DependencyProperty PlotColumnsProperty = DependencyProperty.Register(
        nameof(PlotColumns),
        typeof(int),
        typeof(ChartView),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    protected const double FullChargePercent = Percent.Whole;

    /// <summary>The bottom of a scale that shows values from nothing up.</summary>
    protected const double ScaleFromZero = 0;

    // A lone sample or a period on the battery seen once has no width of its own.
    private const double NarrowestMark = 2;

    public HistoryChart? Chart
    {
        get => (HistoryChart?)GetValue(ChartProperty);
        set => SetValue(ChartProperty, value);
    }

    public Brush? BandBrush
    {
        get => (Brush?)GetValue(BandBrushProperty);
        set => SetValue(BandBrushProperty, value);
    }

    public Brush? LoadBrush
    {
        get => (Brush?)GetValue(LoadBrushProperty);
        set => SetValue(LoadBrushProperty, value);
    }

    public Brush? LoadFillBrush
    {
        get => (Brush?)GetValue(LoadFillBrushProperty);
        set => SetValue(LoadFillBrushProperty, value);
    }

    public Brush? ChargeBrush
    {
        get => (Brush?)GetValue(ChargeBrushProperty);
        set => SetValue(ChargeBrushProperty, value);
    }

    public Brush? ChargeFillBrush
    {
        get => (Brush?)GetValue(ChargeFillBrushProperty);
        set => SetValue(ChargeFillBrushProperty, value);
    }

    public Brush? OnBatteryBrush
    {
        get => (Brush?)GetValue(OnBatteryBrushProperty);
        set => SetValue(OnBatteryBrushProperty, value);
    }

    /// <summary>How many pixels of the screen across the lines are drawn, which is how many points are worth
    /// building. Counted at the element's own size: a scale applied to it from outside is not.</summary>
    public int PlotColumns
    {
        get => (int)GetValue(PlotColumnsProperty);
        set => SetValue(PlotColumnsProperty, value);
    }

    /// <summary>The chart, unless there is none or it spans no time.</summary>
    protected HistoryChart? DrawnChart => Chart is { } chart && chart.To > chart.From ? chart : null;

    protected static DependencyProperty Register<TOwner, TValue>(
        string name,
        FrameworkPropertyMetadataOptions options = FrameworkPropertyMetadataOptions.AffectsRender) =>
        DependencyProperty.Register(name, typeof(TValue), typeof(TOwner), new FrameworkPropertyMetadata(null, options));

    /// <summary>A pen for lines of the chart; a rounded one for the lines of the values.</summary>
    protected static Pen FrozenPen(Brush brush, double thickness, bool isRounded = false)
    {
        var pen = new Pen(brush, thickness);

        if (isRounded)
            (pen.LineJoin, pen.StartLineCap, pen.EndLineCap) = (PenLineJoin.Round, PenLineCap.Round, PenLineCap.Round);

        pen.Freeze();
        return pen;
    }

    /// <summary>Where a moment falls between the left and the right edge of <paramref name="band"/>.</summary>
    protected static double PlaceInTime(HistoryChart chart, DateTimeOffset moment, Rect band) =>
        band.Left + (moment - chart.From) / (chart.To - chart.From) * band.Width;

    /// <summary>Where a value falls in <paramref name="band"/>, which keeps a line of
    /// <paramref name="lineThickness"/> whole inside it at both ends of the scale.</summary>
    protected static double PlaceInScale(double value, double floor, double ceiling, Rect band, double lineThickness) =>
        band.Bottom - lineThickness / 2 - Math.Clamp((value - floor) / (ceiling - floor), 0, 1) * (band.Height - lineThickness);

    /// <summary>Where a value at a moment falls in <paramref name="band"/>.</summary>
    protected static Point Place(
        HistoryChart chart,
        DateTimeOffset at,
        double value,
        Rect band,
        double floor,
        double ceiling,
        double lineThickness) =>
        new(PlaceInTime(chart, at, band), PlaceInScale(value, floor, ceiling, band, lineThickness));

    /// <summary>How wide the lines are drawn at <paramref name="size"/>, in the units of the element.</summary>
    protected abstract double PlotWidth(Size size);

    // Without a peer a drawn element is invisible to a screen reader, which takes the figures from the tooltip.
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        CountPlotColumns(sizeInfo.NewSize, VisualTreeHelper.GetDpi(this));
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        CountPlotColumns(RenderSize, newDpi);
    }

    /// <summary>Painted even where nothing is drawn, so that the pointer and the tooltip answer over the whole
    /// element.</summary>
    protected void PaintWholeArea(DrawingContext drawing) => drawing.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

    protected void DrawTimeOnBattery(DrawingContext drawing, HistoryChart chart, Rect band)
    {
        foreach (var span in chart.OnBattery)
        {
            var left = PlaceInTime(chart, span.From, band);
            var width = Math.Max(PlaceInTime(chart, span.To, band) - left, NarrowestMark);

            drawing.DrawRectangle(OnBatteryBrush, null, new Rect(left, band.Top, width, band.Height));
        }
    }

    /// <param name="fillFrom">Where the fill under or over a line ends: the height of the scale's zero.</param>
    protected static void DrawRuns(
        DrawingContext drawing,
        HistoryChart chart,
        IEnumerable<IEnumerable<ChartPoint>> runs,
        Rect band,
        double floor,
        double ceiling,
        Brush? stroke,
        Brush? fill,
        double lineThickness,
        double fillFrom)
    {
        if (stroke is null || ceiling <= floor)
            return;

        var pen = FrozenPen(stroke, lineThickness, isRounded: true);

        foreach (var run in runs)
        {
            var points = run.Select(point => Place(chart, point.At, point.Value, band, floor, ceiling, lineThickness)).ToList();

            if (points is [var lone])
                points = Widened(lone);

            if (points.Count == 0)
                continue;

            if (fill is not null)
                drawing.DrawGeometry(fill, null, Outline(points, fillFrom));

            drawing.DrawGeometry(null, pen, Line(points));
        }
    }

    /// <summary>The line through the averages of <paramref name="runs"/>.</summary>
    protected static IEnumerable<IEnumerable<ChartPoint>> Averages(IEnumerable<IEnumerable<ChartColumn>> runs) =>
        runs.Select(run => run.Select(column => column.AveragePoint));

    /// <summary>A band from the lowest to the highest of the samples behind every point of a line.</summary>
    protected static void DrawRanges(
        DrawingContext drawing,
        HistoryChart chart,
        IReadOnlyList<IReadOnlyList<ChartColumn>> runs,
        Rect band,
        double floor,
        double ceiling,
        Brush? fill,
        double lineThickness)
    {
        if (fill is null || ceiling <= floor)
            return;

        foreach (var run in runs.Where(run => run.Count > 0))
        {
            var highest = run.Select(column => Place(chart, column.At, column.Max, band, floor, ceiling, lineThickness)).ToList();
            var lowest = run.Select(column => Place(chart, column.At, column.Min, band, floor, ceiling, lineThickness)).ToList();

            if (run.Count == 1)
            {
                highest = Widened(highest[0]);
                lowest = Widened(lowest[0]);
            }

            lowest.Reverse();

            var geometry = new StreamGeometry();

            using (var figure = geometry.Open())
            {
                figure.BeginFigure(highest[0], isFilled: true, isClosed: true);
                figure.PolyLineTo([.. highest.Skip(1), .. lowest], isStroked: false, isSmoothJoin: false);
            }

            geometry.Freeze();
            drawing.DrawGeometry(fill, null, geometry);
        }
    }

    private void CountPlotColumns(Size size, DpiScale dpi) =>
        SetCurrentValue(PlotColumnsProperty, (int)(Math.Max(PlotWidth(size), 0) * dpi.DpiScaleX));

    private static List<Point> Widened(Point lone) =>
        [lone with { X = lone.X - NarrowestMark / 2 }, lone with { X = lone.X + NarrowestMark / 2 }];

    private static Geometry Line(List<Point> points)
    {
        var geometry = new StreamGeometry();

        using (var figure = geometry.Open())
        {
            figure.BeginFigure(points[0], isFilled: false, isClosed: false);
            figure.PolyLineTo(points[1..], isStroked: true, isSmoothJoin: true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static Geometry Outline(List<Point> points, double baseline)
    {
        var geometry = new StreamGeometry();

        using (var figure = geometry.Open())
        {
            figure.BeginFigure(new Point(points[0].X, baseline), isFilled: true, isClosed: true);
            figure.PolyLineTo(points, isStroked: false, isSmoothJoin: false);
            figure.LineTo(new Point(points[^1].X, baseline), isStroked: false, isSmoothJoin: false);
        }

        geometry.Freeze();
        return geometry;
    }

    private sealed class Peer(ChartView owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override string GetClassNameCore() => Owner.GetType().Name;
    }
}
