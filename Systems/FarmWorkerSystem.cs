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

        List<ChildRecord> workers = this.mod.HostSaveData.Children.Values
            .Where(c => c.Stage == ChildLifeStage.Adult && c.IsWorkerEnabled)
            .ToList();
        if (workers.Count == 0)
        {
            this.lastRunDay = day;
            return "No adult child workers available.";
        }

        Farm? farm = Game1.getFarm();
        if (farm is null)
        {
            return "Skipped: farm location unavailable.";
        }

        int watered = 0;
        int fed = 0;
        int collected = 0;
        int harvested = 0;
        int shipped = 0;

        if (this.mod.Config.EnableTaskWater)
        {
            foreach ((Vector2 tile, StardewValley.TerrainFeatures.TerrainFeature feature) in farm.terrainFeatures.Pairs)
            {
                if (feature is not HoeDirt dirt || dirt.crop is null)
                {
                    continue;
                }

                if (dirt.isWatered())
                {
                    continue;
                }

                dirt.state.Value = HoeDirt.watered;
                watered++;
            }
        }

        foreach (FarmAnimal animal in farm.Animals.Values)
        {
            if (this.mod.Config.EnableTaskFeed)
            {
                if (animal.fullness.Value < 200)
                {
                    animal.fullness.Value = 200;
                    fed++;
                }

                animal.wasPet.Value = true;
                animal.happiness.Value = Math.Min(255, animal.happiness.Value + 20);
            }

            if (!this.mod.Config.EnableTaskCollect)
            {
                continue;
            }

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

        if (this.mod.Config.EnableTaskHarvest)
        {
            foreach ((Vector2 tile, StardewValley.TerrainFeatures.TerrainFeature feature) in farm.terrainFeatures.Pairs)
            {
                if (feature is not HoeDirt dirt || dirt.crop is null)
                {
                    continue;
                }

                if (!dirt.readyForHarvest())
                {
                    continue;
                }

                bool harvestedThisTile = dirt.crop.harvest((int)tile.X, (int)tile.Y, dirt, null, false);
                if (harvestedThisTile)
                {
                    harvested++;
                }
            }
        }

        this.lastRunDay = day;
        return $"Worker run day {day}: workers={workers.Count}, watered={watered}, fed={fed}, collected={collected}, harvested={harvested}, shipped={shipped}.";
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
