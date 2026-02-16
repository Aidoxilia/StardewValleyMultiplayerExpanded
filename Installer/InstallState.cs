using System.Text.Json;

namespace PlayerRomanceSetup;

internal sealed class InstallState
{
    public string LastCommitSha { get; set; } = string.Empty;
    public DateTimeOffset LastUpdatedUtc { get; set; }

    public static InstallState? TryLoad(string modFolder)
    {
        string path = GetPath(modFolder);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<InstallState>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string modFolder, InstallState state)
    {
        string path = GetPath(modFolder);
        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json);
    }

    private static string GetPath(string modFolder)
    {
        return Path.Combine(modFolder, "updater-state.json");
    }
}
