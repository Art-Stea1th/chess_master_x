using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace ChessMaster;

internal sealed class SetupForm : ThemedForm
{
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "Chess Master");

    private readonly Panel _body = new() { Dock = DockStyle.Fill, BackColor = AppTheme.Background };
    private readonly KnightMark _mark = new();
    private readonly Label _heading;
    private readonly Label _meta;
    private readonly Label _about;
    private readonly Label _folderCaption;
    private readonly Label _folder;
    private readonly MarkCheck _desktop = new() { Caption = "Ярлык на рабочем столе" };
    private readonly Label _note;
    private readonly Meter _meter = new() { Dock = DockStyle.Top, Height = 2 };
    private readonly Label _status;
    private readonly Button _command;
    private readonly System.Windows.Forms.Timer _timer;

    private Phase _phase = Phase.Ready;
    private bool _busy;
    private bool _relaunch;
    private Process? _engine;
    private string? _progressFile;
    private string? _logFile;
    private int _percent;

    public SetupForm()
        : base(showMinimize: true, showMaximize: false)
    {
        Text = "Шахматы";
        ClientSize = new Size(680, 440);
        MinimumSize = new Size(560, 400);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        _heading = MakeLabel("Шахматы", heading: true, AppTheme.TextLight);
        _meta = MakeLabel("Chess Master   ·   1.0.3", heading: false, AppTheme.TextDark);
        _about = MakeLabel(
            "Сейчас будут установлены шахматы.",
            heading: false,
            AppTheme.Text);
        _folderCaption = MakeLabel("Папка", heading: false, AppTheme.TextDark);
        _folder = MakeLabel(_directory, heading: false, AppTheme.Text);
        _folder.AutoSize = false;
        _folder.AutoEllipsis = true;
        _folder.Height = 20;
        _note = MakeLabel("", heading: false, AppTheme.TextDark);

        var rule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = AppTheme.InnerBorder };
        var row = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.Header };
        var bar = new Panel { Dock = DockStyle.Bottom, Height = 35, BackColor = AppTheme.Header };
        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.Text,
            BackColor = AppTheme.Header,
            Font = AppTheme.UiFont,
            Padding = new Padding(28, 0, 8, 0),
            AutoEllipsis = true,
            UseMnemonic = false,
        };
        _command = AppTheme.CreateCommandButton("Установить", 120);
        _command.Dock = DockStyle.Right;
        _command.Click += (_, _) => OnCommand();
        row.Controls.Add(_status);
        row.Controls.Add(_command);
        bar.Controls.Add(row);
        bar.Controls.Add(_meter);
        bar.Controls.Add(rule);

        _body.Controls.AddRange([_mark, _heading, _meta, _about, _folderCaption, _folder, _desktop, _note]);
        _body.Resize += (_, _) => LayoutBody();
        Content.Controls.Add(_body);
        Content.Controls.Add(bar);

        _timer = new System.Windows.Forms.Timer { Interval = 80 };
        _timer.Tick += (_, _) => Poll();
        KeyDown += OnKeyDown;

        if (!WebView2Installed())
        {
            _note.Text = "WebView2 Runtime не найден. Оболочка установится, страница откроется после runtime.";
        }

        Load += (_, _) =>
        {
            LayoutBody();
            RefreshRunning();
        };
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        RefreshRunning();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_phase == Phase.Installing)
        {
            e.Cancel = true;
            return;
        }

        _timer.Stop();
        base.OnFormClosing(e);
    }

    private void LayoutBody()
    {
        const int left = 28;
        var width = _body.ClientSize.Width;
        var textWidth = Math.Max(40, width - left * 2);
        _mark.SetBounds(left, 28, 56, 56);
        var textX = _mark.Right + 16;
        _heading.Location = new Point(textX, 32);
        _meta.Location = new Point(textX, 60);
        _about.MaximumSize = new Size(textWidth, 0);
        _about.Location = new Point(left, 112);
        var y = _about.Bottom + 26;
        _folderCaption.Location = new Point(left, y);
        y = _folderCaption.Bottom + 4;
        _folder.SetBounds(left, y, textWidth, 20);
        y = _folder.Bottom + 18;
        _desktop.SetBounds(left, y, textWidth, 28);
        y = _desktop.Bottom + 18;
        _note.MaximumSize = new Size(textWidth, 0);
        _note.Location = new Point(left, y);
    }

    private void OnCommand()
    {
        if (_busy)
        {
            return;
        }

        if (_phase == Phase.Done)
        {
            OpenInstalled();
            return;
        }

        StartInstall();
    }

    private void StartInstall()
    {
        _busy = true;
        _phase = Phase.Installing;
        _command.Visible = false;
        _desktop.Enabled = false;
        _status.ForeColor = AppTheme.Text;
        _status.Text = "Установка";
        _percent = 0;
        _meter.Indeterminate = true;
        _meter.Percent = 0;
        _timer.Start();

        _ = Task.Run(() =>
        {
            try
            {
                var closed = CloseRelated();
                if (!closed.Ok)
                {
                    BeginInvoke(() => Fail("Не удалось закрыть шахматы."));
                    return;
                }

                _relaunch = closed.Relaunch;

                var engine = ExtractEngine();
                var folder = Path.GetDirectoryName(engine)!;
                var progress = Path.Combine(folder, "progress.txt");
                var log = Path.Combine(folder, "install.log");
                File.WriteAllText(progress, "0");
                BeginInvoke(() => LaunchEngine(engine, progress, log));
            }
            catch (Exception ex)
            {
                BeginInvoke(() => Fail(ex.Message));
            }
        });
    }

    private void LaunchEngine(string engine, string progress, string log)
    {
        _progressFile = progress;
        _logFile = log;
        var start = new ProcessStartInfo
        {
            FileName = engine,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        start.ArgumentList.Add("/VERYSILENT");
        start.ArgumentList.Add("/SUPPRESSMSGBOXES");
        start.ArgumentList.Add("/NORESTART");
        start.ArgumentList.Add("/DIR=" + _directory);
        start.ArgumentList.Add("/PROGRESS=" + progress);
        start.ArgumentList.Add("/LOG=" + log);
        if (_desktop.Checked)
        {
            start.ArgumentList.Add("/MERGETASKS=desktopicon");
        }

        try
        {
            _engine = Process.Start(start) ?? throw new InvalidOperationException("Установщик не запустился.");
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
    }

    private void Poll()
    {
        if (_phase != Phase.Installing)
        {
            return;
        }

        _meter.Phase = unchecked(_meter.Phase + 18);
        if (_progressFile is not null)
        {
            var percent = ReadPercent(_progressFile);
            if (percent >= 0)
            {
                _percent = percent;
                _meter.Percent = percent;
                _meter.Indeterminate = percent <= 0;
            }
        }

        if (_engine is null || !_engine.HasExited)
        {
            _meter.Invalidate();
            return;
        }

        _timer.Stop();
        _busy = false;
        if (_engine.ExitCode == 0)
        {
            _phase = Phase.Done;
            _meter.Indeterminate = false;
            _meter.Percent = 100;
            if (_relaunch)
            {
                OpenInstalled();
                return;
            }

            _status.ForeColor = AppTheme.Text;
            _status.Text = "Готово";
            SetCommand("Открыть");
            return;
        }

        var detail = Tail(_logFile);
        Fail(string.IsNullOrWhiteSpace(detail) ? "Установка не завершилась." : detail);
    }

    private void Fail(string message)
    {
        _timer.Stop();
        _busy = false;
        _phase = Phase.Failed;
        _meter.Indeterminate = false;
        _desktop.Enabled = true;
        _status.ForeColor = AppTheme.CloseHover;
        _status.Text = message;
        SetCommand("Ещё раз");
    }

    private void OpenInstalled()
    {
        var exe = Path.Combine(_directory, "ChessMaster.exe");
        if (!File.Exists(exe))
        {
            _status.ForeColor = AppTheme.CloseHover;
            _status.Text = "ChessMaster.exe не найден.";
            return;
        }

        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        Close();
    }

    private void RefreshRunning()
    {
        if (_phase is Phase.Installing or Phase.Done || _busy)
        {
            return;
        }

        if (_phase == Phase.NeedClose)
        {
            _phase = Phase.Ready;
            _status.Text = "";
        }
    }

    private void SetCommand(string text)
    {
        _command.Text = text;
        var measured = TextRenderer.MeasureText(text, AppTheme.UiFont);
        _command.Width = Math.Max(108, measured.Width + 28);
        _command.Visible = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !_busy && _command.Visible)
        {
            OnCommand();
        }
        else if (e.KeyCode == Keys.Escape && _phase != Phase.Installing)
        {
            Close();
        }
    }

    private static string ExtractEngine()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChessMaster.Setup.Engine.exe");
        if (stream is null)
        {
            throw new InvalidOperationException("В оболочке нет установщика.");
        }

        var folder = Path.Combine(Path.GetTempPath(), "ChessMaster.Setup");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "engine-" + Environment.ProcessId + ".exe");
        using var file = File.Create(path);
        stream.CopyTo(file);
        return path;
    }

    private readonly record struct CloseResult(bool Ok, bool Relaunch);

    private static CloseResult CloseRelated()
    {
        var running = FindRelated();
        var relaunch = running.Any(IsChessApp);
        AskToClose(running);

        var deadline = Environment.TickCount64 + 400;
        while (Environment.TickCount64 < deadline && FindRelated().Count > 0)
        {
            Thread.Sleep(80);
        }

        AskToClose(FindRelated(), kill: true);
        Thread.Sleep(200);
        return new CloseResult(FindRelated().Count == 0, relaunch);
    }

    private static List<Process> FindRelated()
    {
        var found = new List<Process>();
        foreach (var process in Process.GetProcesses())
        {
            if (process.Id == Environment.ProcessId)
            {
                continue;
            }

            if (IsRelated(process))
            {
                found.Add(process);
            }
        }

        return found;
    }

    private static bool IsRelated(Process process)
    {
        try
        {
            if (process.ProcessName.Contains("ChessMaster", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return process.MainWindowTitle.Equals("Шахматы", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsChessApp(Process process)
    {
        try
        {
            if (process.ProcessName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return process.ProcessName.Equals("ChessMaster", StringComparison.OrdinalIgnoreCase)
                || process.MainWindowTitle.Equals("Шахматы", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void AskToClose(List<Process> processes, bool kill = false)
    {
        foreach (var process in processes)
        {
            try
            {
                if (kill)
                {
                    process.Kill(entireProcessTree: true);
                }
                else
                {
                    process.CloseMainWindow();
                }
            }
            catch (Exception)
            {
            }
        }
    }

    private static int ReadPercent(string path)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return -1;
        }

        if (bytes.Length == 0)
        {
            return -1;
        }

        string text;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }
        else if (bytes.Length >= 2 && bytes[1] == 0)
        {
            text = Encoding.Unicode.GetString(bytes);
        }
        else
        {
            text = Encoding.UTF8.GetString(bytes);
        }

        return int.TryParse(text.Trim().Trim('\0'), out var value) ? Math.Clamp(value, 0, 100) : -1;
    }

    private static string Tail(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return "";
        }

        string text;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                text = Encoding.Unicode.GetString(bytes);
            }
            else if (bytes.Length >= 2 && bytes[1] == 0)
            {
                text = Encoding.Unicode.GetString(bytes);
            }
            else
            {
                text = Encoding.UTF8.GetString(bytes);
            }
        }
        catch (IOException)
        {
            return "";
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 20); i--)
        {
            if (lines[i].Contains("error", StringComparison.OrdinalIgnoreCase) ||
                lines[i].Contains("abort", StringComparison.OrdinalIgnoreCase))
            {
                return lines[i];
            }
        }

        return lines.Length == 0 ? "" : lines[^1];
    }

    private static bool WebView2Installed()
    {
        string[] keys =
        [
            @"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
            @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}",
        ];
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var keyPath in keys)
            {
                using var key = hive.OpenSubKey(keyPath);
                if (key?.GetValue("pv") is string version && version.Length > 0 && version != "0.0.0.0")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static Label MakeLabel(string text, bool heading, Color color)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = color,
            BackColor = AppTheme.Background,
            Font = heading ? new Font("Segoe UI", 16f, FontStyle.Regular, GraphicsUnit.Point) : AppTheme.UiFont,
            UseMnemonic = false,
        };
    }

    private enum Phase
    {
        Ready,
        NeedClose,
        Installing,
        Done,
        Failed,
    }
}

internal sealed class KnightMark : Control
{
    private static readonly Image? Knight = Load();

    public KnightMark()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = AppTheme.Background;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppTheme.Background);
        if (Knight is null)
        {
            return;
        }

        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(Knight, ClientRectangle);
    }

    private static Image? Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ChessMaster.Assets.knight.png");
        if (stream is null)
        {
            return null;
        }

        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }
}

internal sealed class MarkCheck : Control
{
    public MarkCheck()
    {
        Height = 28;
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
    public string Caption { get; set; } = "";

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Checked { get; private set; } = true;

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (Enabled && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location))
        {
            Checked = !Checked;
            Invalidate();
        }

        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppTheme.Background);
        var ink = Enabled ? AppTheme.Text : AppTheme.TextDark;
        var box = new Rectangle(0, (Height - 13) / 2, 13, 13);
        using (var pen = new Pen(Checked ? AppTheme.TextLight : AppTheme.OuterBorder))
        {
            e.Graphics.DrawRectangle(pen, box);
        }

        if (Checked)
        {
            using var brush = new SolidBrush(AppTheme.TextLight);
            e.Graphics.FillRectangle(brush, box.X + 3, box.Y + 3, 7, 7);
        }

        TextRenderer.DrawText(
            e.Graphics,
            Caption,
            AppTheme.UiFont,
            new Rectangle(box.Right + 10, 0, Math.Max(0, Width - box.Right - 10), Height),
            ink,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class Meter : Control
{
    private int _percent;

    public Meter()
    {
        Height = 2;
        BackColor = AppTheme.Header;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Percent
    {
        get => _percent;
        set
        {
            _percent = value;
            Invalidate();
        }
    }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Indeterminate { get; set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Phase { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(AppTheme.Header);
        if (_percent <= 0 && !Indeterminate)
        {
            return;
        }

        using var brush = new SolidBrush(AppTheme.TextLight);
        if (Indeterminate && _percent <= 0)
        {
            var span = Math.Max(48, Width / 5);
            var travel = Math.Max(1, Width + span);
            var x = Phase % travel - span;
            e.Graphics.FillRectangle(brush, x, 0, span, Height);
            return;
        }

        var fill = (int)Math.Round(Width * (Math.Clamp(_percent, 0, 100) / 100.0));
        if (fill > 0)
        {
            e.Graphics.FillRectangle(brush, 0, 0, fill, Height);
        }
    }
}
