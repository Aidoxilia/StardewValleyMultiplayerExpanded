using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Systems;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class RomanceHubMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private readonly ClickableTextureComponent closeButton;
    private readonly List<long> playerIds = new();
    private readonly List<ClickableComponent> playerRows = new();
    private readonly List<ActionButton> actions = new();

    private Rectangle playersPanel;
    private Rectangle statusPanel;
    private Rectangle actionsPanel;
    private Rectangle actionsViewport;
    private bool compact;

    private long selectedPlayerId = -1;
    private string hoverText = string.Empty;
    private int playerScroll;
    private int playerScrollMax;
    private int actionScroll;
    private int actionScrollMax;

    private sealed class ActionButton
    {
        public string Label = string.Empty;
        public ClickableComponent Bounds = new(new Rectangle(0, 0, 0, 0), string.Empty);
        public Func<(bool enabled, string disabledReason)> State = null!;
        public Action Execute = null!;
    }

    public RomanceHubMenu(ModEntry mod)
        : base((Game1.uiViewport.Width - WidthForViewport()) / 2, (Game1.uiViewport.Height - HeightForViewport()) / 2, WidthForViewport(), HeightForViewport(), false)
    {
        this.mod = mod;
        this.closeButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width - 54, this.yPositionOnScreen + 10, 40, 40), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 3.2f);
        this.BuildLayout();
        this.BuildActions();
        this.RefreshPlayers();
        if (!this.mod.IsHostPlayer)
        {
            this.mod.NetSync.RequestSnapshotFromHost();
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.RefreshPlayers();
        this.LayoutRowsAndButtons();
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = null;
            return;
        }

        for (int i = 0; i < this.playerRows.Count; i++)
        {
            Rectangle r = this.playerRows[i].bounds;
            if (r.Contains(x, y))
            {
                this.selectedPlayerId = this.playerIds[i];
                Game1.playSound("smallSelect");
                return;
            }
        }

        foreach (ActionButton action in this.actions)
        {
            if (!action.Bounds.containsPoint(x, y) || !this.IsButtonVisible(action.Bounds.bounds))
            {
                continue;
            }

            (bool enabled, string reason) = action.State();
            if (!enabled)
            {
                Game1.playSound("cancel");
                this.mod.Notifier.NotifyWarn(reason, "[PR.UI.RomanceHub]");
                return;
            }

            Game1.playSound("smallSelect");
            action.Execute();
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        this.LayoutRowsAndButtons();
        Point p = new(Game1.getMouseX(), Game1.getMouseY());
        if (this.actionsViewport.Contains(p) && this.actionScrollMax > 0)
        {
            this.actionScroll = Math.Clamp(this.actionScroll + (direction > 0 ? -1 : 1), 0, this.actionScrollMax);
            return;
        }

        if (this.playersPanel.Contains(p) && this.playerScrollMax > 0)
        {
            this.playerScroll = Math.Clamp(this.playerScroll + (direction > 0 ? -1 : 1), 0, this.playerScrollMax);
            return;
        }

        base.receiveScrollWheelAction(direction);
    }

    public override void performHoverAction(int x, int y)
    {
        this.LayoutRowsAndButtons();
        this.hoverText = string.Empty;
        foreach (ActionButton action in this.actions)
        {
            if (!action.Bounds.containsPoint(x, y) || !this.IsButtonVisible(action.Bounds.bounds))
            {
                continue;
            }

            (bool enabled, string reason) = action.State();
            this.hoverText = enabled ? action.Label : reason;
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        this.RefreshPlayers();
        this.LayoutRowsAndButtons();

        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);
        this.DrawHeader(b);
        this.DrawPlayersPanel(b);
        this.DrawStatusPanel(b);
        this.DrawActionsPanel(b);
        if (!string.IsNullOrWhiteSpace(this.hoverText))
        {
            IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
        }

        this.drawMouse(b);
    }

    private static int WidthForViewport()
    {
        int preferred = Math.Min(1240, (int)(Game1.uiViewport.Width * 0.92f));
        return Math.Min(preferred, Math.Max(520, Game1.uiViewport.Width - 10));
    }

    private static int HeightForViewport()
    {
        int preferred = Math.Min(860, (int)(Game1.uiViewport.Height * 0.9f));
        return Math.Min(preferred, Math.Max(440, Game1.uiViewport.Height - 10));
    }

    private void BuildLayout()
    {
        int pad = this.width < 860 ? 12 : 18;
        int gap = this.width < 860 ? 10 : 14;
        int headerH = 62;
        this.compact = this.width < 980 || this.height < 640;
        int contentTop = this.yPositionOnScreen + headerH;
        int contentHeight = this.height - headerH - pad;

        if (this.compact)
        {
            int w = this.width - pad * 2;
            int playersH = Math.Clamp((int)(contentHeight * 0.24f), 118, 200);
            int statusH = Math.Clamp((int)(contentHeight * 0.28f), 136, 232);
            this.playersPanel = new Rectangle(this.xPositionOnScreen + pad, contentTop, w, playersH);
            this.statusPanel = new Rectangle(this.xPositionOnScreen + pad, this.playersPanel.Bottom + gap, w, statusH);
            this.actionsPanel = new Rectangle(this.xPositionOnScreen + pad, this.statusPanel.Bottom + gap, w, this.yPositionOnScreen + this.height - pad - (this.statusPanel.Bottom + gap));
        }
        else
        {
            int playersW = Math.Clamp((int)(this.width * 0.28f), 250, 350);
            int rightX = this.xPositionOnScreen + pad + playersW + gap;
            int rightW = this.xPositionOnScreen + this.width - pad - rightX;
            int statusH = Math.Clamp((int)(contentHeight * 0.36f), 190, 270);
            this.playersPanel = new Rectangle(this.xPositionOnScreen + pad, contentTop, playersW, contentHeight);
            this.statusPanel = new Rectangle(rightX, contentTop, rightW, statusH);
            this.actionsPanel = new Rectangle(rightX, this.statusPanel.Bottom + gap, rightW, contentTop + contentHeight - (this.statusPanel.Bottom + gap));
        }
    }

    private void LayoutRowsAndButtons()
    {
        this.playerRows.Clear();
        int rowX = this.playersPanel.X + 12;
        int rowYStart = this.playersPanel.Y + 44;
        int rowH = this.compact ? 34 : 38;
        int rowGap = 7;
        int rowStep = rowH + rowGap;
        int rowW = this.playersPanel.Width - 24;
        int visibleRows = Math.Max(1, (Math.Max(1, this.playersPanel.Bottom - 12 - rowYStart) + rowGap) / rowStep);
        this.playerScrollMax = Math.Max(0, this.playerIds.Count - visibleRows);
        this.playerScroll = Math.Clamp(this.playerScroll, 0, this.playerScrollMax);
        for (int i = 0; i < this.playerIds.Count; i++)
        {
            this.playerRows.Add(new ClickableComponent(new Rectangle(rowX, rowYStart + (i - this.playerScroll) * rowStep, rowW, rowH), this.playerIds[i].ToString()));
        }

        int inPad = this.compact ? 12 : 14;
        int top = 44;
        this.actionsViewport = new Rectangle(this.actionsPanel.X + inPad, this.actionsPanel.Y + top, this.actionsPanel.Width - inPad * 2, Math.Max(56, this.actionsPanel.Height - top - 10));
        int cols = this.compact || this.actionsViewport.Width < 620 ? 1 : 2;
        int buttonH = this.compact ? 40 : 44;
        int rowStepButtons = buttonH + 9;
        int colGap = 12;
        int buttonW = cols == 1 ? this.actionsViewport.Width : (this.actionsViewport.Width - colGap) / 2;
        int totalRows = (int)Math.Ceiling(this.actions.Count / (float)cols);
        int visibleRowsButtons = Math.Max(1, (this.actionsViewport.Height + 9) / rowStepButtons);
        this.actionScrollMax = Math.Max(0, totalRows - visibleRowsButtons);
        this.actionScroll = Math.Clamp(this.actionScroll, 0, this.actionScrollMax);
        for (int i = 0; i < this.actions.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            this.actions[i].Bounds.bounds = new Rectangle(this.actionsViewport.X + col * (buttonW + colGap), this.actionsViewport.Y + row * rowStepButtons - this.actionScroll * rowStepButtons, buttonW, buttonH);
        }
    }

    private void DrawHeader(SpriteBatch b)
    {
        Rectangle strip = new(this.xPositionOnScreen + 14, this.yPositionOnScreen + 14, this.width - 80, 36);
        b.Draw(Game1.staminaRect, strip, new Color(248, 227, 181));
        Utility.drawTextWithShadow(b, "Romance Hub", Game1.dialogueFont, new Vector2(strip.X + 8, strip.Y + 2), Color.Black);
        string text = this.compact ? $"Hotkey {this.mod.GetRomanceHubHotkey()} | Left-click player" : $"Hotkey {this.mod.GetRomanceHubHotkey()} | Left-click players for quick actions";
        int sx = strip.X + 214;
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, text, Math.Max(60, strip.Right - sx - 8)), Game1.smallFont, new Vector2(sx, strip.Y + 8), Color.Black);
    }

    private void DrawPlayersPanel(SpriteBatch b)
    {
        this.DrawPanelBox(b, this.playersPanel, "Online Players");
        if (this.playerRows.Count == 0)
        {
            Utility.drawTextWithShadow(b, "No other online players detected.", Game1.smallFont, new Vector2(this.playersPanel.X + 14, this.playersPanel.Y + 50), Color.Black);
            return;
        }

        for (int i = 0; i < this.playerRows.Count; i++)
        {
            Rectangle r = this.playerRows[i].bounds;
            if (r.Y < this.playersPanel.Y + 42 || r.Bottom > this.playersPanel.Bottom - 10)
            {
                continue;
            }

            bool selected = this.playerIds[i] == this.selectedPlayerId;
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), r.X, r.Y, r.Width, r.Height, selected ? new Color(255, 236, 211) : new Color(252, 245, 226), 4f, false);
            Farmer? farmer = this.mod.FindFarmerById(this.playerIds[i], true);
            Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, farmer?.Name ?? this.playerIds[i].ToString(), r.Width - 12), Game1.smallFont, new Vector2(r.X + 8, r.Y + 9), Color.Black);
        }

        if (this.playerScrollMax > 0)
        {
            Utility.drawTextWithShadow(b, $"Scroll {this.playerScroll + 1}/{this.playerScrollMax + 1}", Game1.tinyFont, new Vector2(this.playersPanel.Right - 84, this.playersPanel.Y + 16), Color.Black);
        }
    }

    private void DrawStatusPanel(SpriteBatch b)
    {
        this.DrawPanelBox(b, this.statusPanel, "Couple Status");
        int x = this.statusPanel.X + 14;
        int width = this.statusPanel.Width - 28;
        int y = this.statusPanel.Y + 44;

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, "Select an online player on the left.", width), Game1.smallFont, new Vector2(x, y), Color.Black);
            y += 24;
            Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, "Hearts gain: completed dates, gifts, immersive talks.", width), Game1.smallFont, new Vector2(x, y), Color.Black);
            y += 24;
            Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, "Hearts loss: rejected request or early date end.", width), Game1.smallFont, new Vector2(x, y), Color.Black);
            return;
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        string targetName = target?.Name ?? targetId.ToString();
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, targetId);
        string relationState = relation?.State.ToString() ?? "None";
        int points = relation?.HeartPoints ?? 0;
        int level = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
        string cooldown = relation is null ? "-" : relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()) ? "Ready" : "Used today";
        string pairKey = ConsentSystem.GetPairKey(this.mod.LocalPlayerId, targetId);
        string lastEvent = this.mod.GetLastHeartEvent(pairKey);
        if (string.IsNullOrWhiteSpace(lastEvent))
        {
            lastEvent = "No recent heart change.";
        }

        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, $"Target: {targetName}", width), Game1.smallFont, new Vector2(x, y), Color.Black);
        y += 22;
        Utility.drawTextWithShadow(b, $"Relationship: {relationState}", Game1.smallFont, new Vector2(x, y), Color.Black);
        y += 22;
        Utility.drawTextWithShadow(b, $"Hearts: {level}/{this.mod.Config.MaxHearts} ({points} pts)", Game1.smallFont, new Vector2(x, y), Color.Black);
        y += 22;
        Utility.drawTextWithShadow(b, $"Date cooldown: {cooldown}", Game1.smallFont, new Vector2(x, y), Color.Black);
        y += 22;
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, $"Session: {this.GetActiveSessionText(targetId)}", width), Game1.smallFont, new Vector2(x, y), Color.Black);

        int barY = Math.Min(this.statusPanel.Bottom - 52, y + 8);
        IClickableMenu.drawTextureBox(b, x, barY, width, 20, Color.White);
        float ratio = Math.Clamp(points / (float)Math.Max(1, this.mod.Config.MaxHearts * this.mod.Config.HeartPointsPerHeart), 0f, 1f);
        b.Draw(Game1.staminaRect, new Rectangle(x + 3, barY + 3, (int)((width - 6) * ratio), 14), new Color(214, 60, 74));

        Utility.drawTextWithShadow(
            b,
            this.FitText(Game1.smallFont, $"Last heart event: {lastEvent}", width),
            Game1.smallFont,
            new Vector2(x, this.statusPanel.Bottom - 28),
            Color.Black);
    }

    private void DrawActionsPanel(SpriteBatch b)
    {
        this.DrawPanelBox(b, this.actionsPanel, "Actions");
        foreach (ActionButton action in this.actions)
        {
            Rectangle r = action.Bounds.bounds;
            if (!this.IsButtonVisible(r))
            {
                continue;
            }

            (bool enabled, _) = action.State();
            bool hover = action.Bounds.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            Color fill = enabled ? new Color(248, 239, 220) : new Color(215, 206, 193);
            if (enabled && hover)
            {
                fill = new Color(255, 247, 229);
            }

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), r.X, r.Y, r.Width, r.Height, fill, 4f, false);
            string label = this.FitText(Game1.smallFont, action.Label, r.Width - 16);
            int textY = r.Y + (r.Height - (int)Game1.smallFont.MeasureString(label).Y) / 2;
            Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(r.X + 8, textY), Color.Black);
        }

        if (this.actionScrollMax > 0)
        {
            Utility.drawTextWithShadow(b, $"Mouse wheel: {this.actionScroll + 1}/{this.actionScrollMax + 1}", Game1.tinyFont, new Vector2(this.actionsPanel.Right - 124, this.actionsPanel.Y + 18), Color.Black);
        }
    }

    private void DrawPanelBox(SpriteBatch b, Rectangle panel, string title)
    {
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), panel.X, panel.Y, panel.Width, panel.Height, Color.White, 4f, false);
        Rectangle strip = new(panel.X + 8, panel.Y + 8, panel.Width - 16, 28);
        b.Draw(Game1.staminaRect, strip, new Color(245, 224, 173));
        Utility.drawTextWithShadow(b, title, Game1.smallFont, new Vector2(panel.X + 12, panel.Y + 12), Color.Black);
    }

    private void BuildActions()
    {
        this.actions.Clear();
        void Add(string label, Func<(bool enabled, string disabledReason)> state, Action execute)
        {
            this.actions.Add(new ActionButton { Label = label, State = state, Execute = execute });
        }

        Add("Dating Proposal", this.GetDatingState, () => this.RunResult(this.mod.DatingSystem.RequestDatingFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        Add("Start Date (Town)", () => this.GetImmersiveDateState(ImmersiveDateLocation.Town), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Town, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("Marriage Proposal", this.GetMarriageState, () => this.RunResult(this.mod.MarriageSystem.RequestMarriageFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Marriage]"));
        Add("Try For Baby", this.GetPregnancyState, () => this.RunResult(this.mod.PregnancySystem.RequestTryForBabyFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Pregnancy]"));
        Add("Start Carry", this.GetCarryState, () => this.RunResult(this.mod.CarrySystem.RequestCarryFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        Add("Stop Carry", this.GetCarryStopState, () => this.RunResult(this.mod.CarrySystem.StopCarryFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        Add("Start Holding Hands", this.GetHandsState, () => this.RunResult(this.mod.HoldingHandsSystem.RequestHoldingHandsFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        Add("Stop Holding Hands", this.GetHandsStopState, () => this.RunResult(this.mod.HoldingHandsSystem.StopHoldingHandsFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        Add("Immersive Date: Beach", () => this.GetImmersiveDateState(ImmersiveDateLocation.Beach), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Beach, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("Immersive Date: Forest", () => this.GetImmersiveDateState(ImmersiveDateLocation.Forest), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Forest, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("End Immersive Date", this.GetEndImmersiveState, () => this.RunResult(this.mod.DateImmersionSystem.EndImmersiveDateFromLocal(out string msg), msg, "[PR.System.DateImmersion]"));
    }

    private void RefreshPlayers()
    {
        this.playerIds.Clear();
        HashSet<long> unique = new();
        foreach (Farmer farmer in Game1.getOnlineFarmers().Where(p => p.UniqueMultiplayerID != this.mod.LocalPlayerId))
        {
            if (unique.Add(farmer.UniqueMultiplayerID))
            {
                this.playerIds.Add(farmer.UniqueMultiplayerID);
            }
        }

        foreach (var peer in this.mod.Helper.Multiplayer.GetConnectedPlayers())
        {
            if (peer.PlayerID != this.mod.LocalPlayerId && unique.Add(peer.PlayerID))
            {
                this.playerIds.Add(peer.PlayerID);
            }
        }

        if (this.selectedPlayerId <= 0 || !this.playerIds.Contains(this.selectedPlayerId))
        {
            this.selectedPlayerId = this.playerIds.FirstOrDefault();
        }
    }

    private bool TryGetSelectedTarget(out Farmer? target)
    {
        target = null;
        if (this.selectedPlayerId <= 0)
        {
            return false;
        }

        if (!this.mod.IsPlayerOnline(this.selectedPlayerId))
        {
            return false;
        }

        target = this.mod.FindFarmerById(this.selectedPlayerId, true);
        return true;
    }

    private string GetActiveSessionText(long targetPlayerId)
    {
        if (this.mod.DateImmersionSystem.GetActivePublicState() is { IsActive: true } immersive
            && (immersive.PlayerAId == targetPlayerId || immersive.PlayerBId == targetPlayerId)
            && (immersive.PlayerAId == this.mod.LocalPlayerId || immersive.PlayerBId == this.mod.LocalPlayerId))
        {
            return $"Immersive date ({immersive.Location})";
        }

        if (this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, targetPlayerId))
        {
            return "Holding hands";
        }

        return this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, targetPlayerId) ? "Carry" : "None";
    }

    private (bool enabled, string disabledReason) GetDatingState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, targetId);
        return relation is not null && relation.State != RelationshipState.None ? (false, $"Already {relation.State}.") : (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetMarriageState()
    {
        if (!this.mod.Config.EnableMarriage)
        {
            return (false, "Marriage disabled.");
        }

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, targetId);
        if (relation is null)
        {
            return (false, "Requires Dating first.");
        }

        if (relation.State == RelationshipState.Married)
        {
            return (false, "Already married.");
        }

        return relation.State == RelationshipState.Dating ? (true, string.Empty) : (false, "Requires Dating state.");
    }

    private (bool enabled, string disabledReason) GetPregnancyState()
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            return (false, "Pregnancy disabled.");
        }

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        return this.mod.MarriageSystem.IsMarried(this.mod.LocalPlayerId, targetId) ? (true, string.Empty) : (false, "Requires Married state.");
    }

    private (bool enabled, string disabledReason) GetCarryState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        return this.mod.CarrySystem.CanRequestCarry(this.mod.LocalPlayerId, targetId, out string reason) ? (true, string.Empty) : (false, reason);
    }

    private (bool enabled, string disabledReason) GetCarryStopState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        return this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, targetId) ? (true, string.Empty) : (false, "No carry session with this player.");
    }

    private (bool enabled, string disabledReason) GetHandsState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        return this.mod.HoldingHandsSystem.CanRequestHands(this.mod.LocalPlayerId, targetId, out string reason) ? (true, string.Empty) : (false, reason);
    }

    private (bool enabled, string disabledReason) GetHandsStopState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        return this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, targetId) ? (true, string.Empty) : (false, "No holding hands session with this player.");
    }

    private (bool enabled, string disabledReason) GetImmersiveDateState(ImmersiveDateLocation location)
    {
        if (!this.mod.Config.EnableImmersiveDates)
        {
            return (false, "Immersive dates disabled.");
        }

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        long targetId = target?.UniqueMultiplayerID ?? this.selectedPlayerId;
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, targetId);
        if (relation is null || relation.State == RelationshipState.None)
        {
            return (false, "Requires Dating/Engaged/Married.");
        }

        if (!relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()))
        {
            return (false, "Already used today for this couple.");
        }

        int requiredHearts = this.mod.DateImmersionSystem.GetRequiredHeartsForLocation(location);
        if (!this.mod.HeartsSystem.IsAtLeastHearts(this.mod.LocalPlayerId, targetId, requiredHearts))
        {
            return (false, $"Requires {requiredHearts}+ hearts for {location}.");
        }

        return this.mod.DateImmersionSystem.IsActive ? (false, "Another immersive date is already active.") : (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetEndImmersiveState()
    {
        DateImmersionPublicState? state = this.mod.DateImmersionSystem.GetActivePublicState();
        if (state is null || !state.IsActive)
        {
            return (false, "No active immersive date.");
        }

        return state.PlayerAId == this.mod.LocalPlayerId || state.PlayerBId == this.mod.LocalPlayerId ? (true, string.Empty) : (false, "Only participants can end the date.");
    }

    private bool IsButtonVisible(Rectangle r)
    {
        return r.Bottom > this.actionsViewport.Y && r.Y < this.actionsViewport.Bottom;
    }

    private void RunResult(bool success, string message, string category)
    {
        if (success)
        {
            this.mod.Notifier.NotifyInfo(message, category);
        }
        else
        {
            this.mod.Notifier.NotifyWarn(message, category);
        }
    }

    private string FitText(SpriteFont font, string text, int maxWidth)
    {
        if (font.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "...";
        string value = text;
        while (value.Length > 0 && font.MeasureString(value + ellipsis).X > maxWidth)
        {
            value = value[..^1];
        }

        return value.Length == 0 ? ellipsis : value + ellipsis;
    }
}
