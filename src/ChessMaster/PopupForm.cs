using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace ChessMaster;

internal sealed class PopupForm : ThemedForm
{
    private readonly WebView2 _web = new() { Dock = DockStyle.Fill };
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PopupForm(string? uri)
    {
        Text = string.IsNullOrWhiteSpace(uri) ? "Chess.com" : uri;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 760);
        MinimumSize = new Size(420, 520);
        ShowInTaskbar = true;
        Content.Controls.Add(_web);
        Load += OnLoadAsync;
    }

    public Task Ready => _ready.Task;

    public CoreWebView2? Core => _web.CoreWebView2;

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        try
        {
            await BrowserHost.AttachAsync(_web);
            var core = _web.CoreWebView2;
            core.DocumentTitleChanged += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(core.DocumentTitle))
                {
                    Text = core.DocumentTitle;
                }
            };
            core.WindowCloseRequested += (_, _) => Close();
            core.NewWindowRequested += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Uri))
                {
                    args.Handled = true;
                    core.Navigate(args.Uri);
                }
            };
            _ready.TrySetResult();
        }
        catch (Exception ex)
        {
            _ready.TrySetException(ex);
            MessageBox.Show(this, ex.Message, "Chess Master", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _ready.TrySetResult();
        base.OnFormClosed(e);
    }
}
