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
    private readonly List<ClickableComponent> playerRows = new();
    private readonly List<ActionButton> actions = new();
    private readonly List<long> playerIds = new();
    private long selectedPlayerId = -1;
    private string hoverText = string.Empty;

    private Rectangle playersPanel;
    private Rectangle statusPanel;
    private Rectangle actionsPanel;

    private sealed class ActionButton
    {
        public string Label { get; set; } = string.Empty;
        public ClickableComponent Bounds { get; set; } = null!;
        public Func<(bool enabled, string disabledReason)> GetState { get; set; } = null!;
        public Action Execute { get; set; } = null!;
    }

    public RomanceHubMenu(ModEntry mod)
        : base(
            (Game1.uiViewport.Width - Math.Min(1140, Game1.uiViewport.Width - 64)) / 2,
            (Game1.uiViewport.Height - Math.Min(720, Game1.uiViewport.Height - 64)) / 2,
            Math.Min(1140, Game1.uiViewport.Width - 64),
            Math.Min(720, Game1.uiViewport.Height - 64),
            showUpperRightCloseButton: false)
    {
        this.mod = mod;
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 54, this.yPositionOnScreen + 10, 40, 40),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.2f);

        this.BuildLayout();
        this.RefreshPlayerList();
        if (!this.mod.IsHostPlayer)
        {
            this.mod.NetSync.RequestSnapshotFromHost();
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = null;
            return;
        }

        for (int i = 0; i < this.playerRows.Count; i++)
        {
            if (!this.playerRows[i].containsPoint(x, y))
            {
                continue;
            }

            this.selectedPlayerId = this.playerIds[i];
            Game1.playSound("smallSelect");
            return;
        }

        foreach (ActionButton action in this.actions)
        {
            if (!action.Bounds.containsPoint(x, y))
            {
                continue;
            }

            (bool enabled, string reason) = action.GetState();
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

    public override void performHoverAction(int x, int y)
    {
        this.hoverText = string.Empty;
        foreach (ActionButton action in this.actions)
        {
            if (!action.Bounds.containsPoint(x, y))
            {
                continue;
            }

            (bool enabled, string reason) = action.GetState();
            this.hoverText = enabled ? action.Label : reason;
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        this.RefreshPlayerList();

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

    private void BuildLayout()
    {
        const int pad = 18;
        const int gap = 14;
        const int headerHeight = 58;
        const int playersWidth = 290;
        const int statusHeight = 204;

        int contentTop = this.yPositionOnScreen + headerHeight;
        int contentHeight = this.height - headerHeight - pad;

        this.playersPanel = new Rectangle(
            this.xPositionOnScreen + pad,
            contentTop,
            playersWidth,
            contentHeight);

        int rightX = this.playersPanel.Right + gap;
        int rightWidth = this.xPositionOnScreen + this.width - pad - rightX;

        this.statusPanel = new Rectangle(
            rightX,
            contentTop,
            rightWidth,
            statusHeight);

        this.actionsPanel = new Rectangle(
            rightX,
            this.statusPanel.Bottom + gap,
            rightWidth,
            contentTop + contentHeight - (this.statusPanel.Bottom + gap));

        this.BuildActionButtons();
    }

    private void DrawHeader(SpriteBatch b)
    {
        Rectangle strip = new(this.xPositionOnScreen + 18, this.yPositionOnScreen + 16, this.width - 86, 34);
        b.Draw(Game1.staminaRect, strip, new Color(245, 214, 160));

        b.DrawString(Game1.dialogueFont, "Romance Hub", new Vector2(this.xPositionOnScreen + 22, this.yPositionOnScreen + 18), Color.Black);
        b.DrawString(
            Game1.smallFont,
            $"Hotkey: {this.mod.GetRomanceHubHotkey()}   |   Right-click players for quick actions",
            new Vector2(this.xPositionOnScreen + 220, this.yPositionOnScreen + 24),
            new Color(60, 76, 100));
    }

    private void DrawPlayersPanel(SpriteBatch b)
    {
        this.DrawPanelBox(b, this.playersPanel, "Online Players");

        if (this.playerRows.Count == 0)
        {
            b.DrawString(
                Game1.smallFont,
                "No other online players detected.",
                new Vector2(this.playersPanel.X + 16, this.playersPanel.Y + 52),
                Color.Gray);
            return;
        }

        for (int i = 0; i < this.playerRows.Count; i++)
        {
            ClickableComponent row = this.playerRows[i];
            bool selected = this.playerIds[i] == this.selectedPlayerId;
            Color fill = selected ? new Color(220, 236, 255) : new Color(252, 245, 226);
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                row.bounds.X,
                row.bounds.Y,
                row.bounds.Width,
                row.bounds.Height,
                fill,
                4f,
                false);

            Farmer? farmer = this.mod.FindFarmerById(this.playerIds[i], includeOffline: true);
            string label = farmer?.Name ?? this.playerIds[i].ToString();
            b.DrawString(Game1.smallFont, this.FitText(Game1.smallFont, label, row.bounds.Width - 14), new Vector2(row.bounds.X + 8, row.bounds.Y + 12), Color.Black);
        }
    }

    private void DrawStatusPanel(SpriteBatch b)
    {
        this.DrawPanelBox(b, this.statusPanel, "Couple Status");

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            b.DrawString(Game1.smallFont, "Select an online player on the left.", new Vector2(this.statusPanel.X + 16, this.statusPanel.Y + 52), Color.Gray);
            b.DrawString(Game1.smallFont, "This panel shows hearts, cooldowns, and active sessions.", new Vector2(this.statusPanel.X + 16, this.statusPanel.Y + 80), Color.Gray);
            b.DrawString(Game1.smallFont, "Hearts gain: complete dates, gifts, immersive NPC chats.", new Vector2(this.statusPanel.X + 16, this.statusPanel.Y + 108), Color.Gray);
            b.DrawString(Game1.smallFont, "Hearts loss: rejected requests and early date endings.", new Vector2(this.statusPanel.X + 16, this.statusPanel.Y + 136), Color.Gray);
            return;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        string relationState = relation?.State.ToString() ?? "None";
        int points = relation?.HeartPoints ?? 0;
        int level = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
        string cooldown = relation is null
            ? "-"
            : relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()) ? "Ready" : "Used today";

        string pairKey = ConsentSystem.GetPairKey(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        string heartEvent = this.mod.GetLastHeartEvent(pairKey);
        if (string.IsNullOrWhiteSpace(heartEvent))
        {
            heartEvent = "No recent heart change.";
        }

        string activeSession = this.GetActiveSessionText(target!.UniqueMultiplayerID);

        int x = this.statusPanel.X + 16;
        int y = this.statusPanel.Y + 50;
        b.DrawString(Game1.smallFont, $"Target: {target.Name}", new Vector2(x, y), Color.Black);
        y += 24;
        b.DrawString(Game1.smallFont, $"Relationship: {relationState}", new Vector2(x, y), Color.Black);
        y += 24;
        b.DrawString(Game1.smallFont, $"Hearts: {level}/{this.mod.Config.MaxHearts} ({points} pts)", new Vector2(x, y), Color.Black);
        y += 24;
        b.DrawString(Game1.smallFont, $"Immersive cooldown: {cooldown}", new Vector2(x, y), Color.Black);
        y += 24;
        b.DrawString(Game1.smallFont, $"Active session: {activeSession}", new Vector2(x, y), new Color(62, 80, 110));
        y += 24;
        b.DrawString(Game1.smallFont, "Hearts +: complete date (+0.5), gift offers, immersive talks.", new Vector2(x, y), new Color(62, 80, 110));

        int barX = this.statusPanel.Right - 288;
        int barY = this.statusPanel.Y + 74;
        int barW = 250;
        int barH = 24;
        IClickableMenu.drawTextureBox(b, barX, barY, barW, barH, Color.White);
        float ratio = Math.Clamp(points / (float)Math.Max(1, this.mod.Config.MaxHearts * this.mod.Config.HeartPointsPerHeart), 0f, 1f);
        b.Draw(Game1.staminaRect, new Rectangle(barX + 3, barY + 3, (int)((barW - 6) * ratio), barH - 6), new Color(214, 60, 74));

        b.DrawString(
            Game1.smallFont,
            this.FitText(Game1.smallFont, $"Last heart event: {heartEvent}", this.statusPanel.Width - 32),
            new Vector2(this.statusPanel.X + 16, this.statusPanel.Bottom - 30),
            new Color(84, 84, 84));
    }

    private void DrawActionsPanel(SpriteBatch b)
    {
        this.DrawPanelBox(b, this.actionsPanel, "Actions");

        foreach (ActionButton action in this.actions)
        {
            (bool enabled, _) = action.GetState();
            bool hover = action.Bounds.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            Color fill = enabled ? new Color(250, 242, 221) : new Color(224, 215, 205);
            if (enabled && hover)
            {
                fill = new Color(255, 249, 233);
            }

            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                action.Bounds.bounds.X,
                action.Bounds.bounds.Y,
                action.Bounds.bounds.Width,
                action.Bounds.bounds.Height,
                fill,
                4f,
                false);

            string label = this.FitText(Game1.smallFont, action.Label, action.Bounds.bounds.Width - 16);
            b.DrawString(
                Game1.smallFont,
                label,
                new Vector2(action.Bounds.bounds.X + 8, action.Bounds.bounds.Y + 12),
                enabled ? new Color(33, 33, 33) : new Color(98, 98, 98));
        }
    }

    private void DrawPanelBox(SpriteBatch b, Rectangle panel, string title)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            panel.X,
            panel.Y,
            panel.Width,
            panel.Height,
            Color.White,
            4f,
            false);

        Rectangle strip = new(panel.X + 8, panel.Y + 8, panel.Width - 16, 28);
        b.Draw(Game1.staminaRect, strip, new Color(245, 224, 173));
        b.DrawString(Game1.smallFont, title, new Vector2(panel.X + 12, panel.Y + 12), new Color(45, 56, 84));
    }

    private void BuildActionButtons()
    {
        this.actions.Clear();

        int innerPad = 16;
        int topOffset = 44;
        int buttonHeight = 44;
        int columnGap = 14;
        int rowGap = 10;

        int usableWidth = this.actionsPanel.Width - innerPad * 2;
        int buttonWidth = (usableWidth - columnGap) / 2;
        int startX = this.actionsPanel.X + innerPad;
        int startY = this.actionsPanel.Y + topOffset;

        int col = 0;
        int row = 0;

        void Add(string label, Func<(bool enabled, string disabledReason)> state, Action execute)
        {
            Rectangle bounds = new(
                startX + col * (buttonWidth + columnGap),
                startY + row * (buttonHeight + rowGap),
                buttonWidth,
                buttonHeight);

            this.actions.Add(new ActionButton
            {
                Label = label,
                Bounds = new ClickableComponent(bounds, label),
                GetState = state,
                Execute = execute
            });

            col++;
            if (col >= 2)
            {
                col = 0;
                row++;
            }
        }

        Add("Dating Proposal", this.GetDatingState, () => this.RunResult(this.mod.DatingSystem.RequestDatingFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        Add(
            "Start Date (Town)",
            () => this.GetImmersiveDateState(ImmersiveDateLocation.Town),
            () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Town, out string msg), msg, "[PR.System.DateImmersion]"));
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

    private void RefreshPlayerList()
    {
        this.playerIds.Clear();
        this.playerRows.Clear();

        HashSet<long> uniqueIds = new();
        foreach (Farmer farmer in Game1.getOnlineFarmers().Where(p => p.UniqueMultiplayerID != this.mod.LocalPlayerId))
        {
            if (uniqueIds.Add(farmer.UniqueMultiplayerID))
            {
                this.playerIds.Add(farmer.UniqueMultiplayerID);
            }
        }

        foreach (StardewModdingAPI.IMultiplayerPeer peer in this.mod.Helper.Multiplayer.GetConnectedPlayers())
        {
            if (peer.PlayerID == this.mod.LocalPlayerId)
            {
                continue;
            }

            if (uniqueIds.Add(peer.PlayerID))
            {
                this.playerIds.Add(peer.PlayerID);
            }
        }

        if (this.selectedPlayerId <= 0 || !this.playerIds.Contains(this.selectedPlayerId))
        {
            this.selectedPlayerId = this.playerIds.FirstOrDefault();
        }

        int rowX = this.playersPanel.X + 12;
        int rowY = this.playersPanel.Y + 46;
        int rowWidth = this.playersPanel.Width - 24;
        const int rowHeight = 38;
        const int rowGap = 8;

        foreach (long playerId in this.playerIds)
        {
            this.playerRows.Add(new ClickableComponent(new Rectangle(rowX, rowY, rowWidth, rowHeight), playerId.ToString()));
            rowY += rowHeight + rowGap;
        }
    }

    private bool TryGetSelectedTarget(out Farmer? target)
    {
        target = null;
        if (this.selectedPlayerId <= 0)
        {
            return false;
        }

        target = this.mod.FindFarmerById(this.selectedPlayerId, includeOffline: true);
        return target is not null && this.mod.IsPlayerOnline(target.UniqueMultiplayerID);
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

        if (this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, targetPlayerId))
        {
            return "Carry";
        }

        return "None";
    }

    private (bool enabled, string disabledReason) GetDatingState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        if (relation is not null && relation.State != RelationshipState.None)
        {
            return (false, $"Already {relation.State}.");
        }

        return (true, string.Empty);
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

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        if (relation is null)
        {
            return (false, "Requires Dating first.");
        }

        if (relation.State == RelationshipState.Married)
        {
            return (false, "Already married.");
        }

        return relation.State == RelationshipState.Dating
            ? (true, string.Empty)
            : (false, "Requires Dating state.");
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

        return this.mod.MarriageSystem.IsMarried(this.mod.LocalPlayerId, target!.UniqueMultiplayerID)
            ? (true, string.Empty)
            : (false, "Requires Married state.");
    }

    private (bool enabled, string disabledReason) GetCarryState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        return this.mod.CarrySystem.CanRequestCarry(this.mod.LocalPlayerId, target!.UniqueMultiplayerID, out string reason)
            ? (true, string.Empty)
            : (false, reason);
    }

    private (bool enabled, string disabledReason) GetCarryStopState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        return this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, target!.UniqueMultiplayerID)
            ? (true, string.Empty)
            : (false, "No carry session with this player.");
    }

    private (bool enabled, string disabledReason) GetHandsState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        return this.mod.HoldingHandsSystem.CanRequestHands(this.mod.LocalPlayerId, target!.UniqueMultiplayerID, out string reason)
            ? (true, string.Empty)
            : (false, reason);
    }

    private (bool enabled, string disabledReason) GetHandsStopState()
    {
        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        return this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, target!.UniqueMultiplayerID)
            ? (true, string.Empty)
            : (false, "No holding hands session with this player.");
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

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        if (relation is null || relation.State == RelationshipState.None)
        {
            return (false, "Requires Dating/Engaged/Married.");
        }

        if (!relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()))
        {
            return (false, "Already used today for this couple.");
        }

        int requiredHearts = this.mod.DateImmersionSystem.GetRequiredHeartsForLocation(location);
        if (!this.mod.HeartsSystem.IsAtLeastHearts(this.mod.LocalPlayerId, target!.UniqueMultiplayerID, requiredHearts))
        {
            return (false, $"Requires {requiredHearts}+ hearts for {location}.");
        }

        if (this.mod.DateImmersionSystem.IsActive)
        {
            return (false, "Another immersive date is already active.");
        }

        return (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetEndImmersiveState()
    {
        DateImmersionPublicState? state = this.mod.DateImmersionSystem.GetActivePublicState();
        if (state is null || !state.IsActive)
        {
            return (false, "No active immersive date.");
        }

        return state.PlayerAId == this.mod.LocalPlayerId || state.PlayerBId == this.mod.LocalPlayerId
            ? (true, string.Empty)
            : (false, "Only participants can end the date.");
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
        string working = text;
        while (working.Length > 0 && font.MeasureString(working + ellipsis).X > maxWidth)
        {
            working = working[..^1];
        }

        return working.Length == 0 ? ellipsis : working + ellipsis;
    }
}

