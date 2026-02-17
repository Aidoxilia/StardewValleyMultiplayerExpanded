using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Net;
using PlayerRomance.Systems;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;
using xTile.Dimensions;

namespace PlayerRomance.Events;

public sealed class DateEventController
{
    private readonly ModEntry mod;
    private readonly Systems.MapMarkerReader markerReader;
    private readonly Dictionary<string, Data.DateEventDefinition> definitionsById = new(StringComparer.OrdinalIgnoreCase);

    private ActiveDateRuntimeState? activeHost;
    private LocalDateRuntimeState? activeLocal;

    public DateEventController(ModEntry mod)
    {
        this.mod = mod;
        this.markerReader = new Systems.MapMarkerReader(mod);
        this.LoadDefinitions();
    }

    public bool StartDateFromLocal(string partnerToken, out string message)
    {
        return this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(partnerToken, ImmersiveDateLocation.Town, out message);
    }

    public bool StartImmersiveDateFromLocal(string partnerToken, ImmersiveDateLocation location, out string message)
    {
        return this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(partnerToken, location, out message);
    }

    public bool StartDate(string dateId, Farmer farmerA, Farmer farmerB, out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can start map date event.";
            return false;
        }

        if (this.activeHost is not null)
        {
            message = "A date event is already active.";
            return false;
        }

        if (!this.TryValidateDateStart(dateId, farmerA, farmerB, out DateEventDefinition? definition, out message))
        {
            return false;
        }

        return this.StartDateHost(definition!, farmerA, farmerB, out message);
    }

    public bool StartDateDebugFromLocal(string dateId, string partnerToken, out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner) || partner is null)
        {
            message = $"Player '{partnerToken}' not found.";
            return false;
        }

        if (!this.mod.IsHostPlayer)
        {
            StartPairEventMessage request = new()
            {
                PlayerAId = this.mod.LocalPlayerId,
                PlayerBId = partner.UniqueMultiplayerID,
                EventId = "date_map_start_request",
                DateId = dateId
            };
            this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, request, Game1.MasterPlayer.UniqueMultiplayerID);
            message = $"Date start request '{dateId}' sent to host.";
            return true;
        }

        return this.StartDate(dateId, Game1.player, partner, out message);
    }

    public bool EndDateFromLocal(out string message)
    {
        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can end active date event.";
            return false;
        }

        if (this.activeHost is null)
        {
            message = "No active map date event.";
            return false;
        }

        this.EndDateHost(success: false, "manual_end");
        message = "Date event ended.";
        return true;
    }

    public bool DumpMarkersFromLocal(out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        GameLocation? location = Game1.getLocationFromName("Date_Beach");
        if (location is null)
        {
            message = "Location 'Date_Beach' not found.";
            return false;
        }

        Dictionary<string, MarkerInfo> markers = this.markerReader.ReadMarkers(location);
        if (markers.Count == 0)
        {
            message = "No markers found on Date_Beach (fallback coordinates will be used).";
            return true;
        }

        foreach (MarkerInfo marker in markers.Values.OrderBy(p => p.Name))
        {
            this.mod.Monitor.Log($"[PR.System.DateEvent] Marker {marker.Name} class={marker.Class} tile=({marker.TileX},{marker.TileY})", LogLevel.Info);
        }

        message = $"Dumped {markers.Count} marker(s).";
        return true;
    }

    public void HandleStartDateRequestHost(StartPairEventMessage request, long senderId)
    {
        if (string.Equals(request.EventId, "date_map_start_request", StringComparison.OrdinalIgnoreCase))
        {
            this.HandleMapDateStartRequestHost(request, senderId);
            return;
        }

        if (!this.mod.Config.EnableDateEvents)
        {
            this.mod.NetSync.SendError(senderId, "date_disabled", "Date events are disabled.");
            return;
        }

        if (request.PlayerAId != senderId && request.PlayerBId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Date request rejected (sender mismatch).");
            return;
        }

        Farmer? playerA = this.mod.FindFarmerById(request.PlayerAId, includeOffline: false);
        Farmer? playerB = this.mod.FindFarmerById(request.PlayerBId, includeOffline: false);
        if (playerA is null || playerB is null)
        {
            this.mod.NetSync.SendError(senderId, "player_offline", "Date request rejected (player not available).");
            return;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(playerA.UniqueMultiplayerID, playerB.UniqueMultiplayerID);
        if (relation is null || relation.State == RelationshipState.None)
        {
            this.mod.NetSync.SendError(senderId, "not_in_relationship", "Date events require at least Dating status.");
            return;
        }

        this.mod.Monitor.Log(
            $"[PR.System.Dating] Starting date event for {playerA.Name} and {playerB.Name}.",
            StardewModdingAPI.LogLevel.Info);

        this.DispatchPairEventToParticipants(request, request.PlayerAId, request.PlayerBId);
    }

    public void HandleImmersiveDateRequestHost(ImmersiveDateRequestMessage request, long senderId)
    {
        this.mod.DateImmersionSystem.HandleImmersiveDateRequestHost(request, senderId);
    }

    public void OnDayStartedHost()
    {
        if (this.mod.IsHostPlayer && this.activeHost is not null)
        {
            this.EndDateHost(success: false, "day_changed");
        }
    }

    public void OnUpdateTicked()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.OnUpdateTickedLocal();
        if (this.mod.IsHostPlayer)
        {
            this.OnUpdateTickedHost();
        }
    }

    public bool TryHandleInputBlock(ButtonPressedEventArgs e)
    {
        if (!this.IsLocalParticipantInActiveDate())
        {
            return false;
        }

        this.mod.Helper.Input.Suppress(e.Button);
        return true;
    }

    public void ApplyEventStepClient(StartPairEventMessage step)
    {
        if (step.PlayerAId != this.mod.LocalPlayerId && step.PlayerBId != this.mod.LocalPlayerId)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(step.DialogText))
        {
            Game1.drawObjectDialogue(step.DialogText);
        }
    }

    public void TryStartEventClient(StartPairEventMessage start)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        if (start.PlayerAId != this.mod.LocalPlayerId && start.PlayerBId != this.mod.LocalPlayerId)
        {
            return;
        }
        if (string.Equals(start.EventId, "immersive_warp", StringComparison.OrdinalIgnoreCase)
            && !this.mod.DateImmersionSystem.ShouldApplyImmersiveWarpClient(start, out string skipReason))
        {
            this.mod.Monitor.Log(
                $"[PR.System.DateImmersion] Ignored immersive warp event ({skipReason}).",
                LogLevel.Trace);
            return;
        }

        if (string.Equals(start.EventId, "date_map_event", StringComparison.OrdinalIgnoreCase))
        {
            this.StartDateClient(start);
            return;
        }

        if (!string.IsNullOrWhiteSpace(start.LocationName))
        {
            Game1.warpFarmer(start.LocationName, start.TileX, start.TileY, false);
        }

        Game1.player.freezePause = 350;
        if (!string.IsNullOrWhiteSpace(start.DialogText))
        {
            Game1.drawObjectDialogue(start.DialogText);
        }

        if (start.EventId == "date")
        {
            Item? gift = ItemRegistry.Create("(O)421", 1, 0, allowNull: true);
            if (gift is not null)
            {
                Game1.player.addItemToInventoryBool(gift);
            }
        }
        else if (start.EventId == "wedding")
        {
            Item? ring = ItemRegistry.Create("(O)801", 1, 0, allowNull: true);
            if (ring is not null)
            {
                Game1.player.addItemToInventoryBool(ring);
            }
        }
    }

    public void ApplyDatePhaseClient(DateEventPhaseMessage phase)
    {
        if (this.activeLocal is null || this.activeLocal.SessionId != phase.SessionId)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(phase.DialogText)
            && (phase.TargetPlayerId <= 0 || phase.TargetPlayerId == this.mod.LocalPlayerId))
        {
            Game1.drawObjectDialogue(phase.DialogText);
        }

        if (phase.EmoteId >= 0)
        {
            Game1.player.doEmote(phase.EmoteId);
        }
    }

    public void ApplyEndDateClient(EndDateEventMessage end)
    {
        if (this.activeLocal is null || this.activeLocal.SessionId != end.SessionId)
        {
            return;
        }

        this.CleanupLocalDateActors();

        if (this.activeLocal.ReturnLocationName.Length > 0)
        {
            Game1.warpFarmer(this.activeLocal.ReturnLocationName, this.activeLocal.ReturnTileX, this.activeLocal.ReturnTileY, false);
        }

        if (end.Success)
        {
            if (end.RewardMoney > 0)
            {
                Game1.player.Money += end.RewardMoney;
            }

            if (!string.IsNullOrWhiteSpace(end.RewardItemId))
            {
                Item? reward = ItemRegistry.Create(end.RewardItemId, Math.Max(1, end.RewardItemCount));
                if (reward is not null)
                {
                    Game1.player.addItemToInventoryBool(reward);
                }
            }
        }

        Game1.player.CanMove = true;
        Game1.player.UsingTool = false;
        Game1.player.canReleaseTool = true;
        this.activeLocal = null;
        this.mod.Notifier.NotifyInfo(end.Success ? "Date event finished." : $"Date event ended ({end.Reason}).", "[PR.System.DateEvent]");
    }

    private void HandleMapDateStartRequestHost(StartPairEventMessage request, long senderId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (request.PlayerAId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Date map request rejected (sender mismatch).");
            return;
        }

        Farmer? a = this.mod.FindFarmerById(request.PlayerAId, includeOffline: false);
        Farmer? b = this.mod.FindFarmerById(request.PlayerBId, includeOffline: false);
        if (a is null || b is null)
        {
            this.mod.NetSync.SendError(senderId, "player_offline", "Both players must be online.");
            return;
        }

        if (!this.StartDate(request.DateId, a, b, out string msg))
        {
            this.mod.NetSync.SendError(senderId, "date_start_failed", msg);
        }
    }

    private bool StartDateHost(DateEventDefinition definition, Farmer farmerA, Farmer farmerB, out string message)
    {
        GameLocation? dateLocation = Game1.getLocationFromName(definition.MapName);
        if (dateLocation is null)
        {
            message = $"Date map '{definition.MapName}' not found.";
            return false;
        }

        Dictionary<string, MarkerInfo> markers = this.markerReader.ReadMarkers(dateLocation);
        Vector2 playerATile = Systems.MapMarkerReader.GetMarkerTileOrFallback(markers, "PlayerA_Spot", new Vector2(10, 10));
        Vector2 playerBTile = Systems.MapMarkerReader.GetMarkerTileOrFallback(markers, "PlayerB_Spot", new Vector2(12, 10));

        List<DateEventNpcPlacementMessage> placements = this.BuildNpcPlacements(definition, markers);
        string sessionId = $"date_{Guid.NewGuid():N}";

        this.activeHost = new ActiveDateRuntimeState
        {
            SessionId = sessionId,
            DateId = definition.DateId,
            PlayerAId = farmerA.UniqueMultiplayerID,
            PlayerBId = farmerB.UniqueMultiplayerID,
            PlayerAReturnLocation = farmerA.currentLocation?.NameOrUniqueName ?? "Farm",
            PlayerBReturnLocation = farmerB.currentLocation?.NameOrUniqueName ?? "Farm",
            PlayerAReturnTile = farmerA.Tile,
            PlayerBReturnTile = farmerB.Tile,
            LockedTime = Game1.timeOfDay,
            StartTick = Game1.ticks,
            Placements = placements,
            Reward = definition.Reward
        };

        StartPairEventMessage startMessage = new()
        {
            EventId = "date_map_event",
            DateId = definition.DateId,
            SessionId = sessionId,
            PlayerAId = farmerA.UniqueMultiplayerID,
            PlayerBId = farmerB.UniqueMultiplayerID,
            LocationName = definition.MapName,
            PlayerATileX = (int)playerATile.X,
            PlayerATileY = (int)playerATile.Y,
            PlayerBTileX = (int)playerBTile.X,
            PlayerBTileY = (int)playerBTile.Y,
            FreezeTime = true,
            LockInputs = true,
            LockedTime = Game1.timeOfDay,
            NpcPlacements = placements,
            DialogText = "Date event started."
        };

        this.SuspendCarryAndHandsForEventPair(farmerA.UniqueMultiplayerID, farmerB.UniqueMultiplayerID);

        this.StartDateClient(startMessage);
        this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, startMessage, farmerA.UniqueMultiplayerID);
        this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, startMessage, farmerB.UniqueMultiplayerID);
        this.BroadcastPhase("intro", "You arrived at Date Beach.", emoteId: -1);

        message = $"Date event '{definition.DateId}' started.";
        this.mod.Monitor.Log($"[PR.System.DateEvent] Started session {sessionId} for {farmerA.Name}/{farmerB.Name} on {definition.MapName}.", LogLevel.Info);
        return true;
    }

    private void StartDateClient(StartPairEventMessage start)
    {
        this.CleanupLocalDateActors();

        this.activeLocal = new LocalDateRuntimeState
        {
            SessionId = start.SessionId,
            DateId = start.DateId,
            PlayerAId = start.PlayerAId,
            PlayerBId = start.PlayerBId,
            IsInputLocked = start.LockInputs,
            IsTimeFrozen = start.FreezeTime,
            LockedTime = start.LockedTime,
            ReturnLocationName = Game1.currentLocation?.NameOrUniqueName ?? "Farm",
            ReturnTileX = (int)Game1.player.Tile.X,
            ReturnTileY = (int)Game1.player.Tile.Y
        };

        int tx = this.mod.LocalPlayerId == start.PlayerAId ? start.PlayerATileX : start.PlayerBTileX;
        int ty = this.mod.LocalPlayerId == start.PlayerAId ? start.PlayerATileY : start.PlayerBTileY;
        Game1.warpFarmer(start.LocationName, tx, ty, false);
        this.PlaceAndFreezeLocalPlayer(tx, ty, this.mod.LocalPlayerId == start.PlayerAId ? 1 : 3);
        this.ApplyNpcPlacementsLocal(start.NpcPlacements, start.LocationName);

        if (!string.IsNullOrWhiteSpace(start.DialogText))
        {
            Game1.drawObjectDialogue(start.DialogText);
        }
    }

    private void OnUpdateTickedHost()
    {
        if (this.activeHost is null)
        {
            return;
        }

        if (this.activeHost.LockedTime > 0 && Game1.timeOfDay != this.activeHost.LockedTime)
        {
            Game1.timeOfDay = this.activeHost.LockedTime;
        }

        int elapsed = Game1.ticks - this.activeHost.StartTick;
        if (!this.activeHost.Phase1Sent && elapsed >= 240)
        {
            this.activeHost.Phase1Sent = true;
            this.BroadcastPhase("phase_dialog", "What a lovely moment together.", emoteId: 20);
        }

        if (!this.activeHost.Phase2Sent && elapsed >= 540)
        {
            this.activeHost.Phase2Sent = true;
            this.BroadcastPhase("phase_reward", "Date complete!", emoteId: -1);
            this.EndDateHost(success: true, "completed");
        }
    }

    private void OnUpdateTickedLocal()
    {
        if (this.activeLocal is null)
        {
            return;
        }

        if (this.activeLocal.IsTimeFrozen && this.activeLocal.LockedTime > 0 && Game1.timeOfDay != this.activeLocal.LockedTime)
        {
            Game1.timeOfDay = this.activeLocal.LockedTime;
        }

        if (this.activeLocal.IsInputLocked)
        {
            Game1.player.CanMove = false;
            Game1.player.UsingTool = false;
            Game1.player.canReleaseTool = false;
            if (Game1.activeClickableMenu is not null && Game1.activeClickableMenu is not DialogueBox)
            {
                Game1.activeClickableMenu = null;
            }
        }
    }

    private void EndDateHost(bool success, string reason)
    {
        if (this.activeHost is null)
        {
            return;
        }

        EndDateEventMessage end = new()
        {
            SessionId = this.activeHost.SessionId,
            DateId = this.activeHost.DateId,
            Success = success,
            Reason = reason,
            RewardMoney = success ? this.activeHost.Reward.Money : 0,
            RewardItemId = success ? this.activeHost.Reward.ItemId : string.Empty,
            RewardItemCount = success ? this.activeHost.Reward.ItemCount : 0
        };

        this.ApplyEndDateClient(end);
        this.mod.NetSync.SendToPlayer(MessageType.EndDateEvent, end, this.activeHost.PlayerAId);
        this.mod.NetSync.SendToPlayer(MessageType.EndDateEvent, end, this.activeHost.PlayerBId);

        if (success)
        {
            this.mod.HeartsSystem.AddPointsForPlayers(this.activeHost.PlayerAId, this.activeHost.PlayerBId, this.activeHost.Reward.HeartsDelta, "date_map_reward");
        }

        this.ResumeCarryAndHandsAfterEventPair();
        this.activeHost = null;
    }

    private bool TryValidateDateStart(string dateId, Farmer farmerA, Farmer farmerB, out DateEventDefinition? definition, out string message)
    {
        definition = null;
        if (!this.definitionsById.TryGetValue(dateId, out DateEventDefinition? loaded))
        {
            message = $"Unknown dateId '{dateId}'.";
            return false;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(farmerA.UniqueMultiplayerID, farmerB.UniqueMultiplayerID);
        if (relation is null || relation.State == RelationshipState.None)
        {
            message = "Date event requires at least dating relationship.";
            return false;
        }

        if (!this.mod.HeartsSystem.IsAtLeastHearts(farmerA.UniqueMultiplayerID, farmerB.UniqueMultiplayerID, loaded.RequiredHearts))
        {
            message = $"Requires at least {loaded.RequiredHearts} hearts.";
            return false;
        }

        if (Game1.timeOfDay < loaded.MinStartTime || Game1.timeOfDay > loaded.MaxStartTime)
        {
            message = $"Date event only available between {loaded.MinStartTime} and {loaded.MaxStartTime}.";
            return false;
        }

        definition = loaded;
        message = string.Empty;
        return true;
    }

    private List<DateEventNpcPlacementMessage> BuildNpcPlacements(DateEventDefinition definition, IReadOnlyDictionary<string, Systems.MarkerInfo> markers)
    {
        List<DateEventNpcPlacementMessage> placements = new();
        foreach (DateEventNpcDefinition npc in definition.NpcList)
        {
            Vector2 tile = Systems.MapMarkerReader.GetMarkerTileOrFallback(markers, npc.SpotName, new Vector2(10 + placements.Count, 12));
            placements.Add(new DateEventNpcPlacementMessage
            {
                SpotName = npc.SpotName,
                NpcName = npc.NpcName,
                TileX = (int)tile.X,
                TileY = (int)tile.Y,
                FacingDirection = npc.FacingDirection,
                IsVendor = false
            });
        }

        if (definition.VendorNpc is not null)
        {
            Vector2 tile = Systems.MapMarkerReader.GetMarkerTileOrFallback(markers, definition.VendorNpc.SpotName, new Vector2(14, 12));
            placements.Add(new DateEventNpcPlacementMessage
            {
                SpotName = definition.VendorNpc.SpotName,
                NpcName = definition.VendorNpc.NpcName,
                TileX = (int)tile.X,
                TileY = (int)tile.Y,
                FacingDirection = definition.VendorNpc.FacingDirection,
                IsVendor = true
            });
        }

        return placements;
    }

    private void ApplyNpcPlacementsLocal(IEnumerable<DateEventNpcPlacementMessage> placements, string locationName)
    {
        if (Game1.getLocationFromName(locationName) is not GameLocation location)
        {
            return;
        }

        foreach (DateEventNpcPlacementMessage placement in placements)
        {
            NPC? npc = location.characters.FirstOrDefault(p => p.Name.Equals(placement.NpcName, StringComparison.OrdinalIgnoreCase));
            bool created = false;
            if (npc is null)
            {
                this.mod.Monitor.Log($"[PR.System.DateEvent] NPC '{placement.NpcName}' not present in map; skipped placement.", LogLevel.Warn);
                continue;
            }

            npc.currentLocation = location;
            npc.setTileLocation(new Vector2(placement.TileX, placement.TileY));
            npc.Position = new Vector2(placement.TileX, placement.TileY) * 64f;
            npc.FacingDirection = placement.FacingDirection;
            npc.Speed = 0;

            _ = created;
        }
    }

    private void CleanupLocalDateActors()
    {
        if (this.activeLocal is null || this.activeLocal.TempNpcNames.Count == 0)
        {
            return;
        }

        GameLocation? location = Game1.currentLocation;
        if (location is null)
        {
            this.activeLocal.TempNpcNames.Clear();
            return;
        }

        location.characters.RemoveWhere(p => this.activeLocal.TempNpcNames.Contains(p.Name));
        this.activeLocal.TempNpcNames.Clear();
    }

    private void BroadcastPhase(string phaseId, string dialog, int emoteId)
    {
        if (this.activeHost is null)
        {
            return;
        }

        DateEventPhaseMessage phase = new()
        {
            SessionId = this.activeHost.SessionId,
            DateId = this.activeHost.DateId,
            PhaseId = phaseId,
            DialogText = dialog,
            EmoteId = emoteId
        };

        this.ApplyDatePhaseClient(phase);
        this.mod.NetSync.SendToPlayer(MessageType.DateEventPhase, phase, this.activeHost.PlayerAId);
        this.mod.NetSync.SendToPlayer(MessageType.DateEventPhase, phase, this.activeHost.PlayerBId);
    }

    private bool IsLocalParticipantInActiveDate()
    {
        return this.activeLocal is not null
               && (this.activeLocal.PlayerAId == this.mod.LocalPlayerId || this.activeLocal.PlayerBId == this.mod.LocalPlayerId);
    }

    private void PlaceAndFreezeLocalPlayer(int tileX, int tileY, int facing)
    {
        Vector2 tile = new(tileX, tileY);
        Game1.player.setTileLocation(tile);
        Game1.player.Position = tile * 64f;
        Game1.player.FacingDirection = facing;
        Game1.player.CanMove = false;
        Game1.player.UsingTool = false;
        Game1.player.canReleaseTool = false;
    }

    private void SuspendCarryAndHandsForEventPair(long playerAId, long playerBId)
    {
        this.activeHost!.ResumeHandsAfterEvent = false;
        this.activeHost.ResumeCarryAfterEvent = false;

        HoldingHandsSessionState? hands = this.mod.HoldingHandsSystem
            .GetActiveSessionsSnapshot()
            .FirstOrDefault(p => p.Active && ((p.LeaderId == playerAId && p.FollowerId == playerBId) || (p.LeaderId == playerBId && p.FollowerId == playerAId)));
        if (hands is not null)
        {
            this.activeHost.ResumeHandsAfterEvent = true;
            this.mod.HoldingHandsSystem.HandleHoldingHandsStopHost(
                new HoldingHandsStopMessage { FromPlayerId = hands.LeaderId, TargetPlayerId = hands.FollowerId },
                hands.LeaderId);
        }

        CarrySessionState? carry = this.mod.CarrySystem
            .GetActiveSessionsSnapshot()
            .FirstOrDefault(p => p.Active && ((p.CarrierId == playerAId && p.CarriedId == playerBId) || (p.CarrierId == playerBId && p.CarriedId == playerAId)));
        if (carry is not null)
        {
            this.activeHost.ResumeCarryAfterEvent = true;
            this.mod.CarrySystem.HandleCarryStopHost(
                new CarryStopMessage { FromPlayerId = carry.CarrierId, TargetPlayerId = carry.CarriedId },
                carry.CarrierId);
        }
    }

    private void ResumeCarryAndHandsAfterEventPair()
    {
        if (this.activeHost is null)
        {
            return;
        }

        if (this.activeHost.ResumeHandsAfterEvent)
        {
            try
            {
                this.mod.Helper.Reflection.GetMethod(this.mod.HoldingHandsSystem, "StartHandsHost").Invoke(this.activeHost.PlayerAId, this.activeHost.PlayerBId);
            }
            catch
            {
            }
        }

        if (this.activeHost.ResumeCarryAfterEvent)
        {
            try
            {
                this.mod.Helper.Reflection.GetMethod(this.mod.CarrySystem, "StartCarryHost").Invoke(this.activeHost.PlayerAId, this.activeHost.PlayerBId);
            }
            catch
            {
            }
        }
    }

    private void LoadDefinitions()
    {
        this.definitionsById.Clear();
        List<DateEventDefinition>? defs = this.mod.Helper.Data.ReadJsonFile<List<DateEventDefinition>>("assets/date-events/date_event_definitions.json");
        if (defs is null || defs.Count == 0)
        {
            defs = new List<DateEventDefinition>
            {
                new()
                {
                    DateId = "beach_picnic",
                    RequiredHearts = 4,
                    MapName = "Date_Beach",
                    NpcList = new List<DateEventNpcDefinition>
                    {
                        new() { NpcName = "Leah", SpotName = "Npc_Spot_1", FacingDirection = 2 },
                        new() { NpcName = "Elliott", SpotName = "Npc_Spot_2", FacingDirection = 2 }
                    },
                    VendorNpc = new DateEventNpcDefinition { NpcName = "Gus", SpotName = "Npc_Spot_Vendor", FacingDirection = 2 },
                    Reward = new DateEventRewardDefinition { HeartsDelta = 15, Money = 100, ItemId = "(O)421", ItemCount = 1 }
                }
            };
        }

        foreach (DateEventDefinition def in defs.Where(p => !string.IsNullOrWhiteSpace(p.DateId)))
        {
            this.definitionsById[def.DateId] = def;
        }

        this.mod.Monitor.Log($"[PR.System.DateEvent] Loaded {this.definitionsById.Count} date definition(s).", LogLevel.Trace);
    }

    private void DispatchPairEventToParticipants(StartPairEventMessage message, long playerAId, long playerBId)
    {
        if (playerAId != this.mod.LocalPlayerId)
        {
            this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, message, playerAId);
        }
        else
        {
            this.TryStartEventClient(message);
        }

        if (playerBId != this.mod.LocalPlayerId)
        {
            this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, message, playerBId);
        }
        else
        {
            this.TryStartEventClient(message);
        }
    }

    private sealed class ActiveDateRuntimeState
    {
        public string SessionId { get; set; } = string.Empty;
        public string DateId { get; set; } = string.Empty;
        public long PlayerAId { get; set; }
        public long PlayerBId { get; set; }
        public string PlayerAReturnLocation { get; set; } = string.Empty;
        public string PlayerBReturnLocation { get; set; } = string.Empty;
        public Vector2 PlayerAReturnTile { get; set; }
        public Vector2 PlayerBReturnTile { get; set; }
        public int StartTick { get; set; }
        public int LockedTime { get; set; }
        public bool Phase1Sent { get; set; }
        public bool Phase2Sent { get; set; }
        public bool ResumeHandsAfterEvent { get; set; }
        public bool ResumeCarryAfterEvent { get; set; }
        public DateEventRewardDefinition Reward { get; set; } = new();
        public List<DateEventNpcPlacementMessage> Placements { get; set; } = new();
    }

    private sealed class LocalDateRuntimeState
    {
        public string SessionId { get; set; } = string.Empty;
        public string DateId { get; set; } = string.Empty;
        public long PlayerAId { get; set; }
        public long PlayerBId { get; set; }
        public bool IsInputLocked { get; set; }
        public bool IsTimeFrozen { get; set; }
        public int LockedTime { get; set; }
        public string ReturnLocationName { get; set; } = string.Empty;
        public int ReturnTileX { get; set; }
        public int ReturnTileY { get; set; }
        public HashSet<string> TempNpcNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
