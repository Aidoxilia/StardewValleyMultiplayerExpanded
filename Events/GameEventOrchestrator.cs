using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace PlayerRomance.Events;

public sealed class GameEventOrchestrator
{
    private readonly ModEntry mod;

    public GameEventOrchestrator(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Register()
    {
        IModEvents events = this.mod.Helper.Events;
        events.GameLoop.GameLaunched += this.OnGameLaunched;
        events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        events.GameLoop.Saving += this.OnSaving;
        events.GameLoop.DayStarted += this.OnDayStarted;
        events.GameLoop.DayEnding += this.OnDayEnding;
        events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked;
        events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
        events.Multiplayer.PeerConnected += this.OnPeerConnected;
        events.Multiplayer.PeerDisconnected += this.OnPeerDisconnected;
        events.Input.ButtonPressed += this.OnButtonPressed;
        events.Display.RenderedActiveMenu += this.OnRenderedActiveMenu;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        this.mod.Monitor.Log("[PR.Core] Game launched. Player Romance initialized.", LogLevel.Info);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        this.mod.OnSaveLoaded();
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        this.mod.OnSaving();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        this.mod.DateImmersionSystem.OnDayStartedHost();
        this.mod.PregnancySystem.OnDayStartedHost();
        this.mod.ChildGrowthSystem.OnDayStartedHost();
        this.mod.FarmWorkerSystem.OnDayStartedHost();
    }

    private void OnDayEnding(object? sender, DayEndingEventArgs e)
    {
        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        this.mod.DateImmersionSystem.OnDayEndingHost();
        this.mod.MarkDataDirty("Day ending flush.", flushNow: false);
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.mod.RequestPrompts.TryShowNextPrompt();
        this.mod.DateImmersionSystem.OnUpdateTickedLocal();

        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        this.mod.DateImmersionSystem.OnUpdateTickedHost();
        this.mod.HoldingHandsSystem.OnUpdateTickedHost();
        this.mod.CarrySystem.OnUpdateTickedHost();
    }

    private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
        {
            return;
        }

        this.mod.NetSync.ClientWatchdogTick();

        if (!this.mod.IsHostPlayer)
        {
            return;
        }

        this.mod.DateImmersionSystem.OnOneSecondUpdateTickedHost();
        this.mod.CarrySystem.OnOneSecondUpdateTickedHost();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        this.mod.ResetRuntimeState();
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        this.mod.MessageRouter.Handle(e);
    }

    private void OnPeerConnected(object? sender, PeerConnectedEventArgs e)
    {
        if (!this.mod.IsHostPlayer || !Context.IsWorldReady)
        {
            return;
        }

        this.mod.NetSync.SendSnapshotTo(e.Peer.PlayerID);
    }

    private void OnPeerDisconnected(object? sender, PeerDisconnectedEventArgs e)
    {
        if (this.mod.IsHostPlayer)
        {
            this.mod.DateImmersionSystem.OnPeerDisconnectedHost(e.Peer.PlayerID);
            this.mod.HoldingHandsSystem.OnPeerDisconnectedHost(e.Peer.PlayerID);
            this.mod.CarrySystem.OnPeerDisconnectedHost(e.Peer.PlayerID);
        }

        this.mod.Monitor.Log($"[PR.Net] Peer disconnected: {e.Peer.PlayerID}", LogLevel.Trace);
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree)
        {
            return;
        }

        if (this.mod.DateImmersionSystem.TryHandleLocalInteractionButton(e))
        {
            return;
        }

        if (e.Button == SButton.MouseLeft && Game1.activeClickableMenu is null)
        {
            Microsoft.Xna.Framework.Point absolutePoint = new(
                (int)e.Cursor.AbsolutePixels.X,
                (int)e.Cursor.AbsolutePixels.Y);
            Farmer? target = this.mod.TryGetOnlineFarmerAtCursor(e.Cursor.GrabTile, absolutePoint);
            if (target is not null)
            {
                Game1.activeClickableMenu = new UI.PlayerInteractionMenu(this.mod, target.UniqueMultiplayerID);
                return;
            }
        }

        if (e.Button == this.mod.GetRomanceHubHotkey())
        {
            this.mod.Monitor.Log("[PR.UI.RomanceHub] Opening romance hub menu.", LogLevel.Trace);
            Game1.activeClickableMenu = new UI.RomanceHubMenu(this.mod);
            return;
        }

        if (e.Button != SButton.F8)
        {
            return;
        }

        Game1.activeClickableMenu = new UI.RomanceMenu(this.mod);
    }

    private void OnRenderedActiveMenu(object? sender, RenderedActiveMenuEventArgs e)
    {
        this.mod.SocialOverlay.Draw(e.SpriteBatch);
    }
}
