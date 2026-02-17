using Microsoft.Xna.Framework;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PlayerRomance.Systems;

public sealed class CarrySystem
{
    private readonly ModEntry mod;
    private readonly Dictionary<long, long> pendingByTarget = new();
    private readonly Dictionary<string, CarrySessionState> activeHostSessions = new();
    private readonly Dictionary<string, CarrySessionState> observedSessions = new();
    private long? localPendingRequesterId;

    public CarrySystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Reset()
    {
        foreach (CarrySessionState session in this.activeHostSessions.Values)
        {
            Farmer? carried = this.mod.FindFarmerById(session.CarriedId, includeOffline: false);
            if (carried is not null)
            {
                carried.CanMove = true;
            }
        }

        this.pendingByTarget.Clear();
        this.activeHostSessions.Clear();
        this.observedSessions.Clear();
        this.localPendingRequesterId = null;
    }

    public bool RequestCarryFromLocal(string targetToken, out string message)
    {
        if (!this.mod.Config.EnableCarry)
        {
            message = "Carry system is disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(targetToken, out Farmer? target) || !this.mod.IsPlayerOnline(target.UniqueMultiplayerID))
        {
            message = $"Player '{targetToken}' not found online.";
            return false;
        }

        if (!this.CanRequestCarry(this.mod.LocalPlayerId, target.UniqueMultiplayerID, out message))
        {
            return false;
        }

        CarryRequestMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            FromPlayerName = this.mod.LocalPlayerName,
            TargetPlayerId = target.UniqueMultiplayerID
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleCarryRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.CarryRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Carry request sent to {target.Name}.";
        return true;
    }

    public bool RespondToPendingCarryLocal(bool accept, out string message)
    {
        if (!this.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out long requesterId))
        {
            message = "No pending carry request.";
            return false;
        }

        CarryDecisionMessage payload = new()
        {
            RequesterId = requesterId,
            ResponderId = this.mod.LocalPlayerId,
            Accepted = accept
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleCarryDecisionHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.CarryDecision, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = accept ? "Carry request accepted." : "Carry request rejected.";
        return true;
    }

    public bool StopCarryFromLocal(string? partnerToken, out string message)
    {
        if (!this.TryGetActiveSessionForPlayer(this.mod.LocalPlayerId, out CarrySessionState? session))
        {
            message = "No active carry session.";
            return false;
        }

        long expectedPartner = session!.CarrierId == this.mod.LocalPlayerId ? session.CarriedId : session.CarrierId;
        if (!string.IsNullOrWhiteSpace(partnerToken))
        {
            if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner) || partner.UniqueMultiplayerID != expectedPartner)
            {
                message = $"Active carry partner is not '{partnerToken}'.";
                return false;
            }
        }

        CarryStopMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            TargetPlayerId = expectedPartner
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleCarryStopHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.CarryStop, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = "Carry stopped.";
        return true;
    }

    public void HandleCarryRequestHost(CarryRequestMessage request, long senderId)
    {
        if (!this.mod.Config.EnableCarry)
        {
            this.mod.NetSync.SendError(senderId, "carry_disabled", "Carry is disabled.");
            return;
        }

        if (request.FromPlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Carry request rejected (sender mismatch).");
            return;
        }

        if (!this.CanRequestCarry(request.FromPlayerId, request.TargetPlayerId, out string reason))
        {
            this.mod.NetSync.SendError(senderId, "carry_invalid", reason);
            return;
        }

        this.pendingByTarget[request.TargetPlayerId] = request.FromPlayerId;
        if (request.TargetPlayerId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = request.FromPlayerId;
            this.mod.RequestPrompts.Enqueue(
                $"carry:{request.FromPlayerId}:{request.TargetPlayerId}",
                "Carry Request",
                $"{request.FromPlayerName} asks to carry you.",
                () =>
                {
                    if (!this.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out long pending) || pending != request.FromPlayerId)
                    {
                        return (false, "Carry request is no longer pending.");
                    }

                    bool accepted = this.RespondToPendingCarryLocal(true, out string msg);
                    return (accepted, msg);
                },
                () =>
                {
                    if (!this.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out long pending) || pending != request.FromPlayerId)
                    {
                        return (false, "Carry request is no longer pending.");
                    }

                    bool rejected = this.RespondToPendingCarryLocal(false, out string msg);
                    return (rejected, msg);
                },
                "[PR.System.Carry]");
        }

        this.mod.NetSync.SendToPlayer(MessageType.CarryRequest, request, request.TargetPlayerId);
        this.mod.Monitor.Log(
            $"[PR.System.Carry] Carry request validated: {request.FromPlayerId} -> {request.TargetPlayerId}.",
            StardewModdingAPI.LogLevel.Trace);
    }

    public void HandleCarryDecisionHost(CarryDecisionMessage decision, long senderId)
    {
        if (!this.mod.Config.EnableCarry)
        {
            this.mod.NetSync.SendError(senderId, "carry_disabled", "Carry is disabled.");
            return;
        }

        if (decision.ResponderId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Carry decision rejected (sender mismatch).");
            return;
        }

        if (!this.pendingByTarget.TryGetValue(decision.ResponderId, out long requesterId) || requesterId != decision.RequesterId)
        {
            this.mod.NetSync.SendError(senderId, "missing_request", "No matching pending carry request found.");
            return;
        }

        this.pendingByTarget.Remove(decision.ResponderId);
        if (decision.ResponderId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = null;
        }

        this.mod.NetSync.Broadcast(
            MessageType.CarryDecision,
            decision,
            decision.RequesterId,
            decision.ResponderId);

        if (!decision.Accepted)
        {
            this.mod.HeartsSystem.AddPointsForPlayers(
                decision.RequesterId,
                decision.ResponderId,
                -Math.Abs(this.mod.Config.RejectionHeartPenalty),
                "carry_rejected");
            string key = ConsentSystem.GetPairKey(decision.RequesterId, decision.ResponderId);
            if (this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relation))
            {
                relation.RejectionsCount++;
                this.mod.MarkDataDirty("Relationship rejection counter incremented.", flushNow: true);
            }
            return;
        }

        this.StartCarryHost(decision.RequesterId, decision.ResponderId);
    }

    public void HandleCarryStopHost(CarryStopMessage stop, long senderId)
    {
        if (stop.FromPlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Carry stop rejected (sender mismatch).");
            return;
        }

        if (!this.TryGetActiveSessionBetween(stop.FromPlayerId, stop.TargetPlayerId, out CarrySessionState? session))
        {
            this.mod.NetSync.SendError(senderId, "not_carrying", "No active carry session to stop.");
            return;
        }

        this.StopCarryHost(session!, "stopped");
    }

    public void OnUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableCarry || this.activeHostSessions.Count == 0)
        {
            return;
        }

        foreach (CarrySessionState session in this.activeHostSessions.Values.ToList())
        {
            Farmer? carrier = this.mod.FindFarmerById(session.CarrierId, includeOffline: false);
            Farmer? carried = this.mod.FindFarmerById(session.CarriedId, includeOffline: false);
            if (carrier is null || carried is null)
            {
                this.StopCarryHost(session, "player offline");
                continue;
            }

            if (carrier.currentLocation != carried.currentLocation)
            {
                if (!this.TryReconcileLocationMismatch(session, carrier, carried))
                {
                    this.StopCarryHost(session, "location changed");
                    continue;
                }
            }

            carried.CanMove = false;
            float bob = (float)Math.Sin(Game1.ticks / 8f) * 2f;
            Vector2 target = carrier.Position + new Vector2(0f, this.mod.Config.CarryOffsetY + bob);
            carried.Position = Vector2.Lerp(carried.Position, target, 0.72f);
            carried.FacingDirection = carrier.FacingDirection;
        }
    }

    public void OnOneSecondUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableCarry || this.activeHostSessions.Count == 0)
        {
            return;
        }

        int regen = Math.Max(0, this.mod.Config.CarryEnergyRegenPerSecond);
        if (regen == 0)
        {
            return;
        }

        foreach (CarrySessionState session in this.activeHostSessions.Values)
        {
            Farmer? carried = this.mod.FindFarmerById(session.CarriedId, includeOffline: false);
            if (carried is null)
            {
                continue;
            }

            if (!IsFatigued(carried))
            {
                continue;
            }

            float before = carried.Stamina;
            carried.Stamina = Math.Min(carried.MaxStamina, carried.Stamina + regen);
            if (carried.Stamina > 0f && carried.exhausted.Value)
            {
                carried.exhausted.Value = false;
            }

            if (carried.Stamina > before)
            {
                this.mod.Monitor.Log(
                    $"[PR.System.Carry] Regen +{regen} for {carried.Name} while carried.",
                    StardewModdingAPI.LogLevel.Trace);
            }
        }
    }

    public void OnPeerDisconnectedHost(long playerId)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        foreach (long target in this.pendingByTarget.Where(p => p.Key == playerId || p.Value == playerId).Select(p => p.Key).ToList())
        {
            this.pendingByTarget.Remove(target);
        }

        foreach (CarrySessionState session in this.activeHostSessions.Values.Where(p => p.CarrierId == playerId || p.CarriedId == playerId).ToList())
        {
            this.StopCarryHost(session, "peer disconnected");
        }
    }

    public void OnWarpedHost(WarpedEventArgs e)
    {
        if (!this.mod.IsHostPlayer || !Context.IsWorldReady || !e.IsLocalPlayer)
        {
            return;
        }

        long warpedPlayerId = this.mod.LocalPlayerId;
        foreach (CarrySessionState session in this.activeHostSessions.Values.Where(p => p.CarrierId == warpedPlayerId || p.CarriedId == warpedPlayerId).ToList())
        {
            Farmer? carrier = this.mod.FindFarmerById(session.CarrierId, includeOffline: false);
            Farmer? carried = this.mod.FindFarmerById(session.CarriedId, includeOffline: false);
            if (carrier is null || carried is null)
            {
                this.StopCarryHost(session, "player offline");
                continue;
            }

            if (!this.TryReconcileLocationMismatch(session, carrier, carried))
            {
                this.StopCarryHost(session, "location changed");
            }
        }
    }

    public void ApplySnapshot(NetSnapshot snapshot)
    {
        this.observedSessions.Clear();
        foreach (CarrySessionState session in snapshot.CarrySessions.Where(p => p.Active))
        {
            this.observedSessions[GetSessionKey(session.CarrierId, session.CarriedId)] = new CarrySessionState
            {
                CarrierId = session.CarrierId,
                CarriedId = session.CarriedId,
                Active = true
            };
        }
    }

    public void ApplyCarryStateMessage(CarryStateMessage message)
    {
        string key = GetSessionKey(message.CarrierId, message.CarriedId);
        if (message.Active)
        {
            this.observedSessions[key] = new CarrySessionState
            {
                CarrierId = message.CarrierId,
                CarriedId = message.CarriedId,
                Active = true
            };
        }
        else
        {
            this.observedSessions.Remove(key);
        }

        if (!message.Active && this.mod.LocalPlayerId == message.CarriedId)
        {
            Game1.player.CanMove = true;
        }
    }

    public void SetLocalPendingCarry(CarryRequestMessage request)
    {
        if (request.TargetPlayerId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = request.FromPlayerId;
        }
    }

    public void ClearLocalPendingCarry(CarryDecisionMessage decision)
    {
        if (decision.ResponderId == this.mod.LocalPlayerId || decision.RequesterId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = null;
        }
    }

    public bool TryGetPendingCarryForPlayer(long targetPlayerId, out long requesterId)
    {
        if (this.mod.IsHostPlayer && this.pendingByTarget.TryGetValue(targetPlayerId, out requesterId))
        {
            return true;
        }

        if (targetPlayerId == this.mod.LocalPlayerId && this.localPendingRequesterId.HasValue)
        {
            requesterId = this.localPendingRequesterId.Value;
            return true;
        }

        requesterId = -1;
        return false;
    }

    public bool IsCarryActiveBetween(long playerA, long playerB)
    {
        return this.GetSessionsForRead().Any(p =>
            p.Active &&
            (p.CarrierId == playerA && p.CarriedId == playerB
             || p.CarrierId == playerB && p.CarriedId == playerA));
    }

    public bool IsPlayerInActiveCarry(long playerId)
    {
        return this.GetSessionsForRead().Any(p => p.Active && (p.CarrierId == playerId || p.CarriedId == playerId));
    }

    public IReadOnlyList<CarrySessionState> GetActiveSessionsSnapshot()
    {
        return this.GetSessionsForRead()
            .Where(p => p.Active)
            .Select(p => new CarrySessionState
            {
                CarrierId = p.CarrierId,
                CarriedId = p.CarriedId,
                Active = p.Active
            })
            .ToList();
    }

    public bool CanRequestCarry(long requesterId, long targetId, out string reason)
    {
        if (!this.mod.Config.EnableCarry)
        {
            reason = "Carry disabled.";
            return false;
        }

        if (requesterId == targetId)
        {
            reason = "You cannot carry yourself.";
            return false;
        }

        Farmer? requester = this.mod.FindFarmerById(requesterId, includeOffline: false);
        Farmer? target = this.mod.FindFarmerById(targetId, includeOffline: false);
        if (requester is null || target is null)
        {
            reason = "Both players must be online.";
            return false;
        }

        if (requester.currentLocation != target.currentLocation)
        {
            reason = "Players must be in the same location.";
            return false;
        }

        float distance = Vector2.Distance(requester.Tile, target.Tile);
        if (distance > 3.5f)
        {
            reason = "You must be close to the target player.";
            return false;
        }

        if (this.IsPlayerInActiveCarry(requesterId) || this.IsPlayerInActiveCarry(targetId))
        {
            reason = "One player is already in a carry session.";
            return false;
        }

        if (this.mod.HoldingHandsSystem.IsPlayerInHandsSession(requesterId) || this.mod.HoldingHandsSystem.IsPlayerInHandsSession(targetId))
        {
            reason = "Cannot start carry while holding hands is active.";
            return false;
        }

        if (this.pendingByTarget.ContainsKey(targetId))
        {
            reason = "Target player already has a pending carry request.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void StartCarryHost(long carrierId, long carriedId)
    {
        if (this.IsPlayerInActiveCarry(carrierId) || this.IsPlayerInActiveCarry(carriedId))
        {
            this.mod.NetSync.SendError(carrierId, "already_carrying", "Carry start rejected because a session already exists.");
            return;
        }

        if (this.mod.HoldingHandsSystem.IsPlayerInHandsSession(carrierId) || this.mod.HoldingHandsSystem.IsPlayerInHandsSession(carriedId))
        {
            this.mod.NetSync.SendError(carrierId, "hands_active", "Cannot start carry while holding hands is active.");
            return;
        }

        CarrySessionState session = new()
        {
            CarrierId = carrierId,
            CarriedId = carriedId,
            Active = true
        };
        this.activeHostSessions[GetSessionKey(carrierId, carriedId)] = session;

        Farmer? carried = this.mod.FindFarmerById(carriedId, includeOffline: false);
        if (carried is not null)
        {
            carried.CanMove = false;
        }
        this.TryPlayEmote(carrierId, 20);
        this.TryPlayEmote(carriedId, 20);

        this.mod.NetSync.Broadcast(
            MessageType.CarryState,
            new CarryStateMessage
            {
                CarrierId = carrierId,
                CarriedId = carriedId,
                Active = true,
                Reason = "started"
            });
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private void StopCarryHost(CarrySessionState session, string reason)
    {
        string key = GetSessionKey(session.CarrierId, session.CarriedId);
        if (!this.activeHostSessions.Remove(key))
        {
            return;
        }

        Farmer? carried = this.mod.FindFarmerById(session.CarriedId, includeOffline: false);
        if (carried is not null)
        {
            carried.CanMove = true;
        }

        this.mod.NetSync.Broadcast(
            MessageType.CarryState,
            new CarryStateMessage
            {
                CarrierId = session.CarrierId,
                CarriedId = session.CarriedId,
                Active = false,
                Reason = reason
            });
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private bool TryGetActiveSessionForPlayer(long playerId, out CarrySessionState? session)
    {
        session = this.GetSessionsForRead().FirstOrDefault(p => p.Active && (p.CarrierId == playerId || p.CarriedId == playerId));
        return session is not null;
    }

    private bool TryGetActiveSessionBetween(long playerA, long playerB, out CarrySessionState? session)
    {
        session = this.GetSessionsForRead().FirstOrDefault(p =>
            p.Active &&
            (p.CarrierId == playerA && p.CarriedId == playerB
             || p.CarrierId == playerB && p.CarriedId == playerA));
        return session is not null;
    }

    private IReadOnlyCollection<CarrySessionState> GetSessionsForRead()
    {
        return this.mod.IsHostPlayer ? this.activeHostSessions.Values : this.observedSessions.Values;
    }

    private static string GetSessionKey(long carrierId, long carriedId)
    {
        return $"{carrierId}->{carriedId}";
    }

    private static bool IsFatigued(Farmer farmer)
    {
        return farmer.exhausted.Value || farmer.Stamina <= 0f;
    }

    private bool TryReconcileLocationMismatch(CarrySessionState session, Farmer carrier, Farmer carried)
    {
        GameLocation? targetLocation = carrier.currentLocation;
        if (targetLocation is null)
        {
            return false;
        }

        Vector2 targetTile = carrier.Tile + new Vector2(1f, 0f);
        Vector2 targetPos = targetTile * 64f;
        if (!float.IsFinite(targetPos.X) || !float.IsFinite(targetPos.Y))
        {
            return false;
        }

        if (carried.currentLocation != targetLocation)
        {
            carried.currentLocation = targetLocation;
            carried.setTileLocation(targetTile);
            this.mod.Monitor.Log(
                $"[PR.System.Carry] Carry session {session.CarrierId}->{session.CarriedId}: warped carried player to '{targetLocation.NameOrUniqueName}'.",
                StardewModdingAPI.LogLevel.Trace);
        }

        carried.CanMove = false;
        carried.Position = targetPos + new Vector2(0f, this.mod.Config.CarryOffsetY);
        carried.FacingDirection = carrier.FacingDirection;
        this.mod.NetSync.Broadcast(
            MessageType.CarryState,
            new CarryStateMessage
            {
                CarrierId = session.CarrierId,
                CarriedId = session.CarriedId,
                Active = true,
                Reason = "location_sync"
            });
        this.mod.NetSync.BroadcastSnapshotToAll();
        return true;
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
}
