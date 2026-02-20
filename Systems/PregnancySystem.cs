using Microsoft.Xna.Framework;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buffs;
using System.Diagnostics.CodeAnalysis;

namespace PlayerRomance.Systems;

public sealed class PregnancySystem
{
    private const string SupportBuffId = "PlayerRomance.PregnancySupport";
    private readonly ModEntry mod;
    private readonly Dictionary<long, int> lastEnergyTickByPlayer = new();
    private readonly Dictionary<long, int> lastSupportBuffSecondByPlayer = new();

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
        this.EnsureV2Defaults(record);

        if (record.IsPregnant)
        {
            this.mod.NetSync.SendError(senderId, "already_pregnant", "A pregnancy is already in progress.");
            return;
        }

        if (!record.ParentAOptIn || !record.ParentBOptIn)
        {
            this.mod.NetSync.SendError(senderId, "opt_in_required", "Both parents must enable baby opt-in first.");
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

        this.EnsureV2Defaults(record);

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
            int duration = this.GetConfiguredPregnancyDurationDays();
            record.IsPregnant = true;
            record.PregnancyDurationDays = duration;
            record.CurrentPregnancyDay = 1;
            record.DaysRemaining = duration;
            record.StartedOnDay = this.mod.GetCurrentDayNumber();
            record.LastProcessedDay = this.mod.GetCurrentDayNumber();
            record.PregnantPlayerId = decision.FromPlayerId;
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
        int duration = Math.Max(1, days);

        record.ParentAOptIn = true;
        record.ParentBOptIn = true;
        record.PendingTryForBabyFrom = null;
        record.IsPregnant = true;
        record.PregnancyDurationDays = duration;
        record.CurrentPregnancyDay = 1;
        record.DaysRemaining = duration;
        record.StartedOnDay = this.mod.GetCurrentDayNumber();
        record.LastProcessedDay = this.mod.GetCurrentDayNumber();
        record.PregnantPlayerId = record.ParentAId;

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
        this.EnsureV2Defaults(record);

        ChildRecord child = this.CompleteBirth(record);

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
            this.EnsureV2Defaults(record);
            if (!record.IsPregnant || record.LastProcessedDay == day)
            {
                continue;
            }

            record.LastProcessedDay = day;
            record.DaysRemaining = Math.Max(0, record.DaysRemaining - 1);
            record.CurrentPregnancyDay = Math.Clamp(record.PregnancyDurationDays - record.DaysRemaining + 1, 1, record.PregnancyDurationDays);
            anyChanged = true;

            if (record.DaysRemaining > 0)
            {
                continue;
            }

            ChildRecord born = this.CompleteBirth(record);
            this.mod.Notifier.NotifyInfo($"A child was born: {born.ChildName}.", "[PR.System.Pregnancy]");
        }

        if (anyChanged)
        {
            this.mod.MarkDataDirty("Pregnancy day update processed.", flushNow: true);
            this.mod.NetSync.BroadcastSnapshotToAll();
        }
    }

    public void OnOneSecondUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnablePregnancy || !Context.IsWorldReady)
        {
            return;
        }

        int nowSecond = (int)Game1.currentGameTime.TotalGameTime.TotalSeconds;

        foreach (PregnancyRecord record in this.mod.HostSaveData.Pregnancies.Values)
        {
            this.EnsureV2Defaults(record);
            if (!record.IsPregnant)
            {
                continue;
            }

            Farmer? pregnant = this.mod.FindFarmerById(record.PregnantPlayerId, includeOffline: false)
                ?? this.mod.FindFarmerById(record.ParentAId, includeOffline: false)
                ?? this.mod.FindFarmerById(record.ParentBId, includeOffline: false);
            if (pregnant is null)
            {
                continue;
            }

            this.ApplyPregnancyEnergyEffects(pregnant, record, nowSecond);
        }
    }

    public bool TryGetPendingTryForBabyForPlayer(long targetPlayerId, out long requesterId)
    {
        return this.TryGetPendingTryForBaby(targetPlayerId, out _, out requesterId);
    }

    public bool TryGetActivePregnancyForPlayer(long playerId, [NotNullWhen(true)] out PregnancyRecord? record)
    {
        foreach (PregnancyRecord entry in this.GetPregnanciesForRead())
        {
            if (!entry.IsPregnant)
            {
                continue;
            }

            if (entry.PregnantPlayerId == playerId || entry.ParentAId == playerId || entry.ParentBId == playerId)
            {
                record = entry;
                return true;
            }
        }

        record = null;
        return false;
    }

    public float GetPregnancyProgress01(long playerId)
    {
        if (!this.TryGetActivePregnancyForPlayer(playerId, out PregnancyRecord? record) || record.PregnancyDurationDays <= 0)
        {
            return 0f;
        }

        int day = Math.Clamp(record.CurrentPregnancyDay, 1, record.PregnancyDurationDays);
        return Math.Clamp(day / (float)record.PregnancyDurationDays, 0f, 1f);
    }

    public string GetPregnancyStageText(long playerId)
    {
        if (!this.TryGetActivePregnancyForPlayer(playerId, out PregnancyRecord? record))
        {
            return "No active pregnancy";
        }

        if (this.IsNearBirthWindow(record))
        {
            return "Late stage (day 6/7 window)";
        }

        if (record.CurrentPregnancyDay <= 2)
        {
            return "Early stage";
        }

        return "Mid stage";
    }

    public IEnumerable<PregnancyRecord> GetPregnanciesForPlayer(long playerId)
    {
        foreach (PregnancyRecord entry in this.GetPregnanciesForRead())
        {
            if (entry.ParentAId == playerId || entry.ParentBId == playerId)
            {
                yield return entry;
            }
        }
    }

    private ChildRecord CompleteBirth(PregnancyRecord record)
    {
        record.PendingTryForBabyFrom = null;
        record.IsPregnant = false;
        record.DaysRemaining = 0;
        record.StartedOnDay = 0;
        record.LastProcessedDay = this.mod.GetCurrentDayNumber();
        record.CurrentPregnancyDay = 0;
        record.PregnantPlayerId = 0;

        ChildRecord child = this.mod.ChildGrowthSystem.CreateNewbornFromPregnancy(record);
        this.mod.NetSync.Broadcast(
            MessageType.ChildBorn,
            new ChildSyncMessage
            {
                Child = child
            },
            record.ParentAId,
            record.ParentBId);

        return child;
    }

    private void ApplyPregnancyEnergyEffects(Farmer pregnant, PregnancyRecord record, int nowSecond)
    {
        if (!this.IsEnergyTickReady(pregnant.UniqueMultiplayerID, nowSecond))
        {
            return;
        }

        bool inLateWindow = this.IsNearBirthWindow(record);
        float drain = inLateWindow ? 0.45f : 0.22f;

        long partnerId = record.ParentAId == pregnant.UniqueMultiplayerID ? record.ParentBId : record.ParentAId;
        Farmer? partner = this.mod.FindFarmerById(partnerId, includeOffline: false);
        bool supportActive = partner is not null
            && partner.currentLocation == pregnant.currentLocation
            && Vector2.Distance(partner.Tile, pregnant.Tile) <= 4f;

        if (supportActive)
        {
            drain *= 0.5f;
            this.ApplySupportBuffIcon(pregnant, inLateWindow, nowSecond);
        }

        pregnant.Stamina = Math.Max(20f, pregnant.Stamina - drain);
    }

    private void ApplySupportBuffIcon(Farmer farmer, bool lateStage, int nowSecond)
    {
        if (this.lastSupportBuffSecondByPlayer.TryGetValue(farmer.UniqueMultiplayerID, out int last)
            && nowSecond - last < 15)
        {
            return;
        }

        this.lastSupportBuffSecondByPlayer[farmer.UniqueMultiplayerID] = nowSecond;
        try
        {
            string source = lateStage ? "Partner Support (Late)" : "Partner Support";
            string description = lateStage
                ? "Partner support reduces late pregnancy fatigue."
                : "Partner support reduces pregnancy fatigue.";

            Buff iconBuff = new(
                SupportBuffId,
                source,
                source,
                4600,
                Game1.buffsIcons,
                0,
                new BuffEffects(),
                false,
                description,
                string.Empty);
            farmer.applyBuff(iconBuff);
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log($"[PR.System.Pregnancy] Failed to apply support buff icon: {ex.Message}", LogLevel.Trace);
        }
    }

    private bool IsNearBirthWindow(PregnancyRecord record)
    {
        this.EnsureV2Defaults(record);
        int threshold = Math.Max(1, record.PregnancyDurationDays - 1);
        return record.CurrentPregnancyDay >= threshold;
    }

    private bool IsEnergyTickReady(long playerId, int nowSecond)
    {
        if (!this.lastEnergyTickByPlayer.TryGetValue(playerId, out int last))
        {
            this.lastEnergyTickByPlayer[playerId] = nowSecond;
            return true;
        }

        if (nowSecond <= last)
        {
            return false;
        }

        this.lastEnergyTickByPlayer[playerId] = nowSecond;
        return true;
    }

    private int GetConfiguredPregnancyDurationDays()
    {
        if (this.mod.Config.PregnancyDurationDays > 0)
        {
            return Math.Max(2, this.mod.Config.PregnancyDurationDays);
        }

        return Math.Max(2, this.mod.Config.PregnancyDays);
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
                LastProcessedDay = 0,
                PregnantPlayerId = 0,
                PregnancyDurationDays = this.GetConfiguredPregnancyDurationDays(),
                CurrentPregnancyDay = 0
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

        this.EnsureV2Defaults(record);
        return record;
    }

    private void EnsureV2Defaults(PregnancyRecord record)
    {
        if (record.PregnancyDurationDays <= 0)
        {
            record.PregnancyDurationDays = this.GetConfiguredPregnancyDurationDays();
        }

        if (!record.IsPregnant)
        {
            record.CurrentPregnancyDay = 0;
            record.DaysRemaining = Math.Max(0, record.DaysRemaining);
            if (record.PregnantPlayerId == 0)
            {
                record.PregnantPlayerId = 0;
            }

            return;
        }

        if (record.PregnantPlayerId == 0)
        {
            record.PregnantPlayerId = record.ParentAId;
        }

        if (record.DaysRemaining <= 0)
        {
            record.DaysRemaining = Math.Max(1, record.PregnancyDurationDays - Math.Max(record.CurrentPregnancyDay - 1, 0));
        }

        if (record.CurrentPregnancyDay <= 0)
        {
            int inferred = record.PregnancyDurationDays - record.DaysRemaining + 1;
            record.CurrentPregnancyDay = Math.Clamp(inferred, 1, record.PregnancyDurationDays);
        }
    }

    private IEnumerable<PregnancyRecord> GetPregnanciesForRead()
    {
        return this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Pregnancies.Values
            : this.mod.ClientSnapshot.Pregnancies;
    }

    private bool TryGetPendingTryForBaby(long targetPlayerId, [NotNullWhen(true)] out PregnancyRecord? record, out long requesterId)
    {
        foreach (PregnancyRecord entry in this.GetPregnanciesForRead())
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
