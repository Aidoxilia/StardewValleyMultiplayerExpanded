using Microsoft.Xna.Framework;
using PlayerRomance.Data;
using StardewValley;

namespace PlayerRomance.Systems;

public sealed class CoupleSynergySystem
{
    private readonly ModEntry mod;
    private readonly Random random = new();
    private readonly Dictionary<string, int> lastAuraSecondByPair = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lastRpSecondByPair = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lastCombatSecondByPair = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> lastKissDayByPair = new(StringComparer.OrdinalIgnoreCase);
    private int lastRelationshipEventDay = -1;

    public CoupleSynergySystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Reset()
    {
        this.lastAuraSecondByPair.Clear();
        this.lastRpSecondByPair.Clear();
        this.lastCombatSecondByPair.Clear();
        this.lastKissDayByPair.Clear();
        this.lastRelationshipEventDay = -1;
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (this.mod.Config.EnableCoupleSynergy && this.mod.Config.EnableWakeupCuddleBuff)
        {
            this.ApplyWakeupCuddleHost();
        }

        if (this.mod.Config.EnableRelationshipEvents)
        {
            this.TryRollRelationshipEventHost();
        }
    }

    public void OnDayEndingHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableChildEducationTraits)
        {
            return;
        }

        int boosted = 0;
        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            if (!child.IsCaredToday || !child.IsPlayedToday)
            {
                continue;
            }

            child.FeedingProgress++;
            boosted++;
        }

        if (boosted <= 0)
        {
            return;
        }

        this.mod.MarkDataDirty($"Child education trait bonus applied to {boosted} child(ren).", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.Monitor.Log($"[PR.System.Synergy] Child education bonus: +1 FeedingProgress for {boosted} child(ren).", StardewModdingAPI.LogLevel.Info);
    }

    public void OnOneSecondUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        int nowSecond = Math.Max(0, Game1.ticks / 60);
        foreach ((RelationshipRecord relation, Farmer a, Farmer b) in this.GetOnlineActivePairs())
        {
            if (relation.State == RelationshipState.None)
            {
                continue;
            }

            if (a.currentLocation != b.currentLocation)
            {
                continue;
            }

            float distance = Vector2.Distance(a.Tile, b.Tile);
            if (this.mod.Config.EnableCoupleSynergy && this.mod.Config.EnableLoveAura)
            {
                this.TryApplyLoveAuraHost(relation, a, b, distance, nowSecond);
            }

            if (this.mod.Config.EnableCoupleSynergy && this.mod.Config.EnableRegeneratorKiss)
            {
                this.TryApplyRegeneratorKissHost(relation, a, b, distance);
            }

            if (this.mod.Config.EnableRpInteractions)
            {
                this.TryApplyRpInteractionHost(relation, a, b, distance, nowSecond);
            }

            if (this.mod.Config.EnableCombatDuo)
            {
                this.TryApplyCombatDuoHost(relation, a, b, distance, nowSecond);
            }
        }
    }

    public bool RunDebugProbe(out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can run synergy probe.";
            return false;
        }

        List<(RelationshipRecord relation, Farmer a, Farmer b)> pairs = this.GetOnlineActivePairs().ToList();
        int closePairs = pairs.Count(p => p.a.currentLocation == p.b.currentLocation && Vector2.Distance(p.a.Tile, p.b.Tile) <= this.mod.Config.LoveAuraRangeTiles);

        message =
            $"pairsOnline={pairs.Count}, pairsInAuraRange={closePairs}, " +
            $"couple={this.mod.Config.EnableCoupleSynergy}, wakeup={this.mod.Config.EnableWakeupCuddleBuff}, aura={this.mod.Config.EnableLoveAura}, kiss={this.mod.Config.EnableRegeneratorKiss}, " +
            $"rp={this.mod.Config.EnableRpInteractions}, events={this.mod.Config.EnableRelationshipEvents}, combat={this.mod.Config.EnableCombatDuo}, childEdu={this.mod.Config.EnableChildEducationTraits}";
        return true;
    }

    private void ApplyWakeupCuddleHost()
    {
        float restore = Math.Max(3f, this.mod.Config.KissEnergyRestorePercent * 0.12f);
        int boostedPairs = 0;
        foreach ((RelationshipRecord relation, Farmer a, Farmer b) in this.GetOnlineActivePairs())
        {
            if (a.currentLocation != b.currentLocation || Vector2.Distance(a.Tile, b.Tile) > 2.5f)
            {
                continue;
            }

            bool aChanged = TryRestoreStamina(a, restore);
            bool bChanged = TryRestoreStamina(b, restore);
            if (!aChanged && !bChanged)
            {
                continue;
            }

            boostedPairs++;
            this.mod.HeartsSystem.AddPointsForPlayers(relation.PlayerAId, relation.PlayerBId, 2, "wakeup_cuddle");
        }

        if (boostedPairs > 0)
        {
            this.mod.Monitor.Log($"[PR.System.Synergy] Wakeup cuddle applied to {boostedPairs} pair(s).", StardewModdingAPI.LogLevel.Trace);
        }
    }

    private void TryRollRelationshipEventHost()
    {
        int day = this.mod.GetCurrentDayNumber();
        if (day <= 0 || this.lastRelationshipEventDay == day)
        {
            return;
        }

        this.lastRelationshipEventDay = day;
        List<RelationshipRecord> candidates = this.mod.HostSaveData.Relationships.Values
            .Where(p => p.State is RelationshipState.Dating or RelationshipState.Engaged or RelationshipState.Married)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        if (this.random.NextDouble() > 0.35d)
        {
            return;
        }

        RelationshipRecord picked = candidates[this.random.Next(candidates.Count)];
        this.mod.HeartsSystem.AddPointsForPlayers(picked.PlayerAId, picked.PlayerBId, 5, "relationship_event_daily");
        this.mod.Monitor.Log($"[PR.System.Synergy] Relationship event bonus triggered for pair {picked.PairKey}.", StardewModdingAPI.LogLevel.Info);
    }

    private void TryApplyLoveAuraHost(RelationshipRecord relation, Farmer a, Farmer b, float distance, int nowSecond)
    {
        if (distance > Math.Max(1, this.mod.Config.LoveAuraRangeTiles) || !this.IsCooldownReady(this.lastAuraSecondByPair, relation.PairKey, nowSecond, 1))
        {
            return;
        }

        float gain = Math.Clamp(1f - this.mod.Config.LoveAuraStaminaMultiplier, 0.05f, 2f);
        TryRestoreStamina(a, gain);
        TryRestoreStamina(b, gain);
    }

    private void TryApplyRegeneratorKissHost(RelationshipRecord relation, Farmer a, Farmer b, float distance)
    {
        if (distance > 1.15f)
        {
            return;
        }

        int day = this.mod.GetCurrentDayNumber();
        if (this.lastKissDayByPair.TryGetValue(relation.PairKey, out int lastDay) && lastDay == day)
        {
            return;
        }

        this.lastKissDayByPair[relation.PairKey] = day;
        float restore = Math.Max(2f, Math.Min(20f, a.MaxStamina * (this.mod.Config.KissEnergyRestorePercent / 100f) * 0.18f));
        bool changed = TryRestoreStamina(a, restore) | TryRestoreStamina(b, restore);
        if (!changed)
        {
            return;
        }

        this.TryPlayEmote(a, 20);
        this.TryPlayEmote(b, 20);
        this.mod.HeartsSystem.AddPointsForPlayers(relation.PlayerAId, relation.PlayerBId, 2, "regenerator_kiss");
    }

    private void TryApplyRpInteractionHost(RelationshipRecord relation, Farmer a, Farmer b, float distance, int nowSecond)
    {
        if (distance > 2.25f || !this.mod.HoldingHandsSystem.IsHandsActiveBetween(relation.PlayerAId, relation.PlayerBId))
        {
            return;
        }

        if (!this.IsCooldownReady(this.lastRpSecondByPair, relation.PairKey, nowSecond, 45))
        {
            return;
        }

        this.mod.HeartsSystem.AddPointsForPlayers(relation.PlayerAId, relation.PlayerBId, 1, "rp_interaction_hands");
    }

    private void TryApplyCombatDuoHost(RelationshipRecord relation, Farmer a, Farmer b, float distance, int nowSecond)
    {
        if (distance > 7f || !this.IsCombatLocation(a.currentLocation) || !this.IsCooldownReady(this.lastCombatSecondByPair, relation.PairKey, nowSecond, 8))
        {
            return;
        }

        TryRestoreStamina(a, 1f);
        TryRestoreStamina(b, 1f);
    }

    private IEnumerable<(RelationshipRecord relation, Farmer a, Farmer b)> GetOnlineActivePairs()
    {
        foreach (RelationshipRecord relation in this.mod.HostSaveData.Relationships.Values)
        {
            if (relation.State is not (RelationshipState.Dating or RelationshipState.Engaged or RelationshipState.Married))
            {
                continue;
            }

            Farmer? a = this.mod.FindFarmerById(relation.PlayerAId, includeOffline: false);
            Farmer? b = this.mod.FindFarmerById(relation.PlayerBId, includeOffline: false);
            if (a is null || b is null)
            {
                continue;
            }

            yield return (relation, a, b);
        }
    }

    private static bool TryRestoreStamina(Farmer farmer, float amount)
    {
        if (amount <= 0f)
        {
            return false;
        }

        float before = farmer.Stamina;
        farmer.Stamina = Math.Min(farmer.MaxStamina, farmer.Stamina + amount);
        if (farmer.Stamina > 0f && farmer.exhausted.Value)
        {
            farmer.exhausted.Value = false;
        }

        return farmer.Stamina > before;
    }

    private bool IsCooldownReady(IDictionary<string, int> map, string pairKey, int nowSecond, int cooldownSeconds)
    {
        if (map.TryGetValue(pairKey, out int lastSecond) && nowSecond - lastSecond < Math.Max(1, cooldownSeconds))
        {
            return false;
        }

        map[pairKey] = nowSecond;
        return true;
    }

    private bool IsCombatLocation(GameLocation? location)
    {
        if (location is null)
        {
            return false;
        }

        string name = location.NameOrUniqueName;
        return name.Contains("Mine", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Skull", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Volcano", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Dungeon", StringComparison.OrdinalIgnoreCase);
    }

    private void TryPlayEmote(Farmer farmer, int emoteId)
    {
        try
        {
            farmer.doEmote(emoteId);
        }
        catch
        {
        }
    }
}