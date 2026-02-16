namespace PlayerRomance.Data;

public sealed class HoldingHandsSessionState
{
    public long LeaderId { get; set; }
    public long FollowerId { get; set; }
    public bool Active { get; set; }
}

public sealed class HoldingHandsPairRecord
{
    public string PairKey { get; set; } = string.Empty;
    public int LastStartedDay { get; set; } = -1;
    public int TotalSessions { get; set; }
}
