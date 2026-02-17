using PlayerRomance.Data;

namespace PlayerRomance.Net;

public sealed class SnapshotRequestMessage
{
    public long PlayerId { get; set; }
}

public sealed class SnapshotMessage
{
    public NetSnapshot Snapshot { get; set; } = new();
}

public sealed class RelationshipProposalMessage
{
    public long FromPlayerId { get; set; }
    public string FromPlayerName { get; set; } = string.Empty;
    public long TargetPlayerId { get; set; }
}

public sealed class RelationshipDecisionMessage
{
    public long ResponderId { get; set; }
    public long RequesterId { get; set; }
    public bool Accepted { get; set; }
}

public sealed class StartPairEventMessage
{
    public long PlayerAId { get; set; }
    public long PlayerBId { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string LocationName { get; set; } = "Town";
    public int TileX { get; set; } = 24;
    public int TileY { get; set; } = 62;
    public string DialogText { get; set; } = string.Empty;
    public string DateId { get; set; } = string.Empty;
    public bool LockInputs { get; set; }
    public bool FreezeTime { get; set; }
    public int LockedTime { get; set; }
    public int PlayerATileX { get; set; } = 10;
    public int PlayerATileY { get; set; } = 10;
    public int PlayerBTileX { get; set; } = 12;
    public int PlayerBTileY { get; set; } = 10;
    public List<DateEventNpcPlacementMessage> NpcPlacements { get; set; } = new();
}

public sealed class DateEventNpcPlacementMessage
{
    public string SpotName { get; set; } = string.Empty;
    public string NpcName { get; set; } = string.Empty;
    public int TileX { get; set; }
    public int TileY { get; set; }
    public int FacingDirection { get; set; } = 2;
    public bool IsVendor { get; set; }
}

public sealed class DateEventPhaseMessage
{
    public string SessionId { get; set; } = string.Empty;
    public string DateId { get; set; } = string.Empty;
    public string PhaseId { get; set; } = string.Empty;
    public string DialogText { get; set; } = string.Empty;
    public int EmoteId { get; set; } = -1;
    public long TargetPlayerId { get; set; } = -1;
}

public sealed class EndDateEventMessage
{
    public string SessionId { get; set; } = string.Empty;
    public string DateId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RewardMoney { get; set; }
    public string RewardItemId { get; set; } = string.Empty;
    public int RewardItemCount { get; set; } = 1;
}

public sealed class PregnancyOptInMessage
{
    public long PlayerId { get; set; }
    public long PartnerId { get; set; }
    public bool OptIn { get; set; }
}

public sealed class TryForBabyMessage
{
    public long FromPlayerId { get; set; }
    public long PartnerId { get; set; }
    public bool Accepted { get; set; }
    public bool IsDecision { get; set; }
}

public sealed class ChildSyncMessage
{
    public ChildRecord Child { get; set; } = new();
}

public sealed class FarmWorkReportMessage
{
    public int DayNumber { get; set; }
    public string Report { get; set; } = string.Empty;
}

public sealed class CarryRequestMessage
{
    public long FromPlayerId { get; set; }
    public string FromPlayerName { get; set; } = string.Empty;
    public long TargetPlayerId { get; set; }
}

public sealed class CarryDecisionMessage
{
    public long RequesterId { get; set; }
    public long ResponderId { get; set; }
    public bool Accepted { get; set; }
}

public sealed class CarryStateMessage
{
    public long CarrierId { get; set; }
    public long CarriedId { get; set; }
    public bool Active { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class CarryStopMessage
{
    public long FromPlayerId { get; set; }
    public long TargetPlayerId { get; set; }
}

public sealed class ImmersiveDateRequestMessage
{
    public long RequesterId { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public long PartnerId { get; set; }
    public ImmersiveDateLocation Location { get; set; }
}

public sealed class ImmersiveDateDecisionMessage
{
    public long RequesterId { get; set; }
    public long ResponderId { get; set; }
    public ImmersiveDateLocation Location { get; set; }
    public bool Accepted { get; set; }
}

public sealed class ImmersiveDateStateMessage
{
    public string SessionId { get; set; } = string.Empty;
    public bool Active { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateImmersionPublicState? State { get; set; }
}

public sealed class ImmersiveDateInteractionRequestMessage
{
    public string RequestId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public long ActorId { get; set; }
    public DateInteractionType InteractionType { get; set; }
    public DateStandType StandType { get; set; }
    public string OfferItemId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public sealed class ImmersiveDateInteractionResultMessage
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int GoldSpent { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public bool OfferedToPartner { get; set; }
    public int HeartDelta { get; set; }
}

public sealed class HeartDeltaMessage
{
    public string PairKey { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int Delta { get; set; }
    public int NewPoints { get; set; }
    public int NewLevel { get; set; }
}

public sealed class HoldingHandsRequestMessage
{
    public long FromPlayerId { get; set; }
    public string FromPlayerName { get; set; } = string.Empty;
    public long TargetPlayerId { get; set; }
}

public sealed class HoldingHandsDecisionMessage
{
    public long RequesterId { get; set; }
    public long ResponderId { get; set; }
    public bool Accepted { get; set; }
}

public sealed class HoldingHandsStateMessage
{
    public long LeaderId { get; set; }
    public long FollowerId { get; set; }
    public bool Active { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class HoldingHandsStopMessage
{
    public long FromPlayerId { get; set; }
    public long TargetPlayerId { get; set; }
}

public enum ChildCommandAction
{
    Feed = 0,
    SetAgeYears = 1,
    SetTask = 2,
    Care = 3,
    Play = 4
}

public sealed class ChildCommandMessage
{
    public long RequesterId { get; set; }
    public ChildCommandAction Action { get; set; }
    public string ChildIdOrName { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public int AgeYears { get; set; } = -1;
    public string TaskToken { get; set; } = string.Empty;
}

public sealed class ChildCommandResultMessage
{
    public bool Success { get; set; }
    public string ChildId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ErrorMessage
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
