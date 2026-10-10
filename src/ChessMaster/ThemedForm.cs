using System.Reflection;
using System.Runtime.InteropServices;

namespace ChessMaster;

internal class ThemedForm : Form
{
    private const int Grip = 6;
    private const int WmNcHitTest = 0x84;
    private const int WmNcLButtonDown = 0xA1;
    private const int HtCaption = 0x2;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmDoNotRound = 1;

    private readonly Panel _titleBar;
    private readonly TightTitle _title;
    private readonly WindowCaptionButton? _maxButton;
    private bool _active = true;
    private readonly bool _showMaximize;

    protected Panel Content { get; }

    protected ThemedForm(bool showMinimize = true, bool showMaximize = true)
    {
        _showMaximize = showMaximize;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = AppTheme.Background;
        Font = AppTheme.UiFont;
        Padding = new Padding(1);
        ForeColor = AppTheme.TextLight;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);

        Content = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AppTheme.Background,
            Margin = new Padding(0),
        };

        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = AppTheme.Header,
            Margin = new Padding(0),
        };
        _title = new TightTitle
        {
            Dock = DockStyle.Fill,
            ForeColor = AppTheme.TextLight,
            BackColor = AppTheme.Header,
            Font = AppTheme.UiFont,
        };
        var captionIcon = new CaptionIcon();
        captionIcon.MouseDown += OnCaptionMouseDown;

        if (showMinimize)
        {
            var minimize = new WindowCaptionButton(WindowGlyph.Minimize);
            minimize.Click += (_, _) => WindowState = FormWindowState.Minimized;
            _titleBar.Controls.Add(minimize);
        }

        if (showMaximize)
        {
            _maxButton = new WindowCaptionButton(WindowGlyph.Restore);
            _maxButton.Click += (_, _) => ToggleMaximize();
            _titleBar.Controls.Add(_maxButton);
        }

        var close = new WindowCaptionButton(WindowGlyph.Close, close: true);
        close.Click += (_, _) => Close();
        _titleBar.Controls.Add(close);

        _titleBar.Controls.Add(_title);
        _titleBar.Controls.Add(captionIcon);
        var rule = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = AppTheme.InnerBorder,
        };
        _titleBar.Controls.Add(rule);

        _titleBar.MouseDown += OnCaptionMouseDown;
        _title.MouseDown += OnCaptionMouseDown;

        Controls.Add(Content);
        Controls.Add(_titleBar);
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            base.Text = value ?? "";
            if (_title is not null)
            {
                _title.Text = value;
            }
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var icon = AppIcon();
        if (icon is not null)
        {
            Icon = icon;
        }
        MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
        var preference = DwmDoNotRound;
        DwmSetWindowAttribute(Handle, DwmWindowCornerPreference, ref preference, sizeof(int));
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        if (IsHandleCreated)
        {
            MaximizedBounds = Screen.FromHandle(Handle).WorkingArea;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Padding = WindowState == FormWindowState.Maximized ? Padding.Empty : new Padding(1);
        if (_maxButton is not null)
        {
            _maxButton.Glyph = WindowState == FormWindowState.Maximized
                ? WindowGlyph.Maximize
                : WindowGlyph.Restore;
            _maxButton.Invalidate();
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        _active = true;
        Invalidate();
        base.OnActivated(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        _active = false;
        Invalidate();
        base.OnDeactivate(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (WindowState == FormWindowState.Maximized)
        {
            return;
        }

        using var pen = new Pen(_active ? AppTheme.OuterBorder : AppTheme.OuterBorderIdle);
        e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest && WindowState != FormWindowState.Maximized)
        {
            var packed = m.LParam.ToInt32();
            var screen = new Point((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));
            var point = PointToClient(screen);
            var left = point.X <= Grip;
            var right = point.X >= ClientSize.Width - Grip;
            var top = point.Y <= Grip;
            var bottom = point.Y >= ClientSize.Height - Grip;

            if (top && left) { m.Result = (IntPtr)13; return; }
            if (top && right) { m.Result = (IntPtr)14; return; }
            if (bottom && left) { m.Result = (IntPtr)16; return; }
            if (bottom && right) { m.Result = (IntPtr)17; return; }
            if (left) { m.Result = (IntPtr)10; return; }
            if (right) { m.Result = (IntPtr)11; return; }
            if (top) { m.Result = (IntPtr)12; return; }
            if (bottom) { m.Result = (IntPtr)15; return; }

            if (GetChildAtPoint(point) == _titleBar)
            {
                var nested = _titleBar.GetChildAtPoint(_titleBar.PointToClient(screen));
                if (nested is WindowCaptionButton)
                {
                    base.WndProc(ref m);
                    return;
                }

                m.Result = (IntPtr)2;
                return;
            }
        }

        base.WndProc(ref m);
    }

    protected void BeginWindowDrag()
    {
        ReleaseCapture();
        SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
    }

    private void OnCaptionMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (e.Clicks >= 2)
        {
            if (_showMaximize)
            {
                ToggleMaximize();
            }

            return;
        }

        BeginWindowDrag();
    }

    private void ToggleMaximize()
    {
        if (!_showMaximize)
        {
            return;
        }

        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private static MemoryStream? _iconStream;
    private static Icon? _icon;

    private static Icon? AppIcon()
    {
        if (_icon is not null)
        {
            return _icon;
        }

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChessMaster.Assets.knight.ico");
        if (stream is null)
        {
            return null;
        }

        _iconStream = new MemoryStream();
        stream.CopyTo(_iconStream);
        _iconStream.Position = 0;
        _icon = new Icon(_iconStream);
        return _icon;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}

internal sealed class TightTitle : Label
{
    public TightTitle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            ClientRectangle,
            ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

internal sealed class CaptionIcon : Control
{
    private const int Margin = 8;
    private static readonly Image? Knight = LoadKnight();
    private static MemoryStream? _knightStream;

    public CaptionIcon()
    {
        Dock = DockStyle.Left;
        Width = 28;
        BackColor = AppTheme.Header;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (Knight is null || Height <= Margin * 2)
        {
            return;
        }

        var glyph = GlyphSize();
        var width = Margin + glyph.Width + Margin;
        if (Width != width)
        {
            Width = width;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppTheme.Header);
        if (Knight is null)
        {
            return;
        }

        var glyph = GlyphSize();
        var rect = new Rectangle(Margin, (Height - glyph.Height) / 2, glyph.Width, glyph.Height);
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(Knight, rect);
    }

    private Size GlyphSize()
    {
        var height = Math.Max(1, Height - Margin * 2);
        var width = Math.Max(1, (int)Math.Round(height * (Knight!.Width / (double)Knight.Height)));
        return new Size(width, height);
    }

    private static Image? LoadKnight()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChessMaster.Assets.knight.png");
        if (stream is null)
        {
            return null;
        }

        _knightStream = new MemoryStream();
        stream.CopyTo(_knightStream);
        _knightStream.Position = 0;
        using var source = Image.FromStream(_knightStream);
        using var full = new Bitmap(source);
        return full.Clone(OpaqueBounds(full), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
    }

    private static Rectangle OpaqueBounds(Bitmap bitmap)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = 0;
        var maxY = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 16)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX)
        {
            return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        }

        return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
    }
}
