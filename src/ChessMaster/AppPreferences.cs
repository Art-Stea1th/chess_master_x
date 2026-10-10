using System.Text.Json;

namespace ChessMaster;

internal static class AppPreferences
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ChessMaster",
        "settings.json");

    public static bool FlatStyle { get; private set; } = true;

    static AppPreferences()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return;
            }

            var settings = JsonSerializer.Deserialize<ShellSettings>(File.ReadAllText(FilePath));
            if (settings is not null)
            {
                FlatStyle = settings.FlatStyle;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
        }
    }

    public static void SetFlatStyle(bool value)
    {
        if (FlatStyle == value)
        {
            return;
        }

        FlatStyle = value;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(new ShellSettings { FlatStyle = value }));
        }
        catch (IOException)
        {
        }
    }

    private sealed class ShellSettings
    {
        public bool FlatStyle { get; set; }
    }
}
