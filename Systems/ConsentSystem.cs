using PlayerRomance.Data;

namespace PlayerRomance.Systems;

public static class ConsentSystem
{
    public static string GetPairKey(long playerA, long playerB)
    {
        return playerA < playerB ? $"{playerA}_{playerB}" : $"{playerB}_{playerA}";
    }

    public static int GetDayNumber()
    {
        if (StardewValley.Game1.stats is null)
        {
            return 0;
        }

        return (int)StardewValley.Game1.stats.DaysPlayed;
    }

    public static RelationshipRecord GetOrCreateRelationship(
        RomanceSaveData data,
        long playerA,
        long playerB,
        string playerAName,
        string playerBName)
    {
        string key = GetPairKey(playerA, playerB);
        if (!data.Relationships.TryGetValue(key, out RelationshipRecord? relationship))
        {
            bool isAFirst = playerA < playerB;
            relationship = new RelationshipRecord
            {
                PairKey = key,
                PlayerAId = isAFirst ? playerA : playerB,
                PlayerAName = isAFirst ? playerAName : playerBName,
                PlayerBId = isAFirst ? playerB : playerA,
                PlayerBName = isAFirst ? playerBName : playerAName,
                State = RelationshipState.None,
                RelationshipStartedDay = 0,
                LastStatusChangeDay = GetDayNumber()
            };
            data.Relationships[key] = relationship;
        }
        else
        {
            if (relationship.PlayerAId == playerA)
            {
                relationship.PlayerAName = playerAName;
                relationship.PlayerBName = playerBName;
            }
            else
            {
                relationship.PlayerAName = playerBName;
                relationship.PlayerBName = playerAName;
            }
        }

        return relationship;
    }
}
