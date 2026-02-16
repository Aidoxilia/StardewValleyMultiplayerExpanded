namespace PlayerRomanceSetup;

internal static class ShortcutHelper
{
    public static void CreateShortcuts(string updaterExePath, string gameDirectory, Action<string> log)
    {
        string args = $"--launch --silent --game-dir \"{gameDirectory}\"";
        string icon = Path.Combine(gameDirectory, "StardewModdingAPI.exe");

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string desktopShortcut = Path.Combine(desktop, AppConfig.ShortcutName);
        CreateShortcut(desktopShortcut, updaterExePath, args, gameDirectory, icon, log);

        string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        string startShortcut = Path.Combine(programs, AppConfig.ShortcutName);
        CreateShortcut(startShortcut, updaterExePath, args, gameDirectory, icon, log);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string arguments, string workingDirectory, string iconPath, Action<string> log)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                log("[Setup] Could not create shortcut: WScript.Shell unavailable.");
                return;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = arguments;
            shortcut.WorkingDirectory = workingDirectory;
            shortcut.Description = "Launch Stardew Valley with MultiplayerExpanded auto-updater";
            if (File.Exists(iconPath))
            {
                shortcut.IconLocation = iconPath;
            }

            shortcut.Save();
            log($"[Setup] Shortcut created: {shortcutPath}");
        }
        catch (Exception ex)
        {
            log($"[Setup] Failed to create shortcut '{shortcutPath}': {ex.Message}");
        }
    }
}
