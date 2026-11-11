using System.Text.Json;
using System.Text.Json.Serialization;

namespace PuzzlerGui.Services;

public sealed class AppSettings
{
    [JsonPropertyName("solverPath")]
    public string? SolverPath { get; set; }

    [JsonPropertyName("recentFiles")]
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>"System", "Light" or "Dark".</summary>
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "System";
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "puzzler-gui");
            }

            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return Path.Combine(xdg, "puzzler-gui");
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "puzzler-gui");
        }
    }

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Fall back to defaults; settings are not critical.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are not critical; ignore disk failures.
        }
    }
}
