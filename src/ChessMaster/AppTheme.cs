namespace ChessMaster;

internal static class AppTheme
{
    public static readonly Color Background = Color.FromArgb(7, 7, 8);
    public static readonly Color Header = Color.FromArgb(15, 15, 16);
    public static readonly Color ControlLight = Color.FromArgb(31, 31, 33);
    public static readonly Color ControlDark = Color.FromArgb(19, 19, 20);
    public static readonly Color TextLight = Color.FromArgb(223, 223, 225);
    public static readonly Color Text = Color.FromArgb(175, 175, 177);
    public static readonly Color TextDark = Color.FromArgb(127, 127, 129);
    public static readonly Color InnerBorder = Color.FromArgb(27, 27, 29);
    public static readonly Color OuterBorder = Color.FromArgb(54, 54, 58);
    public static readonly Color OuterBorderIdle = Color.FromArgb(36, 36, 40);
    public static readonly Color CloseHover = Color.FromArgb(196, 43, 28);
    public static readonly Color ClosePressed = Color.FromArgb(168, 35, 22);

    public static readonly Font UiFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);

    public static Button CreateCommandButton(string text, int width)
    {
        var measured = TextRenderer.MeasureText(text, UiFont);
        var button = new Button
        {
            Text = text,
            Width = Math.Max(width, measured.Width + 16),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Text,
            BackColor = Header,
            Font = UiFont,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0),
            TabStop = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.BorderColor = Header;
        button.FlatAppearance.MouseOverBackColor = ControlLight;
        button.FlatAppearance.MouseDownBackColor = ControlDark;
        return button;
    }
}

internal enum WindowGlyph
{
    Minimize,
    Maximize,
    Restore,
    Close,
}

internal sealed class WindowCaptionButton : Control
{
    private bool _hover;
    private bool _pressed;

    public WindowCaptionButton(WindowGlyph glyph, bool close = false)
    {
        Glyph = glyph;
        CloseAccent = close;
        Width = close ? 54 : 40;
        Dock = DockStyle.Right;
        Cursor = Cursors.Hand;
        TabStop = false;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public WindowGlyph Glyph { get; set; }

    private bool CloseAccent { get; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pressed = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var background = AppTheme.Header;
        if (CloseAccent && _pressed)
        {
            background = AppTheme.ClosePressed;
        }
        else if (CloseAccent && _hover)
        {
            background = AppTheme.CloseHover;
        }
        else if (_pressed)
        {
            background = AppTheme.ControlDark;
        }
        else if (_hover)
        {
            background = AppTheme.ControlLight;
        }
        e.Graphics.Clear(background);
        WindowGlyphs.Draw(e.Graphics, ClientRectangle, Glyph, AppTheme.TextLight);
    }
}

internal static class WindowGlyphs
{
    public static void Draw(Graphics graphics, Rectangle bounds, WindowGlyph glyph, Color color)
    {
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
        using var brush = new SolidBrush(color);
        var slot = new Rectangle(bounds.X + (bounds.Width - 10) / 2, bounds.Y + (bounds.Height - 11) / 2, 10, 11);
        var marginTop = glyph == WindowGlyph.Minimize ? 6 : 1;
        var area = new Rectangle(slot.X, slot.Y + marginTop, slot.Width, slot.Height - marginTop);
        using var path = new System.Drawing.Drawing2D.GraphicsPath(System.Drawing.Drawing2D.FillMode.Alternate);
        switch (glyph)
        {
            case WindowGlyph.Minimize:
                Add(path, "1,12 15,12 15,15 1,15");
                Fit(graphics, path, new RectangleF(1, 12, 14, 3), area, brush);
                break;
            case WindowGlyph.Maximize:
                Add(path, "25,18 23,18 23,12 11,12 11,10 25,10");
                Add(path, "21,24 7,24 7,16 21,16");
                Add(path, "27,6 9,6 9,12 5,12 5,26 23,26 23,20 27,20");
                Fit(graphics, path, new RectangleF(5, 6, 22, 20), area, brush);
                break;
            case WindowGlyph.Restore:
                Add(path, "2,4 30,4 30,27 2,27");
                Add(path, "4,10 28,10 28,25 4,25");
                Fit(graphics, path, new RectangleF(2, 4, 28, 23), area, brush);
                break;
            case WindowGlyph.Close:
                Add(path, "8,10 12,10 16,14 20,10 24,10 18,16 24,22 20,22 16,18 12,22 8,22 14,16");
                Fit(graphics, path, new RectangleF(8, 10, 16, 12), area, brush);
                break;
        }
    }

    private static void Add(System.Drawing.Drawing2D.GraphicsPath path, string points)
    {
        var list = new List<PointF>();
        foreach (var pair in points.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var xy = pair.Split(',');
            list.Add(new PointF(float.Parse(xy[0]), float.Parse(xy[1])));
        }

        path.AddPolygon(list.ToArray());
    }

    private static void Fit(Graphics graphics, System.Drawing.Drawing2D.GraphicsPath path, RectangleF source, Rectangle destination, Brush brush)
    {
        var scale = Math.Min(destination.Width / source.Width, destination.Height / source.Height);
        var drawnW = source.Width * scale;
        var drawnH = source.Height * scale;
        var originX = destination.X + (destination.Width - drawnW) / 2f;
        var originY = destination.Y + (destination.Height - drawnH) / 2f;
        var state = graphics.Save();
        graphics.TranslateTransform(originX, originY);
        graphics.ScaleTransform(scale, scale);
        graphics.TranslateTransform(-source.X, -source.Y);
        graphics.FillPath(brush, path);
        graphics.Restore(state);
    }
}
