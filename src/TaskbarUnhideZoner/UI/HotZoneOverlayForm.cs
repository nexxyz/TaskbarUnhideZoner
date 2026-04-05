using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.UI;

internal sealed class HotZoneOverlayForm : Form
{
    private readonly bool _assistEnabled;
    private readonly double _assistStrength;
    private readonly double _assistCurve;
    private bool _dragging;
    private Point _dragStart;
    private Rectangle _selection;

    private HotZoneOverlayForm(TriggerAssistConfig assist)
    {
        OverlayHeatMap.NormalizeAssist(assist, out _assistEnabled, out _assistStrength, out _assistCurve);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = 0.30;
        Cursor = Cursors.Cross;
    }

    public Rectangle? SelectedRectangle { get; private set; }

    public static Rectangle? SelectRectangle(TriggerAssistConfig assist)
    {
        using var form = new HotZoneOverlayForm(assist);
        return form.ShowDialog() == DialogResult.OK ? form.SelectedRectangle : null;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = true;
        _dragStart = PointToScreen(e.Location);
        _selection = Rectangle.Empty;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging)
        {
            return;
        }

        var current = PointToScreen(e.Location);
        _selection = Normalize(_dragStart, current);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging || e.Button != MouseButtons.Left)
        {
            return;
        }

        _dragging = false;
        var current = PointToScreen(e.Location);
        _selection = Normalize(_dragStart, current);

        if (_selection.Width < 2 || _selection.Height < 2)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        SelectedRectangle = _selection;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Escape)
        {
            return;
        }

        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_selection.IsEmpty)
        {
            return;
        }

        var localSelection = new Rectangle(
            _selection.X - Bounds.X,
            _selection.Y - Bounds.Y,
            _selection.Width,
            _selection.Height);

        DrawGradientFill(e.Graphics, localSelection);
        using var border = new Pen(Color.FromArgb(230, 170, 230, 255), 2f);

        e.Graphics.DrawRectangle(border, localSelection);
    }

    private void DrawGradientFill(Graphics graphics, Rectangle localSelection)
    {
        if (!_assistEnabled || _assistStrength <= 0.001)
        {
            using var flatFill = new SolidBrush(Color.FromArgb(120, 70, 140, 230));
            graphics.FillRectangle(flatFill, localSelection);
            return;
        }

        var weakColor = Color.FromArgb(35, 125, 245);
        var strongColor = Color.FromArgb(255, 20, 20);
        const int cellSize = 14;
        var centerX = localSelection.Left + (localSelection.Width / 2.0);
        var centerY = localSelection.Top + (localSelection.Height / 2.0);
        var halfW = localSelection.Width / 2.0;
        var halfH = localSelection.Height / 2.0;
        var maxDistance = Math.Sqrt((halfW * halfW) + (halfH * halfH));
        if (maxDistance <= 0.001)
        {
            return;
        }

        for (var y = localSelection.Top; y < localSelection.Bottom; y += cellSize)
        {
            for (var x = localSelection.Left; x < localSelection.Right; x += cellSize)
            {
                var w = Math.Min(cellSize, localSelection.Right - x);
                var h = Math.Min(cellSize, localSelection.Bottom - y);
                var sampleX = x + (w / 2.0);
                var sampleY = y + (h / 2.0);

                var dx = sampleX - centerX;
                var dy = sampleY - centerY;
                var distance = Math.Sqrt((dx * dx) + (dy * dy));
                var closeness = 1.0 - Math.Clamp(distance / maxDistance, 0.0, 1.0);
                var visualHeat = OverlayHeatMap.ComputeVisualHeat(closeness, _assistStrength, _assistCurve);
                var color = OverlayHeatMap.BlendHeatColor(weakColor, strongColor, visualHeat);

                using var brush = new SolidBrush(color);
                graphics.FillRectangle(brush, x, y, w, h);
            }
        }
    }

    private static Rectangle Normalize(Point a, Point b)
    {
        var left = Math.Min(a.X, b.X);
        var top = Math.Min(a.Y, b.Y);
        var right = Math.Max(a.X, b.X);
        var bottom = Math.Max(a.Y, b.Y);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }
}
