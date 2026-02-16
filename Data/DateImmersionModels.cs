namespace PlayerRomance.Data;

public enum ImmersiveDateLocation
{
    Town = 0,
    Beach = 1,
    Forest = 2
}

public enum DateStandType
{
    IceCream = 0,
    Roses = 1,
    Clothing = 2
}

public enum DateInteractionType
{
    TalkNpc = 0,
    OpenStand = 1,
    BuyForSelf = 2,
    BuyAndOffer = 3,
    EndDate = 4
}

public sealed class DateImmersionSaveState
{
    public string SessionId { get; set; } = string.Empty;
    public long PlayerAId { get; set; }
    public long PlayerBId { get; set; }
    public string PlayerAName { get; set; } = string.Empty;
    public string PlayerBName { get; set; } = string.Empty;
    public string PairKey { get; set; } = string.Empty;
    public ImmersiveDateLocation Location { get; set; } = ImmersiveDateLocation.Town;
    public int StartedDay { get; set; }
    public int StartedTime { get; set; }
    public bool IsActive { get; set; }
    public int PlayerABonusTalks { get; set; }
    public int PlayerBBonusTalks { get; set; }
}

public sealed class DateImmersionPublicState
{
    public string SessionId { get; set; } = string.Empty;
    public long PlayerAId { get; set; }
    public long PlayerBId { get; set; }
    public string PlayerAName { get; set; } = string.Empty;
    public string PlayerBName { get; set; } = string.Empty;
    public ImmersiveDateLocation Location { get; set; }
    public int StartedDay { get; set; }
    public int StartedTime { get; set; }
    public bool IsActive { get; set; }
}

public sealed class StandOfferDefinition
{
    public DateStandType StandType { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Price { get; set; }
    public int HeartDeltaOnOffer { get; set; }
}
