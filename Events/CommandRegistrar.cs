using PlayerRomance.Data;
using PlayerRomance.Systems;
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
        this.mod.Helper.ConsoleCommands.Add("romance", "Developer command root. Usage: romance help dev", this.OnRomanceCommand);
        this.mod.Helper.ConsoleCommands.Add("romance_help_dev", "Alias: list all debug/dev commands.", this.OnRomanceHelpDev);

        this.mod.Helper.ConsoleCommands.Add("pr.propose", "Send dating proposal. Usage: pr.propose <playerNameOrId>", this.OnProposeDating);
        this.mod.Helper.ConsoleCommands.Add("pr.accept", "Accept pending request (dating/marriage/pregnancy/carry/hands).", this.OnAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.reject", "Reject pending request (dating/marriage/pregnancy/carry/hands).", this.OnReject);
        this.mod.Helper.ConsoleCommands.Add("pr.status", "Show relationship status. Usage: pr.status [playerNameOrId]", this.OnStatus);
        this.mod.Helper.ConsoleCommands.Add("pr.menu", "Open Player Romance menu.", (_, _) => Game1.activeClickableMenu = new UI.RomanceMenu(this.mod));

        this.mod.Helper.ConsoleCommands.Add("pr.date.start", "Force date start. Usage: pr.date.start <playerNameOrId> OR pr.date.start <dateId> [playerNameOrId]", this.OnDateStart);
        this.mod.Helper.ConsoleCommands.Add("pr.date.end", "Force end active date runtime (map + immersive).", this.OnDateEnd);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.propose", "Send marriage proposal. Usage: pr.marry.propose <playerNameOrId>", this.OnProposeMarriage);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.accept", "Accept pending marriage request.", this.OnMarriageAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.reject", "Reject pending marriage request.", this.OnMarriageReject);
        this.mod.Helper.ConsoleCommands.Add("pr.marry.force", "Cheat: force married state with a player (host). Usage: pr.marry.force <player>", this.OnMarriageForce);
        this.mod.Helper.ConsoleCommands.Add("force_marriage", "Cheat alias for pr.marry.force. Usage: force_marriage <player>", this.OnMarriageForce);

        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.optin", "Set baby opt-in. Usage: pr.pregnancy.optin <player> [on/off]", this.OnPregnancyOptIn);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.try", "Request try-for-baby. Usage: pr.pregnancy.try <player>", this.OnPregnancyTry);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.accept", "Accept pending try-for-baby request.", this.OnPregnancyAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.reject", "Reject pending try-for-baby request.", this.OnPregnancyReject);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.force", "Cheat: force pregnancy with a player (host). Usage: pr.pregnancy.force <player> [days]", this.OnPregnancyForce);
        this.mod.Helper.ConsoleCommands.Add("pr.pregnancy.birth", "Cheat: force immediate birth with a player (host). Usage: pr.pregnancy.birth <player>", this.OnPregnancyBirth);

        this.mod.Helper.ConsoleCommands.Add("pr.worker.runonce", "Run adult child worker pass immediately (host).", this.OnRunWorker);
        this.mod.Helper.ConsoleCommands.Add("pr.child.feed", "Feed a child (host-authoritative). Usage: pr.child.feed <childId> [itemId]", this.OnChildFeed);
        this.mod.Helper.ConsoleCommands.Add("pr.child.feed.menu", "Open feed inventory menu for child. Usage: pr.child.feed.menu <childId>", this.OnChildFeedMenu);
        this.mod.Helper.ConsoleCommands.Add("pr.child.interact", "Run child interaction action. Usage: pr.child.interact <childId> <care|play|feed>", this.OnChildInteract);
        this.mod.Helper.ConsoleCommands.Add("pr.child.status", "Show child status. Usage: pr.child.status [childId]", this.OnChildStatus);
        this.mod.Helper.ConsoleCommands.Add("pr.child.age.set", "Debug set child age in years (host). Usage: pr.child.age.set <childIdOrName> <years> OR pr.child.age.set <years> (if single child)", this.OnChildAgeSet);
        this.mod.Helper.ConsoleCommands.Add("pr.child.task", "Assign child task. Usage: pr.child.task <childId> <auto|water|feed|collect|harvest|ship|fish|stop>", this.OnChildTask);
        this.mod.Helper.ConsoleCommands.Add("pr.child.work.force", "Debug force child work run immediately. Usage: pr.child.work.force <childIdOrName>", this.OnChildWorkForce);
        this.mod.Helper.ConsoleCommands.Add("pr.child.job.set", "Debug force child legacy job/specialization. Usage: pr.child.job.set <childIdOrName> <none|forager|crabpot|rancher|artisan|geologist>", this.OnChildJobSet);
        this.mod.Helper.ConsoleCommands.Add("pr.child.dump", "Debug dump complete child state. Usage: pr.child.dump <childIdOrName>", this.OnChildDump);
        this.mod.Helper.ConsoleCommands.Add("pr.child.age", "Debug child aging (host). Usage: pr.child.age <childIdOrName> <days>", this.OnAgeChild);
        this.mod.Helper.ConsoleCommands.Add("pr.child.grow.force", "Force child transition to adult stage with diagnostics (host). Usage: pr.child.grow.force <childIdOrName> [years>=16]", this.OnChildGrowForce);
        this.mod.Helper.ConsoleCommands.Add("pr.child.work.where", "Show where a child is currently assigned/working (host). Usage: pr.child.work.where <childIdOrName>", this.OnChildWorkWhere);
        this.mod.Helper.ConsoleCommands.Add("pr.child.work.force.now", "Alias of pr.child.work.force to run immediate child work pass.", this.OnChildWorkForce);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.request", "Request to carry another player. Usage: pr.carry.request <player>", this.OnCarryRequest);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.accept", "Accept pending carry request.", this.OnCarryAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.reject", "Reject pending carry request.", this.OnCarryReject);
        this.mod.Helper.ConsoleCommands.Add("pr.carry.stop", "Stop active carry. Usage: pr.carry.stop [player]", this.OnCarryStop);

        this.mod.Helper.ConsoleCommands.Add("pr.hearts.status", "Show heart points/level with a player. Usage: pr.hearts.status <player>", this.OnHeartsStatus);
        this.mod.Helper.ConsoleCommands.Add("pr.hearts.add", "Debug add heart points (host). Usage: pr.hearts.add <player> <delta>", this.OnHeartsAdd);
        this.mod.Helper.ConsoleCommands.Add("pr.hearts.max", "Cheat: set hearts to max with a player (host). Usage: pr.hearts.max <player>", this.OnHeartsMax);
        this.mod.Helper.ConsoleCommands.Add("max_hearth", "Cheat alias for pr.hearts.max. Usage: max_hearth <player>", this.OnHeartsMax);

        this.mod.Helper.ConsoleCommands.Add("pr.date.immersive.start", "Start immersive date. Usage: pr.date.immersive.start <player> <town|beach|forest>", this.OnImmersiveDateStart);
        this.mod.Helper.ConsoleCommands.Add("pr.date.immersive.end", "End active immersive date.", this.OnImmersiveDateEnd);
        this.mod.Helper.ConsoleCommands.Add("pr.date.immersive.retry", "Retry immersive date start handshake when session is not yet confirmed.", this.OnImmersiveDateRetry);
        this.mod.Helper.ConsoleCommands.Add("pr.date.debug.spawnstands", "Debug spawn immersive stands locally. Usage: pr.date.debug.spawnstands <town|beach|forest>", this.OnDateDebugSpawnStands);
        this.mod.Helper.ConsoleCommands.Add("pr.date.debug.cleanup", "Debug cleanup immersive date runtime objects.", this.OnDateDebugCleanup);
        this.mod.Helper.ConsoleCommands.Add("pr.date.reset_state", "Debug force reset all date/immersive states and lockouts.", this.OnDateResetState);
        this.mod.Helper.ConsoleCommands.Add("date_start", "Host-only map date start. Usage: date_start <dateId> <playerBNameOrId>", this.OnDateMapStart);
        this.mod.Helper.ConsoleCommands.Add("date_markers_dump", "Host-only dump Date_Beach markers.", this.OnDateMarkersDump);
        this.mod.Helper.ConsoleCommands.Add("date_end", "Host-only end active map date.", this.OnDateMapEnd);
        this.mod.Helper.ConsoleCommands.Add("date_asset_test", "Host-only: load Maps/Date_Beach and log diagnostics.", this.OnDateAssetTest);
        this.mod.Helper.ConsoleCommands.Add("date_warp_test", "Host-only: warp to Date_Beach at (10,10).", this.OnDateWarpTest);
        this.mod.Helper.ConsoleCommands.Add("date_export_hint", "Show patch export guidance for Date_Beach.", this.OnDateExportHint);
        this.mod.Helper.ConsoleCommands.Add("pr.date.asset_test", "Alias: host-only map asset load test for Date_Beach.", this.OnDateAssetTest);
        this.mod.Helper.ConsoleCommands.Add("pr.date.warp_test", "Alias: host-only warp test to Date_Beach.", this.OnDateWarpTest);
        this.mod.Helper.ConsoleCommands.Add("pr.date.markers_dump", "Alias: host-only dump Date_Beach markers.", this.OnDateMarkersDump);
        this.mod.Helper.ConsoleCommands.Add("pr.vendor.shop.open", "Vendor shop command. Usage: pr.vendor.shop.open <vanilla|ice|roses|clothing> [itemId] [offer]", this.OnVendorShopOpen);
        this.mod.Helper.ConsoleCommands.Add("pr.sim.morning", "Debug simulate morning pass (day-start + 06:10 host tick).", this.OnSimMorning);

        this.mod.Helper.ConsoleCommands.Add("pr.hands.request", "Request holding hands with a player. Usage: pr.hands.request <player>", this.OnHandsRequest);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.accept", "Accept pending holding hands request.", this.OnHandsAccept);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.reject", "Reject pending holding hands request.", this.OnHandsReject);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.stop", "Stop active holding hands session. Usage: pr.hands.stop [player]", this.OnHandsStop);
        this.mod.Helper.ConsoleCommands.Add("pr.hands.debug.status", "Debug holding hands runtime status.", this.OnHandsDebugStatus);

        this.mod.Helper.ConsoleCommands.Add("romance_status", "Debug relationship and systems status (host only).", this.OnRomanceStatus);
        this.mod.Helper.ConsoleCommands.Add("children_list", "Debug list of children state (host only).", this.OnChildrenList);
        this.mod.Helper.ConsoleCommands.Add("synergy_test", "Debug run couple synergy checks (host only).", this.OnSynergyTest);
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

        string firstArg = args[0];
        if (this.mod.IsHostPlayer
            && firstArg.StartsWith("date_", StringComparison.OrdinalIgnoreCase))
        {
            string partnerToken;
            if (args.Length >= 2)
            {
                partnerToken = args[1];
            }
            else if (!this.TryResolveDefaultDatePartnerToken(out partnerToken))
            {
                this.mod.Notifier.NotifyWarn("No online partner found. Usage: pr.date.start <dateId> <player>", "[PR.System.DateEvent]");
                return;
            }

            this.Finish(this.mod.DateEventController.StartDateDebugFromLocal(firstArg, partnerToken, out string mapMsg), mapMsg);
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.ForceStartImmersiveDateDebugFromLocal(firstArg, ImmersiveDateLocation.Town, out string msg), msg);
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

    private void OnMarriageForce(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.marry.force / force_marriage.", "[PR.System.Marriage]");
            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? target))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Marriage]");
            return;
        }

        this.Finish(this.mod.MarriageSystem.ForceMarriageHost(this.mod.LocalPlayerId, target.UniqueMultiplayerID, out string msg), msg);
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

    private void OnPregnancyForce(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.pregnancy.force.", "[PR.System.Pregnancy]");
            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? partner))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Pregnancy]");
            return;
        }

        int days = this.mod.Config.PregnancyDays;
        if (args.Length >= 2 && !int.TryParse(args[1], out days))
        {
            this.mod.Notifier.NotifyWarn("Days must be an integer.", "[PR.System.Pregnancy]");
            return;
        }

        this.Finish(this.mod.PregnancySystem.ForcePregnancyHost(this.mod.LocalPlayerId, partner.UniqueMultiplayerID, days, out string msg), msg);
    }

    private void OnPregnancyBirth(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.pregnancy.birth.", "[PR.System.Pregnancy]");
            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? partner))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Pregnancy]");
            return;
        }

        this.Finish(this.mod.PregnancySystem.ForceBirthHost(this.mod.LocalPlayerId, partner.UniqueMultiplayerID, out string msg), msg);
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

    private void OnChildFeed(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        string? itemId = args.Length >= 2 ? args[1] : null;
        this.Finish(this.mod.ChildGrowthSystem.FeedChildFromLocal(args[0], itemId, out string msg), msg);
    }

    private void OnChildFeedMenu(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.ChildGrowthSystem.OpenFeedInventoryMenuFromLocal(args[0], out string msg), msg);
    }

    private void OnChildInteract(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        this.Finish(this.mod.ChildGrowthSystem.InteractChildFromLocal(args[0], args[1], out string msg), msg);
    }

    private void OnChildStatus(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        string? child = args.Length >= 1 ? args[0] : null;
        this.Finish(this.mod.ChildGrowthSystem.GetChildStatusFromLocal(child, out string msg), msg);
    }

    private void OnChildAgeSet(string command, string[] args)
    {
        if (!this.RequireWorldReady() || args.Length == 0)
        {
            return;
        }

        string childToken;
        int years;

        if (args.Length == 1)
        {
            if (!int.TryParse(args[0], out years))
            {
                this.mod.Notifier.NotifyWarn("Expected integer age. Usage: pr.child.age.set <childIdOrName> <years> OR <years> (single child).", "[PR.System.ChildGrowth]");
                return;
            }

            List<ChildRecord> pool = this.mod.IsHostPlayer
                ? this.mod.HostSaveData.Children.Values.ToList()
                : this.mod.ClientSnapshot.Children.ToList();
            if (pool.Count == 1)
            {
                childToken = pool[0].ChildId;
                this.mod.Monitor.Log($"[PR.System.ChildGrowth] age.set fallback used single-child mode for '{pool[0].ChildName}' ({pool[0].ChildId}).", LogLevel.Info);
            }
            else if (pool.Count == 0)
            {
                this.mod.Notifier.NotifyWarn("No child found for single-argument fallback.", "[PR.System.ChildGrowth]");
                return;
            }
            else
            {
                this.mod.Notifier.NotifyWarn($"Ambiguous child selection ({pool.Count} children). Provide child id/name explicitly.", "[PR.System.ChildGrowth]");
                this.mod.Monitor.Log("[PR.System.ChildGrowth] age.set ambiguity: multiple children present for single-arg call.", LogLevel.Warn);
                return;
            }
        }
        else
        {
            string last = args[^1];
            if (!int.TryParse(last, out years))
            {
                this.mod.Notifier.NotifyWarn($"Expected integer age, got '{last}'.", "[PR.System.ChildGrowth]");
                return;
            }

            childToken = string.Join(' ', args.Take(args.Length - 1)).Trim();
            if (string.IsNullOrWhiteSpace(childToken))
            {
                this.mod.Notifier.NotifyWarn("Missing child id/name before age integer.", "[PR.System.ChildGrowth]");
                return;
            }
        }

        this.Finish(this.mod.ChildGrowthSystem.SetChildAgeYearsFromLocal(childToken, years, out string msg), msg);
    }

    private void OnChildWorkForce(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.child.work.force.", "[PR.System.Worker]");
            return;
        }

        bool okWorker = this.mod.FarmWorkerSystem.RunWorkerForChildDebug(args[0], out string workerMsg);
        bool okLegacy = this.mod.LegacyChildrenSystem.ForceRunChildDailyDebug(args[0], out string legacyMsg);
        this.Finish(okWorker && okLegacy, $"{workerMsg} | {legacyMsg}");
    }

    private void OnChildJobSet(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.child.job.set.", "[PR.System.Legacy]");
            return;
        }

        this.Finish(this.mod.LegacyChildrenSystem.SetChildJobDebug(args[0], args[1], out string msg), msg);
    }

    private void OnChildDump(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        List<ChildRecord> pool = this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Children.Values.ToList()
            : this.mod.ClientSnapshot.Children.ToList();
        ChildRecord? child = pool.FirstOrDefault(c =>
            c.ChildId.Equals(args[0], StringComparison.OrdinalIgnoreCase)
            || c.ChildName.Equals(args[0], StringComparison.OrdinalIgnoreCase));
        if (child is null)
        {
            this.mod.Notifier.NotifyWarn($"Child '{args[0]}' not found.", "[PR.System.ChildGrowth]");
            return;
        }

        string dump =
            $"child={child.ChildName} id={child.ChildId} age={child.AgeYears}y/{child.AgeDays}d stage={child.Stage} " +
            $"fed={child.IsFedToday} care={child.IsCaredToday} play={child.IsPlayedToday} feedProg={child.FeedingProgress} " +
            $"task={child.AssignedTask} auto={child.AutoMode} worker={child.IsWorkerEnabled} lastWorkDay={child.LastWorkedDay} " +
            $"legacyAssign={child.LegacyAssignment} legacySpec={child.LegacySpecialization} edu={child.EducationScore} routine={child.RoutineZone}";
        this.mod.Monitor.Log($"[PR.System.ChildGrowth] {dump}", LogLevel.Info);
        this.mod.Notifier.NotifyInfo($"Child dump logged for {child.ChildName}.", "[PR.System.ChildGrowth]");
    }

    private void OnChildTask(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        this.Finish(this.mod.ChildGrowthSystem.SetChildTaskFromLocal(args[0], args[1], out string msg), msg);
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

    private void OnChildGrowForce(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        int years = 16;
        if (args.Length >= 2 && (!int.TryParse(args[1], out years) || years < 16))
        {
            this.mod.Notifier.NotifyWarn("Years must be an integer >= 16.", "[PR.System.ChildGrowth]");
            return;
        }

        this.Finish(this.mod.ChildGrowthSystem.ForceGrowToAdultFromLocal(args[0], years, out string msg), msg);
    }

    private void OnChildWorkWhere(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        this.Finish(this.mod.FarmWorkerSystem.GetChildWorkWhereDebug(args[0], out string msg), msg);
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

    private void OnHeartsMax(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run max_hearth / pr.hearts.max.", "[PR.System.Hearts]");
            return;
        }

        if (!this.mod.TryResolvePlayerToken(args[0], out Farmer? partner))
        {
            this.mod.Notifier.NotifyWarn($"Player '{args[0]}' not found.", "[PR.System.Hearts]");
            return;
        }

        int maxPoints = Math.Max(1, this.mod.Config.MaxHearts * Math.Max(1, this.mod.Config.HeartPointsPerHeart));
        RelationshipRecord relation = ConsentSystem.GetOrCreateRelationship(
            this.mod.HostSaveData,
            this.mod.LocalPlayerId,
            partner.UniqueMultiplayerID,
            this.mod.LocalPlayerName,
            partner.Name);

        int delta = maxPoints - relation.HeartPoints;
        this.mod.HeartsSystem.AddPointsForPlayers(this.mod.LocalPlayerId, partner.UniqueMultiplayerID, delta, "debug_max_hearth");

        int level = relation.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts);
        this.mod.Notifier.NotifyInfo(
            $"Hearts with {partner.Name} forced to max: {relation.HeartPoints} pts (Lv {level}/{this.mod.Config.MaxHearts}).",
            "[PR.System.Hearts]");
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

        this.Finish(this.mod.DateImmersionSystem.ForceStartImmersiveDateDebugFromLocal(args[0], location, out string msg), msg);
    }

    private void OnImmersiveDateEnd(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.EndImmersiveDateFromLocal(out string msg), msg);
    }

    private void OnDateEnd(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        bool immersiveOk = this.mod.DateImmersionSystem.EndImmersiveDateFromLocal(out string immersiveMsg);
        bool mapOk = this.mod.DateEventController.EndDateFromLocal(out string mapMsg);
        bool ok = immersiveOk || mapOk;
        this.Finish(ok, $"{immersiveMsg} | {mapMsg}");
    }

    private void OnImmersiveDateRetry(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.DateImmersionSystem.RetryStartFromLocal(out string msg), msg);
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

    private void OnDateMapStart(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 2))
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run date_start.", "[PR.System.DateEvent]");
            return;
        }

        this.Finish(this.mod.DateEventController.StartDateDebugFromLocal(args[0], args[1], out string msg), msg);
    }

    private void OnDateMarkersDump(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run date_markers_dump.", "[PR.System.DateEvent]");
            return;
        }

        this.Finish(this.mod.DateEventController.DumpMarkersFromLocal(out string msg), msg);
    }

    private void OnDateMapEnd(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run date_end.", "[PR.System.DateEvent]");
            return;
        }

        this.Finish(this.mod.DateEventController.EndDateFromLocal(out string msg), msg);
    }

    private void OnDateAssetTest(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run date_asset_test.", "[PR.System.DateEvent]");
            return;
        }

        this.Finish(this.mod.DateEventController.DateAssetTestFromLocal(out string msg), msg);
    }

    private void OnDateWarpTest(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run date_warp_test.", "[PR.System.DateEvent]");
            return;
        }

        this.Finish(this.mod.DateEventController.DateWarpTestFromLocal(out string msg), msg);
    }

    private void OnDateExportHint(string command, string[] args)
    {
        this.mod.DateEventController.LogDateExportHint();
        this.mod.Notifier.NotifyInfo("Date export hint logged.", "[PR.System.DateEvent]");
    }

    private void OnDateResetState(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.date.reset_state.", "[PR.System.DateEvent]");
            return;
        }

        bool okImmersive = this.mod.DateImmersionSystem.ForceResetDateStateHost(clearDailyHistory: true, out string immersiveMsg);
        bool okMap = this.mod.DateEventController.ForceResetDateRuntimeFromLocal(out string mapMsg);
        bool ok = okImmersive && okMap;
        string msg = $"{immersiveMsg} | {mapMsg}";
        this.Finish(ok, msg);
    }

    private void OnVendorShopOpen(string command, string[] args)
    {
        if (!this.RequireWorldReady() || !this.RequireArg(args, 1))
        {
            return;
        }

        if (args[0].Equals("vanilla", StringComparison.OrdinalIgnoreCase)
            || args[0].Equals("open", StringComparison.OrdinalIgnoreCase))
        {
            this.Finish(this.mod.DateImmersionSystem.OpenVendorShopFromLocal(out string shopMsg), shopMsg);
            return;
        }

        if (!TryParseStandType(args[0], out DateStandType standType))
        {
            this.mod.Notifier.NotifyWarn("Stand must be ice, roses, clothing, or 'vanilla'.", "[PR.System.DateImmersion]");
            return;
        }

        IReadOnlyList<StandOfferDefinition> offers = this.mod.DateImmersionSystem.GetStandOffers(standType);
        if (offers.Count == 0)
        {
            this.mod.Notifier.NotifyWarn("No offers found for this stand.", "[PR.System.DateImmersion]");
            return;
        }

        if (args.Length == 1)
        {
            this.mod.Notifier.NotifyInfo($"Offers at {standType} stand:", "[PR.System.DateImmersion]");
            foreach (StandOfferDefinition offer in offers)
            {
                this.mod.Monitor.Log($"[PR.System.DateImmersion] - {offer.ItemId} | {offer.DisplayName} | {offer.Price}g | hearts +{offer.HeartDeltaOnOffer}", LogLevel.Info);
            }

            this.mod.Notifier.NotifyInfo("Use: pr.vendor.shop.open <stand> <itemId> [offer]", "[PR.System.DateImmersion]");
            return;
        }

        string itemId = args[1];
        bool offerToPartner = args.Length >= 3 && args[2].Equals("offer", StringComparison.OrdinalIgnoreCase);
        this.Finish(this.mod.DateImmersionSystem.RequestStandPurchaseFromLocal(standType, itemId, offerToPartner, out string msg), msg);
    }

    private void OnSimMorning(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run pr.sim.morning.", "[PR.Core]");
            return;
        }

        int previousTime = Game1.timeOfDay;
        Game1.timeOfDay = 610;
        this.mod.DateImmersionSystem.OnDayStartedHost();
        this.mod.PregnancySystem.OnDayStartedHost();
        this.mod.ChildGrowthSystem.OnDayStartedHost();
        this.mod.FarmWorkerSystem.OnDayStartedHost();
        this.mod.LegacyChildrenSystem.OnDayStartedHost();
        this.mod.CoupleSynergySystem.OnDayStartedHost();
        this.mod.DateEventController.OnDayStartedHost();
        this.mod.DateImmersionSystem.OnOneSecondUpdateTickedHost();
        this.mod.CoupleSynergySystem.OnOneSecondUpdateTickedHost();

        this.mod.Monitor.Log($"[PR.Core] Debug morning simulation executed (time {previousTime} -> {Game1.timeOfDay}).", LogLevel.Info);
        this.mod.Notifier.NotifyInfo("Debug morning simulation completed.", "[PR.Core]");
    }

    private void OnRomanceCommand(string command, string[] args)
    {
        if (args.Length >= 2
            && args[0].Equals("help", StringComparison.OrdinalIgnoreCase)
            && args[1].Equals("dev", StringComparison.OrdinalIgnoreCase))
        {
            this.OnRomanceHelpDev(command, args);
            return;
        }

        this.mod.Notifier.NotifyWarn("Usage: romance help dev", "[PR.Core]");
    }

    private void OnRomanceHelpDev(string command, string[] args)
    {
        string[] lines =
        {
            "DEV COMMANDS:",
            "  romance help dev",
            "  romance_help_dev",
            "  pr.date.immersive.start <player|DummyPartner> <town|beach|forest>",
            "  pr.date.immersive.end",
            "  pr.date.start <player>  (force immersive town)",
            "  pr.date.start <dateId> [player]  (force map date)",
            "  pr.date.end",
            "  pr.date.immersive.retry",
            "  pr.date.reset_state",
            "  pr.date.asset_test / date_asset_test",
            "  pr.date.warp_test / date_warp_test",
            "  pr.date.markers_dump / date_markers_dump",
            "  date_start <dateId> <player>",
            "  date_end",
            "  pr.vendor.shop.open <vanilla|ice|roses|clothing> [itemId] [offer]",
            "  pr.child.age.set <child> <years>",
            "  pr.child.age.set <years>   (single child fallback)",
            "  pr.child.grow.force <child> [years>=16]",
            "  pr.child.task <child> <auto|water|feed|collect|harvest|ship|fish|stop>",
            "  pr.child.work.force <child>",
            "  pr.child.work.where <child>",
            "  pr.child.job.set <child> <none|forager|crabpot|rancher|artisan|geologist>",
            "  pr.child.dump <child>",
            "  pr.sim.morning",
            "  pr.worker.runonce",
            "  romance_status, children_list, synergy_test",
            "Examples:",
            "  pr.date.immersive.start DummyPartner beach",
            "  pr.date.reset_state",
            "  pr.child.age.set 16",
            "  pr.child.work.force AliceChild",
            "  pr.child.job.set AliceChild artisan"
        };

        foreach (string line in lines)
        {
            this.mod.Monitor.Log($"[PR.Core] {line}", LogLevel.Info);
        }

        this.mod.Notifier.NotifyInfo("Developer command list logged to SMAPI console.", "[PR.Core]");
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

    private void OnHandsDebugStatus(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        this.Finish(this.mod.HoldingHandsSystem.GetDebugStatusFromLocal(out string msg), msg);
    }

    private void OnRomanceStatus(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run romance_status.", "[PR.Core]");
            return;
        }

        int rel = this.mod.HostSaveData.Relationships.Count;
        int children = this.mod.HostSaveData.Children.Count;
        int preg = this.mod.HostSaveData.Pregnancies.Count;
        this.mod.Notifier.NotifyInfo($"Host status: relationships={rel}, children={children}, pregnancies={preg}.", "[PR.Core]");
    }

    private void OnChildrenList(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run children_list.", "[PR.System.ChildGrowth]");
            return;
        }

        if (this.mod.HostSaveData.Children.Count == 0)
        {
            this.mod.Notifier.NotifyInfo("No children found.", "[PR.System.ChildGrowth]");
            return;
        }

        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values.OrderBy(p => p.ChildName))
        {
            this.mod.Notifier.NotifyInfo(
                $"id={child.ChildId} name={child.ChildName} age={child.AgeYears}y stage={child.Stage} fed={child.IsFedToday} care={child.IsCaredToday} play={child.IsPlayedToday} task={child.AssignedTask}",
                "[PR.System.ChildGrowth]");
        }
    }

    private void OnSynergyTest(string command, string[] args)
    {
        if (!this.RequireWorldReady())
        {
            return;
        }

        if (!this.mod.IsHostPlayer)
        {
            this.mod.Notifier.NotifyWarn("Only host can run synergy_test.", "[PR.System.Synergy]");
            return;
        }

        if (!this.mod.Config.EnableCoupleSynergy)
        {
            this.mod.Notifier.NotifyWarn("Couple synergy module is disabled in config.", "[PR.System.Synergy]");
            return;
        }

        if (this.mod.CoupleSynergySystem.RunDebugProbe(out string report))
        {
            this.mod.Notifier.NotifyInfo($"Synergy probe: {report}", "[PR.System.Synergy]");
        }
        else
        {
            this.mod.Notifier.NotifyWarn(report, "[PR.System.Synergy]");
        }
    }

    private bool TryResolveDefaultDatePartnerToken(out string partnerToken)
    {
        partnerToken = string.Empty;
        foreach (RelationshipRecord relation in this.mod.HostSaveData.Relationships.Values)
        {
            if (!relation.Includes(this.mod.LocalPlayerId)
                || relation.State < RelationshipState.Dating)
            {
                continue;
            }

            long otherId = relation.GetOther(this.mod.LocalPlayerId);
            Farmer? other = this.mod.FindFarmerById(otherId, includeOffline: false);
            if (other is null)
            {
                continue;
            }

            partnerToken = other.Name;
            return true;
        }

        return false;
    }

    private static bool TryParseStandType(string token, out DateStandType standType)
    {
        standType = DateStandType.IceCream;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        switch (token.Trim().ToLowerInvariant())
        {
            case "ice":
            case "icecream":
            case "ice_cream":
                standType = DateStandType.IceCream;
                return true;
            case "rose":
            case "roses":
                standType = DateStandType.Roses;
                return true;
            case "cloth":
            case "clothes":
            case "clothing":
                standType = DateStandType.Clothing;
                return true;
            default:
                return false;
        }
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
