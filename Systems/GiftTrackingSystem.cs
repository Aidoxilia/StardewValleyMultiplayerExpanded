using PlayerRomance.Data;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Systems;

public sealed class GiftTrackingSystem
{
    private readonly ModEntry mod;
    private readonly Dictionary<string, DateTime> recentEventByKey = new(StringComparer.OrdinalIgnoreCase);
    private DateTime lastDebugLogUtc = DateTime.MinValue;

    public GiftTrackingSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool TryProcessGift(Farmer giver, Farmer receiver, Item item, int count, string source, out string result)
    {
        result = string.Empty;
        if (!this.mod.Config.EnableGiftDetection)
        {
            return false;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.LogThrottled("[PR.System.Gift] Gift observed on non-host; ignored by design.");
            return false;
        }

        if (giver.UniqueMultiplayerID == receiver.UniqueMultiplayerID)
        {
            return false;
        }

        RelationshipRecord? relationship = this.mod.DatingSystem.GetRelationship(giver.UniqueMultiplayerID, receiver.UniqueMultiplayerID);
        if (relationship is null || relationship.State == RelationshipState.None)
        {
            return false;
        }

        string itemId = item.QualifiedItemId;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        int quantity = Math.Max(1, count);
        string dedupe = $"{giver.UniqueMultiplayerID}>{receiver.UniqueMultiplayerID}:{itemId}:{quantity}:{Game1.ticks}:{Game1.dayOfMonth}";
        if (this.IsDuplicate(dedupe))
        {
            return false;
        }

        GiftProgressRecord progress = this.GetOrCreateProgress(relationship);
        progress.LastGiftItemId = itemId;
        progress.LastGiftItemName = item.DisplayName;
        progress.LastGiftCount = quantity;
        progress.LastFromPlayerId = giver.UniqueMultiplayerID;
        progress.LastToPlayerId = receiver.UniqueMultiplayerID;
        progress.LastGiftDay = this.mod.GetCurrentDayNumber();
        progress.LastSource = source;
        progress.LastObservedUtc = DateTime.UtcNow;

        progress.BaselineCount += quantity;

        bool dateGift = this.mod.DateVendorShopService.IsDateGiftItem(itemId);
        if (dateGift)
        {
            progress.DateGiftCount += quantity;
        }

        if (this.IsFavoriteGift(receiver, itemId))
        {
            progress.FavoriteGiftCount += quantity;
        }

        int pointsAwarded = this.ApplyMilestonePoints(giver, receiver, relationship, progress);
        this.mod.MarkDataDirty("Gift counters updated.", flushNow: true);

        int heartsWhole = pointsAwarded / Math.Max(1, this.mod.Config.HeartPointsPerHeart);
        if (this.mod.Config.EnableGiftHudMessages && heartsWhole > 0)
        {
            Game1.addHUDMessage(new HUDMessage($"Gift bond strengthened (+{heartsWhole} heart{(heartsWhole > 1 ? "s" : string.Empty)})", HUDMessage.newQuest_type));
        }

        result = $"gift pair={relationship.PairKey} item={itemId} qty={quantity} points={pointsAwarded}";
        this.LogThrottled($"[PR.System.Gift] {result}");
        return pointsAwarded > 0;
    }

    public bool ResetCountersHost(long playerAId, long playerBId, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can reset gift counters.";
            return false;
        }

        string key = ConsentSystem.GetPairKey(playerAId, playerBId);
        if (!this.mod.HostSaveData.GiftProgressByPair.Remove(key))
        {
            message = "No counters found for that pair.";
            return false;
        }

        this.mod.MarkDataDirty("Gift counters reset.", flushNow: true);
        message = $"Gift counters reset for pair {key}.";
        return true;
    }

    public bool AddBaselineCountHost(long playerAId, long playerBId, int addCount, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can modify gift counters.";
            return false;
        }

        if (addCount <= 0)
        {
            message = "Count must be > 0.";
            return false;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(playerAId, playerBId);
        if (relation is null)
        {
            message = "Relationship not found for this pair.";
            return false;
        }

        GiftProgressRecord progress = this.GetOrCreateProgress(relation);
        progress.BaselineCount += addCount;
        int points = this.ApplyMilestonePoints(null, null, relation, progress);

        this.mod.MarkDataDirty("Gift counters debug add.", flushNow: true);
        message = $"Added {addCount} gifts to pair {relation.PairKey}. Points awarded: {points}.";
        return true;
    }

    public string DumpLastGiftEvent()
    {
        GiftProgressRecord? latest = this.mod.HostSaveData.GiftProgressByPair.Values
            .OrderByDescending(p => p.LastObservedUtc)
            .FirstOrDefault();

        if (latest is null || latest.LastObservedUtc == default)
        {
            return "No gift event recorded yet.";
        }

        return $"pair={latest.PairKey} from={latest.LastFromPlayerId} to={latest.LastToPlayerId} item={latest.LastGiftItemId} ({latest.LastGiftItemName}) qty={latest.LastGiftCount} day={latest.LastGiftDay} baseline={latest.BaselineCount} date={latest.DateGiftCount} favorite={latest.FavoriteGiftCount}";
    }

    private int ApplyMilestonePoints(Farmer? giver, Farmer? receiver, RelationshipRecord relation, GiftProgressRecord progress)
    {
        int pointsPerHeart = Math.Max(1, this.mod.Config.HeartPointsPerHeart);

        int baselineThreshold = Math.Max(1, this.mod.Config.GiftBaselineThreshold);
        int baselineMilestones = progress.BaselineCount / baselineThreshold;
        int baselineDelta = Math.Max(0, baselineMilestones - progress.BaselineMilestones);
        progress.BaselineMilestones = baselineMilestones;

        int dateThreshold = Math.Max(1, this.mod.Config.GiftDateThreshold);
        int dateMilestones = progress.DateGiftCount / dateThreshold;
        int dateDelta = Math.Max(0, dateMilestones - progress.DateMilestones);
        progress.DateMilestones = dateMilestones;

        int favThreshold = Math.Max(1, this.mod.Config.GiftFavoriteThreshold);
        int favMilestones = progress.FavoriteGiftCount / favThreshold;
        int favDelta = Math.Max(0, favMilestones - progress.FavoriteMilestones);
        progress.FavoriteMilestones = favMilestones;

        int totalPoints = (baselineDelta * (pointsPerHeart / 2))
            + (dateDelta * pointsPerHeart)
            + (favDelta * pointsPerHeart * 2);

        if (totalPoints <= 0)
        {
            return 0;
        }

        this.mod.HeartsSystem.AddPointsForPlayers(relation.PlayerAId, relation.PlayerBId, totalPoints, "vanilla_gift");
        return totalPoints;
    }

    private GiftProgressRecord GetOrCreateProgress(RelationshipRecord relation)
    {
        if (!this.mod.HostSaveData.GiftProgressByPair.TryGetValue(relation.PairKey, out GiftProgressRecord? record))
        {
            record = new GiftProgressRecord
            {
                PairKey = relation.PairKey,
                PlayerAId = relation.PlayerAId,
                PlayerBId = relation.PlayerBId
            };
            this.mod.HostSaveData.GiftProgressByPair[relation.PairKey] = record;
        }

        return record;
    }

    private bool IsDuplicate(string eventKey)
    {
        DateTime now = DateTime.UtcNow;
        if (this.recentEventByKey.TryGetValue(eventKey, out DateTime previous)
            && now - previous < TimeSpan.FromSeconds(1))
        {
            return true;
        }

        this.recentEventByKey[eventKey] = now;
        if (this.recentEventByKey.Count > 256)
        {
            DateTime cutoff = now - TimeSpan.FromSeconds(15);
            foreach (string key in this.recentEventByKey.Where(p => p.Value < cutoff).Select(p => p.Key).ToList())
            {
                this.recentEventByKey.Remove(key);
            }
        }

        return false;
    }

    private bool IsFavoriteGift(Farmer receiver, string qualifiedItemId)
    {
        bool CheckList(string key)
        {
            if (!this.mod.Config.FavoriteGiftItemIdsByPlayer.TryGetValue(key, out List<string>? list) || list.Count == 0)
            {
                return false;
            }

            return list.Select(NormalizeQualifiedId)
                .Any(id => string.Equals(id, qualifiedItemId, StringComparison.OrdinalIgnoreCase));
        }

        return CheckList(receiver.UniqueMultiplayerID.ToString()) || CheckList(receiver.Name);
    }

    private void LogThrottled(string message)
    {
        DateTime now = DateTime.UtcNow;
        if (now - this.lastDebugLogUtc < TimeSpan.FromMilliseconds(900))
        {
            return;
        }

        this.lastDebugLogUtc = now;
        this.mod.Monitor.Log(message, LogLevel.Trace);
    }

    private static string NormalizeQualifiedId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string id = raw.Trim();
        if (id.StartsWith("(", StringComparison.OrdinalIgnoreCase))
        {
            return id;
        }

        if (int.TryParse(id, out int objectId))
        {
            return $"(O){objectId}";
        }

        return id;
    }
}
