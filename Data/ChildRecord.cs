namespace PlayerRomance.Data;

public enum ChildLifeStage
{
    Infant = 0,
    Child = 1,
    Teen = 2,
    Adult = 3
}

public enum ChildTaskType
{
    Auto = 0,
    Water = 1,
    FeedAnimals = 2,
    Collect = 3,
    Harvest = 4,
    Ship = 5,
    Fish = 6,
    Stop = 7
}

public sealed class ChildVisualProfile
{
    public int MixSeed { get; set; }
    public string SkinToneHex { get; set; } = "#F4D7B5";
    public string HairColorHex { get; set; } = "#6B4D3A";
    public string OutfitColorHex { get; set; } = "#6EA6D8";
    public string InfantTemplateNpc { get; set; } = "Vincent";
    public string ChildTemplateNpc { get; set; } = "Jas";
    public string TeenTemplateNpc { get; set; } = "Sam";
    public string AdultTemplateNpc { get; set; } = "Abigail";
    public bool IsFallback { get; set; }
}

public sealed class ChildRecord
{
    public string ChildId { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public long ParentAId { get; set; }
    public long ParentBId { get; set; }
    public string ParentAName { get; set; } = string.Empty;
    public string ParentBName { get; set; } = string.Empty;
    public int AgeYears { get; set; }
    public int AgeDays { get; set; }
    public ChildLifeStage Stage { get; set; } = ChildLifeStage.Infant;
    public int BirthDayNumber { get; set; }
    public int LastProcessedDay { get; set; }
    public bool IsFedToday { get; set; }
    public bool IsCaredToday { get; set; }
    public bool IsPlayedToday { get; set; }
    public int FeedingProgress { get; set; }
    public ChildTaskType AssignedTask { get; set; } = ChildTaskType.Auto;
    public bool AutoMode { get; set; } = true;
    public int LastWorkedDay { get; set; } = -1;
    public string RoutineZone { get; set; } = "FarmHouse";
    public string RuntimeNpcName { get; set; } = string.Empty;
    public bool RuntimeNpcSpawned { get; set; }
    public ChildVisualProfile VisualProfile { get; set; } = new();
    public bool IsWorkerEnabled { get; set; } = true;
    public string AdultNpcName { get; set; } = string.Empty;
    public bool AdultNpcSpawned { get; set; }
}
