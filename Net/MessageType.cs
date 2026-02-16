namespace PlayerRomance.Net;

public enum MessageType
{
    RequestSnapshot = 0,
    Snapshot = 1,
    ProposalDating = 2,
    DatingDecision = 3,
    StartDateEvent = 4,
    DateEventStep = 5,
    ProposalMarriage = 6,
    MarriageDecision = 7,
    PregnancyOptIn = 8,
    TryForBabyRequest = 9,
    TryForBabyDecision = 10,
    ChildBorn = 11,
    ChildGrewUp = 12,
    FarmWorkReport = 13,
    CarryRequest = 14,
    CarryDecision = 15,
    CarryState = 16,
    CarryStop = 17,
    ImmersiveDateRequest = 18,
    ImmersiveDateState = 19,
    ImmersiveDateInteractionRequest = 20,
    ImmersiveDateInteractionResult = 21,
    HeartDelta = 22,
    HoldingHandsRequest = 23,
    HoldingHandsDecision = 24,
    HoldingHandsState = 25,
    HoldingHandsStop = 26,
    Error = 27
}
