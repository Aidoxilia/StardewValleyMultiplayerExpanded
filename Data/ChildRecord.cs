namespace PlayerRomance.Data;

public enum ChildLifeStage
{
    Infant = 0,
    Child = 1,
    Teen = 2,
    Adult = 3
}

public sealed class ChildRecord
{
    public string ChildId { get; set; } = string.Empty;
    public string ChildName { get; set; } = string.Empty;
    public long ParentAId { get; set; }
    public long ParentBId { get; set; }
    public string ParentAName { get; set; } = string.Empty;
    public string ParentBName { get; set; } = string.Empty;
    public int AgeDays { get; set; }
    public ChildLifeStage Stage { get; set; } = ChildLifeStage.Infant;
    public int BirthDayNumber { get; set; }
    public int LastProcessedDay { get; set; }
    public bool IsWorkerEnabled { get; set; } = true;
    public string AdultNpcName { get; set; } = string.Empty;
    public bool AdultNpcSpawned { get; set; }
}
