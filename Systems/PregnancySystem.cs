using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;
using System.Diagnostics.CodeAnalysis;

namespace PlayerRomance.Systems;

public sealed class PregnancySystem
{
    private readonly ModEntry mod;

    public PregnancySystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool SetOptInFromLocal(string partnerToken, bool optIn, out string message)
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            message = "Pregnancy system is disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner))
        {
            message = $"Player '{partnerToken}' not found.";
            return false;
        }

        PregnancyOptInMessage payload = new()
        {
            PlayerId = this.mod.LocalPlayerId,
            PartnerId = partner.UniqueMultiplayerID,
            OptIn = optIn
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleOptInHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.PregnancyOptIn, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Baby opt-in set to {(optIn ? "ON" : "OFF")} with {partner.Name}.";
        return true;
    }

    public bool RequestTryForBabyFromLocal(string partnerToken, out string message)
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            message = "Pregnancy system is disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner))
        {
            message = $"Player '{partnerToken}' not found.";
            return false;
        }

        TryForBabyMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            PartnerId = partner.UniqueMultiplayerID,
            Accepted = false,
            IsDecision = false
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleTryForBabyRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.TryForBabyRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Try-for-baby request sent to {partner.Name}.";
        return true;
    }

    public bool RespondTryForBabyFromLocal(bool accept, out string message)
    {
        if (!this.TryGetPendingTryForBaby(this.mod.LocalPlayerId, out PregnancyRecord? record, out long requesterId))
        {
            message = "No pending try-for-baby request.";
            return false;
        }

        TryForBabyMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            PartnerId = requesterId,
            Accepted = accept,
            IsDecision = true
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleTryForBabyDecisionHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.TryForBabyDecision, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = accept
            ? $"Try-for-baby accepted ({record.ParentAName}/{record.ParentBName})."
            : $"Try-for-baby rejected ({record.ParentAName}/{record.ParentBName}).";
        return true;
    }

    public void HandleOptInHost(PregnancyOptInMessage message, long senderId)
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            this.mod.NetSync.SendError(senderId, "pregnancy_disabled", "Pregnancy is disabled.");
            return;
        }

        if (message.PlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Opt-in rejected (sender mismatch).");
            return;
        }

        Farmer? player = this.mod.FindFarmerById(message.PlayerId, includeOffline: false);
        Farmer? partner = this.mod.FindFarmerById(message.PartnerId, includeOffline: false);
        if (player is null || partner is null)
        {
            this.mod.NetSync.SendError(senderId, "player_offline", "Opt-in rejected (player not available).");
            return;
        }

        if (!this.mod.MarriageSystem.IsMarried(player.UniqueMultiplayerID, partner.UniqueMultiplayerID))
        {
            this.mod.NetSync.SendError(senderId, "not_married", "Couple must be married to configure pregnancy.");
            return;
        }

        PregnancyRecord record = this.GetOrCreateRecord(player, partner);
        if (record.ParentAId == player.UniqueMultiplayerID)
        {
            record.ParentAOptIn = message.OptIn;
        }
        else
        {
            record.ParentBOptIn = message.OptIn;
        }

        this.mod.MarkDataDirty("Pregnancy opt-in updated.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.NetSync.SendToPlayer(MessageType.PregnancyOptIn, message, partner.UniqueMultiplayerID);
    }

    public void HandleTryForBabyRequestHost(TryForBabyMessage request, long senderId)
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            this.mod.NetSync.SendError(senderId, "pregnancy_disabled", "Pregnancy is disabled.");
            return;
        }

        if (request.FromPlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Try-for-baby request rejected (sender mismatch).");
            return;
        }

        Farmer? from = this.mod.FindFarmerById(request.FromPlayerId, includeOffline: false);
        Farmer? partner = this.mod.FindFarmerById(request.PartnerId, includeOffline: false);
        if (from is null || partner is null)
        {
            this.mod.NetSync.SendError(senderId, "player_offline", "Try-for-baby rejected (player not available).");
            return;
        }

        if (!this.mod.MarriageSystem.IsMarried(from.UniqueMultiplayerID, partner.UniqueMultiplayerID))
        {
            this.mod.NetSync.SendError(senderId, "not_married", "Couple must be married to try for baby.");
            return;
        }

        PregnancyRecord record = this.GetOrCreateRecord(from, partner);
        if (record.IsPregnant)
        {
            this.mod.NetSync.SendError(senderId, "already_pregnant", "A pregnancy is already in progress.");
            return;
        }

        if (record.PendingTryForBabyFrom.HasValue)
        {
            this.mod.NetSync.SendError(senderId, "pending_exists", "A try-for-baby request is already pending.");
            return;
        }

        record.PendingTryForBabyFrom = from.UniqueMultiplayerID;
        this.mod.MarkDataDirty("Try-for-baby request created.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        if (partner.UniqueMultiplayerID == this.mod.LocalPlayerId)
        {
            this.mod.RequestPrompts.Enqueue(
                $"trybaby:{from.UniqueMultiplayerID}:{partner.UniqueMultiplayerID}",
                "Try For Baby",
                $"{from.Name} asks to try for baby.",
                () =>
                {
                    if (!this.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out long pending) || pending != from.UniqueMultiplayerID)
                    {
                        return (false, "Try-for-baby request is no longer pending.");
                    }

                    bool accepted = this.RespondTryForBabyFromLocal(true, out string msg);
                    return (accepted, msg);
                },
                () =>
                {
                    if (!this.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out long pending) || pending != from.UniqueMultiplayerID)
                    {
                        return (false, "Try-for-baby request is no longer pending.");
                    }

                    bool rejected = this.RespondTryForBabyFromLocal(false, out string msg);
                    return (rejected, msg);
                },
                "[PR.System.Pregnancy]");
        }

        this.mod.NetSync.SendToPlayer(MessageType.TryForBabyRequest, request, partner.UniqueMultiplayerID);
    }

    public void HandleTryForBabyDecisionHost(TryForBabyMessage decision, long senderId)
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            this.mod.NetSync.SendError(senderId, "pregnancy_disabled", "Pregnancy is disabled.");
            return;
        }

        if (decision.FromPlayerId != senderId || !decision.IsDecision)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Try-for-baby decision rejected.");
            return;
        }

        string coupleKey = ConsentSystem.GetPairKey(decision.FromPlayerId, decision.PartnerId);
        if (!this.mod.HostSaveData.Pregnancies.TryGetValue(coupleKey, out PregnancyRecord? record))
        {
            this.mod.NetSync.SendError(senderId, "missing_request", "No pending try-for-baby request found.");
            return;
        }

        bool valid = record.PendingTryForBabyFrom.HasValue
            && record.PendingTryForBabyFrom.Value == decision.PartnerId
            && (record.ParentAId == decision.FromPlayerId || record.ParentBId == decision.FromPlayerId);
        if (!valid)
        {
            this.mod.NetSync.SendError(senderId, "invalid_request", "No matching pending try-for-baby request found.");
            return;
        }

        record.PendingTryForBabyFrom = null;
        if (decision.Accepted)
        {
            record.IsPregnant = true;
            record.DaysRemaining = Math.Max(1, this.mod.Config.PregnancyDays);
            record.StartedOnDay = this.mod.GetCurrentDayNumber();
            record.LastProcessedDay = this.mod.GetCurrentDayNumber();
        }

        this.mod.MarkDataDirty("Try-for-baby decision applied.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        this.mod.NetSync.Broadcast(MessageType.TryForBabyDecision, decision, record.ParentAId, record.ParentBId);
    }

    public bool ForcePregnancyHost(long playerAId, long playerBId, int days, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can force pregnancy.";
            return false;
        }

        if (playerAId == playerBId)
        {
            message = "Cannot use the same player for both parents.";
            return false;
        }

        Farmer? playerA = this.mod.FindFarmerById(playerAId, includeOffline: true);
        Farmer? playerB = this.mod.FindFarmerById(playerBId, includeOffline: true);
        if (playerA is null || playerB is null)
        {
            message = "Both players must exist in this save.";
            return false;
        }

        PregnancyRecord record = this.GetOrCreateRecord(playerA, playerB);
        record.ParentAOptIn = true;
        record.ParentBOptIn = true;
        record.PendingTryForBabyFrom = null;
        record.IsPregnant = true;
        record.DaysRemaining = Math.Max(1, days);
        record.StartedOnDay = this.mod.GetCurrentDayNumber();
        record.LastProcessedDay = this.mod.GetCurrentDayNumber();

        this.mod.MarkDataDirty("Pregnancy force command applied.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        message = $"Forced pregnancy applied for {playerA.Name} + {playerB.Name} ({record.DaysRemaining} day(s) remaining).";
        return true;
    }

    public bool ForceBirthHost(long playerAId, long playerBId, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can force birth.";
            return false;
        }

        if (playerAId == playerBId)
        {
            message = "Cannot use the same player for both parents.";
            return false;
        }

        Farmer? playerA = this.mod.FindFarmerById(playerAId, includeOffline: true);
        Farmer? playerB = this.mod.FindFarmerById(playerBId, includeOffline: true);
        if (playerA is null || playerB is null)
        {
            message = "Both players must exist in this save.";
            return false;
        }

        PregnancyRecord record = this.GetOrCreateRecord(playerA, playerB);
        record.ParentAOptIn = true;
        record.ParentBOptIn = true;
        record.PendingTryForBabyFrom = null;
        record.IsPregnant = false;
        record.DaysRemaining = 0;
        record.StartedOnDay = 0;
        record.LastProcessedDay = this.mod.GetCurrentDayNumber();

        ChildRecord child = this.CreateChild(record);
        this.mod.HostSaveData.Children[child.ChildId] = child;

        this.mod.NetSync.Broadcast(
            MessageType.ChildBorn,
            new ChildSyncMessage
            {
                Child = child
            },
            record.ParentAId,
            record.ParentBId);
        this.mod.ChildGrowthSystem.RebuildChildrenForActiveState();
        this.mod.MarkDataDirty("Birth force command applied.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        message = $"Forced birth complete: {child.ChildName} ({playerA.Name} + {playerB.Name}).";
        return true;
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnablePregnancy)
        {
            return;
        }

        int day = this.mod.GetCurrentDayNumber();
        bool anyChanged = false;

        foreach (PregnancyRecord record in this.mod.HostSaveData.Pregnancies.Values)
        {
            if (!record.IsPregnant || record.LastProcessedDay == day)
            {
                continue;
            }

            record.LastProcessedDay = day;
            record.DaysRemaining = Math.Max(0, record.DaysRemaining - 1);
            anyChanged = true;

            if (record.DaysRemaining > 0)
            {
                continue;
            }

            ChildRecord child = this.CreateChild(record);
            this.mod.HostSaveData.Children[child.ChildId] = child;
            record.IsPregnant = false;

            this.mod.NetSync.Broadcast(
                MessageType.ChildBorn,
                new ChildSyncMessage
                {
                    Child = child
                },
                record.ParentAId,
                record.ParentBId);
            this.mod.ChildGrowthSystem.RebuildChildrenForActiveState();

            this.mod.Notifier.NotifyInfo($"A child was born: {child.ChildName}.", "[PR.System.Pregnancy]");
        }

        if (anyChanged)
        {
            this.mod.MarkDataDirty("Pregnancy day update processed.", flushNow: true);
            this.mod.NetSync.BroadcastSnapshotToAll();
        }
    }

    public bool TryGetPendingTryForBabyForPlayer(long targetPlayerId, out long requesterId)
    {
        return this.TryGetPendingTryForBaby(targetPlayerId, out _, out requesterId);
    }

    private ChildRecord CreateChild(PregnancyRecord record)
    {
        string childId = $"child_{Guid.NewGuid():N}";
        int familyChildren = this.mod.HostSaveData.Children.Values.Count(p =>
            p.ParentAId == record.ParentAId && p.ParentBId == record.ParentBId
            || p.ParentAId == record.ParentBId && p.ParentBId == record.ParentAId);

        string childName = $"Kid {record.ParentAName[..Math.Min(record.ParentAName.Length, 3)]}{record.ParentBName[..Math.Min(record.ParentBName.Length, 3)]}{familyChildren + 1}";

        return new ChildRecord
        {
            ChildId = childId,
            ChildName = childName,
            ParentAId = record.ParentAId,
            ParentAName = record.ParentAName,
            ParentBId = record.ParentBId,
            ParentBName = record.ParentBName,
            AgeYears = 0,
            AgeDays = 0,
            Stage = ChildLifeStage.Infant,
            BirthDayNumber = this.mod.GetCurrentDayNumber(),
            LastProcessedDay = this.mod.GetCurrentDayNumber(),
            IsFedToday = false,
            FeedingProgress = 0,
            AssignedTask = ChildTaskType.Auto,
            AutoMode = true,
            LastWorkedDay = -1,
            RoutineZone = "FarmHouse",
            RuntimeNpcName = $"PR_Child_{childId[..8]}",
            RuntimeNpcSpawned = false,
            VisualProfile = new ChildVisualProfile(),
            IsWorkerEnabled = true,
            AdultNpcName = $"PR_AdultChild_{childId[..8]}",
            AdultNpcSpawned = false
        };
    }

    private PregnancyRecord GetOrCreateRecord(Farmer player, Farmer partner)
    {
        string key = ConsentSystem.GetPairKey(player.UniqueMultiplayerID, partner.UniqueMultiplayerID);
        if (!this.mod.HostSaveData.Pregnancies.TryGetValue(key, out PregnancyRecord? record))
        {
            bool playerIsA = player.UniqueMultiplayerID < partner.UniqueMultiplayerID;
            record = new PregnancyRecord
            {
                CoupleKey = key,
                ParentAId = playerIsA ? player.UniqueMultiplayerID : partner.UniqueMultiplayerID,
                ParentAName = playerIsA ? player.Name : partner.Name,
                ParentBId = playerIsA ? partner.UniqueMultiplayerID : player.UniqueMultiplayerID,
                ParentBName = playerIsA ? partner.Name : player.Name,
                ParentAOptIn = false,
                ParentBOptIn = false,
                PendingTryForBabyFrom = null,
                IsPregnant = false,
                DaysRemaining = 0,
                StartedOnDay = 0,
                LastProcessedDay = 0
            };
            this.mod.HostSaveData.Pregnancies[key] = record;
        }
        else
        {
            if (record.ParentAId == player.UniqueMultiplayerID)
            {
                record.ParentAName = player.Name;
                record.ParentBName = partner.Name;
            }
            else
            {
                record.ParentAName = partner.Name;
                record.ParentBName = player.Name;
            }
        }

        return record;
    }

    private bool TryGetPendingTryForBaby(long targetPlayerId, [NotNullWhen(true)] out PregnancyRecord? record, out long requesterId)
    {
        IEnumerable<PregnancyRecord> list = this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Pregnancies.Values
            : this.mod.ClientSnapshot.Pregnancies;

        foreach (PregnancyRecord entry in list)
        {
            if (!entry.PendingTryForBabyFrom.HasValue)
            {
                continue;
            }

            long from = entry.PendingTryForBabyFrom.Value;
            long target = entry.ParentAId == from ? entry.ParentBId : entry.ParentAId;
            if (target == targetPlayerId)
            {
                record = entry;
                requesterId = from;
                return true;
            }
        }

        record = null;
        requesterId = -1;
        return false;
    }
}
