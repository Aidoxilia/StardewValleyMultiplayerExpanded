namespace PlayerRomance.Data;

public sealed class PregnancyRecord
{
    public string CoupleKey { get; set; } = string.Empty;
    public long ParentAId { get; set; }
    public long ParentBId { get; set; }
    public string ParentAName { get; set; } = string.Empty;
    public string ParentBName { get; set; } = string.Empty;
    public bool ParentAOptIn { get; set; }
    public bool ParentBOptIn { get; set; }
    public long? PendingTryForBabyFrom { get; set; }
    public bool IsPregnant { get; set; }
    public int DaysRemaining { get; set; }
    public int StartedOnDay { get; set; }
    public int LastProcessedDay { get; set; }
    public long PregnantPlayerId { get; set; }
    public int PregnancyDurationDays { get; set; } = 7;
    public int CurrentPregnancyDay { get; set; }
}
