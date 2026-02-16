using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;

namespace PlayerRomance.Systems;

public sealed class ChildGrowthSystem
{
    private readonly ModEntry mod;
    private const int ChildStageAge = 14;
    private const int TeenStageAge = 56;
    private const int AdultStageAge = 112;

    public ChildGrowthSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildGrowth)
        {
            return;
        }

        int day = this.mod.GetCurrentDayNumber();
        bool anyChanged = false;

        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            if (child.LastProcessedDay == day)
            {
                continue;
            }

            child.LastProcessedDay = day;
            child.AgeDays++;
            ChildLifeStage previousStage = child.Stage;
            child.Stage = this.GetStageForAge(child.AgeDays);

            if (child.Stage != previousStage)
            {
                anyChanged = true;
                this.mod.NetSync.Broadcast(
                    MessageType.ChildGrewUp,
                    new ChildSyncMessage
                    {
                        Child = child
                    });
            }

            if (child.Stage == ChildLifeStage.Adult && !child.AdultNpcSpawned)
            {
                this.TrySpawnAdultNpc(child);
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            this.mod.MarkDataDirty("Child growth progression updated.", flushNow: true);
            this.mod.NetSync.BroadcastSnapshotToAll();
        }
    }

    public void RebuildAdultChildrenForActiveState()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildGrowth)
        {
            return;
        }

        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            if (child.Stage == ChildLifeStage.Adult)
            {
                this.TrySpawnAdultNpc(child);
            }
        }
    }

    public bool DebugAgeChild(string token, int days, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can age children.";
            return false;
        }

        ChildRecord? child = this.mod.HostSaveData.Children.Values.FirstOrDefault(c =>
            c.ChildId.Equals(token, StringComparison.OrdinalIgnoreCase)
            || c.ChildName.Equals(token, StringComparison.OrdinalIgnoreCase));
        if (child is null)
        {
            message = $"Child '{token}' not found.";
            return false;
        }

        child.AgeDays = Math.Max(0, child.AgeDays + days);
        child.Stage = this.GetStageForAge(child.AgeDays);
        if (child.Stage == ChildLifeStage.Adult)
        {
            this.TrySpawnAdultNpc(child);
        }

        this.mod.MarkDataDirty($"Debug age changed for child {child.ChildId}.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        message = $"{child.ChildName} age is now {child.AgeDays} days ({child.Stage}).";
        return true;
    }

    private ChildLifeStage GetStageForAge(int ageDays)
    {
        if (ageDays < ChildStageAge)
        {
            return ChildLifeStage.Infant;
        }

        if (ageDays < TeenStageAge)
        {
            return ChildLifeStage.Child;
        }

        if (ageDays < AdultStageAge)
        {
            return ChildLifeStage.Teen;
        }

        return ChildLifeStage.Adult;
    }

    private void TrySpawnAdultNpc(ChildRecord child)
    {
        if (!this.mod.Config.EnableChildGrowth || !this.mod.IsHostPlayer)
        {
            return;
        }

        Farm? farm = Game1.getFarm();
        if (farm is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(child.AdultNpcName))
        {
            child.AdultNpcName = $"PR_AdultChild_{child.ChildId[..Math.Min(8, child.ChildId.Length)]}";
        }

        NPC? existing = farm.getCharacterFromName(child.AdultNpcName);
        if (existing is not null)
        {
            child.AdultNpcSpawned = true;
            return;
        }

        int slot = Math.Abs(child.ChildId.GetHashCode()) % 6;
        int tileX = 63 + slot;
        int tileY = 15;

        Texture2D texture = Game1.content.Load<Texture2D>("Characters\\Abigail");
        AnimatedSprite sprite = new("Characters\\Abigail", 0, 16, 32);
        NPC npc = new(
            sprite,
            new Vector2(tileX * 64, tileY * 64),
            "Farm",
            2,
            child.AdultNpcName,
            false,
            texture);

        npc.modData[$"{this.mod.ModManifest.UniqueID}/childId"] = child.ChildId;
        npc.modData[$"{this.mod.ModManifest.UniqueID}/displayName"] = child.ChildName;
        farm.addCharacter(npc);

        child.AdultNpcSpawned = true;
        this.mod.Monitor.Log(
            $"[PR.System.ChildGrowth] Spawned adult child NPC '{child.AdultNpcName}' for {child.ChildName}.",
            StardewModdingAPI.LogLevel.Trace);
    }
}
