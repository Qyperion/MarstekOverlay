using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MarstekOverlay.Desktop.Services;

/// <summary>Paints the menu of the notification-area icon in the colours of the app's windows. WinForms has no
/// dark menu of its own, and the stock one is light next to a dark panel.</summary>
public sealed class TrayMenuRenderer(TrayMenuColors colors) : ToolStripProfessionalRenderer(new Table(colors))
{
    // The tick is drawn on a grid of 16 units, the size of the box at 100% scaling, and scaled to the box.
    private const float GridSize = 16;
    private const float CheckMarkThickness = 1.8f;

    private static readonly PointF[] Tick = [new(3.5f, 8.5f), new(6.5f, 11.5f), new(12.5f, 4.5f)];

    public void ApplyTo(ContextMenuStrip menu)
    {
        menu.Renderer = this;
        menu.BackColor = colors.Background;
        menu.ForeColor = colors.Text;
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = colors.Text;
        base.OnRenderItemText(e);
    }

    // The stock mark is a black tick in a light blue box.
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var box = e.ImageRectangle;
        var unit = Math.Min(box.Width, box.Height) / GridSize;
        var left = box.Left + (box.Width - GridSize * unit) / 2;
        var top = box.Top + (box.Height - GridSize * unit) / 2;
        var tick = Tick.Select(point => new PointF(left + point.X * unit, top + point.Y * unit)).ToArray();

        var smoothing = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var pen = new Pen(colors.CheckMark, CheckMarkThickness * unit) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            e.Graphics.DrawLines(pen, tick);

        e.Graphics.SmoothingMode = smoothing;
    }

    private sealed class Table(TrayMenuColors colors) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => colors.Background;

        public override Color ImageMarginGradientBegin => colors.Background;

        public override Color ImageMarginGradientMiddle => colors.Background;

        public override Color ImageMarginGradientEnd => colors.Background;

        public override Color MenuBorder => colors.Border;

        public override Color MenuItemBorder => colors.Highlight;

        public override Color MenuItemSelected => colors.Highlight;

        public override Color SeparatorDark => colors.Border;

        public override Color SeparatorLight => colors.Border;
    }
}
