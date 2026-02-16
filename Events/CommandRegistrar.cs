using PlayerRomance.Data;
using StardewModdingAPI;
using StardewValley;

namespace PlayerRomance.Events;

public sealed class CommandRegistrar
{
    private readonly ModEntry mod;

    public CommandRegistrar(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Register()
    {
        this.mod.Helper.ConsoleCommands.Add("pr.propose", "Send dating proposal. Usage: pr.propose <playerNameOrId>", this.OnProposeDating);
        this.mod.Helper.ConsoleCommands.Add("pr.accept", "Accept pending request (dating/marriage/pregnancy/carry/hands).", this.OnAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.reject", "Reject pending request (dating/marriage/pregnancy/carry/hands).", this.OnReject);
        this.mod.Helper.ConsoleCommands.Add("pr.status", "Show relationship status. Usage: pr.status [playerNameOrId]", this.OnStatus);
        this.mod.Helper.ConsoleCommands.Add("pr.menu", "Open Player Romance menu.", (_, _) => Game1.activeClickableMenu = new UI.RomanceMenu(this.mod));

        this.mod.Helper.ConsoleCommands.Add("pr.date.start", "Start date event. Usage: pr.date.start <playerNameOrId>", this.OnDateStart);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.propose", "Send marriage proposal. Usage: pr.marry.propose <playerNameOrId>", this.OnProposeMarriage);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.accept", "Accept pending marriage request.", this.OnMarriageAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.reject", "Reject pending marriage request.", this.OnMarriageReject);

        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.optin", "Set baby opt-in. Usage: pr.pregnancy.optin <player> [on/off]", this.OnPregnancyOptIn);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.try", "Request try-for-baby. Usage: pr.pregnancy.try <player>", this.OnPregnancyTry);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.accept", "Accept pending try-for-baby request.", this.OnPregnancyAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.reject", "Reject pending try-for-baby request.", this.OnPregnancyReject);

        this.mod.Helper.ConsoleCommands.Add("pr.worker.runonce", "Run adult child worker pass immediately (host).", this.OnRunWorker);
        this.mod.Helper.ConsoleCommands.Add("pr.child.age", "Debug child aging (host). Usage: pr.child.age <childIdOrName> <days>", this.OnAgeChild);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.request", "Request to carry another player. Usage: pr.carry.request <player>", this.OnCarryRequest);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.accept", "Accept pending carry request.", this.OnCarryAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.reject", "Reject pending carry request.", this.OnCarryReject);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.stop", "Stop active carry. Usage: pr.carry.stop [player]", this.OnCarryStop);

        this.mod.Helper.ConsoleCommands.Add("pr.hearts.status", "Show heart points/level with a player. Usage: pr.hearts.status <player>", this.OnHeartsStatus);
        this.mod.Helper.ConsoleCommands.Add("pr.hearts.add", "Debug add heart points (host). Usage: pr.hearts.add <player> <delta>", this.OnHeartsAdd);

        this.mod.Helper.ConsoleCommands.Add("pr.date.immersive.start", "Start immersive date. Usage: pr.date.immersive.start <player> <town|beach|forest>", this.OnImmersiveDateStart);
        this.mod.Helper.ConsoleCommands.Add("pr.date.immersive.end", "End active immersive date.", this.OnImmersiveDateEnd);
        this.mod.Helper.ConsoleCommands.Add("pr.date.debug.spawnstands", "Debug spawn immersive stands locally. Usage: pr.date.debug.spawnstands <town|beach|forest>", this.OnDateDebugSpawnStands);
        this.mod.Helper.ConsoleCommands.Add("pr.date.debug.cleanup", "Debug cleanup immersive date runtime objects.", this.OnDateDebugCleanup);

        this.mod.Helper.ConsoleCommands.Add("pr.hands.request", "Request holding hands with a player. Usage: pr.hands.request <player>", this.OnHandsRequest);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.accept", "Accept pending holding hands request.", this.OnHandsAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.reject", "Reject pending holding hands request.", this.OnHandsReject);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.stop", "Stop active holding hands session. Usage: pr.hands.stop [player]", this.OnHandsStop);
    }

    private void OnProposeDating(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.DatingSystem.RequestDatingFromLocal(args[0], out string msg), msg);
    }

    private void OnAccept(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (this.mod.DatingSystem.RespondToPendingDatingLocal(accept: true, out string datingMsg))
        {
            this.mod.Notifier.NotifyInfo(datingMsg, "[PR.System.Dating]");
            return;
        }

        if (this.mod.MarriageSystem.RespondToPendingMarriageLocal(accept: true, out string marriageMsg))
        {
            this.mod.Notifier.NotifyInfo(marriageMsg, "[PR.System.Marriage]");
            return;
        }

        if (this.mod.PregnancySystem.RespondTryForBabyFromLocal(accept: true, out string babyMsg))
        {
            this.mod.Notifier.NotifyInfo(babyMsg, "[PR.System.Pregnancy]");
            return;
        }

        if (this.mod.CarrySystem.RespondToPendingCarryLocal(accept: true, out string carryMsg))
        {
            this.mod.Notifier.NotifyInfo(carryMsg, "[PR.System.Carry]");
            return;
        }

        if (this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(accept: true, out string handsMsg))
        {
            this.mod.Notifier.NotifyInfo(handsMsg, "[PR.System.HoldingHands]");
            return;
        }

        this.mod.Notifier.NotifyWarn("No pending request to accept.", "[PR.Core]");
    }

    private void OnReject(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (this.mod.DatingSystem.RespondToPendingDatingLocal(accept: false, out string datingMsg))
        {
            this.mod.Notifier.NotifyInfo(datingMsg, "[PR.System.Dating]");
            return;
        }

        if (this.mod.MarriageSystem.RespondToPendingMarriageLocal(accept: false, out string marriageMsg))
        {
            this.mod.Notifier.NotifyInfo(marriageMsg, "[PR.System.Marriage]");
            return;
        }

        if (this.mod.PregnancySystem.RespondTryForBabyFromLocal(accept: false, out string babyMsg))
        {
            this.mod.Notifier.NotifyInfo(babyMsg, "[PR.System.Pregnancy]");
            return;
        }

        if (this.mod.CarrySystem.RespondToPendingCarryLocal(accept: false, out string carryMsg))
        {
            this.mod.Notifier.NotifyInfo(carryMsg, "[PR.System.Carry]");
            return;
        }

        if (this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(accept: false, out string handsMsg))
        {
            this.mod.Notifier.NotifyInfo(handsMsg, "[PR.System.HoldingHands]");
            return;
        }

        this.mod.Notifier.NotifyWarn("No pending request to reject.", "[PR.Core]");
    }

    private void OnStatus(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (args.Length == 0)
        {
            IEnumerable<RelationshipRecord> relationships = this.mod.DatingSystem.GetRelationshipsForPlayer(this.mod.LocalPlayerId);
            int count = 0;
            foreach (RelationshipRecord relation in relationships)
            {
                count++;
                this.mod.Notifier.NotifyInfo(
                    $"Status with {relation.GetOtherName(this.mod.LocalPlayerId)}: {relation.State}.",
                    "[PR.System.Dating]");
            }

            if (count == 0)
            {
                this.mod.Notifier.NotifyInfo("No relationships found.", "[PR.System.Dating]");
            }

            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? target))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Dating]");
            return;
        }

        RelationshipRecord? selectedRelation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        if (selectedRelation is null)
        {
            this.mod.Notifier.NotifyInfo($"No relationship state with {target.Name}.", "[PR.System.Dating]");
            return;
        }

        this.mod.Notifier.NotifyInfo($"Status with {target.Name}: {selectedRelation.State}.", "[PR.System.Dating]");
    }

    private void OnDateStart(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.DateEventController.StartDateFromLocal(args[0], out string msg), msg);
    }

    private void OnProposeMarriage(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.MarriageSystem.RequestMarriageFromLocal(args[0], out string msg), msg);
    }

    private void OnMarriageAccept(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.MarriageSystem.RespondToPendingMarriageLocal(true, out string msg), msg);
    }

    private void OnMarriageReject(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.MarriageSystem.RespondToPendingMarriageLocal(false, out string msg), msg);
    }

    private void OnPregnancyOptIn(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        bool optIn = true;
        if (args.Length >= 2)
        {
            string switchValue = args[1].Trim().ToLowerInvariant();
            optIn = switchValue is "on" or "true" or "1" or "yes";
        }

        this.Finish(this.mod.PregnancySystem.SetOptInFromLocal(args[0], optIn, out string msg), msg);
    }

    private void OnPregnancyTry(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.PregnancySystem.RequestTryForBabyFromLocal(args[0], out string msg), msg);
    }

    private void OnPregnancyAccept(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.PregnancySystem.RespondTryForBabyFromLocal(true, out string msg), msg);
    }

    private void OnPregnancyReject(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.PregnancySystem.RespondTryForBabyFromLocal(false, out string msg), msg);
    }

    private void OnRunWorker(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        string report = this.mod.FarmWorkerSystem.RunWorkerPass(force: true);
        this.mod.FarmWorkerSystem.PublishReport(report);
    }

    private void OnAgeChild(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        if (!int.TryParse(args[1], out int days))
        {
            this.mod.Notifier.NotifyWarn("Days must be an integer.", "[PR.System.ChildGrowth]");
            return;
        }

        this.Finish(this.mod.ChildGrowthSystem.DebugAgeChild(args[0], days, out string msg), msg);
    }

    private void OnCarryRequest(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.CarrySystem.RequestCarryFromLocal(args[0], out string msg), msg);
    }

    private void OnCarryAccept(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.CarrySystem.RespondToPendingCarryLocal(true, out string msg), msg);
    }

    private void OnCarryReject(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.CarrySystem.RespondToPendingCarryLocal(false, out string msg), msg);
    }

    private void OnCarryStop(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        string? partner = args.Length > 0 ? args[0] : null;
        this.Finish(this.mod.CarrySystem.StopCarryFromLocal(partner, out string msg), msg);
    }

    private void OnHeartsStatus(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? partner))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Hearts]");
            return;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, partner.UniqueMultiplayerID);
        if (relation is null)
        {
            this.mod.Notifier.NotifyInfo($"No relationship record with {partner.Name}.", "[PR.System.Hearts]");
            return;
        }

        int level = relation.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts);
        this.mod.Notifier.NotifyInfo(
            $"Hearts with {partner.Name}: {relation.HeartPoints} pts, level {level}/{this.mod.Config.MaxHearts}.",
            "[PR.System.Hearts]");
    }

    private void OnHeartsAdd(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.hearts.add.", "[PR.System.Hearts]");
            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? partner))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Hearts]");
            return;
        }

        if (!int.TryParse(args[1], out int delta))
        {
            this.mod.Notifier.NotifyWarn("Delta must be an integer.", "[PR.System.Hearts]");
            return;
        }

        this.mod.HeartsSystem.AddPointsForPlayers(this.mod.LocalPlayerId, partner.UniqueMultiplayerID, delta, "debug_cmd");
        this.mod.Notifier.NotifyInfo($"Hearts delta applied: {delta}.", "[PR.System.Hearts]");
    }

    private void OnImmersiveDateStart(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        if (!Enum.TryParse(args[1], ignoreCase: true, out ImmersiveDateLocation location))
        {
            this.mod.Notifier.NotifyWarn("Location must be town, beach, or forest.", "[PR.System.DateImmersion]");
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(args[0], location, out string msg), msg);
    }

    private void OnImmersiveDateEnd(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.EndImmersiveDateFromLocal(out string msg), msg);
    }

    private void OnDateDebugSpawnStands(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Debug stand spawn is host only.", "[PR.System.DateImmersion]");
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.DebugSpawnStands(args[0], out string msg), msg);
    }

    private void OnDateDebugCleanup(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.DebugCleanup(out string msg), msg);
    }

    private void OnHandsRequest(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.HoldingHandsSystem.RequestHoldingHandsFromLocal(args[0], out string msg), msg);
    }

    private void OnHandsAccept(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(true, out string msg), msg);
    }

    private void OnHandsReject(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(false, out string msg), msg);
    }

    private void OnHandsStop(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        string? partner = args.Length > 0 ? args[0] : null;
        this.Finish(this.mod.HoldingHandsSystem.StopHoldingHandsFromLocal(partner, out string msg), msg);
    }

    private bool RequireWorldReady()
    {
        if (Context.IsWorldReady)
        {
            return true;
        }

        this.mod.Monitor.Log("[PR.Core] Command ignored: world not ready.", LogLevel.Warn);
        return false;
    }

    private bool RequireArg(string[] args, int requiredCount)
    {
        if (args.Length >= requiredCount)
        {
            return true;
        }

        this.mod.Notifier.NotifyWarn("Missing arguments.", "[PR.Core]");
        return false;
    }

    private void Finish(bool success, string message)
    {
        if (success)
        {
            this.mod.Notifier.NotifyInfo(message, "[PR.Core]");
        }
        else
        {
            this.mod.Notifier.NotifyWarn(message, "[PR.Core]");
        }
    }
}
