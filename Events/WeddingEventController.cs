using PlayerRomance.Data;
using PlayerRomance.Net;

namespace PlayerRomance.Events;

public sealed class WeddingEventController
{
    private readonly ModEntry mod;

    public WeddingEventController(ModEntry mod)
    {
        this.mod = mod;
    }

    public void StartWeddingHost(RelationshipRecord relation)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        StartPairEventMessage wedding = new()
        {
            PlayerAId = relation.PlayerAId,
            PlayerBId = relation.PlayerBId,
            EventId = "wedding",
            LocationName = "Town",
            TileX = 52,
            TileY = 64,
            DialogText = $"{relation.PlayerAName} and {relation.PlayerBName} are now married!"
        };

        this.mod.DateEventController.TryStartEventClient(wedding);
        this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, wedding, relation.PlayerAId);
        this.mod.NetSync.SendToPlayer(MessageType.StartDateEvent, wedding, relation.PlayerBId);

        this.mod.MarriageSystem.CompleteMarriageAfterCeremony(relation.PairKey);
        this.mod.Notifier.NotifyInfo(
            $"Wedding completed for {relation.PlayerAName} and {relation.PlayerBName}.",
            "[PR.System.Marriage]");
    }
}
