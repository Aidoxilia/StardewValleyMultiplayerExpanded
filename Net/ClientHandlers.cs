using StardewModdingAPI.Events;
using PlayerRomance.Data;
using StardewValley;

namespace PlayerRomance.Net;

public sealed class ClientHandlers
{
    private readonly ModEntry mod;

    public ClientHandlers(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Handle(MessageType type, ModMessageReceivedEventArgs e)
    {
        switch (type)
        {
            case MessageType.Snapshot:
                {
                    SnapshotMessage snapshot = e.ReadAs<SnapshotMessage>();
                    this.mod.NetSync.ApplySnapshot(snapshot.Snapshot);
                    this.mod.ChildGrowthSystem.RebuildAdultChildrenForActiveState();
                    break;
                }
            case MessageType.ProposalDating:
                {
                    RelationshipProposalMessage proposal = e.ReadAs<RelationshipProposalMessage>();
                    if (proposal.TargetPlayerId == this.mod.LocalPlayerId)
                    {
                        this.mod.RequestPrompts.Enqueue(
                            $"dating:{proposal.FromPlayerId}:{proposal.TargetPlayerId}",
                            "Dating Request",
                            $"{proposal.FromPlayerName} asks you to start dating.",
                            () => this.ResolveDatingProposal(proposal.FromPlayerId, accept: true),
                            () => this.ResolveDatingProposal(proposal.FromPlayerId, accept: false),
                            "[PR.System.Dating]");
                    }
                    break;
                }
            case MessageType.DatingDecision:
                {
                    RelationshipDecisionMessage decision = e.ReadAs<RelationshipDecisionMessage>();
                    if (decision.RequesterId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            decision.Accepted ? "Dating request accepted." : "Dating request rejected.",
                            "[PR.System.Dating]");
                    }
                    break;
                }
            case MessageType.ProposalMarriage:
                {
                    RelationshipProposalMessage proposal = e.ReadAs<RelationshipProposalMessage>();
                    if (proposal.TargetPlayerId == this.mod.LocalPlayerId)
                    {
                        this.mod.RequestPrompts.Enqueue(
                            $"marriage:{proposal.FromPlayerId}:{proposal.TargetPlayerId}",
                            "Marriage Request",
                            $"{proposal.FromPlayerName} asks you to get married.",
                            () => this.ResolveMarriageProposal(proposal.FromPlayerId, accept: true),
                            () => this.ResolveMarriageProposal(proposal.FromPlayerId, accept: false),
                            "[PR.System.Marriage]");
                    }
                    break;
                }
            case MessageType.MarriageDecision:
                {
                    RelationshipDecisionMessage decision = e.ReadAs<RelationshipDecisionMessage>();
                    if (decision.RequesterId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            decision.Accepted ? "Marriage request accepted." : "Marriage request rejected.",
                            "[PR.System.Marriage]");
                    }
                    break;
                }
            case MessageType.StartDateEvent:
                {
                    StartPairEventMessage start = e.ReadAs<StartPairEventMessage>();
                    this.mod.DateEventController.TryStartEventClient(start);
                    break;
                }
            case MessageType.DateEventStep:
                {
                    StartPairEventMessage step = e.ReadAs<StartPairEventMessage>();
                    this.mod.DateEventController.ApplyEventStepClient(step);
                    break;
                }
            case MessageType.DateEventPhase:
                {
                    DateEventPhaseMessage phase = e.ReadAs<DateEventPhaseMessage>();
                    this.mod.DateEventController.ApplyDatePhaseClient(phase);
                    break;
                }
            case MessageType.EndDateEvent:
                {
                    EndDateEventMessage end = e.ReadAs<EndDateEventMessage>();
                    this.mod.DateEventController.ApplyEndDateClient(end);
                    break;
                }
            case MessageType.PregnancyOptIn:
                {
                    PregnancyOptInMessage optIn = e.ReadAs<PregnancyOptInMessage>();
                    if (optIn.PartnerId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            $"Partner changed baby opt-in to {(optIn.OptIn ? "ON" : "OFF")}.",
                            "[PR.System.Pregnancy]");
                    }
                    break;
                }
            case MessageType.TryForBabyRequest:
                {
                    TryForBabyMessage request = e.ReadAs<TryForBabyMessage>();
                    if (request.PartnerId == this.mod.LocalPlayerId)
                    {
                        Farmer? from = this.mod.FindFarmerById(request.FromPlayerId, includeOffline: true);
                        this.mod.RequestPrompts.Enqueue(
                            $"trybaby:{request.FromPlayerId}:{request.PartnerId}",
                            "Try For Baby",
                            $"{from?.Name ?? "Your partner"} asks to try for baby.",
                            () => this.ResolveTryForBaby(request.FromPlayerId, accept: true),
                            () => this.ResolveTryForBaby(request.FromPlayerId, accept: false),
                            "[PR.System.Pregnancy]");
                    }
                    break;
                }
            case MessageType.TryForBabyDecision:
                {
                    TryForBabyMessage decision = e.ReadAs<TryForBabyMessage>();
                    if (decision.PartnerId == this.mod.LocalPlayerId || decision.FromPlayerId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            decision.Accepted ? "Try for baby accepted." : "Try for baby rejected.",
                            "[PR.System.Pregnancy]");
                    }
                    break;
                }
            case MessageType.ChildBorn:
                {
                    ChildSyncMessage childBorn = e.ReadAs<ChildSyncMessage>();
                    this.mod.Notifier.NotifyInfo(
                        $"A child was born: {childBorn.Child.ChildName}.",
                        "[PR.System.Pregnancy]");
                    break;
                }
            case MessageType.ChildGrewUp:
                {
                    ChildSyncMessage grew = e.ReadAs<ChildSyncMessage>();
                    this.mod.Notifier.NotifyInfo(
                        $"{grew.Child.ChildName} reached stage: {grew.Child.Stage}.",
                        "[PR.System.ChildGrowth]");
                    break;
                }
            case MessageType.FarmWorkReport:
                {
                    FarmWorkReportMessage report = e.ReadAs<FarmWorkReportMessage>();
                    this.mod.LastFarmWorkReport = report.Report;
                    if (!string.IsNullOrWhiteSpace(report.Report))
                    {
                        this.mod.Notifier.NotifyInfo(report.Report, "[PR.System.Worker]");
                    }

                    break;
                }
            case MessageType.CarryRequest:
                {
                    CarryRequestMessage request = e.ReadAs<CarryRequestMessage>();
                    this.mod.CarrySystem.SetLocalPendingCarry(request);
                    if (request.TargetPlayerId == this.mod.LocalPlayerId)
                    {
                        this.mod.RequestPrompts.Enqueue(
                            $"carry:{request.FromPlayerId}:{request.TargetPlayerId}",
                            "Carry Request",
                            $"{request.FromPlayerName} asks to carry you.",
                            () => this.ResolveCarryRequest(request.FromPlayerId, accept: true),
                            () => this.ResolveCarryRequest(request.FromPlayerId, accept: false),
                            "[PR.System.Carry]");
                    }
                    break;
                }
            case MessageType.CarryDecision:
                {
                    CarryDecisionMessage decision = e.ReadAs<CarryDecisionMessage>();
                    this.mod.CarrySystem.ClearLocalPendingCarry(decision);
                    if (decision.RequesterId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            decision.Accepted ? "Carry request accepted." : "Carry request rejected.",
                            "[PR.System.Carry]");
                    }
                    break;
                }
            case MessageType.CarryState:
                {
                    CarryStateMessage state = e.ReadAs<CarryStateMessage>();
                    this.mod.CarrySystem.ApplyCarryStateMessage(state);
                    if (state.CarrierId == this.mod.LocalPlayerId || state.CarriedId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            state.Active ? "Carry started." : $"Carry stopped ({state.Reason}).",
                            "[PR.System.Carry]");
                    }
                    break;
                }
            case MessageType.HoldingHandsRequest:
                {
                    HoldingHandsRequestMessage request = e.ReadAs<HoldingHandsRequestMessage>();
                    this.mod.HoldingHandsSystem.SetLocalPending(request);
                    if (request.TargetPlayerId == this.mod.LocalPlayerId)
                    {
                        this.mod.RequestPrompts.Enqueue(
                            $"hands:{request.FromPlayerId}:{request.TargetPlayerId}",
                            "Holding Hands",
                            $"{request.FromPlayerName} asks to hold hands.",
                            () => this.ResolveHoldingHandsRequest(request.FromPlayerId, accept: true),
                            () => this.ResolveHoldingHandsRequest(request.FromPlayerId, accept: false),
                            "[PR.System.HoldingHands]");
                    }

                    break;
                }
            case MessageType.HoldingHandsDecision:
                {
                    HoldingHandsDecisionMessage decision = e.ReadAs<HoldingHandsDecisionMessage>();
                    this.mod.HoldingHandsSystem.ClearLocalPending(decision);
                    if (decision.RequesterId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            decision.Accepted ? "Holding hands request accepted." : "Holding hands request rejected.",
                            "[PR.System.HoldingHands]");
                    }

                    break;
                }
            case MessageType.HoldingHandsState:
                {
                    HoldingHandsStateMessage state = e.ReadAs<HoldingHandsStateMessage>();
                    this.mod.HoldingHandsSystem.ApplyState(state);
                    if (state.LeaderId == this.mod.LocalPlayerId || state.FollowerId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            state.Active ? "Holding hands started." : $"Holding hands stopped ({state.Reason}).",
                            "[PR.System.HoldingHands]");
                    }

                    break;
                }
            case MessageType.ImmersiveDateState:
                {
                    ImmersiveDateStateMessage state = e.ReadAs<ImmersiveDateStateMessage>();
                    this.mod.DateImmersionSystem.ApplyStateMessageClient(state);
                    break;
                }
            case MessageType.ImmersiveDateRequest:
                {
                    if (this.mod.IsHostPlayer)
                    {
                        break;
                    }

                    ImmersiveDateRequestMessage request = e.ReadAs<ImmersiveDateRequestMessage>();
                    if (request.PartnerId == this.mod.LocalPlayerId)
                    {
                        string requesterName = request.RequesterName;
                        if (string.IsNullOrWhiteSpace(requesterName))
                        {
                            Farmer? from = this.mod.FindFarmerById(request.RequesterId, includeOffline: true);
                            requesterName = from?.Name ?? request.RequesterId.ToString();
                        }

                        this.mod.RequestPrompts.Enqueue(
                            $"idate:{request.RequesterId}:{request.PartnerId}:{request.Location}",
                            "Immersive Date",
                            $"{requesterName} asks to start a {request.Location} date.",
                            () => this.ResolveImmersiveDateRequest(request, accept: true),
                            () => this.ResolveImmersiveDateRequest(request, accept: false),
                            "[PR.System.DateImmersion]");
                    }
                    break;
                }
            case MessageType.ImmersiveDateDecision:
                {
                    ImmersiveDateDecisionMessage decision = e.ReadAs<ImmersiveDateDecisionMessage>();
                    if (decision.RequesterId == this.mod.LocalPlayerId)
                    {
                        this.mod.Notifier.NotifyInfo(
                            decision.Accepted
                                ? $"Immersive date accepted ({decision.Location})."
                                : $"Immersive date declined ({decision.Location}).",
                            "[PR.System.DateImmersion]");
                    }

                    break;
                }
            case MessageType.ImmersiveDateInteractionResult:
                {
                    ImmersiveDateInteractionResultMessage result = e.ReadAs<ImmersiveDateInteractionResultMessage>();
                    this.mod.DateImmersionSystem.ApplyInteractionResultClient(result);
                    break;
                }
            case MessageType.HeartDelta:
                {
                    HeartDeltaMessage delta = e.ReadAs<HeartDeltaMessage>();
                    RelationshipRecord? relation = this.mod.ClientSnapshot.Relationships.FirstOrDefault(p => p.PairKey == delta.PairKey);
                    relation ??= this.TryCreateClientRelationFromPairKey(delta.PairKey);
                    if (relation is not null)
                    {
                        relation.HeartPoints = delta.NewPoints;
                        relation.LastHeartChangeDay = this.mod.GetCurrentDayNumber();
                    }
                    else
                    {
                        this.mod.NetSync.RequestSnapshotFromHost();
                    }

                    this.mod.Notifier.NotifyInfo(
                        $"Hearts updated ({delta.Source}): {(delta.Delta >= 0 ? "+" : string.Empty)}{delta.Delta}, level {delta.NewLevel}.",
                        "[PR.System.Hearts]");
                    this.mod.RecordHeartEvent(
                        delta.PairKey,
                        $"{(delta.Delta >= 0 ? "+" : string.Empty)}{delta.Delta} hearts ({delta.Source}) -> Lv {delta.NewLevel}");
                    break;
                }
            case MessageType.Error:
                {
                    ErrorMessage error = e.ReadAs<ErrorMessage>();
                    this.mod.Notifier.NotifyWarn(error.Message, "[PR.Net]");
                    break;
                }
            case MessageType.ChildCommandResult:
                {
                    ChildCommandResultMessage result = e.ReadAs<ChildCommandResultMessage>();
                    if (result.Success)
                    {
                        this.mod.Notifier.NotifyInfo(result.Message, "[PR.System.ChildGrowth]");
                    }
                    else
                    {
                        this.mod.Notifier.NotifyWarn(result.Message, "[PR.System.ChildGrowth]");
                    }

                    break;
                }
            case MessageType.NpcSync:
                {
                    NpcSyncMessage sync = e.ReadAs<NpcSyncMessage>();
                    this.mod.DateImmersionSystem.ApplyNpcSyncClient(sync);
                    this.mod.ChildGrowthSystem.ApplyNpcSyncClient(sync);
                    break;
                }
        }
    }

    private (bool success, string message) ResolveDatingProposal(long requesterId, bool accept)
    {
        if (!this.mod.DatingSystem.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out long pendingRequester)
            || pendingRequester != requesterId)
        {
            return (false, "Dating request is no longer pending.");
        }

        bool ok = this.mod.DatingSystem.RespondToPendingDatingLocal(accept, out string message);
        return (ok, message);
    }

    private (bool success, string message) ResolveMarriageProposal(long requesterId, bool accept)
    {
        if (!this.mod.MarriageSystem.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out long pendingRequester)
            || pendingRequester != requesterId)
        {
            return (false, "Marriage request is no longer pending.");
        }

        bool ok = this.mod.MarriageSystem.RespondToPendingMarriageLocal(accept, out string message);
        return (ok, message);
    }

    private (bool success, string message) ResolveTryForBaby(long requesterId, bool accept)
    {
        if (!this.mod.PregnancySystem.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out long pendingRequester)
            || pendingRequester != requesterId)
        {
            return (false, "Try-for-baby request is no longer pending.");
        }

        bool ok = this.mod.PregnancySystem.RespondTryForBabyFromLocal(accept, out string message);
        return (ok, message);
    }

    private (bool success, string message) ResolveCarryRequest(long requesterId, bool accept)
    {
        if (!this.mod.CarrySystem.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out long pendingRequester)
            || pendingRequester != requesterId)
        {
            return (false, "Carry request is no longer pending.");
        }

        bool ok = this.mod.CarrySystem.RespondToPendingCarryLocal(accept, out string message);
        return (ok, message);
    }

    private (bool success, string message) ResolveHoldingHandsRequest(long requesterId, bool accept)
    {
        if (!this.mod.HoldingHandsSystem.TryGetPendingForPlayer(this.mod.LocalPlayerId, out long pendingRequester)
            || pendingRequester != requesterId)
        {
            return (false, "Holding hands request is no longer pending.");
        }

        bool ok = this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(accept, out string message);
        return (ok, message);
    }

    private (bool success, string message) ResolveImmersiveDateRequest(ImmersiveDateRequestMessage request, bool accept)
    {
        ImmersiveDateDecisionMessage payload = new()
        {
            RequesterId = request.RequesterId,
            ResponderId = this.mod.LocalPlayerId,
            Location = request.Location,
            Accepted = accept
        };

        if (this.mod.IsHostPlayer)
        {
            this.mod.DateImmersionSystem.HandleImmersiveDateDecisionHost(payload, this.mod.LocalPlayerId);
        }
        else
        {
            this.mod.NetSync.SendToPlayer(MessageType.ImmersiveDateDecision, payload, Game1.MasterPlayer.UniqueMultiplayerID);
        }

        return (true, accept ? "Immersive date accepted." : "Immersive date declined.");
    }

    private RelationshipRecord? TryCreateClientRelationFromPairKey(string pairKey)
    {
        if (!TryParsePairKey(pairKey, out long playerAId, out long playerBId))
        {
            return null;
        }

        Farmer? playerA = this.mod.FindFarmerById(playerAId, includeOffline: true);
        Farmer? playerB = this.mod.FindFarmerById(playerBId, includeOffline: true);
        RelationshipRecord relation = new()
        {
            PairKey = pairKey,
            PlayerAId = playerAId,
            PlayerBId = playerBId,
            PlayerAName = playerA?.Name ?? playerAId.ToString(),
            PlayerBName = playerB?.Name ?? playerBId.ToString()
        };

        this.mod.ClientSnapshot.Relationships.Add(relation);
        return relation;
    }

    private static bool TryParsePairKey(string pairKey, out long playerAId, out long playerBId)
    {
        playerAId = -1;
        playerBId = -1;
        if (string.IsNullOrWhiteSpace(pairKey))
        {
            return false;
        }

        string[] parts = pairKey.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        return long.TryParse(parts[0], out playerAId) && long.TryParse(parts[1], out playerBId);
    }
}
