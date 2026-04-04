using System.Drawing.Drawing2D;
using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.UI;

internal sealed class EdgeZoneOverlayForm : Form
{
    private readonly EdgePosition _edge;
    private readonly Rectangle _virtualScreen;
    private readonly bool _assistEnabled;
    private readonly double _assistStrength;
    private readonly double _assistCurve;
    private Rectangle _selection;

    private EdgeZoneOverlayForm(EdgePosition edge, TriggerAssistConfig assist)
    {
        _edge = edge;
        _virtualScreen = SystemInformation.VirtualScreen;
        _assistEnabled = assist.Enabled;
        _assistStrength = 1.0 - (Math.Clamp(assist.MinDelayPercent, 10, 100) / 100.0);
        _assistCurve = Math.Clamp(assist.CurveExponent, 0.35, 3.0);

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = _virtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = 0.30;
        Cursor = Cursors.Cross;
    }

    public Rectangle? SelectedRectangle { get; private set; }

    public static Rectangle? SelectEdgeRectangle(EdgePosition edge, TriggerAssistConfig assist)
    {
        using var form = new EdgeZoneOverlayForm(edge, assist);
        return form.ShowDialog() == DialogResult.OK ? form.SelectedRectangle : null;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var cursor = PointToScreen(e.Location);
        _selection = ComputeSelection(cursor);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var cursor = PointToScreen(e.Location);
        _selection = ComputeSelection(cursor);
        if (_selection.Width < 1 || _selection.Height < 1)
        {
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
            _selection.X - _virtualScreen.X,
            _selection.Y - _virtualScreen.Y,
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
        var startPoint = _edge switch
        {
            EdgePosition.Top => new Point(localSelection.Left, localSelection.Top),
            EdgePosition.Bottom => new Point(localSelection.Left, localSelection.Bottom),
            EdgePosition.Left => new Point(localSelection.Left, localSelection.Top),
            EdgePosition.Right => new Point(localSelection.Right, localSelection.Top),
            _ => new Point(localSelection.Left, localSelection.Top)
        };

        var endPoint = _edge switch
        {
            EdgePosition.Top => new Point(localSelection.Left, localSelection.Bottom),
            EdgePosition.Bottom => new Point(localSelection.Left, localSelection.Top),
            EdgePosition.Left => new Point(localSelection.Right, localSelection.Top),
            EdgePosition.Right => new Point(localSelection.Left, localSelection.Top),
            _ => new Point(localSelection.Left, localSelection.Bottom)
        };

        using var gradientBrush = new LinearGradientBrush(startPoint, endPoint, weakColor, strongColor);
        var blend = new ColorBlend(16)
        {
            Colors = new Color[16],
            Positions = new float[16]
        };

        for (var i = 0; i < blend.Colors.Length; i++)
        {
            var t = i / (double)(blend.Colors.Length - 1);
            var closeness = 1.0 - Math.Clamp(t, 0.0, 1.0);
            var boost = Math.Pow(closeness, _assistCurve);
            var reduction = boost * _assistStrength;
            var visualHeat = _assistStrength > 0.001 ? reduction / _assistStrength : 0.0;
            var alpha = 55 + (int)(150 * visualHeat);
            var r = Lerp(weakColor.R, strongColor.R, visualHeat);
            var g = Lerp(weakColor.G, strongColor.G, visualHeat);
            var b = Lerp(weakColor.B, strongColor.B, visualHeat);

            blend.Colors[i] = Color.FromArgb(alpha, r, g, b);
            blend.Positions[i] = (float)t;
        }

        gradientBrush.InterpolationColors = blend;
        graphics.FillRectangle(gradientBrush, localSelection);
    }

    private static int Lerp(int a, int b, double t)
    {
        var clamped = Math.Clamp(t, 0.0, 1.0);
        return (int)Math.Round(a + ((b - a) * clamped));
    }

    private Rectangle ComputeSelection(Point cursor)
    {
        var x = Math.Clamp(cursor.X, _virtualScreen.Left, _virtualScreen.Right - 1);
        var y = Math.Clamp(cursor.Y, _virtualScreen.Top, _virtualScreen.Bottom - 1);

        return _edge switch
        {
            EdgePosition.Top => new Rectangle(_virtualScreen.Left, _virtualScreen.Top, _virtualScreen.Width, Math.Max(1, y - _virtualScreen.Top + 1)),
            EdgePosition.Bottom => new Rectangle(_virtualScreen.Left, y, _virtualScreen.Width, Math.Max(1, _virtualScreen.Bottom - y)),
            EdgePosition.Left => new Rectangle(_virtualScreen.Left, _virtualScreen.Top, Math.Max(1, x - _virtualScreen.Left + 1), _virtualScreen.Height),
            EdgePosition.Right => new Rectangle(x, _virtualScreen.Top, Math.Max(1, _virtualScreen.Right - x), _virtualScreen.Height),
            _ => Rectangle.Empty
        };
    }
}
