namespace PlayerRomance.Data;

public sealed class GiftProgressRecord
{
    public string PairKey { get; set; } = string.Empty;
    public long PlayerAId { get; set; }
    public long PlayerBId { get; set; }
    public int BaselineCount { get; set; }
    public int DateGiftCount { get; set; }
    public int FavoriteGiftCount { get; set; }
    public int BaselineMilestones { get; set; }
    public int DateMilestones { get; set; }
    public int FavoriteMilestones { get; set; }
    public string LastGiftItemId { get; set; } = string.Empty;
    public string LastGiftItemName { get; set; } = string.Empty;
    public int LastGiftCount { get; set; }
    public long LastFromPlayerId { get; set; }
    public long LastToPlayerId { get; set; }
    public int LastGiftDay { get; set; }
    public string LastSource { get; set; } = string.Empty;
    public DateTime LastObservedUtc { get; set; }
}
