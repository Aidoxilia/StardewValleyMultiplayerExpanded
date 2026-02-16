using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Events;

public sealed class DateEventController
{
    private readonly ModEntry mod;

    public DateEventController(ModEntry mod)
    {
        this.mod = mod;
    }

    public bool StartDateFromLocal(string partnerToken, out string message)
    {
        if (!this.mod.Config.EnableDateEvents)
        {
            message = "Date events are disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner))
        {
            message = $"Player '{partnerToken}' not found.";
            return false;
        }

        StartPairEventMessage payload = new()
        {
            PlayerAId = this.mod.LocalPlayerId,
            PlayerBId = partner.UniqueMultiplayerID,
            EventId = "date",
            LocationName = "Town",
            TileX = 24,
            TileY = 62,
            DialogText = $"{this.mod.LocalPlayerName} and {partner.Name} enjoyed a peaceful date in town."
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleStartDateRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Date request sent to host for {partner.Name}.";
        return true;
    }

    public bool StartImmersiveDateFromLocal(string partnerToken, ImmersiveDateLocation location, out string message)
    {
        return this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(partnerToken, location, out message);
    }

    public void HandleStartDateRequestHost(StartPairEventMessage request, long senderId)
    {
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
}
