namespace PlayerRomance.Data;

public enum RelationshipState
{
    None = 0,
    Dating = 1,
    Engaged = 2,
    Married = 3
}

public sealed class RelationshipRecord
{
    public const int DefaultPointsPerHeart = 250;
    public const int DefaultMaxHearts = 14;

    public string PairKey { get; set; } = string.Empty;
    public long PlayerAId { get; set; }
    public long PlayerBId { get; set; }
    public string PlayerAName { get; set; } = string.Empty;
    public string PlayerBName { get; set; } = string.Empty;
    public RelationshipState State { get; set; } = RelationshipState.None;
    public long? PendingDatingFrom { get; set; }
    public long? PendingMarriageFrom { get; set; }
    public int RelationshipStartedDay { get; set; }
    public int LastStatusChangeDay { get; set; }
    public int HeartPoints { get; set; }
    public int LastHeartChangeDay { get; set; }
    public int LastImmersiveDateRequestedDay { get; set; } = -1;
    public int LastImmersiveDateConfirmedDay { get; set; } = -1;
    public int LastImmersiveDateDay { get; set; } = -1;
    public int ImmersiveDateCount { get; set; }
    public int GiftsOfferedCount { get; set; }
    public int RejectionsCount { get; set; }

    public bool Includes(long playerId)
    {
        return this.PlayerAId == playerId || this.PlayerBId == playerId;
    }

    public long GetOther(long playerId)
    {
        return this.PlayerAId == playerId ? this.PlayerBId : this.PlayerAId;
    }

    public string GetOtherName(long playerId)
    {
        return this.PlayerAId == playerId ? this.PlayerBName : this.PlayerAName;
    }

    public int GetHeartLevel(int pointsPerHeart = DefaultPointsPerHeart, int maxHearts = DefaultMaxHearts)
    {
        int safePointsPerHeart = Math.Max(1, pointsPerHeart);
        int safeMaxHearts = Math.Max(1, maxHearts);
        int clamped = Math.Max(0, this.HeartPoints);
        return Math.Min(safeMaxHearts, clamped / safePointsPerHeart);
    }

    public bool CanStartImmersiveDateToday(int dayNumber)
    {
        int effectiveLastConfirmedDay = this.LastImmersiveDateConfirmedDay >= 0
            ? this.LastImmersiveDateConfirmedDay
            : this.LastImmersiveDateDay;
        return effectiveLastConfirmedDay != dayNumber;
    }
}
