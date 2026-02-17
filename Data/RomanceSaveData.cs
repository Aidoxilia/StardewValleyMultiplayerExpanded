namespace PlayerRomance.Data;

public sealed class RomanceSaveData
{
    public int Version { get; set; } = 3;
    public int LastProcessedDay { get; set; }
    public Dictionary<string, RelationshipRecord> Relationships { get; set; } = new();
    public Dictionary<string, PregnancyRecord> Pregnancies { get; set; } = new();
    public Dictionary<string, ChildRecord> Children { get; set; } = new();
    public Dictionary<string, float> SynergyMeterByPair { get; set; } = new();
    public DateImmersionSaveState? ActiveImmersiveDate { get; set; }
    public Dictionary<string, HoldingHandsPairRecord> HoldingHandsHistory { get; set; } = new();
}
