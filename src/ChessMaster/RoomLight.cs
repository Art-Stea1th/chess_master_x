using System.Drawing.Drawing2D;

namespace ChessMaster;

internal sealed class RoomLight : Panel
{
    private readonly System.Windows.Forms.Timer _clock;
    private float _time;

    public RoomLight()
    {
        Dock = DockStyle.Fill;
        Margin = new Padding(0);
        BackColor = AppTheme.Background;
        Padding = new Padding(18, 8, 18, 56);
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _clock = new System.Windows.Forms.Timer { Interval = 32 };
        _clock.Tick += (_, _) =>
        {
            _time += 0.032f;
            Invalidate();
        };
        HandleCreated += (_, _) => _clock.Start();
        HandleDestroyed += (_, _) => _clock.Stop();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(AppTheme.Background);
        if (Width < 40 || Height < 40)
        {
            return;
        }

        var well = new Rectangle(
            Padding.Left,
            Padding.Top,
            Math.Max(1, Width - Padding.Horizontal),
            Math.Max(1, Height - Padding.Vertical));
        var angle = _time * 0.42f;
        var lamp = new PointF(
            Width * (0.5f + 0.36f * MathF.Cos(angle)),
            well.Bottom + (Height - well.Bottom) * (0.55f + 0.12f * MathF.Sin(angle * 0.8f)));

        PaintLamp(g, lamp);
        PaintShelf(g, well, lamp);
        PaintContact(g, well);
    }

    private void PaintLamp(Graphics g, PointF lamp)
    {
        float radius = Math.Max(Width, Height) * 0.72f;
        var bounds = new RectangleF(lamp.X - radius, lamp.Y - radius, radius * 2f, radius * 2f);
        using var path = new GraphicsPath();
        path.AddEllipse(bounds);
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = lamp,
            CenterColor = Color.FromArgb(42, 168, 142, 96),
        };
        brush.SurroundColors = [Color.FromArgb(0, 7, 7, 8)];
        g.FillEllipse(brush, bounds);
    }

    private void PaintShelf(Graphics g, Rectangle well, PointF lamp)
    {
        var shelf = new Rectangle(0, well.Bottom, Width, Math.Max(1, Height - well.Bottom));
        if (shelf.Height <= 1)
        {
            return;
        }

        using var plane = new LinearGradientBrush(
            shelf,
            Color.FromArgb(18, 0, 0, 0),
            Color.FromArgb(34, 46, 40, 30),
            LinearGradientMode.Vertical);
        g.FillRectangle(plane, shelf);

        float reach = Math.Clamp(shelf.Width * 0.22f, 120f, 280f);
        float left = Math.Clamp(lamp.X - reach / 2f, well.Left, well.Right - reach);
        var gleam = new RectangleF(left, well.Bottom + 1, reach, Math.Max(2, shelf.Height - 8));
        using var gleamPath = new GraphicsPath();
        gleamPath.AddEllipse(gleam);
        using var gleamBrush = new PathGradientBrush(gleamPath)
        {
            CenterColor = Color.FromArgb(90, 214, 186, 132),
        };
        gleamBrush.SurroundColors = [Color.FromArgb(0, 214, 186, 132)];
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(gleamBrush, gleam);
        g.SmoothingMode = SmoothingMode.None;

        using var edge = new Pen(Color.FromArgb(70, 186, 164, 118));
        g.DrawLine(edge, well.Left, well.Bottom, well.Right, well.Bottom);
    }

    private static void PaintContact(Graphics g, Rectangle well)
    {
        var left = new Rectangle(0, well.Top, Math.Max(1, well.Left), well.Height);
        var right = new Rectangle(well.Right, well.Top, Math.Max(1, left.Width), well.Height);
        if (left.Width > 1)
        {
            using var shade = new LinearGradientBrush(
                left,
                Color.FromArgb(0, 0, 0, 0),
                Color.FromArgb(70, 0, 0, 0),
                LinearGradientMode.Horizontal);
            g.FillRectangle(shade, left);
        }

        if (right.Width > 1)
        {
            using var shade = new LinearGradientBrush(
                right,
                Color.FromArgb(70, 0, 0, 0),
                Color.FromArgb(0, 0, 0, 0),
                LinearGradientMode.Horizontal);
            g.FillRectangle(shade, right);
        }
    }
}
