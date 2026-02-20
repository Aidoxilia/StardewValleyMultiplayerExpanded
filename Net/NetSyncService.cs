using PlayerRomance.Data;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Net;

public sealed class NetSyncService
{
    private readonly ModEntry mod;
    private DateTime lastSnapshotAppliedUtc = DateTime.UtcNow;
    private DateTime lastSnapshotRequestUtc = DateTime.MinValue;

    public NetSyncService(ModEntry mod)
    {
        this.mod = mod;
    }

    public void RequestSnapshotFromHost()
    {
        if (!Context.IsMultiplayer || this.mod.IsHostPlayer || !Context.IsWorldReady)
        {
            return;
        }

        this.lastSnapshotRequestUtc = DateTime.UtcNow;

        this.mod.Helper.Multiplayer.SendMessage(
            new SnapshotRequestMessage
            {
                PlayerId = this.mod.LocalPlayerId
            },
            MessageType.RequestSnapshot.ToString(),
            new[] { this.mod.ModManifest.UniqueID },
            new[] { Game1.MasterPlayer.UniqueMultiplayerID });
    }

    public void BroadcastSnapshotToAll()
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        SnapshotMessage packet = new()
        {
            Snapshot = this.BuildSnapshot()
        };

        this.mod.Helper.Multiplayer.SendMessage(
            packet,
            MessageType.Snapshot.ToString(),
            new[] { this.mod.ModManifest.UniqueID });
    }

    public void SendSnapshotTo(long playerId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        SnapshotMessage packet = new()
        {
            Snapshot = this.BuildSnapshot()
        };

        this.mod.Helper.Multiplayer.SendMessage(
            packet,
            MessageType.Snapshot.ToString(),
            new[] { this.mod.ModManifest.UniqueID },
            new[] { playerId });
    }

    public void ApplySnapshot(NetSnapshot snapshot)
    {
        this.mod.ClientSnapshot = snapshot ?? new NetSnapshot();
        this.lastSnapshotAppliedUtc = DateTime.UtcNow;
        this.mod.CarrySystem.ApplySnapshot(this.mod.ClientSnapshot);
        this.mod.HoldingHandsSystem.ApplySnapshot(this.mod.ClientSnapshot);
        this.mod.DateImmersionSystem.ApplySnapshot(this.mod.ClientSnapshot);
        this.mod.Monitor.Log(
            $"[PR.Net] Snapshot applied: {this.mod.ClientSnapshot.Relationships.Count} relationships, {this.mod.ClientSnapshot.Children.Count} children.",
            LogLevel.Trace);
    }

    public void ClientWatchdogTick()
    {
        if (!Context.IsWorldReady || !Context.IsMultiplayer || this.mod.IsHostPlayer)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (now - this.lastSnapshotAppliedUtc < TimeSpan.FromSeconds(16))
        {
            return;
        }

        if (now - this.lastSnapshotRequestUtc < TimeSpan.FromSeconds(8))
        {
            return;
        }

        this.mod.Monitor.Log("[PR.Net] Snapshot watchdog triggered a resync request.", LogLevel.Trace);
        this.RequestSnapshotFromHost();
    }

    public void SendToPlayer<T>(MessageType type, T payload, long playerId)
        where T : class
    {
        this.mod.Helper.Multiplayer.SendMessage(
            payload,
            type.ToString(),
            new[] { this.mod.ModManifest.UniqueID },
            new[] { playerId });
    }

    public void Broadcast<T>(MessageType type, T payload, params long[] playerIds)
        where T : class
    {
        this.mod.Helper.Multiplayer.SendMessage(
            payload,
            type.ToString(),
            new[] { this.mod.ModManifest.UniqueID },
            playerIds.Length > 0 ? playerIds : null);
    }

    public NetSnapshot BuildSnapshot()
    {
        if (this.mod.IsHostPlayer)
        {
            NetSnapshot snapshot = new()
            {
                DayNumber = this.mod.GetCurrentDayNumber(),
                LastFarmWorkReport = this.mod.LastFarmWorkReport
            };

            foreach (RelationshipRecord entry in this.mod.HostSaveData.Relationships.Values)
            {
                snapshot.Relationships.Add(Clone(entry));
            }

            foreach (PregnancyRecord entry in this.mod.HostSaveData.Pregnancies.Values)
            {
                snapshot.Pregnancies.Add(Clone(entry));
            }

            foreach (ChildRecord entry in this.mod.HostSaveData.Children.Values)
            {
                snapshot.Children.Add(Clone(entry));
            }
            foreach (ChildPublicState entry in this.mod.ChildGrowthSystem.BuildPublicStatesSnapshot())
            {
                snapshot.ChildRuntimeStates.Add(entry);
            }

            foreach (CarrySessionState entry in this.mod.CarrySystem.GetActiveSessionsSnapshot())
            {
                snapshot.CarrySessions.Add(Clone(entry));
            }

            foreach (HoldingHandsSessionState entry in this.mod.HoldingHandsSystem.GetActiveSessionsSnapshot())
            {
                snapshot.HoldingHandsSessions.Add(Clone(entry));
            }

            if (this.mod.HostSaveData.ActiveImmersiveDate is not null)
            {
                snapshot.ActiveImmersiveDate = this.mod.DateImmersionSystem.BuildPublicState(this.mod.HostSaveData.ActiveImmersiveDate);
            }

            return snapshot;
        }

        return this.mod.ClientSnapshot;
    }

    public void SendError(long playerId, string code, string message)
    {
        this.SendToPlayer(
            MessageType.Error,
            new ErrorMessage
            {
                Code = code,
                Message = message
            },
            playerId);
    }

    private static RelationshipRecord Clone(RelationshipRecord source)
    {
        return new RelationshipRecord
        {
            PairKey = source.PairKey,
            PlayerAId = source.PlayerAId,
            PlayerAName = source.PlayerAName,
            PlayerBId = source.PlayerBId,
            PlayerBName = source.PlayerBName,
            State = source.State,
            PendingDatingFrom = source.PendingDatingFrom,
            PendingMarriageFrom = source.PendingMarriageFrom,
            RelationshipStartedDay = source.RelationshipStartedDay,
            LastStatusChangeDay = source.LastStatusChangeDay,
            HeartPoints = source.HeartPoints,
            LastHeartChangeDay = source.LastHeartChangeDay,
            LastImmersiveDateRequestedDay = source.LastImmersiveDateRequestedDay,
            LastImmersiveDateConfirmedDay = source.LastImmersiveDateConfirmedDay,
            LastImmersiveDateDay = source.LastImmersiveDateDay,
            ImmersiveDateCount = source.ImmersiveDateCount,
            GiftsOfferedCount = source.GiftsOfferedCount,
            RejectionsCount = source.RejectionsCount
        };
    }

    private static PregnancyRecord Clone(PregnancyRecord source)
    {
        return new PregnancyRecord
        {
            CoupleKey = source.CoupleKey,
            ParentAId = source.ParentAId,
            ParentAName = source.ParentAName,
            ParentBId = source.ParentBId,
            ParentBName = source.ParentBName,
            ParentAOptIn = source.ParentAOptIn,
            ParentBOptIn = source.ParentBOptIn,
            PendingTryForBabyFrom = source.PendingTryForBabyFrom,
            IsPregnant = source.IsPregnant,
            DaysRemaining = source.DaysRemaining,
            StartedOnDay = source.StartedOnDay,
            LastProcessedDay = source.LastProcessedDay,
            PregnantPlayerId = source.PregnantPlayerId,
            PregnancyDurationDays = source.PregnancyDurationDays,
            CurrentPregnancyDay = source.CurrentPregnancyDay
        };
    }

    private static ChildRecord Clone(ChildRecord source)
    {
        return new ChildRecord
        {
            ChildId = source.ChildId,
            ChildName = source.ChildName,
            ParentAId = source.ParentAId,
            ParentAName = source.ParentAName,
            ParentBId = source.ParentBId,
            ParentBName = source.ParentBName,
            AgeYears = source.AgeYears,
            AgeDays = source.AgeDays,
            Stage = source.Stage,
            BirthDayNumber = source.BirthDayNumber,
            LastProcessedDay = source.LastProcessedDay,
            IsFedToday = source.IsFedToday,
            FeedingProgress = source.FeedingProgress,
            AssignedTask = source.AssignedTask,
            AutoMode = source.AutoMode,
            LastWorkedDay = source.LastWorkedDay,
            EducationScore = source.EducationScore,
            LegacyAssignment = source.LegacyAssignment,
            LegacySpecialization = source.LegacySpecialization,
            LastTuitionDay = source.LastTuitionDay,
            LastLegacyTaskDay = source.LastLegacyTaskDay,
            RoutineZone = source.RoutineZone,
            RuntimeNpcName = source.RuntimeNpcName,
            RuntimeNpcSpawned = source.RuntimeNpcSpawned,
            VisualProfile = Clone(source.VisualProfile),
            IsWorkerEnabled = source.IsWorkerEnabled,
            AdultNpcName = source.AdultNpcName,
            AdultNpcSpawned = source.AdultNpcSpawned
        };
    }

    private static CarrySessionState Clone(CarrySessionState source)
    {
        return new CarrySessionState
        {
            CarrierId = source.CarrierId,
            CarriedId = source.CarriedId,
            Active = source.Active
        };
    }

    private static HoldingHandsSessionState Clone(HoldingHandsSessionState source)
    {
        return new HoldingHandsSessionState
        {
            LeaderId = source.LeaderId,
            FollowerId = source.FollowerId,
            Active = source.Active
        };
    }

    private static ChildVisualProfile Clone(ChildVisualProfile? source)
    {
        if (source is null)
        {
            return new ChildVisualProfile();
        }

        return new ChildVisualProfile
        {
            MixSeed = source.MixSeed,
            SkinToneHex = source.SkinToneHex,
            HairColorHex = source.HairColorHex,
            OutfitColorHex = source.OutfitColorHex,
            InfantTemplateNpc = source.InfantTemplateNpc,
            ChildTemplateNpc = source.ChildTemplateNpc,
            TeenTemplateNpc = source.TeenTemplateNpc,
            AdultTemplateNpc = source.AdultTemplateNpc,
            IsFallback = source.IsFallback
        };
    }
}
