using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ChessMaster;

internal sealed class MainForm : ThemedForm
{
    private const string StartUrl = "https://www.chess.com/";

    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly Button _back;

    public MainForm()
    {
        Text = "Шахматы";
        ClientSize = new Size(1440, 900);
        MinimumSize = new Size(960, 640);

        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = AppTheme.Header,
            Padding = new Padding(0),
        };
        var rule = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 1,
            BackColor = AppTheme.InnerBorder,
        };
        _back = AppTheme.CreateCommandButton("< Назад", 88);
        _back.Dock = DockStyle.Left;
        _back.Click += (_, _) => GoBack();
        bar.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && e.Clicks < 2)
            {
                BeginWindowDrag();
            }
        };
        bar.Controls.Add(rule);
        bar.Controls.Add(_back);

        var room = new RoomLight();
        room.Controls.Add(_web);
        Content.Controls.Add(room);
        Content.Controls.Add(bar);
        RestorePlacement();
        Load += OnLoadAsync;
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        try
        {
            await BrowserHost.AttachAsync(_web);
            var core = _web.CoreWebView2;
            core.HistoryChanged += (_, _) => UpdateBackButton();
            core.NavigationCompleted += (_, _) => UpdateBackButton();
            core.NewWindowRequested += OnNewWindowRequestedAsync;
            core.Navigate(StartUrl);
            UpdateBackButton();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show(
                this,
                "Не найден WebView2 Runtime. Это Chromium-движок оболочки. Установите Evergreen Runtime и откройте Chess Master снова.",
                "Chess Master",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Оболочка не смогла открыть chess.com.\n\n" + ex.Message,
                "Chess Master",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            Close();
        }
    }

    private async void OnNewWindowRequestedAsync(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            e.Handled = true;
            var popup = new PopupForm(e.Uri);
            popup.Show(this);
            await popup.Ready;
            if (popup.Core is not null)
            {
                e.NewWindow = popup.Core;
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void GoBack()
    {
        var core = _web.CoreWebView2;
        if (core is null || !core.CanGoBack)
        {
            UpdateBackButton();
            return;
        }

        core.GoBack();
    }

    private void UpdateBackButton()
    {
        var canGoBack = _web.CoreWebView2?.CanGoBack == true;
        _back.ForeColor = canGoBack ? AppTheme.Text : AppTheme.TextDark;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SavePlacement();
        base.OnFormClosing(e);
    }

    private void RestorePlacement()
    {
        if (!File.Exists(BrowserHost.WindowFile))
        {
            return;
        }

        try
        {
            var placement = JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(BrowserHost.WindowFile));
            if (placement is null || placement.Width < 640 || placement.Height < 480)
            {
                return;
            }

            var bounds = new Rectangle(placement.X, placement.Y, placement.Width, placement.Height);
            var visible = Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(bounds));
            if (!visible)
            {
                return;
            }

            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            Bounds = bounds;
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void SavePlacement()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var placement = new WindowPlacement(bounds.X, bounds.Y, bounds.Width, bounds.Height, FormWindowState.Normal);
        Directory.CreateDirectory(Path.GetDirectoryName(BrowserHost.WindowFile)!);
        File.WriteAllText(BrowserHost.WindowFile, JsonSerializer.Serialize(placement));
    }

    private sealed record WindowPlacement(int X, int Y, int Width, int Height, FormWindowState State);
}
