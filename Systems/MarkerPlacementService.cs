using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Systems;

internal static class MarkerPlacementService
{
    private const string MarkerTileXKey = "PlayerRomance/MarkerTileX";
    private const string MarkerTileYKey = "PlayerRomance/MarkerTileY";
    private const string MarkerOwnerKey = "PlayerRomance/MarkerOwner";
    private const string MarkerNameKey = "PlayerRomance/MarkerName";
    private const string MarkerModeKey = "PlayerRomance/MarkerMode";
    private const string MarkerLastOverrideTickKey = "PlayerRomance/MarkerLastOverrideTick";

    public static void PlaceNpcOnMarker(
        ModEntry mod,
        NPC npc,
        GameLocation location,
        Vector2 markerTile,
        int facingDirection,
        bool fixedMode,
        string markerName,
        string ownerSystem)
    {
        npc.currentLocation = location;
        npc.setTileLocation(markerTile);
        npc.Position = markerTile * 64f;
        npc.FacingDirection = Math.Clamp(facingDirection, 0, 3);
        npc.Halt();
        npc.controller = null;
        npc.Speed = 0;
        if (fixedMode)
        {
            npc.movementPause = Math.Max(npc.movementPause, 999999);
        }
        else
        {
            npc.movementPause = 0;
        }

        npc.Sprite?.UpdateSourceRect();
        npc.modData[MarkerTileXKey] = markerTile.X.ToString(System.Globalization.CultureInfo.InvariantCulture);
        npc.modData[MarkerTileYKey] = markerTile.Y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        npc.modData[MarkerOwnerKey] = ownerSystem;
        npc.modData[MarkerNameKey] = markerName;
        npc.modData[MarkerModeKey] = fixedMode ? "fixed" : "free";

        mod.Monitor.Log(
            $"[PR.System.{ownerSystem}] Placement npc={npc.Name} marker={markerName} tile=({markerTile.X:0.##},{markerTile.Y:0.##}) pixel=({npc.Position.X:0.##},{npc.Position.Y:0.##}) mode={(fixedMode ? "fixed" : "free")} controller={(npc.controller is null ? "null" : npc.controller.GetType().Name)}",
            LogLevel.Info);
    }

    public static void PlacePlayerOnMarker(Farmer farmer, Vector2 markerTile, int facingDirection)
    {
        farmer.setTileLocation(markerTile);
        farmer.Position = markerTile * 64f;
        farmer.FacingDirection = Math.Clamp(facingDirection, 0, 3);
        farmer.completelyStopAnimatingOrDoingAction();
        farmer.faceDirection(farmer.FacingDirection);
        farmer.Sprite.UpdateSourceRect();
    }

    public static void EnforceFixedMarkerAnchor(ModEntry mod, NPC npc, string ownerSystem, string detectedBySystem)
    {
        // ✅ Ne pas toucher aux NPC enfants gérés par ChildGrowthSystem
        if (npc.modData.ContainsKey("PlayerRomance/ChildNpc"))
            return;

        if (!npc.modData.TryGetValue(MarkerModeKey, out string? mode)
            || !string.Equals(mode, "fixed", StringComparison.OrdinalIgnoreCase)
            || !npc.modData.TryGetValue(MarkerTileXKey, out string? xText)
            || !npc.modData.TryGetValue(MarkerTileYKey, out string? yText)
            || !float.TryParse(xText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float tx)
            || !float.TryParse(yText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ty))
        {
            return;
        }

        Vector2 anchor = new(tx, ty);
        float deltaTiles = Vector2.Distance(npc.Tile, anchor);
        if (deltaTiles <= 0.10f)
        {
            return;
        }

        int nowTick = Game1.ticks;
        int lastTick = -9999;
        if (npc.modData.TryGetValue(MarkerLastOverrideTickKey, out string? tickText))
        {
            _ = int.TryParse(tickText, out lastTick);
        }

        if (nowTick - lastTick >= 60)
        {
            string markerName = npc.modData.TryGetValue(MarkerNameKey, out string? mn) ? mn : "(unknown)";
            mod.Monitor.Log(
                $"[PR.System.{detectedBySystem}] OVERRIDE detected: owner={ownerSystem} moved {npc.Name} from ({npc.Tile.X:0.##},{npc.Tile.Y:0.##}) to marker ({anchor.X:0.##},{anchor.Y:0.##}) marker={markerName}",
                LogLevel.Warn);
        }

        npc.modData[MarkerLastOverrideTickKey] = nowTick.ToString(System.Globalization.CultureInfo.InvariantCulture);
        npc.controller = null;
        npc.Halt();
        npc.setTileLocation(anchor);
        npc.Position = anchor * 64f;
        npc.movementPause = Math.Max(npc.movementPause, 999999);
        npc.Sprite?.UpdateSourceRect();
    }
}