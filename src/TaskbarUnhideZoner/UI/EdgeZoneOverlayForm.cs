using TaskbarUnhideZoner.Models;

namespace TaskbarUnhideZoner.UI;

internal sealed class EdgeZoneOverlayForm : Form
{
    private readonly EdgePosition _edge;
    private readonly Rectangle _virtualScreen;
    private Rectangle _selection;

    private EdgeZoneOverlayForm(EdgePosition edge)
    {
        _edge = edge;
        _virtualScreen = SystemInformation.VirtualScreen;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = _virtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = 0.18;
        Cursor = Cursors.Cross;
    }

    public Rectangle? SelectedRectangle { get; private set; }

    public static Rectangle? SelectEdgeRectangle(EdgePosition edge)
    {
        using var form = new EdgeZoneOverlayForm(edge);
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

        using var fill = new SolidBrush(Color.FromArgb(90, 80, 180, 255));
        using var border = new Pen(Color.FromArgb(230, 170, 230, 255), 2f);

        e.Graphics.FillRectangle(fill, localSelection);
        e.Graphics.DrawRectangle(border, localSelection);
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
