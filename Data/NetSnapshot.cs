namespace PlayerRomance.Data;

public sealed class NetSnapshot
{
    public int DayNumber { get; set; }
    public List<RelationshipRecord> Relationships { get; set; } = new();
    public List<PregnancyRecord> Pregnancies { get; set; } = new();
    public List<PregnancyRecord> ActivePregnancies
    {
        get => this.Pregnancies;
        set => this.Pregnancies = value ?? new();
    }
    public List<ChildRecord> Children { get; set; } = new();
    public List<ChildPublicState> ChildRuntimeStates { get; set; } = new();
    public List<CarrySessionState> CarrySessions { get; set; } = new();
    public List<HoldingHandsSessionState> HoldingHandsSessions { get; set; } = new();
    public DateImmersionPublicState? ActiveImmersiveDate { get; set; }
    public string LastFarmWorkReport { get; set; } = string.Empty;
}

public sealed class ChildPublicState
{
    public string ChildId { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public int AgeYears { get; set; }
    public ChildLifeStage Stage { get; set; }
    public bool IsFedToday { get; set; }
    public int FeedingProgress { get; set; }
    public ChildTaskType AssignedTask { get; set; } = ChildTaskType.Auto;
    public bool AutoMode { get; set; } = true;
    public bool IsWorkerEnabled { get; set; } = true;
    public string RoutineZone { get; set; } = string.Empty;
    public string RuntimeNpcName { get; set; } = string.Empty;
}
