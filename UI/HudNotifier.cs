using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.UI;

public sealed class HudNotifier
{
    private readonly ModEntry mod;

    public HudNotifier(ModEntry mod)
    {
        this.mod = mod;
    }

    public void NotifyInfo(string text, string category = "[PR.Core]")
    {
        this.mod.Monitor.Log($"{category} {text}", LogLevel.Info);
        if (Context.IsWorldReady)
        {
            Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));
        }
    }

    public void NotifyWarn(string text, string category = "[PR.Core]")
    {
        this.mod.Monitor.Log($"{category} {text}", LogLevel.Warn);
        if (Context.IsWorldReady)
        {
            Game1.addHUDMessage(new HUDMessage(text, HUDMessage.error_type));
        }
    }
}
