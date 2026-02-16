namespace PlayerRomance.Data;

public sealed class NetSnapshot
{
    public int DayNumber { get; set; }
    public List<RelationshipRecord> Relationships { get; set; } = new();
    public List<PregnancyRecord> Pregnancies { get; set; } = new();
    public List<ChildRecord> Children { get; set; } = new();
    public List<CarrySessionState> CarrySessions { get; set; } = new();
    public List<HoldingHandsSessionState> HoldingHandsSessions { get; set; } = new();
    public DateImmersionPublicState? ActiveImmersiveDate { get; set; }
    public string LastFarmWorkReport { get; set; } = string.Empty;
}
