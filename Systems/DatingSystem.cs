using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;
using System.Diagnostics.CodeAnalysis;

namespace PlayerRomance.Systems;

public sealed class DatingSystem
{
    private readonly ModEntry mod;

    public DatingSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool RequestDatingFromLocal(string targetToken, out string message)
    {
        if (!this.mod.TryResolvePlayerToken(targetToken, out Farmer? target))
        {
            message = $"Player '{targetToken}' not found.";
            return false;
        }

        if (target.UniqueMultiplayerID == this.mod.LocalPlayerId)
        {
            message = "You cannot propose to yourself.";
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
            this.HandleDatingProposalHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(
                MessageType.ProposalDating,
                payload,
                Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Dating request sent to {target.Name}.";
        return true;
    }

    public bool RespondToPendingDatingLocal(bool accept, out string message)
    {
        if (!this.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out RelationshipRecord? pending, out long requesterId))
        {
            message = "No pending dating request.";
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
            this.HandleDatingDecisionHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(
                MessageType.DatingDecision,
                payload,
                Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = accept
            ? $"Dating request accepted ({pending.GetOtherName(this.mod.LocalPlayerId)})."
            : $"Dating request rejected ({pending.GetOtherName(this.mod.LocalPlayerId)}).";
        return true;
    }

    public void HandleDatingProposalHost(RelationshipProposalMessage proposal, long senderId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (senderId != proposal.FromPlayerId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Proposal rejected (sender mismatch).");
            return;
        }

        Farmer? from = this.mod.FindFarmerById(proposal.FromPlayerId, includeOffline: false);
        Farmer? target = this.mod.FindFarmerById(proposal.TargetPlayerId, includeOffline: false);
        if (from is null || target is null)
        {
            this.mod.NetSync.SendError(senderId, "player_offline", "Proposal rejected (player not available).");
            return;
        }

        if (from.UniqueMultiplayerID == target.UniqueMultiplayerID)
        {
            this.mod.NetSync.SendError(senderId, "invalid_target", "You cannot propose to yourself.");
            return;
        }

        RelationshipRecord relationship = ConsentSystem.GetOrCreateRelationship(
            this.mod.HostSaveData,
            from.UniqueMultiplayerID,
            target.UniqueMultiplayerID,
            from.Name,
            target.Name);

        if (relationship.State != RelationshipState.None)
        {
            this.mod.NetSync.SendError(senderId, "already_in_relationship", "A relationship already exists for this pair.");
            return;
        }

        if (relationship.PendingDatingFrom.HasValue)
        {
            this.mod.NetSync.SendError(senderId, "pending_exists", "A dating request is already pending.");
            return;
        }

        relationship.PendingDatingFrom = from.UniqueMultiplayerID;
        relationship.LastStatusChangeDay = this.mod.GetCurrentDayNumber();

        this.mod.MarkDataDirty("Dating proposal created.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        if (target.UniqueMultiplayerID == this.mod.LocalPlayerId)
        {
            this.mod.RequestPrompts.Enqueue(
                $"dating:{from.UniqueMultiplayerID}:{target.UniqueMultiplayerID}",
                "Dating Request",
                $"{from.Name} asks you to start dating.",
                () =>
                {
                    if (!this.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out long pending) || pending != from.UniqueMultiplayerID)
                    {
                        return (false, "Dating request is no longer pending.");
                    }

                    bool accepted = this.RespondToPendingDatingLocal(true, out string msg);
                    return (accepted, msg);
                },
                () =>
                {
                    if (!this.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out long pending) || pending != from.UniqueMultiplayerID)
                    {
                        return (false, "Dating request is no longer pending.");
                    }

                    bool rejected = this.RespondToPendingDatingLocal(false, out string msg);
                    return (rejected, msg);
                },
                "[PR.System.Dating]");
        }

        this.mod.NetSync.SendToPlayer(MessageType.ProposalDating, proposal, target.UniqueMultiplayerID);

        this.mod.Monitor.Log(
            $"[PR.System.Dating] ProposalDating validated: {from.Name} ({from.UniqueMultiplayerID}) -> {target.Name} ({target.UniqueMultiplayerID}).",
            StardewModdingAPI.LogLevel.Info);
    }

    public void HandleDatingDecisionHost(RelationshipDecisionMessage decision, long senderId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (senderId != decision.ResponderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Decision rejected (sender mismatch).");
            return;
        }

        string key = ConsentSystem.GetPairKey(decision.RequesterId, decision.ResponderId);
        if (!this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relationship))
        {
            this.mod.NetSync.SendError(senderId, "missing_request", "No pending dating request found.");
            return;
        }

        bool responderIsTarget = relationship.PendingDatingFrom.HasValue
            && relationship.PendingDatingFrom.Value == decision.RequesterId
            && relationship.GetOther(decision.RequesterId) == decision.ResponderId;
        if (!responderIsTarget)
        {
            this.mod.NetSync.SendError(senderId, "invalid_request", "No matching pending dating request found.");
            return;
        }

        relationship.PendingDatingFrom = null;
        if (decision.Accepted)
        {
            relationship.State = RelationshipState.Dating;
            relationship.RelationshipStartedDay = this.mod.GetCurrentDayNumber();
            relationship.LastStatusChangeDay = this.mod.GetCurrentDayNumber();
        }
        else
        {
            relationship.RejectionsCount++;
            this.mod.HeartsSystem.AddPointsForPair(
                relationship.PairKey,
                -Math.Abs(this.mod.Config.RejectionHeartPenalty),
                "dating_rejected");
        }

        this.mod.MarkDataDirty("Dating decision applied.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.NetSync.Broadcast(MessageType.DatingDecision, decision, relationship.PlayerAId, relationship.PlayerBId);

        this.mod.Monitor.Log(
            $"[PR.Data] Relationship updated: {relationship.PairKey} -> {relationship.State}.",
            StardewModdingAPI.LogLevel.Info);
    }

    public RelationshipRecord? GetRelationship(long playerA, long playerB)
    {
        string key = ConsentSystem.GetPairKey(playerA, playerB);
        if (this.mod.IsHostPlayer)
        {
            return this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? rel) ? rel : null;
        }

        return this.mod.ClientSnapshot.Relationships.FirstOrDefault(p => p.PairKey == key);
    }

    public IEnumerable<RelationshipRecord> GetRelationshipsForPlayer(long playerId)
    {
        return this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Relationships.Values.Where(p => p.Includes(playerId))
            : this.mod.ClientSnapshot.Relationships.Where(p => p.Includes(playerId));
    }

    public bool TryGetPendingDatingForPlayer(long targetPlayerId, [NotNullWhen(true)] out RelationshipRecord? relationship, out long requesterId)
    {
        IEnumerable<RelationshipRecord> list = this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Relationships.Values
            : this.mod.ClientSnapshot.Relationships;

        foreach (RelationshipRecord relation in list)
        {
            if (!relation.PendingDatingFrom.HasValue)
            {
                continue;
            }

            long pendingFrom = relation.PendingDatingFrom.Value;
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
