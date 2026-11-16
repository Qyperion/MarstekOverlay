using MarstekOverlay.Desktop.Interop;
using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Monitoring;

using System.Drawing;
using System.Drawing.Drawing2D;

namespace MarstekOverlay.Desktop.Services;

/// <summary>A drawn icon together with the handle Windows made for it. <see cref="Icon.FromHandle"/> leaves the
/// handle to its caller, and an icon that is drawn again whenever the charge moves would leak one each time.</summary>
public sealed class TrayIconImage : IDisposable
{
    private readonly nint _handle;

    private TrayIconImage(nint handle)
    {
        _handle = handle;
        Icon = Icon.FromHandle(handle);
    }

    public Icon Icon { get; }

    public static TrayIconLook LookOf(OverlayState state, int size)
    {
        // Readings gone stale are not drawn at all, so they make no difference to the look either.
        if (state.Supply == SupplyMark.NoConnection)
            return new TrayIconLook(size, FilledColumns: 0, Tone.Secondary, SupplyMark.NoConnection);

        var cell = new Layout(size).Cell.Width;
        var filled = state.SocPercent <= 0 ? 0 : Math.Clamp((int)Math.Round(state.SocPercent / Percent.Whole * cell), 1, cell);

        return new TrayIconLook(size, filled, state.SocTone, state.Supply);
    }

    public static TrayIconImage Draw(TrayIconLook look, Func<Tone, Color> colorOf)
    {
        using var bitmap = new Bitmap(look.Size, look.Size);
        using (var graphics = Graphics.FromImage(bitmap))
            Paint(graphics, look, colorOf);

        return new TrayIconImage(bitmap.GetHicon());
    }

    public void Dispose()
    {
        Icon.Dispose();
        NativeMethods.DestroyIcon(_handle);
    }

    private static void Paint(Graphics graphics, TrayIconLook look, Func<Tone, Color> colorOf)
    {
        var layout = new Layout(look.Size);
        var isConnected = look.Mark != SupplyMark.NoConnection;

        graphics.Clear(Color.Transparent);

        using (var outline = new SolidBrush(colorOf(isConnected ? Tone.Primary : Tone.Secondary)))
        {
            graphics.FillRectangle(outline, layout.Body);
            graphics.FillRectangle(outline, layout.Terminal);
        }

        Erase(graphics, eraser => graphics.FillRectangle(eraser, Rectangle.Inflate(layout.Body, -layout.Stroke, -layout.Stroke)));

        if (look.FilledColumns > 0)
        {
            using var charge = new SolidBrush(colorOf(look.ChargeTone));
            graphics.FillRectangle(charge, layout.Cell with { Width = look.FilledColumns });
        }

        switch (look.Mark)
        {
            case SupplyMark.OnBattery:
                PaintDrainingMark(graphics, layout, colorOf(Tone.Warning));
                break;

            case SupplyMark.NoConnection:
                PaintCross(graphics, layout, colorOf(Tone.Critical));
                break;
        }
    }

    // A triangle pointing down: the charge is on its way out.
    private static void PaintDrainingMark(Graphics graphics, Layout layout, Color color)
    {
        var badge = layout.Badge;
        PointF[] triangle =
        [
            new(badge.Left, badge.Top),
            new(badge.Right, badge.Top),
            new(badge.Left + badge.Width / 2f, badge.Bottom),
        ];

        Erase(graphics, eraser =>
        {
            using var rim = new Pen(eraser, 2 * layout.Stroke) { LineJoin = LineJoin.Round };
            graphics.FillPolygon(eraser, triangle);
            graphics.DrawPolygon(rim, triangle);
        });

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(color);
        graphics.FillPolygon(fill, triangle);
    }

    private static void PaintCross(Graphics graphics, Layout layout, Color color)
    {
        var badge = layout.Badge;
        var thickness = 2 * layout.Stroke;

        Erase(graphics, eraser =>
        {
            using var rim = new Pen(eraser, 2 * thickness);
            DrawCross(rim);
        });

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var stroke = new Pen(color, thickness);
        DrawCross(stroke);

        void DrawCross(Pen pen)
        {
            graphics.DrawLine(pen, badge.Left, badge.Top, badge.Right, badge.Bottom);
            graphics.DrawLine(pen, badge.Left, badge.Bottom, badge.Right, badge.Top);
        }
    }

    // Clears instead of painting: the hollow of the battery, and the rim that sets a mark apart from it.
    private static void Erase(Graphics graphics, Action<Brush> paint)
    {
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.CompositingMode = CompositingMode.SourceCopy;
        paint(Brushes.Transparent);
        graphics.CompositingMode = CompositingMode.SourceOver;
    }

    /// <summary>Where the parts go, in the proportions of a 16-pixel icon: the size of the notification area
    /// at 100% scaling.</summary>
    private readonly record struct Layout(int Size)
    {
        private const double GridSize = 16;

        private const double BodyLeft = 1;
        private const double BodyTop = 4;
        private const double BodyWidth = 12;
        private const double BodyHeight = 8;
        private const double TerminalWidth = 1.5;
        private const double BadgeSize = 7;

        // A line drawn to the very edge of the icon would lose half its width.
        private const float BadgeEdgeInset = 0.5f;

        private const int ThinnestLine = 1;

        public int Stroke => Math.Max(ThinnestLine, (int)(Size / GridSize));

        public Rectangle Body => new(Scaled(BodyLeft), Scaled(BodyTop), Scaled(BodyWidth), Scaled(BodyHeight));

        public Rectangle Terminal =>
            new(Body.Right, Body.Top + Body.Height / 4, Math.Max(ThinnestLine, Scaled(TerminalWidth)), Body.Height / 2);

        /// <summary>What the charge fills: the inside of the body, a gap away from its outline.</summary>
        public Rectangle Cell => Rectangle.Inflate(Body, -2 * Stroke, -2 * Stroke);

        public RectangleF Badge => new(
            Size - Scaled(BadgeSize),
            Size - Scaled(BadgeSize),
            Scaled(BadgeSize) - BadgeEdgeInset,
            Scaled(BadgeSize) - BadgeEdgeInset);

        private int Scaled(double units) => (int)Math.Round(units * Size / GridSize, MidpointRounding.AwayFromZero);
    }
}
