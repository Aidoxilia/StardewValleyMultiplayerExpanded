using System.Diagnostics;

namespace PlayerRomanceSetup;

internal sealed class SetupOptions
{
    public bool LaunchMode { get; set; }
    public bool Silent { get; set; }
    public bool ForceUpdate { get; set; }
    public string? GameDirectory { get; set; }

    public static SetupOptions Parse(string[] args)
    {
        SetupOptions options = new();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i].Trim();
            switch (arg.ToLowerInvariant())
            {
                case "--launch":
                    options.LaunchMode = true;
                    break;
                case "--silent":
                    options.Silent = true;
                    break;
                case "--force":
                    options.ForceUpdate = true;
                    break;
                case "--game-dir" when i + 1 < args.Length:
                    options.GameDirectory = args[++i];
                    break;
            }
        }

        return options;
    }
}

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        SetupOptions options = SetupOptions.Parse(args);

        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("This installer is Windows-only.");
            return 1;
        }

        void Log(string message)
        {
            if (!options.Silent)
            {
                Console.WriteLine(message);
            }
        }

        Log("=== Player Romance Setup ===");
        string? gameDir = GameLocator.FindGameDirectory(options.GameDirectory, Log);
        if (string.IsNullOrWhiteSpace(gameDir))
        {
            if (options.Silent)
            {
                return 2;
            }

            Console.Write("Enter Stardew Valley folder path: ");
            string? manual = Console.ReadLine();
            gameDir = GameLocator.FindGameDirectory(manual, Log);
            if (string.IsNullOrWhiteSpace(gameDir))
            {
                Console.WriteLine("Could not locate Stardew Valley folder.");
                return 2;
            }
        }

        string modsDir = Path.Combine(gameDir, "Mods");
        string modDir = Path.Combine(modsDir, AppConfig.ModFolderName);
        Directory.CreateDirectory(modDir);

        if (!options.Silent && !options.LaunchMode)
        {
            Console.WriteLine($"Game folder: {gameDir}");
            Console.Write("Install/Update MultiplayerExpanded now? [Y/n]: ");
            string? answer = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(answer) && answer.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
        }

        bool updated = await InstallOrUpdateAsync(gameDir, modDir, options, Log, CancellationToken.None);
        if (!updated)
        {
            if (!options.Silent)
            {
                Console.WriteLine("Update failed.");
            }

            return 3;
        }

        string updaterTarget = Path.Combine(modDir, AppConfig.SetupExeName);
        CopySelfTo(updaterTarget, Log);

        if (!options.LaunchMode)
        {
            ShortcutHelper.CreateShortcuts(updaterTarget, gameDir, Log);
            if (!GameLocator.HasSmapi(gameDir))
            {
                Console.WriteLine("SMAPI not detected. Install SMAPI before launching from the shortcut.");
                return 0;
            }

            if (!options.Silent)
            {
                Console.Write("Launch Stardew via SMAPI now? [Y/n]: ");
                string? answer = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(answer) && answer.Trim().StartsWith("n", StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }
            }
        }

        if (!GameLocator.HasSmapi(gameDir))
        {
            return 4;
        }

        LaunchSmapi(gameDir, Log);
        return 0;
    }

    private static async Task<bool> InstallOrUpdateAsync(
        string gameDirectory,
        string modDirectory,
        SetupOptions options,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        using HttpClient client = GitHubRuntimeSource.CreateClient();

        string? latestSha = await GitHubRuntimeSource.GetLatestCommitShaAsync(client, cancellationToken);
        InstallState? localState = InstallState.TryLoad(modDirectory);

        bool hasRuntime = AppConfig.RequiredRuntimeFiles.All(file => File.Exists(Path.Combine(modDirectory, file)));
        if (string.IsNullOrWhiteSpace(latestSha) && hasRuntime && options.LaunchMode && !options.ForceUpdate)
        {
            log("[Setup] GitHub unreachable. Starting with installed mod version.");
            return true;
        }

        bool canSkip =
            !options.ForceUpdate
            && hasRuntime
            && !string.IsNullOrWhiteSpace(latestSha)
            && localState is not null
            && string.Equals(localState.LastCommitSha, latestSha, StringComparison.OrdinalIgnoreCase);

        if (canSkip)
        {
            log("[Setup] Mod already up to date.");
            return true;
        }

        log("[Setup] Downloading latest mod runtime from GitHub...");
        bool ok = await GitHubRuntimeSource.DownloadRuntimeFilesAsync(
            client,
            modDirectory,
            preserveExistingConfig: true,
            log,
            cancellationToken);
        if (!ok)
        {
            if (hasRuntime)
            {
                log("[Setup] Update failed, but existing mod files are present. Continuing.");
                return true;
            }

            return false;
        }

        InstallState.Save(modDirectory, new InstallState
        {
            LastCommitSha = latestSha ?? string.Empty,
            LastUpdatedUtc = DateTimeOffset.UtcNow
        });

        log("[Setup] Mod install/update complete.");
        return true;
    }

    private static void CopySelfTo(string destinationPath, Action<string> log)
    {
        string? sourcePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        try
        {
            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destinationPath), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            log($"[Setup] Updater copied to: {destinationPath}");
        }
        catch (Exception ex)
        {
            log($"[Setup] Could not copy updater executable: {ex.Message}");
        }
    }

    private static void LaunchSmapi(string gameDirectory, Action<string> log)
    {
        string smapiPath = Path.Combine(gameDirectory, "StardewModdingAPI.exe");
        if (!File.Exists(smapiPath))
        {
            log("[Setup] StardewModdingAPI.exe not found.");
            return;
        }

        ProcessStartInfo psi = new()
        {
            FileName = smapiPath,
            WorkingDirectory = gameDirectory,
            UseShellExecute = true
        };
        Process.Start(psi);
        log("[Setup] SMAPI launched.");
    }
}
