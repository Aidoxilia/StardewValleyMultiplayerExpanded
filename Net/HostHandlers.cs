using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace PlayerRomance.Net;

public sealed class HostHandlers
{
    private readonly ModEntry mod;
    private readonly Dictionary<string, DateTime> throttleWindow = new();
    private readonly TimeSpan throttleInterval = TimeSpan.FromMilliseconds(250);

    public HostHandlers(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Handle(MessageType type, ModMessageReceivedEventArgs e)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        if (this.IsThrottled(e.FromPlayerID, type))
        {
            this.mod.Monitor.Log($"[PR.Net] Throttled {type} from {e.FromPlayerID}.", LogLevel.Trace);
            return;
        }

        switch (type)
        {
            case MessageType.RequestSnapshot:
            {
                SnapshotRequestMessage request = e.ReadAs<SnapshotRequestMessage>();
                this.mod.NetSync.SendSnapshotTo(request.PlayerId);
                break;
            }
            case MessageType.ProposalDating:
            {
                RelationshipProposalMessage proposal = e.ReadAs<RelationshipProposalMessage>();
                this.mod.DatingSystem.HandleDatingProposalHost(proposal, e.FromPlayerID);
                break;
            }
            case MessageType.DatingDecision:
            {
                RelationshipDecisionMessage decision = e.ReadAs<RelationshipDecisionMessage>();
                this.mod.DatingSystem.HandleDatingDecisionHost(decision, e.FromPlayerID);
                break;
            }
            case MessageType.ProposalMarriage:
            {
                RelationshipProposalMessage proposal = e.ReadAs<RelationshipProposalMessage>();
                this.mod.MarriageSystem.HandleMarriageProposalHost(proposal, e.FromPlayerID);
                break;
            }
            case MessageType.MarriageDecision:
            {
                RelationshipDecisionMessage decision = e.ReadAs<RelationshipDecisionMessage>();
                this.mod.MarriageSystem.HandleMarriageDecisionHost(decision, e.FromPlayerID);
                break;
            }
            case MessageType.StartDateEvent:
            {
                StartPairEventMessage request = e.ReadAs<StartPairEventMessage>();
                this.mod.DateEventController.HandleStartDateRequestHost(request, e.FromPlayerID);
                break;
            }
            case MessageType.PregnancyOptIn:
            {
                PregnancyOptInMessage optIn = e.ReadAs<PregnancyOptInMessage>();
                this.mod.PregnancySystem.HandleOptInHost(optIn, e.FromPlayerID);
                break;
            }
            case MessageType.TryForBabyRequest:
            {
                TryForBabyMessage request = e.ReadAs<TryForBabyMessage>();
                this.mod.PregnancySystem.HandleTryForBabyRequestHost(request, e.FromPlayerID);
                break;
            }
            case MessageType.TryForBabyDecision:
            {
                TryForBabyMessage decision = e.ReadAs<TryForBabyMessage>();
                this.mod.PregnancySystem.HandleTryForBabyDecisionHost(decision, e.FromPlayerID);
                break;
            }
            case MessageType.CarryRequest:
            {
                CarryRequestMessage request = e.ReadAs<CarryRequestMessage>();
                this.mod.CarrySystem.HandleCarryRequestHost(request, e.FromPlayerID);
                break;
            }
            case MessageType.CarryDecision:
            {
                CarryDecisionMessage decision = e.ReadAs<CarryDecisionMessage>();
                this.mod.CarrySystem.HandleCarryDecisionHost(decision, e.FromPlayerID);
                break;
            }
            case MessageType.CarryStop:
            {
                CarryStopMessage stop = e.ReadAs<CarryStopMessage>();
                this.mod.CarrySystem.HandleCarryStopHost(stop, e.FromPlayerID);
                break;
            }
            case MessageType.HoldingHandsRequest:
            {
                HoldingHandsRequestMessage request = e.ReadAs<HoldingHandsRequestMessage>();
                this.mod.HoldingHandsSystem.HandleHoldingHandsRequestHost(request, e.FromPlayerID);
                break;
            }
            case MessageType.HoldingHandsDecision:
            {
                HoldingHandsDecisionMessage decision = e.ReadAs<HoldingHandsDecisionMessage>();
                this.mod.HoldingHandsSystem.HandleHoldingHandsDecisionHost(decision, e.FromPlayerID);
                break;
            }
            case MessageType.HoldingHandsStop:
            {
                HoldingHandsStopMessage stop = e.ReadAs<HoldingHandsStopMessage>();
                this.mod.HoldingHandsSystem.HandleHoldingHandsStopHost(stop, e.FromPlayerID);
                break;
            }
            case MessageType.ImmersiveDateRequest:
            {
                ImmersiveDateRequestMessage request = e.ReadAs<ImmersiveDateRequestMessage>();
                this.mod.DateImmersionSystem.HandleImmersiveDateRequestHost(request, e.FromPlayerID);
                break;
            }
            case MessageType.ImmersiveDateInteractionRequest:
            {
                ImmersiveDateInteractionRequestMessage request = e.ReadAs<ImmersiveDateInteractionRequestMessage>();
                this.mod.DateImmersionSystem.HandleInteractionRequestHost(request, e.FromPlayerID);
                break;
            }
        }
    }

    private bool IsThrottled(long playerId, MessageType messageType)
    {
        string key = $"{playerId}:{messageType}";
        DateTime now = DateTime.UtcNow;
        if (!this.throttleWindow.TryGetValue(key, out DateTime last))
        {
            this.throttleWindow[key] = now;
            return false;
        }

        if (now - last < this.throttleInterval)
        {
            return true;
        }

        this.throttleWindow[key] = now;
        return false;
    }
}
