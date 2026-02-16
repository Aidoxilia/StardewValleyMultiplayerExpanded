using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
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

    private sealed class ActionButton
    {
        public string Label { get; set; } = string.Empty;
        public ClickableComponent Bounds { get; set; } = null!;
        public Func<(bool enabled, string disabledReason)> GetState { get; set; } = null!;
        public Action Execute { get; set; } = null!;
    }

    public RomanceHubMenu(ModEntry mod)
        : base(
            Game1.uiViewport.Width / 2 - 500,
            Game1.uiViewport.Height / 2 - 330,
            1000,
            660,
            showUpperRightCloseButton: false)
    {
        this.mod = mod;
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 54, this.yPositionOnScreen + 10, 40, 40),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.2f);
        this.RefreshPlayerList();
        this.BuildActionButtons();
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
            if (this.playerRows[i].containsPoint(x, y))
            {
                this.selectedPlayerId = this.playerIds[i];
                Game1.playSound("smallSelect");
                return;
            }
        }

        foreach (ActionButton action in this.actions)
        {
            if (!action.Bounds.containsPoint(x, y))
            {
                continue;
            }

            (bool enabled, _) = action.GetState();
            if (!enabled)
            {
                Game1.playSound("cancel");
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

        int x = this.xPositionOnScreen + 24;
        int y = this.yPositionOnScreen + 20;
        b.DrawString(Game1.dialogueFont, "Romance Hub", new Vector2(x, y), Color.Black);
        b.DrawString(Game1.smallFont, $"Hotkey: {this.mod.GetRomanceHubHotkey()}", new Vector2(x + 260, y + 14), Color.DarkSlateGray);

        this.DrawPlayersPanel(b);
        this.DrawStatusPanel(b);
        this.DrawActionsPanel(b);

        if (!string.IsNullOrWhiteSpace(this.hoverText))
        {
            IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
        }

        this.drawMouse(b);
    }

    private void DrawPlayersPanel(SpriteBatch b)
    {
        Rectangle panel = new(this.xPositionOnScreen + 20, this.yPositionOnScreen + 70, 290, this.height - 90);
        IClickableMenu.drawTextureBox(
            b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            panel.X, panel.Y, panel.Width, panel.Height, Color.White, 4f, false);
        b.DrawString(Game1.smallFont, "Online players", new Vector2(panel.X + 14, panel.Y + 12), Color.DarkSlateBlue);

        for (int i = 0; i < this.playerRows.Count; i++)
        {
            ClickableComponent row = this.playerRows[i];
            bool selected = this.playerIds[i] == this.selectedPlayerId;
            IClickableMenu.drawTextureBox(
                b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
                row.bounds.X, row.bounds.Y, row.bounds.Width, row.bounds.Height,
                selected ? new Color(225, 240, 255) : Color.White, 4f, false);
            Farmer? farmer = this.mod.FindFarmerById(this.playerIds[i], includeOffline: true);
            string label = farmer?.Name ?? this.playerIds[i].ToString();
            b.DrawString(Game1.smallFont, label, new Vector2(row.bounds.X + 10, row.bounds.Y + 11), Color.Black);
        }
    }

    private void DrawStatusPanel(SpriteBatch b)
    {
        Rectangle panel = new(this.xPositionOnScreen + 330, this.yPositionOnScreen + 70, this.width - 350, 170);
        IClickableMenu.drawTextureBox(
            b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            panel.X, panel.Y, panel.Width, panel.Height, Color.White, 4f, false);
        b.DrawString(Game1.smallFont, "Couple status", new Vector2(panel.X + 14, panel.Y + 10), Color.DarkSlateBlue);

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            b.DrawString(Game1.smallFont, "Select an online player on the left.", new Vector2(panel.X + 14, panel.Y + 44), Color.Gray);
            return;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        string relationState = relation?.State.ToString() ?? "None";
        int points = relation?.HeartPoints ?? 0;
        int level = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
        string cooldown = relation is null
            ? "-"
            : relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()) ? "ready" : "used today";

        b.DrawString(Game1.smallFont, $"Target: {target.Name}", new Vector2(panel.X + 14, panel.Y + 44), Color.Black);
        b.DrawString(Game1.smallFont, $"Relationship: {relationState}", new Vector2(panel.X + 14, panel.Y + 70), Color.Black);
        b.DrawString(Game1.smallFont, $"Hearts: {level}/{this.mod.Config.MaxHearts} ({points} pts)", new Vector2(panel.X + 14, panel.Y + 96), Color.Black);
        b.DrawString(Game1.smallFont, $"Immersive date cooldown: {cooldown}", new Vector2(panel.X + 14, panel.Y + 122), Color.Black);

        int barX = panel.X + 360;
        int barY = panel.Y + 94;
        int barW = 240;
        int barH = 20;
        IClickableMenu.drawTextureBox(b, barX, barY, barW, barH, Color.White);
        float ratio = Math.Clamp(points / (float)Math.Max(1, this.mod.Config.MaxHearts * this.mod.Config.HeartPointsPerHeart), 0f, 1f);
        b.Draw(Game1.staminaRect, new Rectangle(barX + 2, barY + 2, (int)((barW - 4) * ratio), barH - 4), Color.Crimson);
    }

    private void DrawActionsPanel(SpriteBatch b)
    {
        Rectangle panel = new(this.xPositionOnScreen + 330, this.yPositionOnScreen + 260, this.width - 350, this.height - 280);
        IClickableMenu.drawTextureBox(
            b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            panel.X, panel.Y, panel.Width, panel.Height, Color.White, 4f, false);
        b.DrawString(Game1.smallFont, "Actions", new Vector2(panel.X + 14, panel.Y + 10), Color.DarkSlateBlue);

        foreach (ActionButton action in this.actions)
        {
            (bool enabled, _) = action.GetState();
            IClickableMenu.drawTextureBox(
                b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
                action.Bounds.bounds.X, action.Bounds.bounds.Y,
                action.Bounds.bounds.Width, action.Bounds.bounds.Height,
                enabled ? Color.White : Color.Gray, 4f, false);
            b.DrawString(
                Game1.smallFont,
                action.Label,
                new Vector2(action.Bounds.bounds.X + 10, action.Bounds.bounds.Y + 12),
                enabled ? Color.Black : Color.DimGray);
        }
    }

    private void BuildActionButtons()
    {
        int startX = this.xPositionOnScreen + 350;
        int startY = this.yPositionOnScreen + 300;
        const int buttonWidth = 300;
        const int buttonHeight = 46;
        const int colGap = 18;
        const int rowGap = 12;

        int col = 0;
        int row = 0;

        void Add(string label, Func<(bool enabled, string disabledReason)> state, Action execute)
        {
            Rectangle bounds = new(
                startX + col * (buttonWidth + colGap),
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

        Add("Propose Dating", this.GetDatingState, () => this.RunResult(this.mod.DatingSystem.RequestDatingFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        Add("Start Date Event", this.GetDateState, () => this.RunResult(this.mod.DateEventController.StartDateFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        Add("Propose Marriage", this.GetMarriageState, () => this.RunResult(this.mod.MarriageSystem.RequestMarriageFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Marriage]"));
        Add("Try For Baby", this.GetPregnancyState, () => this.RunResult(this.mod.PregnancySystem.RequestTryForBabyFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Pregnancy]"));
        Add("Carry Player", this.GetCarryState, () => this.RunResult(this.mod.CarrySystem.RequestCarryFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        Add("Stop Carry", this.GetCarryStopState, () => this.RunResult(this.mod.CarrySystem.StopCarryFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        Add("Start Holding Hands", this.GetHandsState, () => this.RunResult(this.mod.HoldingHandsSystem.RequestHoldingHandsFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        Add("Stop Holding Hands", this.GetHandsStopState, () => this.RunResult(this.mod.HoldingHandsSystem.StopHoldingHandsFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        Add("Immersive Date (Town)", () => this.GetImmersiveDateState(ImmersiveDateLocation.Town), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Town, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("Immersive Date (Beach)", () => this.GetImmersiveDateState(ImmersiveDateLocation.Beach), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Beach, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("Immersive Date (Forest)", () => this.GetImmersiveDateState(ImmersiveDateLocation.Forest), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Forest, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("End Immersive Date", this.GetEndImmersiveState, () => this.RunResult(this.mod.DateImmersionSystem.EndImmersiveDateFromLocal(out string msg), msg, "[PR.System.DateImmersion]"));
    }

    private void RefreshPlayerList()
    {
        this.playerIds.Clear();
        this.playerRows.Clear();

        foreach (Farmer farmer in Game1.getOnlineFarmers().Where(p => p.UniqueMultiplayerID != this.mod.LocalPlayerId))
        {
            this.playerIds.Add(farmer.UniqueMultiplayerID);
        }

        if (this.selectedPlayerId <= 0 || !this.playerIds.Contains(this.selectedPlayerId))
        {
            this.selectedPlayerId = this.playerIds.FirstOrDefault();
        }

        int y = this.yPositionOnScreen + 110;
        foreach (long playerId in this.playerIds)
        {
            this.playerRows.Add(new ClickableComponent(new Rectangle(this.xPositionOnScreen + 34, y, 258, 40), playerId.ToString()));
            y += 46;
        }
    }

    private bool TryGetSelectedTarget(out Farmer? target)
    {
        target = null;
        if (this.selectedPlayerId <= 0)
        {
            return false;
        }

        target = this.mod.FindFarmerById(this.selectedPlayerId, includeOffline: false);
        return target is not null;
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

    private (bool enabled, string disabledReason) GetDateState()
    {
        if (!this.mod.Config.EnableDateEvents)
        {
            return (false, "Date events disabled.");
        }

        if (!this.TryGetSelectedTarget(out Farmer? target))
        {
            return (false, "Select an online player.");
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, target!.UniqueMultiplayerID);
        return relation is null || relation.State == RelationshipState.None
            ? (false, "Requires Dating/Engaged/Married.")
            : (true, string.Empty);
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

    private (bool enabled, string disabledReason) GetImmersiveDateState(ImmersiveDateLocation _)
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

        if (!this.mod.HeartsSystem.IsAtLeastHearts(this.mod.LocalPlayerId, target!.UniqueMultiplayerID, this.mod.Config.ImmersiveDateMinHearts))
        {
            return (false, $"Requires {this.mod.Config.ImmersiveDateMinHearts}+ hearts.");
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
}

