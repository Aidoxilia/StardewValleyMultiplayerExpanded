namespace PlayerRomance.Data;

public sealed class PlayerProfileRecord
{
    public long PlayerId { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string BirthdaySeason { get; set; } = string.Empty;
    public int BirthdayDay { get; set; }
    public List<string> FavoriteGiftItemIds { get; set; } = new();
}