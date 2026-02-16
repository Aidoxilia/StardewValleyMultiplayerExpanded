using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PlayerRomance.Systems;

public sealed class DateImmersionSystem
{
    private readonly ModEntry mod;
    private readonly Random random = new();
    private readonly HashSet<string> processedInteractionRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<DateStandType, List<StandOfferDefinition>> offersByStand = new();
    private readonly List<(DateStandType standType, Vector2 tile)> localStands = new();
    private readonly List<string> localNpcNames = new();
    private readonly Dictionary<string, DateTime> lastAmbientBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> lastDuoPulseBySession = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PendingImmersiveRequest> pendingRequests = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTime> joinGraceBySession = new(StringComparer.OrdinalIgnoreCase);
    private string localRuntimeSessionId = string.Empty;
    private DateImmersionPublicState? localPublicState;

    private sealed class PendingImmersiveRequest
    {
        public long RequesterId { get; init; }
        public long PartnerId { get; init; }
        public ImmersiveDateLocation Location { get; init; }
        public DateTime CreatedUtc { get; init; }
    }

    private static readonly string[] TownAmbientLines =
    {
        "A warm breeze passes through town. The date feels alive tonight.",
        "You hear laughter near the fountain and soft music in the distance.",
        "Lantern lights and footsteps make this evening feel special."
    };

    private static readonly string[] BeachAmbientLines =
    {
        "Waves crash softly while lanterns glow along the shore.",
        "The salty air and ocean horizon make the moment feel calm.",
        "You hear gulls, distant chatter, and the sea all at once."
    };

    private static readonly string[] ForestAmbientLines =
    {
        "Leaves rustle and fireflies glow between the trees.",
        "The forest feels quiet, cozy, and perfect for a date.",
        "A distant owl call makes the evening feel magical."
    };

    private static readonly string[] VendorShoutLines =
    {
        "Fresh treats! Gifts for someone special!",
        "Roses and outfits ready for tonight!",
        "Date specials available right now!"
    };

    public DateImmersionSystem(ModEntry mod)
    {
        this.mod = mod;
        this.offersByStand[DateStandType.IceCream] = new List<StandOfferDefinition>
        {
            new() { StandType = DateStandType.IceCream, ItemId = "(O)233", DisplayName = "Ice Cream", Price = 250, HeartDeltaOnOffer = 35 },
            new() { StandType = DateStandType.IceCream, ItemId = "(O)220", DisplayName = "Chocolate Cake", Price = 300, HeartDeltaOnOffer = 40 }
        };
        this.offersByStand[DateStandType.Roses] = new List<StandOfferDefinition>
        {
            new() { StandType = DateStandType.Roses, ItemId = "(O)595", DisplayName = "Fairy Rose", Price = 350, HeartDeltaOnOffer = 55 },
            new() { StandType = DateStandType.Roses, ItemId = "(O)421", DisplayName = "Bouquet", Price = 500, HeartDeltaOnOffer = 70 }
        };
        this.offersByStand[DateStandType.Clothing] = new List<StandOfferDefinition>
        {
            new() { StandType = DateStandType.Clothing, ItemId = "(H)1", DisplayName = "Straw Hat", Price = 750, HeartDeltaOnOffer = 45 },
            new() { StandType = DateStandType.Clothing, ItemId = "(H)27", DisplayName = "Lucky Bow", Price = 900, HeartDeltaOnOffer = 50 }
        };
    }

    public bool IsActive
    {
        get
        {
            if (this.mod.IsHostPlayer)
            {
                return this.mod.HostSaveData.ActiveImmersiveDate?.IsActive == true;
            }

            return this.mod.ClientSnapshot.ActiveImmersiveDate?.IsActive == true;
        }
    }

    public int GetRequiredHeartsForLocation(ImmersiveDateLocation location)
    {
        if (!this.mod.Config.EnableHeartsSystem)
        {
            return 0;
        }

        return location == ImmersiveDateLocation.Town
            ? 0
            : Math.Max(0, this.mod.Config.ImmersiveDateMinHearts);
    }

    public int GetCompletionRewardPoints()
    {
        return Math.Max(1, this.mod.Config.HeartPointsPerHeart / 2);
    }

    public DateImmersionPublicState? GetActivePublicState()
    {
        if (this.mod.IsHostPlayer)
        {
            return this.mod.HostSaveData.ActiveImmersiveDate is null
                ? null
                : this.BuildPublicState(this.mod.HostSaveData.ActiveImmersiveDate);
        }

        return this.mod.ClientSnapshot.ActiveImmersiveDate;
    }

    public IReadOnlyList<StandOfferDefinition> GetStandOffers(DateStandType standType)
    {
        return this.offersByStand.TryGetValue(standType, out List<StandOfferDefinition>? list)
            ? list
            : Array.Empty<StandOfferDefinition>();
    }

    public DateImmersionPublicState BuildPublicState(DateImmersionSaveState state)
    {
        return new DateImmersionPublicState
        {
            SessionId = state.SessionId,
            PlayerAId = state.PlayerAId,
            PlayerBId = state.PlayerBId,
            PlayerAName = state.PlayerAName,
            PlayerBName = state.PlayerBName,
            Location = state.Location,
            StartedDay = state.StartedDay,
            StartedTime = state.StartedTime,
            IsActive = state.IsActive
        };
    }

    public void Reset()
    {
        this.processedInteractionRequests.Clear();
        this.lastAmbientBySession.Clear();
        this.lastDuoPulseBySession.Clear();
        this.pendingRequests.Clear();
        this.joinGraceBySession.Clear();
        this.localPublicState = null;
        this.CleanupLocalRuntime();
    }

    public void ApplySnapshot(NetSnapshot snapshot)
    {
        this.localPublicState = snapshot.ActiveImmersiveDate;
        if (this.localPublicState?.IsActive == true)
        {
            this.EnsureLocalRuntime();
        }
        else
        {
            this.CleanupLocalRuntime();
        }
    }

    public void OnHostSaveLoadedRecovery()
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (this.mod.HostSaveData.ActiveImmersiveDate is null)
        {
            return;
        }

        this.mod.Monitor.Log("[PR.System.DateImmersion] Recovery cleanup: found stale immersive date state in save.", LogLevel.Warn);
        this.CleanupLocalRuntime();
        this.mod.HostSaveData.ActiveImmersiveDate = null;
        this.mod.MarkDataDirty("Recovered stale immersive date state.", flushNow: true);
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableImmersiveDates)
        {
            return;
        }

        if (this.mod.HostSaveData.ActiveImmersiveDate is null)
        {
            return;
        }

        this.EndImmersiveDateHost("new_day", completed: false);
    }

    public void OnDayEndingHost()
    {
        if (!this.mod.IsHostPlayer || this.mod.HostSaveData.ActiveImmersiveDate is null)
        {
            return;
        }

        bool completed = Game1.timeOfDay >= this.mod.Config.ImmersiveDateEndTime;
        this.EndImmersiveDateHost("day_end", completed);
    }

    public void OnUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableImmersiveDates)
        {
            return;
        }

        DateImmersionSaveState? state = this.mod.HostSaveData.ActiveImmersiveDate;
        if (state is null || !state.IsActive)
        {
            return;
        }

        Farmer? playerA = this.mod.FindFarmerById(state.PlayerAId, includeOffline: false);
        Farmer? playerB = this.mod.FindFarmerById(state.PlayerBId, includeOffline: false);
        if (playerA is null || playerB is null)
        {
            this.EndImmersiveDateHost("player_offline", completed: false);
            return;
        }

        string locationName = GetMapName(state.Location);
        if (!IsSameLocation(playerA, playerB, locationName))
        {
            if (this.joinGraceBySession.TryGetValue(state.SessionId, out DateTime graceEnd) && DateTime.UtcNow < graceEnd)
            {
                if (Game1.ticks % 30 == 0)
                {
                    Vector2 start = GetStartTile(state.Location);
                    this.WarpParticipant(state.PlayerAId, locationName, start);
                    this.WarpParticipant(state.PlayerBId, locationName, start + new Vector2(1f, 0f));
                }

                return;
            }

            this.EndImmersiveDateHost("location_changed", completed: false);
            return;
        }

        this.EnsureLocalRuntime();
        this.UpdateLocalNpcMovement();

        if (Game1.timeOfDay >= this.mod.Config.ImmersiveDateEndTime)
        {
            this.EndImmersiveDateHost("completed", completed: true);
        }
    }

    public void OnUpdateTickedLocal()
    {
        if (!Context.IsWorldReady || this.localPublicState?.IsActive != true)
        {
            return;
        }

        this.EnsureLocalRuntime();
        this.UpdateLocalNpcMovement();
    }

    public void OnOneSecondUpdateTickedHost()
    {
        this.CleanupExpiredPendingRequestsHost();

        if (!this.mod.IsHostPlayer || this.mod.HostSaveData.ActiveImmersiveDate is null)
        {
            return;
        }

        DateImmersionSaveState state = this.mod.HostSaveData.ActiveImmersiveDate;
        if (!state.IsActive)
        {
            return;
        }

        this.TryBroadcastAmbientLineHost(state);
        this.TryApplyDuoPulseHost(state);

        if (Game1.timeOfDay >= this.mod.Config.ImmersiveDateEndTime)
        {
            this.EndImmersiveDateHost("completed", completed: true);
        }
    }

    public void OnPeerDisconnectedHost(long playerId)
    {
        this.RemovePendingRequestsForPlayer(playerId);

        if (!this.mod.IsHostPlayer || this.mod.HostSaveData.ActiveImmersiveDate is null)
        {
            return;
        }

        DateImmersionSaveState state = this.mod.HostSaveData.ActiveImmersiveDate;
        if (state.PlayerAId == playerId || state.PlayerBId == playerId)
        {
            this.EndImmersiveDateHost("peer_disconnected", completed: false);
        }
    }

    public bool StartImmersiveDateFromLocal(string partnerToken, ImmersiveDateLocation location, out string message)
    {
        if (!this.mod.Config.EnableImmersiveDates)
        {
            message = "Immersive dates are disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner) || !this.mod.IsPlayerOnline(partner.UniqueMultiplayerID))
        {
            message = $"Player '{partnerToken}' not found online.";
            return false;
        }

        ImmersiveDateRequestMessage payload = new()
        {
            RequesterId = this.mod.LocalPlayerId,
            RequesterName = this.mod.LocalPlayerName,
            PartnerId = partner.UniqueMultiplayerID,
            Location = location
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleImmersiveDateRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.ImmersiveDateRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Immersive date request sent for {partner.Name} ({location}).";
        return true;
    }

    public bool EndImmersiveDateFromLocal(out string message)
    {
        DateImmersionPublicState? state = this.GetActivePublicState();
        if (state is null || !state.IsActive)
        {
            message = "No active immersive date session.";
            return false;
        }

        if (!this.IsParticipant(state, this.mod.LocalPlayerId))
        {
            message = "Only participants can end this immersive date.";
            return false;
        }

        ImmersiveDateInteractionRequestMessage payload = new()
        {
            RequestId = Guid.NewGuid().ToString("N"),
            SessionId = state.SessionId,
            ActorId = this.mod.LocalPlayerId,
            InteractionType = DateInteractionType.EndDate,
            StandType = DateStandType.IceCream
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleInteractionRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.ImmersiveDateInteractionRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = "Immersive date end request sent.";
        return true;
    }

    public bool RequestStandPurchaseFromLocal(DateStandType standType, string itemId, bool offerToPartner, out string message)
    {
        DateImmersionPublicState? state = this.GetActivePublicState();
        if (state is null || !state.IsActive)
        {
            message = "No active immersive date.";
            return false;
        }

        if (!this.IsParticipant(state, this.mod.LocalPlayerId))
        {
            message = "Only date participants can buy gifts.";
            return false;
        }

        ImmersiveDateInteractionRequestMessage payload = new()
        {
            RequestId = Guid.NewGuid().ToString("N"),
            SessionId = state.SessionId,
            ActorId = this.mod.LocalPlayerId,
            InteractionType = offerToPartner ? DateInteractionType.BuyAndOffer : DateInteractionType.BuyForSelf,
            StandType = standType,
            OfferItemId = itemId,
            Quantity = 1
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleInteractionRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.ImmersiveDateInteractionRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = offerToPartner ? "Offer request sent to host." : "Purchase request sent to host.";
        return true;
    }

    public bool TryHandleLocalInteractionButton(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not null || e.Button != SButton.MouseRight)
        {
            return false;
        }

        DateImmersionPublicState? state = this.GetActivePublicState();
        if (state is null || !state.IsActive || !this.IsParticipant(state, this.mod.LocalPlayerId))
        {
            return false;
        }

        string expectedMap = GetMapName(state.Location);
        if (!string.Equals(Game1.currentLocation.NameOrUniqueName, expectedMap, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        DateStandType? stand = this.GetNearestStand(Game1.player.Tile, maxDistance: 1.8f);
        if (stand.HasValue)
        {
            Game1.activeClickableMenu = new UI.DateStandMenu(this.mod, this, stand.Value);
            return true;
        }

        if (this.IsNearDateNpc(Game1.player.Tile, 2f))
        {
            ImmersiveDateInteractionRequestMessage payload = new()
            {
                RequestId = Guid.NewGuid().ToString("N"),
                SessionId = state.SessionId,
                ActorId = this.mod.LocalPlayerId,
                InteractionType = DateInteractionType.TalkNpc,
                StandType = DateStandType.IceCream
            };

            if (this.mod.IsHostPlayer)
            {
                this.HandleInteractionRequestHost(payload, this.mod.LocalPlayerId);
            }
            else
            {
                this.mod.NetSync.SendToPlayer(MessageType.ImmersiveDateInteractionRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
            }

            return true;
        }

        return false;
    }

    public bool DebugSpawnStands(string locationToken, out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        if (!Enum.TryParse(locationToken, ignoreCase: true, out ImmersiveDateLocation location))
        {
            message = "Location must be town, beach, or forest.";
            return false;
        }

        this.localPublicState = new DateImmersionPublicState
        {
            SessionId = $"debug_{Guid.NewGuid():N}",
            PlayerAId = this.mod.LocalPlayerId,
            PlayerBId = this.mod.LocalPlayerId,
            PlayerAName = this.mod.LocalPlayerName,
            PlayerBName = this.mod.LocalPlayerName,
            Location = location,
            StartedDay = this.mod.GetCurrentDayNumber(),
            StartedTime = Game1.timeOfDay,
            IsActive = true
        };
        this.EnsureLocalRuntime();
        message = $"Debug immersive stands spawned in {location}.";
        return true;
    }

    public bool DebugCleanup(out string message)
    {
        this.localPublicState = null;
        this.CleanupLocalRuntime();
        message = "Immersive date runtime cleaned.";
        return true;
    }

    public void HandleImmersiveDateRequestHost(ImmersiveDateRequestMessage request, long senderId)
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableImmersiveDates)
        {
            this.mod.NetSync.SendError(senderId, "immersive_disabled", "Immersive dates are disabled.");
            return;
        }

        if (request.RequesterId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Immersive date request rejected (sender mismatch).");
            return;
        }

        if (request.RequesterId == request.PartnerId)
        {
            this.mod.NetSync.SendError(senderId, "invalid_target", "Cannot start an immersive date with yourself.");
            return;
        }

        string pairKey = ConsentSystem.GetPairKey(request.RequesterId, request.PartnerId);
        if (this.pendingRequests.ContainsKey(pairKey))
        {
            this.mod.NetSync.SendError(senderId, "pending_exists", "An immersive date request is already pending for this couple.");
            return;
        }

        if (!this.ValidateCanStartImmersiveDateHost(request.RequesterId, request.PartnerId, request.Location, out string validationError))
        {
            this.mod.NetSync.SendError(senderId, "invalid_state", validationError);
            return;
        }

        this.pendingRequests[pairKey] = new PendingImmersiveRequest
        {
            RequesterId = request.RequesterId,
            PartnerId = request.PartnerId,
            Location = request.Location,
            CreatedUtc = DateTime.UtcNow
        };

        Farmer? requester = this.mod.FindFarmerById(request.RequesterId, includeOffline: true);
        string requesterName = !string.IsNullOrWhiteSpace(request.RequesterName)
            ? request.RequesterName
            : requester?.Name ?? request.RequesterId.ToString();
        ImmersiveDateRequestMessage prompt = new()
        {
            RequesterId = request.RequesterId,
            RequesterName = requesterName,
            PartnerId = request.PartnerId,
            Location = request.Location
        };

        if (request.PartnerId == this.mod.LocalPlayerId)
        {
            this.mod.RequestPrompts.Enqueue(
                $"idate:{request.RequesterId}:{request.PartnerId}:{request.Location}",
                "Immersive Date",
                $"{requesterName} asks to start a {request.Location} date.",
                () =>
                {
                    this.HandleImmersiveDateDecisionHost(
                        new ImmersiveDateDecisionMessage
                        {
                            RequesterId = request.RequesterId,
                            ResponderId = request.PartnerId,
                            Location = request.Location,
                            Accepted = true
                        },
                        this.mod.LocalPlayerId);
                    return (true, "Immersive date accepted.");
                },
                () =>
                {
                    this.HandleImmersiveDateDecisionHost(
                        new ImmersiveDateDecisionMessage
                        {
                            RequesterId = request.RequesterId,
                            ResponderId = request.PartnerId,
                            Location = request.Location,
                            Accepted = false
                        },
                        this.mod.LocalPlayerId);
                    return (true, "Immersive date declined.");
                },
                "[PR.System.DateImmersion]");
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.ImmersiveDateRequest, prompt, request.PartnerId);
        }

        this.mod.Monitor.Log(
            $"[PR.System.DateImmersion] Request queued: {request.RequesterId} -> {request.PartnerId} ({request.Location}).",
            LogLevel.Info);
    }

    public void HandleImmersiveDateDecisionHost(ImmersiveDateDecisionMessage decision, long senderId)
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableImmersiveDates)
        {
            return;
        }

        if (senderId != decision.ResponderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Immersive date decision rejected (sender mismatch).");
            return;
        }

        string pairKey = ConsentSystem.GetPairKey(decision.RequesterId, decision.ResponderId);
        if (!this.pendingRequests.TryGetValue(pairKey, out PendingImmersiveRequest? pending))
        {
            this.mod.NetSync.SendError(senderId, "missing_request", "No pending immersive date request found.");
            return;
        }

        this.pendingRequests.Remove(pairKey);
        if (pending.Location != decision.Location)
        {
            this.mod.NetSync.SendError(senderId, "mismatch", "Immersive date decision mismatch.");
            return;
        }

        this.mod.NetSync.Broadcast(
            MessageType.ImmersiveDateDecision,
            decision,
            decision.RequesterId,
            decision.ResponderId);

        if (!decision.Accepted)
        {
            return;
        }

        if (!this.ValidateCanStartImmersiveDateHost(decision.RequesterId, decision.ResponderId, decision.Location, out string validationError))
        {
            this.mod.NetSync.SendError(decision.RequesterId, "start_failed", validationError);
            this.mod.NetSync.SendError(decision.ResponderId, "start_failed", validationError);
            return;
        }

        this.StartImmersiveDateSessionHost(decision.RequesterId, decision.ResponderId, decision.Location);
    }

    private bool ValidateCanStartImmersiveDateHost(long requesterId, long partnerId, ImmersiveDateLocation location, out string message)
    {
        if (this.mod.HostSaveData.ActiveImmersiveDate?.IsActive == true)
        {
            message = "Another immersive date is already active.";
            return false;
        }

        Farmer? requester = this.mod.FindFarmerById(requesterId, includeOffline: false);
        Farmer? partner = this.mod.FindFarmerById(partnerId, includeOffline: false);
        if (requester is null || partner is null)
        {
            message = "Both players must be online.";
            return false;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(requesterId, partnerId);
        if (relation is null || relation.State == RelationshipState.None)
        {
            message = "Immersive date requires Dating/Engaged/Married.";
            return false;
        }

        int day = this.mod.GetCurrentDayNumber();
        if (!relation.CanStartImmersiveDateToday(day))
        {
            message = "This couple already started an immersive date today.";
            return false;
        }

        int requiredHearts = this.GetRequiredHeartsForLocation(location);
        if (!this.mod.HeartsSystem.IsAtLeastHearts(requesterId, partnerId, requiredHearts))
        {
            message = $"Requires at least {requiredHearts} hearts for {location} date.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void StartImmersiveDateSessionHost(long requesterId, long partnerId, ImmersiveDateLocation location)
    {
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(requesterId, partnerId);
        if (relation is null)
        {
            return;
        }

        int day = this.mod.GetCurrentDayNumber();
        DateImmersionSaveState state = new()
        {
            SessionId = $"pr_date_{Guid.NewGuid():N}",
            PlayerAId = relation.PlayerAId,
            PlayerAName = relation.PlayerAName,
            PlayerBId = relation.PlayerBId,
            PlayerBName = relation.PlayerBName,
            PairKey = relation.PairKey,
            Location = location,
            StartedDay = day,
            StartedTime = Game1.timeOfDay,
            IsActive = true
        };

        this.mod.HostSaveData.ActiveImmersiveDate = state;
        relation.LastImmersiveDateDay = day;
        relation.ImmersiveDateCount++;
        this.processedInteractionRequests.Clear();
        this.lastAmbientBySession[state.SessionId] = DateTime.MinValue;
        this.lastDuoPulseBySession[state.SessionId] = DateTime.MinValue;
        this.joinGraceBySession[state.SessionId] = DateTime.UtcNow.AddSeconds(10);
        this.mod.MarkDataDirty("Immersive date started.", flushNow: true);

        string mapName = GetMapName(location);
        Vector2 start = GetStartTile(location);
        this.WarpParticipant(relation.PlayerAId, mapName, start);
        this.WarpParticipant(relation.PlayerBId, mapName, start + new Vector2(1f, 0f));
        this.TryPlayEmote(relation.PlayerAId, 20);
        this.TryPlayEmote(relation.PlayerBId, 20);

        this.localPublicState = this.BuildPublicState(state);
        this.EnsureLocalRuntime();
        this.BroadcastImmersiveDateState(active: true, "started");
        this.mod.Notifier.NotifyInfo(
            $"Immersive date started in {location}: {relation.PlayerAName} + {relation.PlayerBName}.",
            "[PR.System.DateImmersion]");
    }

    private void CleanupExpiredPendingRequestsHost()
    {
        if (!this.mod.IsHostPlayer || this.pendingRequests.Count == 0)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        List<string> expired = new();
        foreach ((string key, PendingImmersiveRequest value) in this.pendingRequests)
        {
            if (now - value.CreatedUtc > TimeSpan.FromSeconds(40))
            {
                expired.Add(key);
            }
        }

        foreach (string key in expired)
        {
            this.pendingRequests.Remove(key);
        }
    }

    private void RemovePendingRequestsForPlayer(long playerId)
    {
        if (!this.mod.IsHostPlayer || this.pendingRequests.Count == 0)
        {
            return;
        }

        List<string> removeKeys = new();
        foreach ((string key, PendingImmersiveRequest value) in this.pendingRequests)
        {
            if (value.RequesterId == playerId || value.PartnerId == playerId)
            {
                removeKeys.Add(key);
            }
        }

        foreach (string key in removeKeys)
        {
            this.pendingRequests.Remove(key);
        }
    }

    public void HandleInteractionRequestHost(ImmersiveDateInteractionRequestMessage request, long senderId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        DateImmersionSaveState? state = this.mod.HostSaveData.ActiveImmersiveDate;
        if (state is null || !state.IsActive)
        {
            this.mod.NetSync.SendError(senderId, "no_session", "No immersive date session is active.");
            return;
        }

        if (request.ActorId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Interaction rejected (sender mismatch).");
            return;
        }

        if (!this.IsParticipant(state, request.ActorId))
        {
            this.mod.NetSync.SendError(senderId, "not_participant", "Only immersive date participants can interact.");
            return;
        }

        if (!string.Equals(request.SessionId, state.SessionId, StringComparison.OrdinalIgnoreCase))
        {
            this.mod.NetSync.SendError(senderId, "session_mismatch", "Immersive date session mismatch.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.RequestId) && !this.processedInteractionRequests.Add(request.RequestId))
        {
            this.SendInteractionResult(state, request, success: false, "Duplicate request ignored.", 0, offeredToPartner: false, 0);
            return;
        }

        switch (request.InteractionType)
        {
            case DateInteractionType.TalkNpc:
                this.HandleTalkNpcHost(state, request);
                return;
            case DateInteractionType.BuyForSelf:
                this.HandlePurchaseHost(state, request, offerToPartner: false);
                return;
            case DateInteractionType.BuyAndOffer:
                this.HandlePurchaseHost(state, request, offerToPartner: true);
                return;
            case DateInteractionType.EndDate:
            {
                bool completed = Game1.timeOfDay >= this.mod.Config.ImmersiveDateEndTime;
                this.EndImmersiveDateHost("manual_end", completed);
                this.SendInteractionResult(state, request, success: true, completed ? "Immersive date completed." : "Immersive date ended early.", 0, false, 0);
                return;
            }
            default:
                this.SendInteractionResult(state, request, success: false, "Unsupported interaction.", 0, offeredToPartner: false, 0);
                return;
        }
    }

    public void ApplyStateMessageClient(ImmersiveDateStateMessage stateMessage)
    {
        this.ApplyStateMessageLocal(stateMessage);
    }

    public void ApplyInteractionResultClient(ImmersiveDateInteractionResultMessage result)
    {
        if (result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.Message))
            {
                this.mod.Notifier.NotifyInfo(result.Message, "[PR.System.DateImmersion]");
            }
        }
        else if (!string.IsNullOrWhiteSpace(result.Message))
        {
            this.mod.Notifier.NotifyWarn(result.Message, "[PR.System.DateImmersion]");
        }
    }

    private void ApplyStateMessageLocal(ImmersiveDateStateMessage stateMessage)
    {
        if (stateMessage.Active && stateMessage.State is not null)
        {
            this.localPublicState = stateMessage.State;
            this.mod.ClientSnapshot.ActiveImmersiveDate = stateMessage.State;
            this.EnsureLocalRuntime();
            if (this.IsParticipant(stateMessage.State, this.mod.LocalPlayerId))
            {
                this.mod.Notifier.NotifyInfo(
                    $"Immersive date started in {stateMessage.State.Location}. Right-click stands or date NPCs to interact.",
                    "[PR.System.DateImmersion]");
            }

            return;
        }

        this.localPublicState = null;
        this.mod.ClientSnapshot.ActiveImmersiveDate = null;
        this.CleanupLocalRuntime();
        this.mod.Notifier.NotifyInfo(
            $"Immersive date ended ({stateMessage.Reason}).",
            "[PR.System.DateImmersion]");
    }

    private void HandleTalkNpcHost(DateImmersionSaveState state, ImmersiveDateInteractionRequestMessage request)
    {
        int hearts = this.mod.HeartsSystem.GetHeartLevel(state.PlayerAId, state.PlayerBId);
        string locationText = this.PickAmbientLine(state.Location);
        string heartText = hearts switch
        {
            >= 10 => "The crowd smiles: you two look inseparable.",
            >= 6 => "People can tell you're getting really close.",
            >= 3 => "You make a sweet pair out here.",
            _ => "This could become something really special."
        };
        string vendorHint = this.random.NextDouble() > 0.5d
            ? "A vendor suggests getting a gift for your partner."
            : "Nearby NPCs mention your chemistry tonight.";
        string dialog = $"{locationText} {heartText} {vendorHint}";
        int heartDelta = 0;
        if (this.TryGrantTalkBonus(state, request.ActorId))
        {
            heartDelta = 10;
            this.mod.HeartsSystem.AddPointsForPair(state.PairKey, heartDelta, "immersive_talk");
        }

        this.TryPlayEmote(request.ActorId, 20);
        this.SendInteractionResult(state, request, success: true, dialog, 0, offeredToPartner: false, heartDelta);
    }

    private bool TryGrantTalkBonus(DateImmersionSaveState state, long actorId)
    {
        const int maxTalkBonusesPerPlayer = 2;
        if (actorId == state.PlayerAId)
        {
            if (state.PlayerABonusTalks >= maxTalkBonusesPerPlayer)
            {
                return false;
            }

            state.PlayerABonusTalks++;
            this.mod.MarkDataDirty("Immersive date talk bonus granted.", flushNow: true);
            return true;
        }

        if (actorId == state.PlayerBId)
        {
            if (state.PlayerBBonusTalks >= maxTalkBonusesPerPlayer)
            {
                return false;
            }

            state.PlayerBBonusTalks++;
            this.mod.MarkDataDirty("Immersive date talk bonus granted.", flushNow: true);
            return true;
        }

        return false;
    }

    private void HandlePurchaseHost(DateImmersionSaveState state, ImmersiveDateInteractionRequestMessage request, bool offerToPartner)
    {
        StandOfferDefinition? offer = this.GetStandOffers(request.StandType)
            .FirstOrDefault(p => p.ItemId.Equals(request.OfferItemId, StringComparison.OrdinalIgnoreCase));
        if (offer is null)
        {
            this.SendInteractionResult(state, request, success: false, "Offer not found for this stand.", 0, offeredToPartner: false, 0);
            return;
        }

        Farmer? actor = this.mod.FindFarmerById(request.ActorId, includeOffline: false);
        if (actor is null)
        {
            this.SendInteractionResult(state, request, success: false, "Player is no longer online.", 0, offeredToPartner: false, 0);
            return;
        }

        if (actor.Money < offer.Price)
        {
            this.SendInteractionResult(state, request, success: false, $"Not enough gold ({offer.Price}g required).", 0, offeredToPartner: false, 0);
            return;
        }

        long receiverId = offerToPartner ? (state.PlayerAId == actor.UniqueMultiplayerID ? state.PlayerBId : state.PlayerAId) : actor.UniqueMultiplayerID;
        Farmer? receiver = this.mod.FindFarmerById(receiverId, includeOffline: false);
        if (receiver is null)
        {
            this.SendInteractionResult(state, request, success: false, "Recipient is not available online.", 0, offeredToPartner: offerToPartner, 0);
            return;
        }

        Item? item = ItemRegistry.Create(offer.ItemId, 1, 0, allowNull: true);
        if (item is null)
        {
            this.SendInteractionResult(state, request, success: false, "Failed to create item from registry.", 0, offeredToPartner: offerToPartner, 0);
            return;
        }

        bool added = receiver.addItemToInventoryBool(item);
        if (!added)
        {
            this.SendInteractionResult(state, request, success: false, $"{receiver.Name}'s inventory is full.", 0, offeredToPartner: offerToPartner, 0);
            return;
        }

        actor.Money -= offer.Price;
        int heartDelta = 0;
        if (offerToPartner)
        {
            heartDelta = offer.HeartDeltaOnOffer;
            if (this.mod.HeartsSystem.IsAtLeastHearts(state.PlayerAId, state.PlayerBId, this.mod.Config.GiftsBonusMinHearts))
            {
                heartDelta += 10;
            }

            this.mod.HeartsSystem.AddPointsForPair(state.PairKey, heartDelta, "immersive_gift");
            if (this.mod.HostSaveData.Relationships.TryGetValue(state.PairKey, out RelationshipRecord? relation))
            {
                relation.GiftsOfferedCount++;
            }
        }

        this.mod.MarkDataDirty("Immersive stand transaction committed.", flushNow: true);
        string resultMessage = offerToPartner
            ? $"{actor.Name} offered {offer.DisplayName} to {receiver.Name} (-{offer.Price}g). {receiver.Name} looks genuinely touched."
            : $"{actor.Name} bought {offer.DisplayName} (-{offer.Price}g).";
        this.TryPlayEmote(actor.UniqueMultiplayerID, offerToPartner ? 20 : 12);
        this.TryPlayEmote(receiver.UniqueMultiplayerID, offerToPartner ? 20 : 12);
        this.SendInteractionResult(state, request, success: true, resultMessage, offer.Price, offerToPartner, heartDelta);
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private void SendInteractionResult(
        DateImmersionSaveState state,
        ImmersiveDateInteractionRequestMessage request,
        bool success,
        string message,
        int goldSpent,
        bool offeredToPartner,
        int heartDelta)
    {
        ImmersiveDateInteractionResultMessage result = new()
        {
            RequestId = request.RequestId,
            Success = success,
            Message = message,
            GoldSpent = goldSpent,
            ItemId = request.OfferItemId,
            OfferedToPartner = offeredToPartner,
            HeartDelta = heartDelta
        };

        if (state.PlayerAId == this.mod.LocalPlayerId || state.PlayerBId == this.mod.LocalPlayerId)
        {
            this.ApplyInteractionResultClient(result);
        }

        this.mod.NetSync.Broadcast(
            MessageType.ImmersiveDateInteractionResult,
            result,
            state.PlayerAId,
            state.PlayerBId);
    }

    private void BroadcastImmersiveDateState(bool active, string reason)
    {
        DateImmersionPublicState? publicState = this.mod.HostSaveData.ActiveImmersiveDate is null
            ? null
            : this.BuildPublicState(this.mod.HostSaveData.ActiveImmersiveDate);

        ImmersiveDateStateMessage message = new()
        {
            SessionId = publicState?.SessionId ?? string.Empty,
            Active = active,
            Reason = reason,
            State = active ? publicState : null
        };

        this.ApplyStateMessageLocal(message);
        this.mod.NetSync.Broadcast(MessageType.ImmersiveDateState, message);
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private void EndImmersiveDateHost(string reason, bool completed)
    {
        DateImmersionSaveState? state = this.mod.HostSaveData.ActiveImmersiveDate;
        if (!this.mod.IsHostPlayer || state is null)
        {
            return;
        }

        if (this.mod.HostSaveData.Relationships.TryGetValue(state.PairKey, out RelationshipRecord? relation))
        {
            if (completed)
            {
                this.mod.HeartsSystem.AddPointsForPair(state.PairKey, this.GetCompletionRewardPoints(), "immersive_date_complete");
            }
            else
            {
                this.mod.HeartsSystem.AddPointsForPair(state.PairKey, -Math.Abs(this.mod.Config.EarlyLeaveHeartPenalty), "immersive_date_early_end");
            }

            relation.LastImmersiveDateDay = this.mod.GetCurrentDayNumber();
        }

        this.mod.HostSaveData.ActiveImmersiveDate = null;
        this.processedInteractionRequests.Clear();
        this.lastAmbientBySession.Remove(state.SessionId);
        this.lastDuoPulseBySession.Remove(state.SessionId);
        this.joinGraceBySession.Remove(state.SessionId);
        this.mod.MarkDataDirty($"Immersive date ended ({reason}).", flushNow: true);
        this.CleanupLocalRuntime();
        this.BroadcastImmersiveDateState(active: false, reason);
    }

    private void EnsureLocalRuntime()
    {
        DateImmersionPublicState? state = this.localPublicState ?? this.GetActivePublicState();
        if (state is null || !state.IsActive)
        {
            return;
        }

        if (this.localRuntimeSessionId == state.SessionId && this.localStands.Count > 0)
        {
            return;
        }

        this.CleanupLocalRuntime();
        this.localRuntimeSessionId = state.SessionId;
        this.localPublicState = state;
        foreach ((DateStandType standType, Vector2 tile) stand in GetStandLayout(state.Location))
        {
            this.localStands.Add(stand);
        }

        this.SpawnDateNpcs(state);
    }

    private void SpawnDateNpcs(DateImmersionPublicState state)
    {
        string mapName = GetMapName(state.Location);
        GameLocation? location = Game1.getLocationFromName(mapName);
        if (location is null)
        {
            return;
        }

        string prefix = this.GetNpcPrefix(state.SessionId);
        this.TrySpawnNpc(location, $"{prefix}_vendor_ice", "Alex", "Alex", GetStandTile(DateStandType.IceCream, state.Location) + new Vector2(0f, 1f));
        this.TrySpawnNpc(location, $"{prefix}_vendor_roses", "Haley", "Haley", GetStandTile(DateStandType.Roses, state.Location) + new Vector2(0f, 1f));
        this.TrySpawnNpc(location, $"{prefix}_vendor_cloth", "Emily", "Emily", GetStandTile(DateStandType.Clothing, state.Location) + new Vector2(0f, 1f));
        this.TrySpawnNpc(location, $"{prefix}_walker_1", "Sam", "Sam", GetStartTile(state.Location) + new Vector2(2f, 1f));
        this.TrySpawnNpc(location, $"{prefix}_walker_2", "Leah", "Leah", GetStartTile(state.Location) + new Vector2(-2f, 1f));
        this.TrySpawnNpc(location, $"{prefix}_walker_3", "Abigail", "Abigail", GetStartTile(state.Location) + new Vector2(4f, -1f));
        this.TrySpawnNpc(location, $"{prefix}_walker_4", "Penny", "Penny", GetStartTile(state.Location) + new Vector2(-4f, -1f));
    }

    private void TrySpawnNpc(GameLocation location, string npcName, string spriteName, string portraitName, Vector2 tile)
    {
        try
        {
            if (location.characters.Any(p => p.Name.Equals(npcName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            AnimatedSprite sprite = new($"Characters\\{spriteName}");
            Texture2D portrait = Game1.content.Load<Texture2D>($"Portraits\\{portraitName}");
            NPC npc = new(sprite, tile * 64f, location.NameOrUniqueName, 2, npcName, false, portrait)
            {
                Name = npcName,
                displayName = npcName
            };
            location.addCharacter(npc);
            this.localNpcNames.Add(npcName);
        }
        catch (Exception ex)
        {
            this.mod.Monitor.Log($"[PR.System.DateImmersion] Failed to spawn temporary NPC '{npcName}': {ex.Message}", LogLevel.Trace);
        }
    }

    private void UpdateLocalNpcMovement()
    {
        if (!Context.IsWorldReady || this.localPublicState is null || this.localNpcNames.Count == 0)
        {
            return;
        }

        string prefix = this.GetNpcPrefix(this.localPublicState.SessionId);
        string mapName = GetMapName(this.localPublicState.Location);
        GameLocation? location = Game1.getLocationFromName(mapName);
        if (location is null || Game1.ticks % 40 != 0)
        {
            return;
        }

        foreach (NPC npc in location.characters.Where(p => p.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            bool isVendor = npc.Name.Contains("_vendor_", StringComparison.OrdinalIgnoreCase);
            double moveChance = isVendor ? 0.22d : 0.68d;
            if (this.random.NextDouble() > moveChance)
            {
                continue;
            }

            int px = isVendor ? this.random.Next(-1, 2) * 4 : this.random.Next(-1, 2) * 14;
            int py = isVendor ? this.random.Next(-1, 2) * 4 : this.random.Next(-1, 2) * 14;
            Vector2 drift = new(px, py);
            npc.Position += drift;
            if (!isVendor && this.random.NextDouble() > 0.5d)
            {
                npc.FacingDirection = this.random.Next(0, 4);
            }
        }
    }

    private void CleanupLocalRuntime()
    {
        if (!Context.IsWorldReady)
        {
            this.localRuntimeSessionId = string.Empty;
            this.localStands.Clear();
            this.localNpcNames.Clear();
            return;
        }

        string prefix = !string.IsNullOrWhiteSpace(this.localRuntimeSessionId)
            ? this.GetNpcPrefix(this.localRuntimeSessionId)
            : "PR_Date_";

        foreach (GameLocation location in Game1.locations)
        {
            for (int i = location.characters.Count - 1; i >= 0; i--)
            {
                NPC npc = location.characters[i];
                if (npc.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || npc.Name.StartsWith("PR_Date_", StringComparison.OrdinalIgnoreCase))
                {
                    location.characters.RemoveAt(i);
                }
            }
        }

        this.localRuntimeSessionId = string.Empty;
        this.localStands.Clear();
        this.localNpcNames.Clear();
    }

    private DateStandType? GetNearestStand(Vector2 playerTile, float maxDistance)
    {
        DateStandType? bestStand = null;
        float bestDistance = float.MaxValue;
        foreach ((DateStandType standType, Vector2 tile) stand in this.localStands)
        {
            float distance = Vector2.Distance(playerTile, stand.tile);
            if (distance > maxDistance || distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestStand = stand.standType;
        }

        return bestStand;
    }

    private bool IsNearDateNpc(Vector2 playerTile, float maxDistance)
    {
        if (this.localPublicState is null)
        {
            return false;
        }

        string mapName = GetMapName(this.localPublicState.Location);
        GameLocation? location = Game1.getLocationFromName(mapName);
        if (location is null)
        {
            return false;
        }

        string prefix = this.GetNpcPrefix(this.localPublicState.SessionId);
        return location.characters.Any(npc =>
            npc.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && Vector2.Distance(npc.Tile, playerTile) <= maxDistance);
    }

    private void TryBroadcastAmbientLineHost(DateImmersionSaveState state)
    {
        DateTime now = DateTime.UtcNow;
        if (!this.lastAmbientBySession.TryGetValue(state.SessionId, out DateTime last))
        {
            last = DateTime.MinValue;
        }

        if (now - last < TimeSpan.FromSeconds(16))
        {
            return;
        }

        if (this.random.NextDouble() > 0.45d)
        {
            this.lastAmbientBySession[state.SessionId] = now;
            return;
        }

        string line = this.random.NextDouble() > 0.72d
            ? VendorShoutLines[this.random.Next(VendorShoutLines.Length)]
            : this.PickAmbientLine(state.Location);
        this.lastAmbientBySession[state.SessionId] = now;

        this.mod.NetSync.Broadcast(
            MessageType.ImmersiveDateInteractionResult,
            new ImmersiveDateInteractionResultMessage
            {
                RequestId = $"ambient_{Guid.NewGuid():N}",
                Success = true,
                Message = line
            },
            state.PlayerAId,
            state.PlayerBId);
    }

    private void TryApplyDuoPulseHost(DateImmersionSaveState state)
    {
        if (!this.mod.HeartsSystem.IsAtLeastHearts(state.PlayerAId, state.PlayerBId, this.mod.Config.DuoBuffMinHearts))
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (!this.lastDuoPulseBySession.TryGetValue(state.SessionId, out DateTime last))
        {
            last = DateTime.MinValue;
        }

        if (now - last < TimeSpan.FromSeconds(10))
        {
            return;
        }

        this.lastDuoPulseBySession[state.SessionId] = now;
        bool changed = false;
        foreach (long playerId in new[] { state.PlayerAId, state.PlayerBId })
        {
            Farmer? farmer = this.mod.FindFarmerById(playerId, includeOffline: false);
            if (farmer is null)
            {
                continue;
            }

            float before = farmer.Stamina;
            farmer.Stamina = Math.Min(farmer.MaxStamina, farmer.Stamina + 1f);
            if (farmer.Stamina > before)
            {
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        this.mod.NetSync.Broadcast(
            MessageType.ImmersiveDateInteractionResult,
            new ImmersiveDateInteractionResultMessage
            {
                RequestId = $"duopulse_{Guid.NewGuid():N}",
                Success = true,
                Message = "Your connection gives both of you a small energy boost (+1)."
            },
            state.PlayerAId,
            state.PlayerBId);
    }

    private string PickAmbientLine(ImmersiveDateLocation location)
    {
        string[] pool = location switch
        {
            ImmersiveDateLocation.Beach => BeachAmbientLines,
            ImmersiveDateLocation.Forest => ForestAmbientLines,
            _ => TownAmbientLines
        };

        return pool[this.random.Next(pool.Length)];
    }

    private void TryPlayEmote(long playerId, int emoteId)
    {
        Farmer? farmer = this.mod.FindFarmerById(playerId, includeOffline: false);
        if (farmer is null)
        {
            return;
        }

        try
        {
            farmer.doEmote(emoteId);
        }
        catch
        {
        }
    }

    private bool IsParticipant(DateImmersionSaveState state, long playerId)
    {
        return state.PlayerAId == playerId || state.PlayerBId == playerId;
    }

    private bool IsParticipant(DateImmersionPublicState state, long playerId)
    {
        return state.PlayerAId == playerId || state.PlayerBId == playerId;
    }

    private static bool IsSameLocation(Farmer playerA, Farmer playerB, string expectedLocationName)
    {
        return string.Equals(playerA.currentLocation?.NameOrUniqueName, expectedLocationName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(playerB.currentLocation?.NameOrUniqueName, expectedLocationName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMapName(ImmersiveDateLocation location)
    {
        return location switch
        {
            ImmersiveDateLocation.Beach => "Beach",
            ImmersiveDateLocation.Forest => "Forest",
            _ => "Town"
        };
    }

    private static Vector2 GetStartTile(ImmersiveDateLocation location)
    {
        return location switch
        {
            ImmersiveDateLocation.Beach => new Vector2(37f, 35f),
            ImmersiveDateLocation.Forest => new Vector2(58f, 19f),
            _ => new Vector2(52f, 63f)
        };
    }

    private static IEnumerable<(DateStandType standType, Vector2 tile)> GetStandLayout(ImmersiveDateLocation location)
    {
        yield return (DateStandType.IceCream, GetStandTile(DateStandType.IceCream, location));
        yield return (DateStandType.Roses, GetStandTile(DateStandType.Roses, location));
        yield return (DateStandType.Clothing, GetStandTile(DateStandType.Clothing, location));
    }

    private static Vector2 GetStandTile(DateStandType standType, ImmersiveDateLocation location)
    {
        return location switch
        {
            ImmersiveDateLocation.Beach => standType switch
            {
                DateStandType.IceCream => new Vector2(31f, 34f),
                DateStandType.Roses => new Vector2(36f, 34f),
                _ => new Vector2(41f, 34f)
            },
            ImmersiveDateLocation.Forest => standType switch
            {
                DateStandType.IceCream => new Vector2(54f, 18f),
                DateStandType.Roses => new Vector2(58f, 18f),
                _ => new Vector2(62f, 18f)
            },
            _ => standType switch
            {
                DateStandType.IceCream => new Vector2(47f, 62f),
                DateStandType.Roses => new Vector2(52f, 62f),
                _ => new Vector2(57f, 62f)
            }
        };
    }

    private string GetNpcPrefix(string sessionId)
    {
        return $"PR_Date_{sessionId}";
    }

    private void WarpParticipant(long playerId, string mapName, Vector2 tile)
    {
        if (playerId == this.mod.LocalPlayerId)
        {
            Game1.warpFarmer(mapName, (int)tile.X, (int)tile.Y, false);
            return;
        }

        StartPairEventMessage message = new()
        {
            PlayerAId = playerId,
            PlayerBId = playerId,
            EventId = "immersive_warp",
            LocationName = mapName,
            TileX = (int)tile.X,
            TileY = (int)tile.Y,
            DialogText = "Your immersive date has started."
        };
        this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, message, playerId);
    }
}

