namespace PlayerRomance.Data;

public sealed class ModConfig
{
    public bool EnableVanillaSocialIntegration { get; set; } = true;
    public bool EnableImmersiveDates { get; set; } = true;
    public bool EnableHeartsSystem { get; set; } = true;
    public bool EnableHoldingHands { get; set; } = true;
    public string RomanceHubHotkey { get; set; } = "F7";
    public string ChildrenManagementHotkey { get; set; } = "F8";
    public string PregnancyMenuHotkey { get; set; } = "F9";
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
    public float HoldingHandsSoftMaxDistanceTiles { get; set; } = 1.25f;
    public float HandsSpringStrength { get; set; } = 0.18f;
    public float HandsDamping { get; set; } = 0.74f;
    public int HoldingHandsOffsetPixels { get; set; } = 24;
    public bool EnableCarry { get; set; } = true;
    public int CarryEnergyRegenPerSecond { get; set; } = 1;
    public int CarryOffsetY { get; set; } = -48;
    public bool EnableDateEvents { get; set; } = true;
    public bool EnableMarriage { get; set; } = true;
    public bool EnablePregnancy { get; set; } = true;
    public int PregnancyDays { get; set; } = 7;
    public int PregnancyDurationDays { get; set; } = 7;
    public bool EnableChildGrowth { get; set; } = true;
    public bool EnableChildFeedingSystem { get; set; } = true;
    public int ChildYearsPerFedDayMin { get; set; } = 2;
    public int ChildYearsPerFedDayMax { get; set; } = 3;
    public int AdultWorkMinAge { get; set; } = 16;
    public bool EnableFarmWorker { get; set; } = true;
    public bool AllowAdultChildWork { get; set; } = true;
    public bool EnableTaskWater { get; set; } = true;
    public bool EnableTaskFeed { get; set; } = true;
    public bool EnableTaskCollect { get; set; } = true;
    public bool EnableTaskHarvest { get; set; } = true;
    public bool EnableTaskShip { get; set; } = true;
    public bool EnableChildFishingTask { get; set; } = true;
    public int DateStartConfirmSeconds { get; set; } = 5;
    public int DateStartRetryMaxAttempts { get; set; } = 3;
    public int HandsMaxMovePixelsPerTick { get; set; } = 14;
    public int HandsEmergencyStopDistanceTiles { get; set; } = 8;
    public int MarriageMinDatingDays { get; set; } = 3;

    public bool EnableCoupleSynergy { get; set; } = true;
    public bool EnableSynergySystem { get; set; } = true;
    public bool EnableLegacyChildren { get; set; } = true;
    public bool EnableRpInteractions { get; set; } = false;
    public bool EnableRelationshipEvents { get; set; } = false;
    public bool EnableCombatDuo { get; set; } = false;
    public bool EnableChildEducationTraits { get; set; } = false;
    public bool TuitionEnabled { get; set; } = true;
    public int TuitionMin { get; set; } = 500;
    public int TuitionMax { get; set; } = 2000;
    public float TeenChoreForgetChance { get; set; } = 0.2f;
    public bool AdultSpecialization { get; set; } = true;
    public int TeleportRingCooldownMinutes { get; set; } = 10;

    public bool EnableWakeupCuddleBuff { get; set; } = true;
    public bool EnableLoveAura { get; set; } = true;
    public bool EnableRegeneratorKiss { get; set; } = true;

    public int LoveAuraRangeTiles { get; set; } = 6;
    public float LoveAuraStaminaMultiplier { get; set; } = 0.85f;
    public int KissEnergyRestorePercent { get; set; } = 50;

    public bool EnableDebugCommands { get; set; } = true;
}
