using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;
using System.Diagnostics.CodeAnalysis;

namespace PlayerRomance.Systems;

public sealed class MarriageSystem
{
    private readonly ModEntry mod;

    public MarriageSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool RequestMarriageFromLocal(string targetToken, out string message)
    {
        if (!this.mod.Config.EnableMarriage)
        {
            message = "Marriage system is disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(targetToken, out Farmer? target))
        {
            message = $"Player '{targetToken}' not found.";
            return false;
        }

        RelationshipProposalMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            FromPlayerName = this.mod.LocalPlayerName,
            TargetPlayerId = target.UniqueMultiplayerID
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleMarriageProposalHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.ProposalMarriage, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Marriage request sent to {target.Name}.";
        return true;
    }

    public bool RespondToPendingMarriageLocal(bool accept, out string message)
    {
        if (!this.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out RelationshipRecord? relation, out long requesterId))
        {
            message = "No pending marriage request.";
            return false;
        }

        RelationshipDecisionMessage payload = new()
        {
            RequesterId = requesterId,
            ResponderId = this.mod.LocalPlayerId,
            Accepted = accept
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleMarriageDecisionHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.MarriageDecision, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = accept
            ? $"Marriage request accepted ({relation.GetOtherName(this.mod.LocalPlayerId)})."
            : $"Marriage request rejected ({relation.GetOtherName(this.mod.LocalPlayerId)}).";
        return true;
    }

    public void HandleMarriageProposalHost(RelationshipProposalMessage proposal, long senderId)
    {
        if (!this.mod.Config.EnableMarriage)
        {
            this.mod.NetSync.SendError(senderId, "marriage_disabled", "Marriage is disabled.");
            return;
        }

        if (proposal.FromPlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Marriage proposal rejected (sender mismatch).");
            return;
        }

        Farmer? from = this.mod.FindFarmerById(proposal.FromPlayerId, includeOffline: false);
        Farmer? target = this.mod.FindFarmerById(proposal.TargetPlayerId, includeOffline: false);
        if (from is null || target is null)
        {
            this.mod.NetSync.SendError(senderId, "player_offline", "Marriage proposal rejected (player not available).");
            return;
        }

        string key = ConsentSystem.GetPairKey(from.UniqueMultiplayerID, target.UniqueMultiplayerID);
        if (!this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relation))
        {
            this.mod.NetSync.SendError(senderId, "not_dating", "You must be dating before marriage.");
            return;
        }

        if (relation.State != RelationshipState.Dating)
        {
            this.mod.NetSync.SendError(senderId, "invalid_state", $"Marriage requires Dating state. Current: {relation.State}.");
            return;
        }

        int minDay = relation.RelationshipStartedDay + this.mod.Config.MarriageMinDatingDays;
        if (this.mod.GetCurrentDayNumber() < minDay)
        {
            this.mod.NetSync.SendError(senderId, "cooldown", $"Marriage is available after {this.mod.Config.MarriageMinDatingDays} dating days.");
            return;
        }

        if (relation.PendingMarriageFrom.HasValue)
        {
            this.mod.NetSync.SendError(senderId, "pending_exists", "A marriage request is already pending.");
            return;
        }

        relation.PendingMarriageFrom = from.UniqueMultiplayerID;
        relation.LastStatusChangeDay = this.mod.GetCurrentDayNumber();
        this.mod.MarkDataDirty("Marriage proposal created.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        if (target.UniqueMultiplayerID == this.mod.LocalPlayerId)
        {
            this.mod.RequestPrompts.Enqueue(
                $"marriage:{from.UniqueMultiplayerID}:{target.UniqueMultiplayerID}",
                "Marriage Request",
                $"{from.Name} asks you to get married.",
                () =>
                {
                    if (!this.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out long pending) || pending != from.UniqueMultiplayerID)
                    {
                        return (false, "Marriage request is no longer pending.");
                    }

                    bool accepted = this.RespondToPendingMarriageLocal(true, out string msg);
                    return (accepted, msg);
                },
                () =>
                {
                    if (!this.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out long pending) || pending != from.UniqueMultiplayerID)
                    {
                        return (false, "Marriage request is no longer pending.");
                    }

                    bool rejected = this.RespondToPendingMarriageLocal(false, out string msg);
                    return (rejected, msg);
                },
                "[PR.System.Marriage]");
        }

        this.mod.NetSync.SendToPlayer(MessageType.ProposalMarriage, proposal, target.UniqueMultiplayerID);

        this.mod.Monitor.Log(
            $"[PR.System.Marriage] ProposalMarriage validated: {from.Name} ({from.UniqueMultiplayerID}) -> {target.Name} ({target.UniqueMultiplayerID}).",
            StardewModdingAPI.LogLevel.Info);
    }

    public void HandleMarriageDecisionHost(RelationshipDecisionMessage decision, long senderId)
    {
        if (!this.mod.Config.EnableMarriage)
        {
            this.mod.NetSync.SendError(senderId, "marriage_disabled", "Marriage is disabled.");
            return;
        }

        if (decision.ResponderId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Marriage decision rejected (sender mismatch).");
            return;
        }

        string key = ConsentSystem.GetPairKey(decision.RequesterId, decision.ResponderId);
        if (!this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relation))
        {
            this.mod.NetSync.SendError(senderId, "missing_request", "No pending marriage request found.");
            return;
        }

        bool validRequest = relation.PendingMarriageFrom.HasValue
            && relation.PendingMarriageFrom.Value == decision.RequesterId
            && relation.GetOther(decision.RequesterId) == decision.ResponderId;
        if (!validRequest)
        {
            this.mod.NetSync.SendError(senderId, "invalid_request", "No matching pending marriage request found.");
            return;
        }

        relation.PendingMarriageFrom = null;
        if (decision.Accepted)
        {
            relation.State = RelationshipState.Engaged;
            relation.LastStatusChangeDay = this.mod.GetCurrentDayNumber();
        }
        else
        {
            relation.RejectionsCount++;
            this.mod.HeartsSystem.AddPointsForPair(
                relation.PairKey,
                -Math.Abs(this.mod.Config.RejectionHeartPenalty),
                "marriage_rejected");
        }

        this.mod.MarkDataDirty("Marriage decision applied.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.NetSync.Broadcast(MessageType.MarriageDecision, decision, relation.PlayerAId, relation.PlayerBId);

        if (decision.Accepted)
        {
            this.mod.WeddingEventController.StartWeddingHost(relation);
        }
    }

    public void CompleteMarriageAfterCeremony(string pairKey)
    {
        if (!this.mod.HostSaveData.Relationships.TryGetValue(pairKey, out RelationshipRecord? relation))
        {
            return;
        }

        relation.State = RelationshipState.Married;
        relation.LastStatusChangeDay = this.mod.GetCurrentDayNumber();
        this.mod.MarkDataDirty("Wedding complete. Couple now married.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();

        this.mod.Monitor.Log($"[PR.Data] Relationship updated: {pairKey} -> Married.", StardewModdingAPI.LogLevel.Info);
    }

    public bool IsMarried(long playerA, long playerB)
    {
        string key = ConsentSystem.GetPairKey(playerA, playerB);
        if (this.mod.IsHostPlayer && this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relation))
        {
            return relation.State == RelationshipState.Married;
        }

        RelationshipRecord? snapshotRelation = this.mod.ClientSnapshot.Relationships.FirstOrDefault(p => p.PairKey == key);
        return snapshotRelation?.State == RelationshipState.Married;
    }

    public bool TryGetPendingMarriageForPlayer(long targetPlayerId, out long requesterId)
    {
        return this.TryGetPendingMarriageForPlayer(targetPlayerId, out _, out requesterId);
    }

    private bool TryGetPendingMarriageForPlayer(long targetPlayerId, [NotNullWhen(true)] out RelationshipRecord? relationship, out long requesterId)
    {
        IEnumerable<RelationshipRecord> list = this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Relationships.Values
            : this.mod.ClientSnapshot.Relationships;

        foreach (RelationshipRecord relation in list)
        {
            if (!relation.PendingMarriageFrom.HasValue)
            {
                continue;
            }

            long pendingFrom = relation.PendingMarriageFrom.Value;
            long target = relation.GetOther(pendingFrom);
            if (target == targetPlayerId)
            {
                relationship = relation;
                requesterId = pendingFrom;
                return true;
            }
        }

        relationship = null;
        requesterId = -1;
        return false;
    }
}
