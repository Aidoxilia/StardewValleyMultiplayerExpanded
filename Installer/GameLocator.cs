using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PlayerRomanceSetup;

internal static class GameLocator
{
    public static string? FindGameDirectory(string? explicitPath, Action<string> log)
    {
        HashSet<string> candidates = new(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string full;
            try
            {
                full = Path.GetFullPath(path.Trim().Trim('"'));
            }
            catch
            {
                return;
            }

            candidates.Add(full);
        }

        Add(explicitPath);

        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Stardew Valley"));
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Stardew Valley"));
        Add(@"C:\GOG Games\Stardew Valley");
        Add(@"C:\Program Files\GOG Galaxy\Games\Stardew Valley");
        Add(@"C:\Program Files (x86)\GOG Galaxy\Games\Stardew Valley");
        Add(@"C:\XboxGames\Stardew Valley\Content");

        foreach (string steamRoot in GetSteamRoots())
        {
            Add(Path.Combine(steamRoot, "steamapps", "common", "Stardew Valley"));
            foreach (string libraryPath in GetSteamLibraryPaths(steamRoot))
            {
                Add(Path.Combine(libraryPath, "steamapps", "common", "Stardew Valley"));
            }
        }

        foreach (string registryPath in GetRegistryInstallLocations())
        {
            Add(registryPath);
        }

        foreach (string candidate in candidates)
        {
            if (IsGameDirectory(candidate))
            {
                log($"[Setup] Detected Stardew folder: {candidate}");
                return candidate;
            }
        }

        log("[Setup] Automatic game directory detection failed.");
        return null;
    }

    public static bool HasSmapi(string gameDirectory)
    {
        return File.Exists(Path.Combine(gameDirectory, "StardewModdingAPI.exe"));
    }

    private static bool IsGameDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "Stardew Valley.exe"))
            || File.Exists(Path.Combine(path, "StardewValley.exe"))
            || File.Exists(Path.Combine(path, "StardewModdingAPI.exe"));
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        List<string> values = new();
        string[] valueNames = { "SteamPath", "InstallPath", "SourceModInstallPath" };
        foreach ((RegistryHive hive, RegistryView view) in new[]
                 {
                     (RegistryHive.CurrentUser, RegistryView.Registry64),
                     (RegistryHive.CurrentUser, RegistryView.Registry32),
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32)
                 })
        {
            try
            {
                using RegistryKey? key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(@"Software\Valve\Steam");
                if (key is null)
                {
                    continue;
                }

                foreach (string valueName in valueNames)
                {
                    if (key.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        values.Add(value);
                    }
                }
            }
            catch
            {
            }
        }

        return values;
    }

    private static IEnumerable<string> GetSteamLibraryPaths(string steamRoot)
    {
        string file = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(file))
        {
            yield break;
        }

        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch
        {
            yield break;
        }

        foreach (Match match in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            string raw = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            yield return raw.Replace("\\\\", "\\");
        }
    }

    private static IEnumerable<string> GetRegistryInstallLocations()
    {
        List<string> values = new();
        string[] uninstallKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 413150",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Steam App 413150",
            @"SOFTWARE\GOG.com\Games\1453375253",
            @"SOFTWARE\WOW6432Node\GOG.com\Games\1453375253"
        };

        foreach ((RegistryHive hive, RegistryView view) in new[]
                 {
                     (RegistryHive.CurrentUser, RegistryView.Registry64),
                     (RegistryHive.CurrentUser, RegistryView.Registry32),
                     (RegistryHive.LocalMachine, RegistryView.Registry64),
                     (RegistryHive.LocalMachine, RegistryView.Registry32)
                 })
        {
            foreach (string keyPath in uninstallKeys)
            {
                RegistryKey? key = null;
                try
                {
                    key = RegistryKey.OpenBaseKey(hive, view).OpenSubKey(keyPath);
                    if (key is null)
                    {
                        continue;
                    }

                    foreach (string valueName in new[] { "InstallLocation", "path", "Path" })
                    {
                        if (key.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                        {
                            values.Add(value);
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    key?.Dispose();
                }
            }
        }

        return values;
    }
}
