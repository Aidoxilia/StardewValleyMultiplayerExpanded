namespace PlayerRomance.Data;

public sealed class DateEventRewardDefinition
{
    public int HeartsDelta { get; set; }
    public int Money { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int ItemCount { get; set; } = 1;
}

public sealed class DateEventNpcDefinition
{
    public string NpcName { get; set; } = string.Empty;
    public string SpotName { get; set; } = string.Empty;
    public int FacingDirection { get; set; } = 2;
}

public sealed class DateEventDefinition
{
    public string DateId { get; set; } = string.Empty;
    public int RequiredHearts { get; set; } = 0;
    public string MapName { get; set; } = "Date_Beach";
    public int MinStartTime { get; set; } = 900;
    public int MaxStartTime { get; set; } = 2300;
    public List<DateEventNpcDefinition> NpcList { get; set; } = new();
    public DateEventNpcDefinition? VendorNpc { get; set; }
    public DateEventRewardDefinition Reward { get; set; } = new();
    public bool EnableDecorations { get; set; }
}
