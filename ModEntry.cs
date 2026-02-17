using HarmonyLib;
using PlayerRomance.Data;
using PlayerRomance.Events;
using PlayerRomance.Net;
using PlayerRomance.Systems;
using PlayerRomance.UI;
using StardewModdingAPI;
using StardewValley;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;

namespace PlayerRomance;

public sealed class ModEntry : Mod
{
    private const string SaveDataKey = "player-romance-save";
    private bool dataDirty;

    internal ModConfig Config { get; private set; } = new();
    internal RomanceSaveData HostSaveData { get; set; } = new();
    internal NetSnapshot ClientSnapshot { get; set; } = new();
    internal string LastFarmWorkReport { get; set; } = string.Empty;
    internal Dictionary<string, string> LastHeartEventsByPair { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal HudNotifier Notifier { get; private set; } = null!;
    internal SocialVanillaSystem SocialVanillaSystem { get; private set; } = null!;
    internal RequestPromptService RequestPrompts { get; private set; } = null!;

    internal NetSyncService NetSync { get; private set; } = null!;
    internal HostHandlers HostHandlers { get; private set; } = null!;
    internal ClientHandlers ClientHandlers { get; private set; } = null!;
    internal MessageRouter MessageRouter { get; private set; } = null!;

    internal DatingSystem DatingSystem { get; private set; } = null!;
    internal MarriageSystem MarriageSystem { get; private set; } = null!;
    internal PregnancySystem PregnancySystem { get; private set; } = null!;
    internal ChildGrowthSystem ChildGrowthSystem { get; private set; } = null!;
    internal FarmWorkerSystem FarmWorkerSystem { get; private set; } = null!;
    internal HeartsSystem HeartsSystem { get; private set; } = null!;
    internal CarrySystem CarrySystem { get; private set; } = null!;
    internal HoldingHandsSystem HoldingHandsSystem { get; private set; } = null!;
    internal CoupleSynergySystem CoupleSynergySystem { get; private set; } = null!;
    internal DateImmersionSystem DateImmersionSystem { get; private set; } = null!;

    internal DateEventController DateEventController { get; private set; } = null!;
    internal WeddingEventController WeddingEventController { get; private set; } = null!;
    internal CommandRegistrar CommandRegistrar { get; private set; } = null!;
    internal GameEventOrchestrator GameEvents { get; private set; } = null!;

    internal Harmony? Harmony { get; private set; }

    internal bool IsHostPlayer => Context.IsWorldReady && Context.IsMainPlayer;
    internal long LocalPlayerId => Context.IsWorldReady ? Game1.player.UniqueMultiplayerID : 0;
    internal string LocalPlayerName => Context.IsWorldReady ? Game1.player.Name : "Unknown";

    public override void Entry(IModHelper helper)
    {
        this.Config = helper.ReadConfig<ModConfig>();
        helper.WriteConfig(this.Config);

        this.Harmony = new Harmony(this.ModManifest.UniqueID);

        this.Notifier = new HudNotifier(this);
        this.SocialVanillaSystem = new SocialVanillaSystem(this);
        this.RequestPrompts = new RequestPromptService(this);

        this.NetSync = new NetSyncService(this);
        this.HostHandlers = new HostHandlers(this);
        this.ClientHandlers = new ClientHandlers(this);
        this.MessageRouter = new MessageRouter(this, this.HostHandlers, this.ClientHandlers);

        this.DatingSystem = new DatingSystem(this);
        this.MarriageSystem = new MarriageSystem(this);
        this.PregnancySystem = new PregnancySystem(this);
        this.ChildGrowthSystem = new ChildGrowthSystem(this);
        this.FarmWorkerSystem = new FarmWorkerSystem(this);
        this.HeartsSystem = new HeartsSystem(this);
        this.CarrySystem = new CarrySystem(this);
        this.HoldingHandsSystem = new HoldingHandsSystem(this);
        this.CoupleSynergySystem = new CoupleSynergySystem(this);
        this.DateImmersionSystem = new DateImmersionSystem(this);

        this.DateEventController = new DateEventController(this);
        this.WeddingEventController = new WeddingEventController(this);
        this.CommandRegistrar = new CommandRegistrar(this);
        this.GameEvents = new GameEventOrchestrator(this);

        this.CommandRegistrar.Register();
        this.GameEvents.Register();

        this.Monitor.Log("[PR.Core] Entry complete.", LogLevel.Info);
    }

    internal void OnSaveLoaded()
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.CarrySystem.Reset();
        this.HoldingHandsSystem.Reset();
        this.CoupleSynergySystem.Reset();
        this.DateImmersionSystem.Reset();
        this.ChildGrowthSystem.Reset();
        this.RequestPrompts.Clear();
        this.LastHeartEventsByPair.Clear();

        if (this.IsHostPlayer)
        {
            this.HostSaveData = this.Helper.Data.ReadSaveData<RomanceSaveData>(SaveDataKey) ?? new RomanceSaveData();
            this.DateImmersionSystem.OnHostSaveLoadedRecovery();
            this.dataDirty = false;
            this.Monitor.Log(
                $"[PR.Data] Host save loaded with {this.HostSaveData.Relationships.Count} relationships, {this.HostSaveData.Children.Count} children.",
                LogLevel.Info);
            this.ChildGrowthSystem.RebuildAdultChildrenForActiveState();
            this.NetSync.BroadcastSnapshotToAll();
        }
        else
        {
            this.ClientSnapshot = new NetSnapshot();
            this.NetSync.RequestSnapshotFromHost();
            this.Monitor.Log("[PR.Net] Snapshot requested from host.", LogLevel.Trace);
        }
    }

    internal void OnSaving()
    {
        if (!this.IsHostPlayer || !this.dataDirty)
        {
            return;
        }

        this.Helper.Data.WriteSaveData(SaveDataKey, this.HostSaveData);
        this.dataDirty = false;
        this.Monitor.Log("[PR.Data] Host save data persisted on Saving event.", LogLevel.Trace);
    }

    internal void MarkDataDirty(string reason, bool flushNow)
    {
        if (!this.IsHostPlayer)
        {
            return;
        }

        this.dataDirty = true;
        this.Monitor.Log($"[PR.Data] {reason}", LogLevel.Trace);

        if (!flushNow)
        {
            return;
        }

        this.Helper.Data.WriteSaveData(SaveDataKey, this.HostSaveData);
        this.dataDirty = false;
        this.Monitor.Log("[PR.Data] Host save data persisted immediately.", LogLevel.Trace);
    }

    internal void ResetRuntimeState()
    {
        this.HostSaveData = new RomanceSaveData();
        this.ClientSnapshot = new NetSnapshot();
        this.LastFarmWorkReport = string.Empty;
        this.dataDirty = false;
        this.CarrySystem.Reset();
        this.HoldingHandsSystem.Reset();
        this.CoupleSynergySystem.Reset();
        this.DateImmersionSystem.Reset();
        this.ChildGrowthSystem.Reset();
        this.RequestPrompts.Clear();
        this.LastHeartEventsByPair.Clear();
        this.Monitor.Log("[PR.Core] Runtime state cleared on ReturnedToTitle.", LogLevel.Trace);
    }

    internal SButton GetRomanceHubHotkey()
    {
        if (Enum.TryParse(this.Config.RomanceHubHotkey, ignoreCase: true, out SButton parsed))
        {
            return parsed;
        }

        return SButton.F7;
    }

    internal SButton GetChildrenManagementHotkey()
    {
        if (Enum.TryParse(this.Config.ChildrenManagementHotkey, ignoreCase: true, out SButton parsed))
        {
            return parsed;
        }

        return SButton.F8;
    }

    internal Farmer? FindFarmerById(long playerId, bool includeOffline)
    {
        if (!Context.IsWorldReady)
        {
            return null;
        }

        Farmer? online = Game1.getOnlineFarmers().FirstOrDefault(p => p.UniqueMultiplayerID == playerId);
        if (online is not null || !includeOffline)
        {
            return online;
        }

        return Game1.GetPlayer(playerId, true);
    }

    internal bool TryResolvePlayerToken(string token, [NotNullWhen(true)] out Farmer? farmer)
    {
        farmer = null;
        if (!Context.IsWorldReady || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        string clean = token.Trim();
        if (long.TryParse(clean, out long playerId))
        {
            farmer = this.FindFarmerById(playerId, includeOffline: true);
            return farmer is not null;
        }

        IEnumerable<Farmer> all = Game1.getAllFarmers();
        farmer = all.FirstOrDefault(p => p.Name.Equals(clean, StringComparison.OrdinalIgnoreCase))
                 ?? all.FirstOrDefault(p => p.Name.Contains(clean, StringComparison.OrdinalIgnoreCase));
        return farmer is not null;
    }

    internal bool IsPlayerOnline(long playerId)
    {
        if (!Context.IsWorldReady)
        {
            return false;
        }

        if (Game1.player.UniqueMultiplayerID == playerId)
        {
            return true;
        }

        if (Game1.getOnlineFarmers().Any(p => p.UniqueMultiplayerID == playerId))
        {
            return true;
        }

        return this.Helper.Multiplayer.GetConnectedPlayers().Any(p => p.PlayerID == playerId);
    }

    internal int GetCurrentDayNumber()
    {
        return Context.IsWorldReady ? (int)Game1.stats.DaysPlayed : 0;
    }

    internal Farmer? TryGetOnlineFarmerAtCursor(Vector2 cursorTile, Point absolutePixel)
    {
        if (!Context.IsWorldReady)
        {
            return null;
        }

        Farmer? best = null;
        float bestDistance = float.MaxValue;
        foreach (Farmer farmer in Game1.getOnlineFarmers())
        {
            if (farmer.UniqueMultiplayerID == this.LocalPlayerId || farmer.currentLocation != Game1.currentLocation)
            {
                continue;
            }

            Rectangle box = farmer.GetBoundingBox();
            box.Inflate(20, 20);
            bool hitBox = box.Contains(absolutePixel);
            float tileDistance = Vector2.Distance(farmer.Tile, cursorTile);
            bool hitTile = tileDistance <= 1.35f;
            if (!hitBox && !hitTile)
            {
                continue;
            }

            if (tileDistance < bestDistance)
            {
                bestDistance = tileDistance;
                best = farmer;
            }
        }

        return best;
    }

    internal void RecordHeartEvent(string pairKey, string message)
    {
        if (string.IsNullOrWhiteSpace(pairKey) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        this.LastHeartEventsByPair[pairKey] = message;
    }

    internal string GetLastHeartEvent(string pairKey)
    {
        return this.LastHeartEventsByPair.TryGetValue(pairKey, out string? message)
            ? message
            : string.Empty;
    }
}
