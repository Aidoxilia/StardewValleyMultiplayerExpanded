namespace PlayerRomance.Data;

public sealed class ModConfig
{
    public bool EnableImmersiveDates { get; set; } = true;
    public bool EnableHeartsSystem { get; set; } = true;
    public bool EnableHoldingHands { get; set; } = true;
    public string RomanceHubHotkey { get; set; } = "F7";
    public int HeartPointsPerHeart { get; set; } = 250;
    public int MaxHearts { get; set; } = 14;
    public int ImmersiveDatePointsReward { get; set; } = 120;
    public int RejectionHeartPenalty { get; set; } = 20;
    public int EarlyLeaveHeartPenalty { get; set; } = 15;
    public int ImmersiveDateEndTime { get; set; } = 2200;
    public int HoldingHandsMinHearts { get; set; } = 2;
    public int ImmersiveDateMinHearts { get; set; } = 4;
    public int GiftsBonusMinHearts { get; set; } = 6;
    public int DuoBuffMinHearts { get; set; } = 10;
    public int HoldingHandsBreakDistanceTiles { get; set; } = 4;
    public int HoldingHandsOffsetPixels { get; set; } = 24;
    public bool EnableCarry { get; set; } = true;
    public int CarryEnergyRegenPerSecond { get; set; } = 1;
    public int CarryOffsetY { get; set; } = -48;
    public bool EnableDateEvents { get; set; } = true;
    public bool EnableMarriage { get; set; } = true;
    public bool EnablePregnancy { get; set; } = true;
    public int PregnancyDays { get; set; } = 7;
    public bool EnableChildGrowth { get; set; } = true;
    public bool EnableFarmWorker { get; set; } = true;
    public bool AllowAdultChildWork { get; set; } = true;
    public bool EnableTaskWater { get; set; } = true;
    public bool EnableTaskFeed { get; set; } = true;
    public bool EnableTaskCollect { get; set; } = true;
    public bool EnableTaskHarvest { get; set; } = true;
    public bool EnableTaskShip { get; set; } = true;
    public int MarriageMinDatingDays { get; set; } = 3;
    public bool EnableDebugCommands { get; set; } = true;
}
