namespace ChessMaster;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\ChessMaster.Setup", out var created);
        if (!created)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new SetupForm());
        GC.KeepAlive(mutex);
    }
}
