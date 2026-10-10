using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChessMaster;

internal static class Program
{
    private const string MutexName = @"Local\ChessMaster.Shell";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            FocusExistingWindow();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    private static void FocusExistingWindow()
    {
        var currentId = Environment.ProcessId;
        foreach (var process in Process.GetProcessesByName("ChessMaster"))
        {
            if (process.Id == currentId)
            {
                continue;
            }

            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                continue;
            }

            ShowWindow(handle, SwRestore);
            SetForegroundWindow(handle);
            return;
        }
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
