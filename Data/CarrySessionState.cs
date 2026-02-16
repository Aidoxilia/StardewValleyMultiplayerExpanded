namespace PlayerRomance.Data;

public sealed class CarrySessionState
{
    public long CarrierId { get; set; }
    public long CarriedId { get; set; }
    public bool Active { get; set; }
}
