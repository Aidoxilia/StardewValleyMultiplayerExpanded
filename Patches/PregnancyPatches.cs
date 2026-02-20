using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Patches;

public static class PregnancyPatches
{
    private static ModEntry? mod;
    private static bool initialized;
    private static readonly Dictionary<long, int> lastDrawTickByPlayer = new();

    public static void Initialize(ModEntry entry)
    {
        if (initialized)
        {
            return;
        }

        mod = entry;
        try
        {
            Harmony harmony = entry.Harmony ?? new Harmony(entry.ModManifest.UniqueID);
            foreach (var drawMethod in AccessTools.GetDeclaredMethods(typeof(FarmerRenderer)).Where(p => p.Name == nameof(FarmerRenderer.draw)))
            {
                harmony.Patch(
                    drawMethod,
                    postfix: new HarmonyMethod(typeof(PregnancyPatches), nameof(FarmerRenderer_Draw_Postfix)));
            }

            initialized = true;
        }
        catch (Exception ex)
        {
            entry.Monitor.Log($"[PR.Patch.Pregnancy] Failed to apply belly overlay patches: {ex.Message}", LogLevel.Error);
        }
    }

    private static void FarmerRenderer_Draw_Postfix(object[] __args)
    {
        if (mod is null || !Context.IsWorldReady || !mod.Config.EnablePregnancy)
        {
            return;
        }

        SpriteBatch? spriteBatch = __args.OfType<SpriteBatch>().FirstOrDefault();
        Farmer? farmer = __args.OfType<Farmer>().FirstOrDefault();
        if (spriteBatch is null || farmer is null)
        {
            return;
        }

        if (farmer.currentLocation != Game1.currentLocation)
        {
            return;
        }

        if (!mod.PregnancySystem.TryGetActivePregnancyForPlayer(farmer.UniqueMultiplayerID, out _))
        {
            return;
        }

        int tick = Game1.ticks;
        if (lastDrawTickByPlayer.TryGetValue(farmer.UniqueMultiplayerID, out int lastTick) && lastTick == tick)
        {
            return;
        }

        lastDrawTickByPlayer[farmer.UniqueMultiplayerID] = tick;

        float progress = mod.PregnancySystem.GetPregnancyProgress01(farmer.UniqueMultiplayerID);
        if (progress < 0.15f)
        {
            return;
        }

        Vector2 basePos = farmer.getLocalPosition(Game1.viewport);
        int width = (int)MathHelper.Lerp(10f, 20f, progress);
        int height = (int)MathHelper.Lerp(6f, 13f, progress);

        Rectangle belly = new(
            (int)basePos.X + 32 - width / 2,
            (int)basePos.Y + 38,
            width,
            height);

        Color baseTint = Color.White * MathHelper.Lerp(0.15f, 0.30f, progress);
        Color highlightTint = Color.White * MathHelper.Lerp(0.10f, 0.18f, progress);

        spriteBatch.Draw(Game1.staminaRect, belly, baseTint);
        spriteBatch.Draw(Game1.staminaRect, new Rectangle(belly.X + 2, belly.Y + 1, Math.Max(2, belly.Width - 4), Math.Max(2, belly.Height / 2)), highlightTint);
    }
}
