using PlayerRomance.Data;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;

namespace PlayerRomance.Systems;

public sealed class LegacyChildrenSystem
{
    private readonly ModEntry mod;
    private readonly Random random = new();

    public LegacyChildrenSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Reset()
    {
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableLegacyChildren)
        {
            return;
        }

        int day = this.mod.GetCurrentDayNumber();
        bool changed = false;

        foreach (ChildRecord child in this.mod.HostSaveData.Children.Values)
        {
            changed |= this.ProcessChildLegacyDaily(child, day);
        }

        if (changed)
        {
            this.mod.MarkDataDirty("Legacy children daily update.", flushNow: true);
            this.mod.NetSync.BroadcastSnapshotToAll();
        }
    }

    public bool SetChildJobDebug(string childToken, string jobToken, out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can set child job.";
            return false;
        }

        ChildRecord? child = this.mod.HostSaveData.Children.Values.FirstOrDefault(c =>
            c.ChildId.Equals(childToken, StringComparison.OrdinalIgnoreCase)
            || c.ChildName.Equals(childToken, StringComparison.OrdinalIgnoreCase));
        if (child is null)
        {
            message = $"Child '{childToken}' not found.";
            return false;
        }

        string normalized = (jobToken ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "none":
                child.LegacyAssignment = LegacyChildAssignment.None;
                child.LegacySpecialization = LegacyChildSpecialization.None;
                break;
            case "forager":
                child.LegacyAssignment = LegacyChildAssignment.Forager;
                break;
            case "crabpot":
            case "crab":
            case "crabpotassistant":
                child.LegacyAssignment = LegacyChildAssignment.CrabPotAssistant;
                break;
            case "rancher":
                child.LegacySpecialization = LegacyChildSpecialization.Rancher;
                break;
            case "artisan":
                child.LegacySpecialization = LegacyChildSpecialization.Artisan;
                break;
            case "geologist":
                child.LegacySpecialization = LegacyChildSpecialization.Geologist;
                break;
            default:
                message = "Unknown job token. Use: none|forager|crabpot|rancher|artisan|geologist.";
                return false;
        }

        this.mod.MarkDataDirty($"Debug set child job {child.ChildId} -> {normalized}.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        message = $"{child.ChildName} legacy assignment={child.LegacyAssignment}, specialization={child.LegacySpecialization}.";
        return true;
    }

    public bool ForceRunChildDailyDebug(string childToken, out string message)
    {
        if (!Context.IsWorldReady)
        {
            message = "World not ready.";
            return false;
        }

        if (!this.mod.IsHostPlayer)
        {
            message = "Only host can run forced child daily legacy.";
            return false;
        }

        ChildRecord? child = this.mod.HostSaveData.Children.Values.FirstOrDefault(c =>
            c.ChildId.Equals(childToken, StringComparison.OrdinalIgnoreCase)
            || c.ChildName.Equals(childToken, StringComparison.OrdinalIgnoreCase));
        if (child is null)
        {
            message = $"Child '{childToken}' not found.";
            return false;
        }

        int day = this.mod.GetCurrentDayNumber();
        child.LastLegacyTaskDay = -1;
        bool changed = this.ProcessChildLegacyDaily(child, day);
        if (changed)
        {
            this.mod.MarkDataDirty($"Debug force legacy daily for child {child.ChildId}.", flushNow: true);
            this.mod.NetSync.BroadcastSnapshotToAll();
        }

        message = changed
            ? $"Forced legacy daily run applied for {child.ChildName}."
            : $"Forced legacy run executed for {child.ChildName} (no output changes this pass).";
        return true;
    }

    private bool ProcessChildLegacyDaily(ChildRecord child, int day)
    {
        bool changed = false;
        if (day < 0 || child.LastLegacyTaskDay == day)
        {
            return false;
        }

        child.LastLegacyTaskDay = day;

        bool isMonday = (Game1.stats.DaysPlayed % 7) == 1;
        if (this.mod.Config.TuitionEnabled && isMonday)
        {
            changed |= this.TryApplyTuition(child, day);
        }

        ChildLifeStage stage = child.Stage;
        if (stage == ChildLifeStage.Teen || child.AgeYears is >= 13 and < 18)
        {
            changed |= this.TryRunTeenTask(child);
        }

        if (this.mod.Config.AdultSpecialization && (stage == ChildLifeStage.Adult || child.AgeYears >= 18))
        {
            changed |= this.TryRunAdultSpecialization(child);
        }

        return changed;
    }

    private bool TryApplyTuition(ChildRecord child, int day)
    {
        if (child.LastTuitionDay == day)
        {
            return false;
        }

        int min = Math.Max(0, this.mod.Config.TuitionMin);
        int max = Math.Max(min, this.mod.Config.TuitionMax);
        int amount = this.random.Next(min, max + 1);

        Farmer? payer = this.mod.FindFarmerById(child.ParentAId, includeOffline: true)
                        ?? this.mod.FindFarmerById(child.ParentBId, includeOffline: true)
                        ?? Game1.MasterPlayer;
        if (payer is null || payer.Money < amount)
        {
            this.mod.Monitor.Log($"[PR.System.Legacy] Tuition skipped for {child.ChildName}: insufficient funds.", StardewModdingAPI.LogLevel.Trace);
            return false;
        }

        payer.Money -= amount;
        child.LastTuitionDay = day;
        child.EducationScore += Math.Max(1, amount / 250);
        this.mod.Notifier.NotifyInfo($"Tuition paid for {child.ChildName}: {amount}g (education={child.EducationScore}).", "[PR.System.Legacy]");

        double giftChance = Math.Clamp(0.05d + child.EducationScore / 300d, 0.05d, 0.35d);
        if (this.random.NextDouble() <= giftChance)
        {
            this.DepositRewardToOutputChest("(O)535", 1);
            this.mod.Notifier.NotifyInfo($"{child.ChildName} brought home a small gift.", "[PR.System.Legacy]");
        }

        return true;
    }

    private bool TryRunTeenTask(ChildRecord child)
    {
        if (child.LegacyAssignment == LegacyChildAssignment.None)
        {
            child.LegacyAssignment = LegacyChildAssignment.Forager;
        }

        if (this.random.NextDouble() < Math.Clamp(this.mod.Config.TeenChoreForgetChance, 0f, 0.95f))
        {
            this.mod.Monitor.Log($"[PR.System.Legacy] Teen task missed by {child.ChildName}.", StardewModdingAPI.LogLevel.Trace);
            return true;
        }

        switch (child.LegacyAssignment)
        {
            case LegacyChildAssignment.Forager:
                this.DepositRewardToOutputChest("(O)296", 1);
                this.mod.Notifier.NotifyInfo($"{child.ChildName} foraged useful goods.", "[PR.System.Legacy]");
                return true;
            case LegacyChildAssignment.CrabPotAssistant:
                this.DepositRewardToOutputChest("(O)685", 2);
                this.mod.Notifier.NotifyInfo($"{child.ChildName} assisted crab pots.", "[PR.System.Legacy]");
                return true;
            default:
                return false;
        }
    }

    private bool TryRunAdultSpecialization(ChildRecord child)
    {
        if (child.LegacySpecialization == LegacyChildSpecialization.None)
        {
            child.LegacySpecialization = (LegacyChildSpecialization)(1 + this.random.Next(0, 3));
        }

        switch (child.LegacySpecialization)
        {
            case LegacyChildSpecialization.Rancher:
                this.DepositRewardToOutputChest("(O)176", 2);
                this.mod.Notifier.NotifyInfo($"{child.ChildName} handled ranch duties.", "[PR.System.Legacy]");
                return true;
            case LegacyChildSpecialization.Artisan:
                this.DepositRewardToOutputChest("(O)340", 1);
                this.mod.Notifier.NotifyInfo($"{child.ChildName} completed artisan work.", "[PR.System.Legacy]");
                return true;
            case LegacyChildSpecialization.Geologist:
                this.DepositRewardToOutputChest("(O)390", 1);
                this.mod.Notifier.NotifyInfo($"{child.ChildName} cleaned debris and brought minerals.", "[PR.System.Legacy]");
                return true;
            default:
                return false;
        }
    }

    private void DepositRewardToOutputChest(string qualifiedItemId, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GameLocation? farm = Game1.getFarm();
        if (farm is null)
        {
            return;
        }

        Chest? chest = farm.Objects.Values.OfType<Chest>().FirstOrDefault();
        if (chest is null)
        {
            return;
        }

        Item? item = ItemRegistry.Create(qualifiedItemId, amount);
        if (item is null)
        {
            return;
        }

        chest.addItem(item);
    }
}
