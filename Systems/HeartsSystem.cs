using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;

namespace PlayerRomance.Systems;

public sealed class HeartsSystem
{
    private readonly ModEntry mod;

    public HeartsSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool IsEnabled => this.mod.Config.EnableHeartsSystem;

    public bool TryGetRelationship(long playerAId, long playerBId, out RelationshipRecord? relationship)
    {
        relationship = this.mod.DatingSystem.GetRelationship(playerAId, playerBId);
        return relationship is not null;
    }

    public int GetHeartLevel(long playerAId, long playerBId)
    {
        if (!this.IsEnabled)
        {
            return this.mod.Config.MaxHearts;
        }

        if (!this.TryGetRelationship(playerAId, playerBId, out RelationshipRecord? relationship))
        {
            return 0;
        }

        if (relationship is null)
        {
            return 0;
        }

        return relationship.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts);
    }

    public void AddPointsForPair(string pairKey, int delta, string source)
    {
        if (!this.mod.IsHostPlayer || !this.IsEnabled)
        {
            return;
        }

        if (!this.mod.HostSaveData.Relationships.TryGetValue(pairKey, out RelationshipRecord? relation))
        {
            return;
        }

        this.ApplyDelta(relation, delta, source);
    }

    public void AddPointsForPlayers(long playerAId, long playerBId, int delta, string source)
    {
        if (!this.mod.IsHostPlayer || !this.IsEnabled)
        {
            return;
        }

        string key = ConsentSystem.GetPairKey(playerAId, playerBId);
        if (!this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relation))
        {
            return;
        }

        this.ApplyDelta(relation, delta, source);
    }

    public bool IsAtLeastHearts(long playerAId, long playerBId, int minHearts)
    {
        if (!this.IsEnabled)
        {
            return true;
        }

        if (minHearts <= 0)
        {
            return true;
        }

        return this.GetHeartLevel(playerAId, playerBId) >= minHearts;
    }

    private void ApplyDelta(RelationshipRecord relation, int delta, string source)
    {
        if (delta == 0)
        {
            return;
        }

        int maxPoints = Math.Max(1, this.mod.Config.MaxHearts * Math.Max(1, this.mod.Config.HeartPointsPerHeart));
        int oldPoints = relation.HeartPoints;
        relation.HeartPoints = Math.Clamp(relation.HeartPoints + delta, 0, maxPoints);
        relation.LastHeartChangeDay = this.mod.GetCurrentDayNumber();

        int appliedDelta = relation.HeartPoints - oldPoints;
        if (appliedDelta == 0)
        {
            return;
        }

        HeartDeltaMessage payload = new()
        {
            PairKey = relation.PairKey,
            Source = source,
            Delta = appliedDelta,
            NewPoints = relation.HeartPoints,
            NewLevel = relation.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts)
        };

        this.mod.Monitor.Log(
            $"[PR.System.Hearts] Pair={relation.PairKey} source={source} delta={appliedDelta} points={payload.NewPoints} level={payload.NewLevel}.",
            StardewModdingAPI.LogLevel.Info);
        this.mod.MarkDataDirty($"Hearts updated ({source}).", flushNow: true);
        this.mod.NetSync.Broadcast(MessageType.HeartDelta, payload, relation.PlayerAId, relation.PlayerBId);
        this.mod.NetSync.BroadcastSnapshotToAll();
    }
}
