namespace TaskbarUnhideZoner.UI;

internal sealed class HotZoneOverlayForm : Form
{
    private bool _dragging;
    private Point _dragStart;
    private Rectangle _selection;

    private HotZoneOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        KeyPreview = true;
        DoubleBuffered = true;
        BackColor = Color.Black;
        Opacity = 0.18;
        Cursor = Cursors.Cross;
    }

    public Rectangle? SelectedRectangle { get; private set; }

    public static Rectangle? SelectRectangle()
    {
        using var form = new HotZoneOverlayForm();
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

        using var fill = new SolidBrush(Color.FromArgb(90, 80, 180, 255));
        using var border = new Pen(Color.FromArgb(230, 170, 230, 255), 2f);

        e.Graphics.FillRectangle(fill, localSelection);
        e.Graphics.DrawRectangle(border, localSelection);
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
