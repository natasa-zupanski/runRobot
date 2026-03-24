using System.Text.Json;
using runRobot.Models;

namespace runRobot.Storage;

/// <summary>
/// Persists user profiles and settings presets to %APPDATA%\runRobot\.
/// Profiles store per-runner body data (hip height).
/// Presets store named pipeline configurations.
/// </summary>
public static class AppDataStore
{
    public static readonly string DataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "runRobot");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static List<UserProfile> LoadProfiles()
        => Load<List<UserProfile>>("profiles.json") ?? [];

    public static void SaveProfiles(List<UserProfile> profiles)
        => Save("profiles.json", profiles);

    public static List<SettingsPreset> LoadPresets()
        => Load<List<SettingsPreset>>("presets.json") ?? [];

    public static void SavePresets(List<SettingsPreset> presets)
        => Save("presets.json", presets);

    private static T? Load<T>(string filename)
    {
        string path = Path.Combine(DataDirectory, filename);
        if (!File.Exists(path)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch { return default; }
    }

    private static void Save<T>(string filename, T data)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(Path.Combine(DataDirectory, filename), JsonSerializer.Serialize(data, JsonOptions));
    }
}
