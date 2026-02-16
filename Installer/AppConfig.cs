namespace PlayerRomanceSetup;

internal static class AppConfig
{
    public const string Owner = "Aidoxilia";
    public const string Repo = "StardewValleyMultiplayerExpanded";
    public const string Branch = "main";
    public const string ModFolderName = "MultiplayerExpanded";
    public const string SetupExeName = "PlayerRomanceSetup.exe";
    public const string ShortcutName = "Stardew Valley - Multiplayer Expanded.lnk";

    public static readonly string[] RequiredRuntimeFiles =
    {
        "manifest.json",
        "PlayerRomance.dll",
        "PlayerRomance.deps.json"
    };

    public static readonly string[] OptionalRuntimeFiles =
    {
        "PlayerRomance.pdb",
        "README.md",
        "config.json"
    };
}
