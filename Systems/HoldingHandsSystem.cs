using Microsoft.Xna.Framework;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;
using xTile.Dimensions;

namespace PlayerRomance.Systems;

public sealed class HoldingHandsSystem
{
    private readonly ModEntry mod;
    private readonly Dictionary<long, long> pendingByTarget = new();
    private readonly Dictionary<string, HoldingHandsSessionState> activeHostSessions = new();
    private readonly Dictionary<string, HoldingHandsSessionState> observedSessions = new();
    private readonly Dictionary<string, (Vector2 leader, Vector2 follower)> lastPositions = new();
    private readonly Dictionary<string, Vector2> followerVelocityBySession = new();
    private long? localPendingRequesterId;

    public HoldingHandsSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Reset()
    {
        this.pendingByTarget.Clear();
        this.activeHostSessions.Clear();
        this.observedSessions.Clear();
        this.lastPositions.Clear();
        this.followerVelocityBySession.Clear();
        this.localPendingRequesterId = null;
    }

    public bool RequestHoldingHandsFromLocal(string targetToken, out string message)
    {
        if (!this.mod.Config.EnableHoldingHands)
        {
            message = "Holding hands is disabled in config.";
            return false;
        }

        if (!this.mod.TryResolvePlayerToken(targetToken, out Farmer? target) || !this.mod.IsPlayerOnline(target.UniqueMultiplayerID))
        {
            message = $"Player '{targetToken}' not found online.";
            return false;
        }

        if (!this.CanRequestHands(this.mod.LocalPlayerId, target.UniqueMultiplayerID, out message))
        {
            return false;
        }

        HoldingHandsRequestMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            FromPlayerName = this.mod.LocalPlayerName,
            TargetPlayerId = target.UniqueMultiplayerID
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleHoldingHandsRequestHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.HoldingHandsRequest, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = $"Holding hands request sent to {target.Name}.";
        return true;
    }

    public bool RespondToPendingHoldingHandsLocal(bool accept, out string message)
    {
        if (!this.TryGetPendingForPlayer(this.mod.LocalPlayerId, out long requesterId))
        {
            message = "No pending holding hands request.";
            return false;
        }

        HoldingHandsDecisionMessage payload = new()
        {
            RequesterId = requesterId,
            ResponderId = this.mod.LocalPlayerId,
            Accepted = accept
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleHoldingHandsDecisionHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.HoldingHandsDecision, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = accept ? "Holding hands request accepted." : "Holding hands request rejected.";
        return true;
    }

    public bool StopHoldingHandsFromLocal(string? partnerToken, out string message)
    {
        if (!this.TryGetSessionForPlayer(this.mod.LocalPlayerId, out HoldingHandsSessionState? session))
        {
            message = "No active holding hands session.";
            return false;
        }

        long partnerId = session!.LeaderId == this.mod.LocalPlayerId ? session.FollowerId : session.LeaderId;
        if (!string.IsNullOrWhiteSpace(partnerToken))
        {
            if (!this.mod.TryResolvePlayerToken(partnerToken, out Farmer? partner) || partner.UniqueMultiplayerID != partnerId)
            {
                message = $"Active partner is not '{partnerToken}'.";
                return false;
            }
        }

        HoldingHandsStopMessage payload = new()
        {
            FromPlayerId = this.mod.LocalPlayerId,
            TargetPlayerId = partnerId
        };

        if (this.mod.IsHostPlayer)
        {
            this.HandleHoldingHandsStopHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.HoldingHandsStop, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        message = "Holding hands stopped.";
        return true;
    }

    public void HandleHoldingHandsRequestHost(HoldingHandsRequestMessage request, long senderId)
    {
        if (!this.mod.Config.EnableHoldingHands)
        {
            this.mod.NetSync.SendError(senderId, "holding_hands_disabled", "Holding hands is disabled.");
            return;
        }

        if (request.FromPlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Holding hands request rejected (sender mismatch).");
            return;
        }

        if (!this.CanRequestHands(request.FromPlayerId, request.TargetPlayerId, out string reason))
        {
            this.mod.NetSync.SendError(senderId, "holding_hands_invalid", reason);
            return;
        }

        this.pendingByTarget[request.TargetPlayerId] = request.FromPlayerId;
        if (request.TargetPlayerId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = request.FromPlayerId;
            this.mod.RequestPrompts.Enqueue(
                $"hands:{request.FromPlayerId}:{request.TargetPlayerId}",
                "Holding Hands",
                $"{request.FromPlayerName} asks to hold hands.",
                () =>
                {
                    if (!this.TryGetPendingForPlayer(this.mod.LocalPlayerId, out long pending) || pending != request.FromPlayerId)
                    {
                        return (false, "Holding hands request is no longer pending.");
                    }

                    bool accepted = this.RespondToPendingHoldingHandsLocal(true, out string msg);
                    return (accepted, msg);
                },
                () =>
                {
                    if (!this.TryGetPendingForPlayer(this.mod.LocalPlayerId, out long pending) || pending != request.FromPlayerId)
                    {
                        return (false, "Holding hands request is no longer pending.");
                    }

                    bool rejected = this.RespondToPendingHoldingHandsLocal(false, out string msg);
                    return (rejected, msg);
                },
                "[PR.System.HoldingHands]");
        }

        this.mod.NetSync.SendToPlayer(MessageType.HoldingHandsRequest, request, request.TargetPlayerId);
    }

    public void HandleHoldingHandsDecisionHost(HoldingHandsDecisionMessage decision, long senderId)
    {
        if (decision.ResponderId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Holding hands decision rejected (sender mismatch).");
            return;
        }

        if (!this.pendingByTarget.TryGetValue(decision.ResponderId, out long requesterId) || requesterId != decision.RequesterId)
        {
            this.mod.NetSync.SendError(senderId, "missing_request", "No matching holding hands request.");
            return;
        }

        this.pendingByTarget.Remove(decision.ResponderId);
        if (decision.ResponderId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = null;
        }

        this.mod.NetSync.Broadcast(
            MessageType.HoldingHandsDecision,
            decision,
            decision.RequesterId,
            decision.ResponderId);

        if (!decision.Accepted)
        {
            this.ApplyRejectionPenalty(decision.RequesterId, decision.ResponderId, "holding_hands_rejected");
            return;
        }

        this.StartHandsHost(decision.RequesterId, decision.ResponderId);
    }

    public void HandleHoldingHandsStopHost(HoldingHandsStopMessage stop, long senderId)
    {
        if (stop.FromPlayerId != senderId)
        {
            this.mod.NetSync.SendError(senderId, "sender_mismatch", "Holding hands stop rejected (sender mismatch).");
            return;
        }

        if (!this.TryGetSessionBetween(stop.FromPlayerId, stop.TargetPlayerId, out HoldingHandsSessionState? session))
        {
            this.mod.NetSync.SendError(senderId, "missing_session", "No active holding hands session.");
            return;
        }

        this.StopHandsHost(session!, "stopped");
    }

    public void OnUpdateTickedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableHoldingHands || this.activeHostSessions.Count == 0)
        {
            return;
        }

        foreach ((string key, HoldingHandsSessionState session) in this.activeHostSessions.ToList())
        {
            Farmer? leader = this.mod.FindFarmerById(session.LeaderId, includeOffline: false);
            Farmer? follower = this.mod.FindFarmerById(session.FollowerId, includeOffline: false);
            if (leader is null || follower is null)
            {
                this.LogStopDebug(session, "player_offline", -1f, leader, follower);
                this.StopHandsHost(session, "player offline");
                continue;
            }

            if (leader.currentLocation != follower.currentLocation)
            {
                float mismatchDistance = Vector2.Distance(leader.Tile, follower.Tile);
                this.LogStopDebug(session, "location_mismatch", mismatchDistance, leader, follower);
                this.StopHandsHost(session, "location mismatch");
                continue;
            }

            float distanceTiles = Vector2.Distance(leader.Tile, follower.Tile);
            if (distanceTiles > Math.Max(this.mod.Config.HoldingHandsBreakDistanceTiles, this.mod.Config.HandsEmergencyStopDistanceTiles))
            {
                this.LogStopDebug(session, "emergency_distance", distanceTiles, leader, follower);
                this.StopHandsHost(session, "emergency distance");
                continue;
            }
            if (distanceTiles > this.mod.Config.HoldingHandsBreakDistanceTiles)
            {
                this.LogStopDebug(session, "distance_break", distanceTiles, leader, follower);
                this.StopHandsHost(session, "distance break");
                continue;
            }

            Vector2 sideOffset = this.GetFollowerOffsetPixels(leader.FacingDirection);
            float sway = (float)Math.Sin(Game1.ticks / 11f) * 1.2f;
            Vector2 target = leader.Position + sideOffset + new Vector2(0f, sway);
            Vector2 error = target - follower.Position;
            Vector2 velocity = this.followerVelocityBySession.TryGetValue(key, out Vector2 stored) ? stored : Vector2.Zero;

            float spring = Math.Clamp(this.mod.Config.HandsSpringStrength, 0.01f, 0.8f);
            float damping = Math.Clamp(this.mod.Config.HandsDamping, 0.01f, 0.99f);
            velocity = velocity * damping + error * spring;

            float maxMove = Math.Max(1f, this.mod.Config.HandsMaxMovePixelsPerTick);
            float deltaLen = velocity.Length();
            if (deltaLen > maxMove)
            {
                velocity *= maxMove / deltaLen;
            }

            float softMaxDistTiles = Math.Max(0.75f, this.mod.Config.HoldingHandsSoftMaxDistanceTiles);
            float softMaxDistPx = softMaxDistTiles * 64f;
            Vector2 leaderToFollower = follower.Position - leader.Position;
            if (leaderToFollower.Length() > softMaxDistPx)
            {
                Vector2 clamped = Vector2.Normalize(leaderToFollower) * softMaxDistPx;
                Vector2 desired = leader.Position + clamped;
                velocity = desired - follower.Position;
                float vLen = velocity.Length();
                if (vLen > maxMove)
                {
                    velocity *= maxMove / vLen;
                }
            }

            Vector2 candidate = follower.Position + velocity;
            if (!this.IsSafeFollowerPosition(leader.currentLocation, candidate))
            {
                this.LogStopDebug(session, "invalid_position", distanceTiles, leader, follower);
                this.StopHandsHost(session, "invalid position");
                continue;
            }

            follower.Position = candidate;
            follower.FacingDirection = leader.FacingDirection;
            this.followerVelocityBySession[key] = velocity;
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

        foreach (HoldingHandsSessionState session in this.activeHostSessions.Values.Where(p => p.LeaderId == playerId || p.FollowerId == playerId).ToList())
        {
            this.StopHandsHost(session, "peer disconnected");
        }
    }

    public void ApplySnapshot(NetSnapshot snapshot)
    {
        this.observedSessions.Clear();
        foreach (HoldingHandsSessionState session in snapshot.HoldingHandsSessions.Where(p => p.Active))
        {
            this.observedSessions[this.GetSessionKey(session.LeaderId, session.FollowerId)] = new HoldingHandsSessionState
            {
                LeaderId = session.LeaderId,
                FollowerId = session.FollowerId,
                Active = true
            };
        }
    }

    public void ApplyState(HoldingHandsStateMessage message)
    {
        string key = this.GetSessionKey(message.LeaderId, message.FollowerId);
        if (message.Active)
        {
            this.observedSessions[key] = new HoldingHandsSessionState
            {
                LeaderId = message.LeaderId,
                FollowerId = message.FollowerId,
                Active = true
            };
        }
        else
        {
            this.observedSessions.Remove(key);
        }
    }

    public void SetLocalPending(HoldingHandsRequestMessage request)
    {
        if (request.TargetPlayerId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = request.FromPlayerId;
        }
    }

    public void ClearLocalPending(HoldingHandsDecisionMessage decision)
    {
        if (decision.RequesterId == this.mod.LocalPlayerId || decision.ResponderId == this.mod.LocalPlayerId)
        {
            this.localPendingRequesterId = null;
        }
    }

    public bool TryGetPendingForPlayer(long targetPlayerId, out long requesterId)
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

    public bool IsHandsActiveBetween(long playerAId, long playerBId)
    {
        return this.GetSessionsForRead().Any(p => p.Active
            && (p.LeaderId == playerAId && p.FollowerId == playerBId
             || p.LeaderId == playerBId && p.FollowerId == playerAId));
    }

    public bool IsPlayerInHandsSession(long playerId)
    {
        return this.GetSessionsForRead().Any(p => p.Active && (p.LeaderId == playerId || p.FollowerId == playerId));
    }

    public bool GetDebugStatusFromLocal(out string message)
    {
        IReadOnlyCollection<HoldingHandsSessionState> sessions = this.GetSessionsForRead();
        if (sessions.Count == 0)
        {
            message = "No active holding hands sessions.";
            return false;
        }

        List<string> parts = new();
        foreach (HoldingHandsSessionState session in sessions)
        {
            Farmer? leader = this.mod.FindFarmerById(session.LeaderId, includeOffline: true);
            Farmer? follower = this.mod.FindFarmerById(session.FollowerId, includeOffline: true);
            string leaderLoc = leader?.currentLocation?.NameOrUniqueName ?? "null";
            string followerLoc = follower?.currentLocation?.NameOrUniqueName ?? "null";
            float distanceTiles = leader is not null && follower is not null
                ? Vector2.Distance(leader.Tile, follower.Tile)
                : -1f;
            parts.Add($"session {session.LeaderId}->{session.FollowerId} active={session.Active} dist={distanceTiles:0.00} loc=({leaderLoc}|{followerLoc})");
        }

        message = string.Join(" || ", parts);
        return true;
    }

    public IReadOnlyList<HoldingHandsSessionState> GetActiveSessionsSnapshot()
    {
        return this.GetSessionsForRead()
            .Where(p => p.Active)
            .Select(p => new HoldingHandsSessionState
            {
                LeaderId = p.LeaderId,
                FollowerId = p.FollowerId,
                Active = true
            })
            .ToList();
    }

    public bool CanRequestHands(long requesterId, long targetId, out string reason)
    {
        if (!this.mod.Config.EnableHoldingHands)
        {
            reason = "Holding hands disabled.";
            return false;
        }

        if (requesterId == targetId)
        {
            reason = "You cannot hold hands with yourself.";
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

        if (Vector2.Distance(requester.Tile, target.Tile) > 3.5f)
        {
            reason = "Players must be close to each other.";
            return false;
        }

        if (!this.mod.HeartsSystem.IsAtLeastHearts(requesterId, targetId, this.mod.Config.HoldingHandsMinHearts))
        {
            reason = $"Requires at least {this.mod.Config.HoldingHandsMinHearts} hearts.";
            return false;
        }

        if (this.mod.CarrySystem.IsPlayerInActiveCarry(requesterId) || this.mod.CarrySystem.IsPlayerInActiveCarry(targetId))
        {
            reason = "Cannot start while carry session is active.";
            return false;
        }

        if (this.IsPlayerInHandsSession(requesterId) || this.IsPlayerInHandsSession(targetId))
        {
            reason = "A holding hands session is already active.";
            return false;
        }

        if (this.pendingByTarget.ContainsKey(targetId))
        {
            reason = "Target already has a pending request.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void StartHandsHost(long leaderId, long followerId)
    {
        string key = this.GetSessionKey(leaderId, followerId);
        HoldingHandsSessionState session = new()
        {
            LeaderId = leaderId,
            FollowerId = followerId,
            Active = true
        };
        this.activeHostSessions[key] = session;
        this.lastPositions[key] = (Vector2.Zero, Vector2.Zero);

        string pairKey = ConsentSystem.GetPairKey(leaderId, followerId);
        if (!this.mod.HostSaveData.HoldingHandsHistory.TryGetValue(pairKey, out HoldingHandsPairRecord? history))
        {
            history = new HoldingHandsPairRecord
            {
                PairKey = pairKey
            };
            this.mod.HostSaveData.HoldingHandsHistory[pairKey] = history;
        }

        history.LastStartedDay = this.mod.GetCurrentDayNumber();
        history.TotalSessions++;
        this.mod.MarkDataDirty("Holding hands session started.", flushNow: true);
        this.TryPlayEmote(leaderId, 20);
        this.TryPlayEmote(followerId, 20);

        this.mod.NetSync.Broadcast(
            MessageType.HoldingHandsState,
            new HoldingHandsStateMessage
            {
                LeaderId = leaderId,
                FollowerId = followerId,
                Active = true,
                Reason = "started"
            },
            leaderId,
            followerId);
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private void StopHandsHost(HoldingHandsSessionState session, string reason)
    {
        string key = this.GetSessionKey(session.LeaderId, session.FollowerId);
        if (!this.activeHostSessions.Remove(key))
        {
            return;
        }
        this.mod.Monitor.Log(
            $"[PR.System.HoldingHands] Stop session {session.LeaderId}->{session.FollowerId}, reason={reason}.",
            StardewModdingAPI.LogLevel.Info);

        this.lastPositions.Remove(key);
        this.followerVelocityBySession.Remove(key);
        this.mod.MarkDataDirty($"Holding hands stopped ({reason}).", flushNow: true);

        this.mod.NetSync.Broadcast(
            MessageType.HoldingHandsState,
            new HoldingHandsStateMessage
            {
                LeaderId = session.LeaderId,
                FollowerId = session.FollowerId,
                Active = false,
                Reason = reason
            },
            session.LeaderId,
            session.FollowerId);
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private void ApplyRejectionPenalty(long requesterId, long responderId, string source)
    {
        this.mod.HeartsSystem.AddPointsForPlayers(requesterId, responderId, -Math.Abs(this.mod.Config.RejectionHeartPenalty), source);
        string key = ConsentSystem.GetPairKey(requesterId, responderId);
        if (this.mod.HostSaveData.Relationships.TryGetValue(key, out RelationshipRecord? relation))
        {
            relation.RejectionsCount++;
            this.mod.MarkDataDirty("Relationship rejection counter incremented.", flushNow: true);
        }
    }

    private void HandleLeaderSwitchByMovement(string key, HoldingHandsSessionState session, Farmer leader, Farmer follower)
    {
        if (!this.lastPositions.TryGetValue(key, out (Vector2 leader, Vector2 follower) old))
        {
            this.lastPositions[key] = (leader.Position, follower.Position);
            return;
        }

        float leaderDelta = Vector2.Distance(old.leader, leader.Position);
        float followerDelta = Vector2.Distance(old.follower, follower.Position);
        this.lastPositions[key] = (leader.Position, follower.Position);

        if (followerDelta > 0.35f && leaderDelta < 0.15f)
        {
            session.LeaderId = follower.UniqueMultiplayerID;
            session.FollowerId = leader.UniqueMultiplayerID;
            this.activeHostSessions[key] = session;
        }
    }

    private Vector2 GetFollowerOffsetPixels(int facingDirection)
    {
        int offset = Math.Max(8, this.mod.Config.HoldingHandsOffsetPixels);
        return facingDirection switch
        {
            0 => new Vector2(offset, 0),
            1 => new Vector2(0, -offset),
            2 => new Vector2(-offset, 0),
            3 => new Vector2(0, offset),
            _ => new Vector2(offset, 0)
        };
    }

    private bool IsSafeFollowerPosition(GameLocation location, Vector2 candidatePixels)
    {
        if (float.IsNaN(candidatePixels.X)
            || float.IsNaN(candidatePixels.Y)
            || float.IsInfinity(candidatePixels.X)
            || float.IsInfinity(candidatePixels.Y))
        {
            return false;
        }

        Vector2 tile = candidatePixels / 64f;
        int width = location.Map?.Layers[0]?.LayerWidth ?? 0;
        int height = location.Map?.Layers[0]?.LayerHeight ?? 0;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (tile.X < 1f
            || tile.Y < 1f
            || tile.X >= width - 1f
            || tile.Y >= height - 1f)
        {
            return false;
        }

        Location tileLoc = new((int)Math.Floor(tile.X), (int)Math.Floor(tile.Y));
        return location.isTileLocationOpen(tileLoc);
    }

    private void LogStopDebug(HoldingHandsSessionState session, string reason, float distanceTiles, Farmer? leader, Farmer? follower)
    {
        string leaderLoc = leader?.currentLocation?.NameOrUniqueName ?? "null";
        string followerLoc = follower?.currentLocation?.NameOrUniqueName ?? "null";
        this.mod.Monitor.Log(
            $"[PR.System.HoldingHands] stop reason={reason}, session={session.LeaderId}->{session.FollowerId}, distance={distanceTiles:0.00}, leaderLoc={leaderLoc}, followerLoc={followerLoc}.",
            StardewModdingAPI.LogLevel.Trace);
    }

    private bool TryGetSessionForPlayer(long playerId, out HoldingHandsSessionState? session)
    {
        session = this.GetSessionsForRead().FirstOrDefault(p => p.Active && (p.LeaderId == playerId || p.FollowerId == playerId));
        return session is not null;
    }

    private bool TryGetSessionBetween(long playerAId, long playerBId, out HoldingHandsSessionState? session)
    {
        session = this.GetSessionsForRead().FirstOrDefault(p => p.Active
            && (p.LeaderId == playerAId && p.FollowerId == playerBId
             || p.LeaderId == playerBId && p.FollowerId == playerAId));
        return session is not null;
    }

    private IReadOnlyCollection<HoldingHandsSessionState> GetSessionsForRead()
    {
        return this.mod.IsHostPlayer ? this.activeHostSessions.Values : this.observedSessions.Values;
    }

    private string GetSessionKey(long leaderId, long followerId)
    {
        long a = Math.Min(leaderId, followerId);
        long b = Math.Max(leaderId, followerId);
        return $"{a}_{b}";
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
