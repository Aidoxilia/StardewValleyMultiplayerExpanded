using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class PlayerInteractionMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private readonly long targetPlayerId;
    private readonly List<ActionButton> buttons = new();
    private readonly ClickableTextureComponent closeButton;

    private Rectangle infoPanel;
    private Rectangle actionsPanel;
    private Rectangle actionsViewport;
    private bool compact;
    private int actionScroll;
    private int actionScrollMax;
    private string hoverText = string.Empty;

    private sealed class ActionButton
    {
        public string Label = string.Empty;
        public ClickableComponent Bounds = new(new Rectangle(0, 0, 0, 0), string.Empty);
        public Func<(bool enabled, string disabledReason)> State = null!;
        public Action Execute = null!;
    }

    public PlayerInteractionMenu(ModEntry mod, long targetPlayerId)
        : base((Game1.uiViewport.Width - WidthForViewport()) / 2, (Game1.uiViewport.Height - HeightForViewport()) / 2, WidthForViewport(), HeightForViewport(), true)
    {
        this.mod = mod;
        this.targetPlayerId = targetPlayerId;
        this.closeButton = new ClickableTextureComponent(new Rectangle(this.xPositionOnScreen + this.width - 56, this.yPositionOnScreen + 12, 40, 40), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 3.4f);
        this.BuildLayout();
        this.BuildButtons();
        this.LayoutButtons();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        this.LayoutButtons();
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = null;
            return;
        }

        foreach (ActionButton button in this.buttons)
        {
            if (!button.Bounds.containsPoint(x, y) || !this.IsButtonVisible(button.Bounds.bounds))
            {
                continue;
            }

            (bool enabled, string reason) = button.State();
            if (!enabled)
            {
                Game1.playSound("cancel");
                this.mod.Notifier.NotifyWarn(reason, "[PR.UI.PlayerActions]");
                return;
            }

            Game1.playSound("smallSelect");
            button.Execute();
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        this.LayoutButtons();
        if (this.actionsViewport.Contains(Game1.getMouseX(), Game1.getMouseY()) && this.actionScrollMax > 0)
        {
            this.actionScroll = Math.Clamp(this.actionScroll + (direction > 0 ? -1 : 1), 0, this.actionScrollMax);
            return;
        }

        base.receiveScrollWheelAction(direction);
    }

    public override void performHoverAction(int x, int y)
    {
        this.LayoutButtons();
        this.hoverText = string.Empty;
        foreach (ActionButton button in this.buttons)
        {
            if (!button.Bounds.containsPoint(x, y) || !this.IsButtonVisible(button.Bounds.bounds))
            {
                continue;
            }

            (bool enabled, string disabledReason) = button.State();
            this.hoverText = enabled ? button.Label : disabledReason;
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        this.LayoutButtons();
        Farmer? target = this.mod.FindFarmerById(this.targetPlayerId, includeOffline: true);
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        int hearts = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
        int heartPoints = relation?.HeartPoints ?? 0;

        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);
        this.DrawHeader(b);
        this.DrawPanelBox(b, this.infoPanel, "Target Status");
        this.DrawPanelBox(b, this.actionsPanel, "Actions");

        int x = this.infoPanel.X + 12;
        int y = this.infoPanel.Y + 42;
        int line = this.compact ? 22 : 24;
        string targetName = target?.Name ?? $"Player {this.targetPlayerId}";
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, $"Target: {targetName}", this.infoPanel.Width - 24), Game1.smallFont, new Vector2(x, y), Color.Black);
        y += line;
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, $"Relationship: {relation?.State.ToString() ?? "None"}", this.infoPanel.Width - 24), Game1.smallFont, new Vector2(x, y), Color.Black);
        y += line;
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, $"Hearts: {hearts}/{this.mod.Config.MaxHearts} ({heartPoints} pts)", this.infoPanel.Width - 24), Game1.smallFont, new Vector2(x, y), Color.Black);
        y += line;
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, "Gain hearts: dates, gifts, immersive talks.", this.infoPanel.Width - 24), Game1.smallFont, new Vector2(x, y), Color.Black);
        y += line;
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, "Lose hearts: rejected requests, early leave.", this.infoPanel.Width - 24), Game1.smallFont, new Vector2(x, y), Color.Black);

        foreach (ActionButton button in this.buttons)
        {
            Rectangle r = button.Bounds.bounds;
            if (!this.IsButtonVisible(r))
            {
                continue;
            }

            (bool enabled, _) = button.State();
            bool hover = button.Bounds.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            Color fill = enabled ? new Color(249, 241, 223) : new Color(224, 216, 203);
            if (enabled && hover)
            {
                fill = new Color(255, 249, 235);
            }

            IClickableMenu.drawTextureBox(b, r.X, r.Y, r.Width, r.Height, fill);
            string label = this.FitText(Game1.smallFont, button.Label, r.Width - 36);
            int textY = r.Y + (r.Height - (int)Game1.smallFont.MeasureString(label).Y) / 2;
            Utility.drawTextWithShadow(b, label, Game1.smallFont, new Vector2(r.X + 18, textY), Color.Black);
        }

        if (this.actionScrollMax > 0)
        {
            string hint = $"Mouse wheel: {this.actionScroll + 1}/{this.actionScrollMax + 1}";
            int hintX = this.actionsPanel.Right - 12 - (int)Game1.tinyFont.MeasureString(hint).X;
            int hintY = this.actionsPanel.Bottom - 8 - (int)Game1.tinyFont.MeasureString(hint).Y;
            Utility.drawTextWithShadow(b, hint, Game1.tinyFont, new Vector2(hintX, hintY), Color.Black);
        }

        if (!string.IsNullOrWhiteSpace(this.hoverText))
        {
            IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
        }

        this.drawMouse(b);
    }

    private static int WidthForViewport()
    {
        int preferred = Math.Min(760, (int)(Game1.uiViewport.Width * 0.74f));
        return Math.Min(preferred, Math.Max(480, Game1.uiViewport.Width - 10));
    }

    private static int HeightForViewport()
    {
        int preferred = Math.Min(860, (int)(Game1.uiViewport.Height * 0.9f));
        return Math.Min(preferred, Math.Max(500, Game1.uiViewport.Height - 10));
    }

    private void BuildLayout()
    {
        int pad = this.width < 560 ? 10 : 14;
        int header = 64;
        this.compact = this.width < 560 || this.height < 640;
        this.infoPanel = new Rectangle(this.xPositionOnScreen + pad, this.yPositionOnScreen + header, this.width - pad * 2, this.compact ? 146 : 166);
        int actionsTop = this.infoPanel.Bottom + 10;
        int actionsHeight = this.yPositionOnScreen + this.height - pad - actionsTop;
        if (actionsHeight < 150)
        {
            int deficit = 150 - actionsHeight;
            this.infoPanel.Height = Math.Max(106, this.infoPanel.Height - deficit);
            actionsTop = this.infoPanel.Bottom + 10;
            actionsHeight = this.yPositionOnScreen + this.height - pad - actionsTop;
        }

        this.actionsPanel = new Rectangle(this.xPositionOnScreen + pad, actionsTop, this.width - pad * 2, actionsHeight);
    }

    private void LayoutButtons()
    {
        int inPad = 12;
        int top = 44;
        int footer = 24;
        int buttonHeight = this.compact ? 44 : 50;
        int step = buttonHeight + 11;
        this.actionsViewport = new Rectangle(this.actionsPanel.X + inPad, this.actionsPanel.Y + top, this.actionsPanel.Width - inPad * 2, Math.Max(56, this.actionsPanel.Height - top - footer));
        int visibleRows = Math.Max(1, (this.actionsViewport.Height + 9) / step);
        this.actionScrollMax = Math.Max(0, this.buttons.Count - visibleRows);
        this.actionScroll = Math.Clamp(this.actionScroll, 0, this.actionScrollMax);

        for (int i = 0; i < this.buttons.Count; i++)
        {
            this.buttons[i].Bounds.bounds = new Rectangle(this.actionsViewport.X, this.actionsViewport.Y + (i - this.actionScroll) * step, this.actionsViewport.Width, buttonHeight);
        }
    }

    private void DrawHeader(SpriteBatch b)
    {
        Rectangle strip = new(this.xPositionOnScreen + 14, this.yPositionOnScreen + 14, this.width - 80, 38);
        b.Draw(Game1.staminaRect, strip, new Color(248, 227, 181));
        const string title = "Player Actions";
        Utility.drawTextWithShadow(b, title, Game1.dialogueFont, new Vector2(strip.X + 8, strip.Y + 2), Color.Black);
        int sx = strip.X + 14 + (int)Game1.dialogueFont.MeasureString(title).X + 20;
        sx = Math.Min(sx, strip.Right - 120);
        Utility.drawTextWithShadow(b, this.FitText(Game1.smallFont, "Quick relationship actions with automatic gating", Math.Max(80, strip.Width - (sx - strip.X) - 8)), Game1.smallFont, new Vector2(sx, strip.Y + 10), Color.Black);
    }

    private void DrawPanelBox(SpriteBatch b, Rectangle panel, string title)
    {
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), panel.X, panel.Y, panel.Width, panel.Height, Color.White, 4f, false);
        Rectangle strip = new(panel.X + 8, panel.Y + 8, panel.Width - 16, 30);
        b.Draw(Game1.staminaRect, strip, new Color(245, 224, 173));
        Utility.drawTextWithShadow(b, title, Game1.smallFont, new Vector2(panel.X + 12, panel.Y + 13), Color.Black);
    }

    private void BuildButtons()
    {
        this.buttons.Clear();
        void Add(string label, Func<(bool enabled, string disabledReason)> state, Action execute)
        {
            this.buttons.Add(new ActionButton { Label = label, State = state, Execute = execute });
        }

        Add("Accept Pending", this.GetPendingResolutionState, () => this.RunAction(this.ResolvePendingFromTarget(true, out string msg), msg, "[PR.Core]"));
        Add("Reject Pending", this.GetPendingResolutionState, () => this.RunAction(this.ResolvePendingFromTarget(false, out string msg), msg, "[PR.Core]"));
        Add("Propose Dating", this.GetDatingProposalState, () => this.RunAction(this.mod.DatingSystem.RequestDatingFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        Add("Start Date (Town)", this.GetDateEventState, () => this.RunAction(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.targetPlayerId.ToString(), ImmersiveDateLocation.Town, out string msg), msg, "[PR.System.DateImmersion]"));
        Add("Propose Marriage", this.GetMarriageProposalState, () => this.RunAction(this.mod.MarriageSystem.RequestMarriageFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Marriage]"));
        Add("Try For Baby", this.GetTryForBabyState, () => this.RunAction(this.mod.PregnancySystem.RequestTryForBabyFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Pregnancy]"));
        Add("Carry Player", this.GetCarryStartState, () => this.RunAction(this.mod.CarrySystem.RequestCarryFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        Add("Stop Carry", this.GetCarryStopState, () => this.RunAction(this.mod.CarrySystem.StopCarryFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        Add("Start Holding Hands", this.GetHoldingHandsStartState, () => this.RunAction(this.mod.HoldingHandsSystem.RequestHoldingHandsFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        Add("Stop Holding Hands", this.GetHoldingHandsStopState, () => this.RunAction(this.mod.HoldingHandsSystem.StopHoldingHandsFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
    }

    private bool IsButtonVisible(Rectangle r)
    {
        return r.Bottom > this.actionsViewport.Y && r.Y < this.actionsViewport.Bottom;
    }

    private (bool enabled, string disabledReason) GetDatingProposalState()
    {
        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        if (relation is null)
        {
            return (true, string.Empty);
        }

        if (relation.State != RelationshipState.None)
        {
            return (false, $"Disabled: already {relation.State}.");
        }

        return relation.PendingDatingFrom.HasValue ? (false, "Disabled: dating request already pending.") : (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetDateEventState()
    {
        if (!this.mod.Config.EnableImmersiveDates)
        {
            return (false, "Immersive dates disabled in config.");
        }

        if (this.mod.DateImmersionSystem.IsActive)
        {
            return (false, "Another immersive date is already active.");
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        if (relation is null || relation.State == RelationshipState.None)
        {
            return (false, "Requires Dating/Engaged/Married status.");
        }

        return relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()) ? (true, string.Empty) : (false, "Immersive date already used today for this couple.");
    }

    private (bool enabled, string disabledReason) GetMarriageProposalState()
    {
        if (!this.mod.Config.EnableMarriage)
        {
            return (false, "Disabled in config.");
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        if (relation is null)
        {
            return (false, "Requires Dating status first.");
        }

        if (relation.State == RelationshipState.Married)
        {
            return (false, "Disabled: already married.");
        }

        if (relation.State != RelationshipState.Dating)
        {
            return (false, "Requires Dating status.");
        }

        return relation.PendingMarriageFrom.HasValue ? (false, "Disabled: marriage request already pending.") : (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetTryForBabyState()
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            return (false, "Disabled in config.");
        }

        return this.mod.MarriageSystem.IsMarried(this.mod.LocalPlayerId, this.targetPlayerId) ? (true, string.Empty) : (false, "Requires Married status.");
    }

    private (bool enabled, string disabledReason) GetCarryStartState()
    {
        return this.mod.CarrySystem.CanRequestCarry(this.mod.LocalPlayerId, this.targetPlayerId, out string reason) ? (true, string.Empty) : (false, $"Disabled: {reason}");
    }

    private (bool enabled, string disabledReason) GetCarryStopState()
    {
        return this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, this.targetPlayerId) ? (true, string.Empty) : (false, "No active carry session with this player.");
    }

    private (bool enabled, string disabledReason) GetHoldingHandsStartState()
    {
        return this.mod.HoldingHandsSystem.CanRequestHands(this.mod.LocalPlayerId, this.targetPlayerId, out string reason) ? (true, string.Empty) : (false, $"Disabled: {reason}");
    }

    private (bool enabled, string disabledReason) GetHoldingHandsStopState()
    {
        return this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, this.targetPlayerId) ? (true, string.Empty) : (false, "No active holding hands session with this player.");
    }

    private void RunAction(bool success, string message, string category)
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

    private (bool enabled, string disabledReason) GetPendingResolutionState()
    {
        return this.HasPendingFromTarget(out _) ? (true, string.Empty) : (false, "No pending request from this player.");
    }

    private bool ResolvePendingFromTarget(bool accept, out string message)
    {
        if (this.mod.DatingSystem.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out long datingRequester) && datingRequester == this.targetPlayerId)
        {
            return this.mod.DatingSystem.RespondToPendingDatingLocal(accept, out message);
        }

        if (this.mod.MarriageSystem.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out long marriageRequester) && marriageRequester == this.targetPlayerId)
        {
            return this.mod.MarriageSystem.RespondToPendingMarriageLocal(accept, out message);
        }

        if (this.mod.PregnancySystem.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out long pregnancyRequester) && pregnancyRequester == this.targetPlayerId)
        {
            return this.mod.PregnancySystem.RespondTryForBabyFromLocal(accept, out message);
        }

        if (this.mod.CarrySystem.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out long carryRequester) && carryRequester == this.targetPlayerId)
        {
            return this.mod.CarrySystem.RespondToPendingCarryLocal(accept, out message);
        }

        if (this.mod.HoldingHandsSystem.TryGetPendingForPlayer(this.mod.LocalPlayerId, out long handsRequester) && handsRequester == this.targetPlayerId)
        {
            return this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(accept, out message);
        }

        message = "No pending request from this player.";
        return false;
    }

    private bool HasPendingFromTarget(out long requesterId)
    {
        requesterId = -1;
        if (this.mod.DatingSystem.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out requesterId) && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.MarriageSystem.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out requesterId) && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.PregnancySystem.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out requesterId) && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.CarrySystem.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out requesterId) && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.HoldingHandsSystem.TryGetPendingForPlayer(this.mod.LocalPlayerId, out requesterId) && requesterId == this.targetPlayerId)
        {
            return true;
        }

        requesterId = -1;
        return false;
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
