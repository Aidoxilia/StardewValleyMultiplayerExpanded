using Microsoft.Xna.Framework;
using PlayerRomance.Data;
using PlayerRomance.Net;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace PlayerRomance.Systems;

public sealed class FarmWorkerSystem
{
    private readonly ModEntry mod;
    private int lastRunDay = -1;

    public FarmWorkerSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void OnDayStartedHost()
    {
        if (!this.mod.IsHostPlayer || !this.mod.Config.EnableFarmWorker || !this.mod.Config.AllowAdultChildWork)
        {
            return;
        }

        string report = this.RunWorkerPass(force: false);
        if (report.StartsWith("Skipped", StringComparison.Ordinal))
        {
            return;
        }

        this.PublishReport(report);
    }

    public string RunWorkerPass(bool force)
    {
        if (!this.mod.IsHostPlayer)
        {
            return "Skipped: only host can run farm worker simulation.";
        }

        if (!this.mod.Config.EnableFarmWorker || !this.mod.Config.AllowAdultChildWork)
        {
            return "Skipped: farm worker system disabled by config.";
        }

        int day = this.mod.GetCurrentDayNumber();
        if (!force && this.lastRunDay == day)
        {
            return "Skipped: farm worker already processed for current day.";
        }

        Farm? farm = Game1.getFarm();
        if (farm is null)
        {
            return "Skipped: farm location unavailable.";
        }

        List<ChildRecord> workers = this.mod.HostSaveData.Children.Values
            .Where(c =>
                c.Stage == ChildLifeStage.Adult
                && c.AgeYears >= Math.Max(0, this.mod.Config.AdultWorkMinAge)
                && c.IsWorkerEnabled)
            .ToList();
        if (workers.Count == 0)
        {
            this.lastRunDay = day;
            return "No adult child workers available.";
        }

        List<string> childReports = new();
        int totalWatered = 0;
        int totalFed = 0;
        int totalCollected = 0;
        int totalHarvested = 0;
        int totalShipped = 0;
        int totalFish = 0;

        foreach (ChildRecord worker in workers)
        {
            if (!force && worker.LastWorkedDay == day)
            {
                continue;
            }

            (int watered, int fed, int collected, int harvested, int shipped, int fishCaught) = this.ExecuteChildTasks(worker, farm);
            worker.LastWorkedDay = day;
            totalWatered += watered;
            totalFed += fed;
            totalCollected += collected;
            totalHarvested += harvested;
            totalShipped += shipped;
            totalFish += fishCaught;

            string childReport = $"[{worker.ChildName}] task={(worker.AutoMode ? "auto" : worker.AssignedTask.ToString())}, watered={watered}, fed={fed}, collected={collected}, harvested={harvested}, shipped={shipped}, fish={fishCaught}.";
            childReports.Add(childReport);
            this.PublishChildReport(worker, childReport);
        }

        this.lastRunDay = day;
        this.mod.MarkDataDirty("Child worker pass executed.", flushNow: true);
        this.mod.NetSync.BroadcastSnapshotToAll();
        return $"Worker run day {day}: workers={workers.Count}, watered={totalWatered}, fed={totalFed}, collected={totalCollected}, harvested={totalHarvested}, shipped={totalShipped}, fish={totalFish}.";
    }

    public void PublishReport(string report)
    {
        this.mod.LastFarmWorkReport = report;
        this.mod.Monitor.Log($"[PR.System.Worker] {report}", StardewModdingAPI.LogLevel.Info);
        this.mod.NetSync.Broadcast(
            MessageType.FarmWorkReport,
            new FarmWorkReportMessage
            {
                DayNumber = this.mod.GetCurrentDayNumber(),
                Report = report
            });
        this.mod.NetSync.BroadcastSnapshotToAll();
    }

    private (int watered, int fed, int collected, int harvested, int shipped, int fishCaught) ExecuteChildTasks(ChildRecord worker, Farm farm)
    {
        int watered = 0;
        int fed = 0;
        int collected = 0;
        int harvested = 0;
        int shipped = 0;
        int fishCaught = 0;
        foreach (ChildTaskType task in this.GetTasksToRun(worker))
        {
            switch (task)
            {
                case ChildTaskType.Water:
                    watered += this.RunWaterTask(farm, maxTiles: 18);
                    break;
                case ChildTaskType.FeedAnimals:
                    fed += this.RunFeedAnimalsTask(farm, maxAnimals: 8);
                    break;
                case ChildTaskType.Collect:
                    (int collectedNow, int shippedNow) = this.RunCollectTask(farm, maxAnimals: 6);
                    collected += collectedNow;
                    shipped += shippedNow;
                    break;
                case ChildTaskType.Harvest:
                    harvested += this.RunHarvestTask(farm, maxTiles: 16);
                    break;
                case ChildTaskType.Ship:
                    shipped += this.RunShipTask(farm, maxItems: 4);
                    break;
                case ChildTaskType.Fish:
                    int caughtNow = this.RunFishTask(farm, worker);
                    fishCaught += caughtNow;
                    if (this.mod.Config.EnableTaskShip)
                    {
                        shipped += caughtNow;
                    }
                    break;
            }
        }

        return (watered, fed, collected, harvested, shipped, fishCaught);
    }

    private IEnumerable<ChildTaskType> GetTasksToRun(ChildRecord worker)
    {
        if (!worker.IsWorkerEnabled || worker.AssignedTask == ChildTaskType.Stop)
        {
            yield break;
        }

        if (worker.AutoMode || worker.AssignedTask == ChildTaskType.Auto)
        {
            if (this.mod.Config.EnableTaskWater)
            {
                yield return ChildTaskType.Water;
            }

            if (this.mod.Config.EnableTaskFeed)
            {
                yield return ChildTaskType.FeedAnimals;
            }

            if (this.mod.Config.EnableTaskCollect)
            {
                yield return ChildTaskType.Collect;
            }

            if (this.mod.Config.EnableTaskHarvest)
            {
                yield return ChildTaskType.Harvest;
            }

            if (this.mod.Config.EnableTaskShip)
            {
                yield return ChildTaskType.Ship;
            }

            if (this.mod.Config.EnableChildFishingTask)
            {
                yield return ChildTaskType.Fish;
            }

            yield break;
        }

        yield return worker.AssignedTask;
    }

    private int RunWaterTask(Farm farm, int maxTiles)
    {
        int watered = 0;
        foreach ((Vector2 _, TerrainFeature feature) in farm.terrainFeatures.Pairs)
        {
            if (watered >= maxTiles)
            {
                break;
            }

            if (feature is not HoeDirt dirt || dirt.crop is null || dirt.isWatered())
            {
                continue;
            }

            dirt.state.Value = HoeDirt.watered;
            watered++;
        }

        return watered;
    }

    private int RunFeedAnimalsTask(Farm farm, int maxAnimals)
    {
        int fed = 0;
        foreach (FarmAnimal animal in farm.Animals.Values)
        {
            if (fed >= maxAnimals)
            {
                break;
            }

            if (animal.fullness.Value < 200)
            {
                animal.fullness.Value = 200;
                fed++;
            }

            animal.wasPet.Value = true;
            animal.happiness.Value = Math.Min(255, animal.happiness.Value + 16);
        }

        return fed;
    }

    private (int collected, int shipped) RunCollectTask(Farm farm, int maxAnimals)
    {
        int collected = 0;
        int shipped = 0;
        int scanned = 0;
        foreach (FarmAnimal animal in farm.Animals.Values)
        {
            if (scanned >= maxAnimals)
            {
                break;
            }

            scanned++;
            string produceId = animal.currentProduce.Value;
            if (string.IsNullOrWhiteSpace(produceId) || produceId == "-1")
            {
                continue;
            }

            Item? produce = this.CreateSafeItem(produceId);
            if (produce is null)
            {
                continue;
            }

            if (this.mod.Config.EnableTaskShip)
            {
                farm.shipItem(produce, Game1.player);
                shipped += produce.Stack;
            }
            else
            {
                Game1.player.addItemToInventoryBool(produce);
            }

            animal.currentProduce.Value = "-1";
            animal.daysSinceLastLay.Value = 0;
            collected++;
        }

        return (collected, shipped);
    }

    private int RunHarvestTask(Farm farm, int maxTiles)
    {
        int harvested = 0;
        foreach ((Vector2 tile, TerrainFeature feature) in farm.terrainFeatures.Pairs)
        {
            if (harvested >= maxTiles)
            {
                break;
            }

            if (feature is not HoeDirt dirt || dirt.crop is null || !dirt.readyForHarvest())
            {
                continue;
            }

            if (dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, null, false))
            {
                harvested++;
            }
        }

        return harvested;
    }

    private int RunShipTask(Farm farm, int maxItems)
    {
        int shipped = 0;
        for (int i = 0; i < Game1.player.Items.Count && shipped < maxItems; i++)
        {
            Item? item = Game1.player.Items[i];
            if (item is null || item.Stack <= 0)
            {
                continue;
            }

            Item shippedItem = item.getOne();
            if (shippedItem is null)
            {
                continue;
            }

            farm.shipItem(shippedItem, Game1.player);
            item.Stack--;
            if (item.Stack <= 0)
            {
                Game1.player.Items[i] = null;
            }

            shipped++;
        }

        return shipped;
    }

    private int RunFishTask(Farm farm, ChildRecord worker)
    {
        if (!this.mod.Config.EnableChildFishingTask)
        {
            return 0;
        }

        string[] fishIds = { "(O)128", "(O)129", "(O)130", "(O)131", "(O)132", "(O)136" };
        int seed = Math.Abs(HashCode.Combine(worker.ChildId, this.mod.GetCurrentDayNumber(), worker.AgeYears));
        string chosen = fishIds[seed % fishIds.Length];
        Item? fish = this.CreateSafeItem(chosen);
        if (fish is null)
        {
            return 0;
        }

        if (this.mod.Config.EnableTaskShip)
        {
            farm.shipItem(fish, Game1.player);
        }
        else
        {
            Game1.player.addItemToInventoryBool(fish);
        }

        return 1;
    }

    private void PublishChildReport(ChildRecord child, string report)
    {
        this.mod.NetSync.Broadcast(
            MessageType.FarmWorkReport,
            new FarmWorkReportMessage
            {
                DayNumber = this.mod.GetCurrentDayNumber(),
                Report = report
            },
            child.ParentAId,
            child.ParentBId);
        this.mod.Monitor.Log($"[PR.System.Worker] {report}", StardewModdingAPI.LogLevel.Trace);
    }

    private Item? CreateSafeItem(string produceId)
    {
        Item? item = ItemRegistry.Create(produceId, 1, 0, allowNull: true);
        if (item is not null)
        {
            return item;
        }

        if (!produceId.StartsWith("(O)", StringComparison.Ordinal))
        {
            item = ItemRegistry.Create($"(O){produceId}", 1, 0, allowNull: true);
        }

        return item;
    }
}
