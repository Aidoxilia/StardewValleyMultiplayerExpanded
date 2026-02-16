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
    private string hoverText = string.Empty;

    private sealed class ActionButton
    {
        public ClickableComponent Bounds { get; set; } = null!;
        public string Label { get; set; } = string.Empty;
        public Func<(bool enabled, string disabledReason)> GetState { get; set; } = null!;
        public Action Execute { get; set; } = null!;
    }

    public PlayerInteractionMenu(ModEntry mod, long targetPlayerId)
        : base(Game1.uiViewport.Width / 2 - 260, Game1.uiViewport.Height / 2 - 360, 520, 720, true)
    {
        this.mod = mod;
        this.targetPlayerId = targetPlayerId;
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 56, this.yPositionOnScreen + 12, 40, 40),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.4f);
        this.BuildButtons();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = null;
            return;
        }

        foreach (ActionButton button in this.buttons)
        {
            if (!button.Bounds.containsPoint(x, y))
            {
                continue;
            }

            (bool enabled, _) = button.GetState();
            if (!enabled)
            {
                Game1.playSound("cancel");
                return;
            }

            Game1.playSound("smallSelect");
            button.Execute();
            Game1.activeClickableMenu = null;
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void performHoverAction(int x, int y)
    {
        this.hoverText = string.Empty;
        foreach (ActionButton button in this.buttons)
        {
            if (!button.Bounds.containsPoint(x, y))
            {
                continue;
            }

            (bool enabled, string disabledReason) = button.GetState();
            this.hoverText = enabled ? button.Label : disabledReason;
            break;
        }
    }

    public override void draw(SpriteBatch b)
    {
        Farmer? target = this.mod.FindFarmerById(this.targetPlayerId, includeOffline: true);
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        int x = this.xPositionOnScreen + 28;
        int y = this.yPositionOnScreen + 24;
        b.DrawString(Game1.dialogueFont, "Player Actions", new Vector2(x, y), Color.Black);
        y += 52;

        string targetName = target?.Name ?? $"Player {this.targetPlayerId}";
        b.DrawString(Game1.smallFont, $"Target: {targetName}", new Vector2(x, y), Color.DarkSlateBlue);
        y += 28;

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        string relationText = relation?.State.ToString() ?? "None";
        b.DrawString(Game1.smallFont, $"Relationship: {relationText}", new Vector2(x, y), Color.DarkSlateBlue);
        y += 24;
        int hearts = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
        int heartPoints = relation?.HeartPoints ?? 0;
        b.DrawString(Game1.smallFont, $"Hearts: {hearts}/{this.mod.Config.MaxHearts} ({heartPoints} pts)", new Vector2(x, y), Color.DarkSlateBlue);
        y += 24;

        foreach (ActionButton button in this.buttons)
        {
            (bool enabled, _) = button.GetState();
            Color boxColor = enabled ? Color.White : Color.Gray;
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                button.Bounds.bounds.X,
                button.Bounds.bounds.Y,
                button.Bounds.bounds.Width,
                button.Bounds.bounds.Height,
                boxColor,
                4f,
                false);
            b.DrawString(
                Game1.smallFont,
                button.Label,
                new Vector2(button.Bounds.bounds.X + 16, button.Bounds.bounds.Y + 12),
                enabled ? Color.Black : Color.DarkGray);
        }

        if (!string.IsNullOrWhiteSpace(this.hoverText))
        {
            IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
        }

        this.drawMouse(b);
    }

    private void BuildButtons()
    {
        int x = this.xPositionOnScreen + 26;
        int y = this.yPositionOnScreen + 132;
        int width = this.width - 52;
        const int height = 40;
        const int spacing = 10;

        this.AddButton(
            "Accept Pending",
            new Rectangle(x, y, width, height),
            this.GetPendingResolutionState,
            () => this.RunAction(this.ResolvePendingFromTarget(true, out string msg), msg, "[PR.Core]"));
        y += height + spacing;

        this.AddButton(
            "Reject Pending",
            new Rectangle(x, y, width, height),
            this.GetPendingResolutionState,
            () => this.RunAction(this.ResolvePendingFromTarget(false, out string msg), msg, "[PR.Core]"));
        y += height + spacing;

        this.AddButton(
            "Propose Dating",
            new Rectangle(x, y, width, height),
            this.GetDatingProposalState,
            () => this.RunAction(this.mod.DatingSystem.RequestDatingFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        y += height + spacing;

        this.AddButton(
            "Start Date Event",
            new Rectangle(x, y, width, height),
            this.GetDateEventState,
            () => this.RunAction(this.mod.DateEventController.StartDateFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Dating]"));
        y += height + spacing;

        this.AddButton(
            "Propose Marriage",
            new Rectangle(x, y, width, height),
            this.GetMarriageProposalState,
            () => this.RunAction(this.mod.MarriageSystem.RequestMarriageFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Marriage]"));
        y += height + spacing;

        this.AddButton(
            "Try For Baby",
            new Rectangle(x, y, width, height),
            this.GetTryForBabyState,
            () => this.RunAction(this.mod.PregnancySystem.RequestTryForBabyFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Pregnancy]"));
        y += height + spacing;

        this.AddButton(
            "Carry Player",
            new Rectangle(x, y, width, height),
            this.GetCarryStartState,
            () => this.RunAction(this.mod.CarrySystem.RequestCarryFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        y += height + spacing;

        this.AddButton(
            "Stop Carry",
            new Rectangle(x, y, width, height),
            this.GetCarryStopState,
            () => this.RunAction(this.mod.CarrySystem.StopCarryFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.Carry]"));
        y += height + spacing;

        this.AddButton(
            "Start Holding Hands",
            new Rectangle(x, y, width, height),
            this.GetHoldingHandsStartState,
            () => this.RunAction(this.mod.HoldingHandsSystem.RequestHoldingHandsFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        y += height + spacing;

        this.AddButton(
            "Stop Holding Hands",
            new Rectangle(x, y, width, height),
            this.GetHoldingHandsStopState,
            () => this.RunAction(this.mod.HoldingHandsSystem.StopHoldingHandsFromLocal(this.targetPlayerId.ToString(), out string msg), msg, "[PR.System.HoldingHands]"));
        y += height + spacing;

        this.AddButton(
            "Immersive Date (Town)",
            new Rectangle(x, y, width, height),
            this.GetImmersiveDateStartState,
            () => this.RunAction(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.targetPlayerId.ToString(), ImmersiveDateLocation.Town, out string msg), msg, "[PR.System.DateImmersion]"));
    }

    private void AddButton(string label, Rectangle bounds, Func<(bool enabled, string disabledReason)> state, Action action)
    {
        this.buttons.Add(new ActionButton
        {
            Bounds = new ClickableComponent(bounds, label),
            Label = label,
            GetState = state,
            Execute = action
        });
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

        if (relation.PendingDatingFrom.HasValue)
        {
            return (false, "Disabled: dating request already pending.");
        }

        return (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetDateEventState()
    {
        if (!this.mod.Config.EnableDateEvents)
        {
            return (false, "Disabled in config.");
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        if (relation is null || relation.State == RelationshipState.None)
        {
            return (false, "Requires Dating/Engaged/Married status.");
        }

        return (true, string.Empty);
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

        if (relation.PendingMarriageFrom.HasValue)
        {
            return (false, "Disabled: marriage request already pending.");
        }

        return (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetTryForBabyState()
    {
        if (!this.mod.Config.EnablePregnancy)
        {
            return (false, "Disabled in config.");
        }

        if (!this.mod.MarriageSystem.IsMarried(this.mod.LocalPlayerId, this.targetPlayerId))
        {
            return (false, "Requires Married status.");
        }

        return (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetCarryStartState()
    {
        return this.mod.CarrySystem.CanRequestCarry(this.mod.LocalPlayerId, this.targetPlayerId, out string reason)
            ? (true, string.Empty)
            : (false, $"Disabled: {reason}");
    }

    private (bool enabled, string disabledReason) GetCarryStopState()
    {
        if (!this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, this.targetPlayerId))
        {
            return (false, "No active carry session with this player.");
        }

        return (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetHoldingHandsStartState()
    {
        return this.mod.HoldingHandsSystem.CanRequestHands(this.mod.LocalPlayerId, this.targetPlayerId, out string reason)
            ? (true, string.Empty)
            : (false, $"Disabled: {reason}");
    }

    private (bool enabled, string disabledReason) GetHoldingHandsStopState()
    {
        if (!this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, this.targetPlayerId))
        {
            return (false, "No active holding hands session with this player.");
        }

        return (true, string.Empty);
    }

    private (bool enabled, string disabledReason) GetImmersiveDateStartState()
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

        if (!relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber()))
        {
            return (false, "Immersive date already used today for this couple.");
        }

        if (!this.mod.HeartsSystem.IsAtLeastHearts(this.mod.LocalPlayerId, this.targetPlayerId, this.mod.Config.ImmersiveDateMinHearts))
        {
            return (false, $"Requires at least {this.mod.Config.ImmersiveDateMinHearts} hearts.");
        }

        return (true, string.Empty);
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
        return this.HasPendingFromTarget(out _)
            ? (true, string.Empty)
            : (false, "No pending request from this player.");
    }

    private bool ResolvePendingFromTarget(bool accept, out string message)
    {
        if (this.mod.DatingSystem.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out long datingRequester)
            && datingRequester == this.targetPlayerId)
        {
            return this.mod.DatingSystem.RespondToPendingDatingLocal(accept, out message);
        }

        if (this.mod.MarriageSystem.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out long marriageRequester)
            && marriageRequester == this.targetPlayerId)
        {
            return this.mod.MarriageSystem.RespondToPendingMarriageLocal(accept, out message);
        }

        if (this.mod.PregnancySystem.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out long pregnancyRequester)
            && pregnancyRequester == this.targetPlayerId)
        {
            return this.mod.PregnancySystem.RespondTryForBabyFromLocal(accept, out message);
        }

        if (this.mod.CarrySystem.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out long carryRequester)
            && carryRequester == this.targetPlayerId)
        {
            return this.mod.CarrySystem.RespondToPendingCarryLocal(accept, out message);
        }

        if (this.mod.HoldingHandsSystem.TryGetPendingForPlayer(this.mod.LocalPlayerId, out long handsRequester)
            && handsRequester == this.targetPlayerId)
        {
            return this.mod.HoldingHandsSystem.RespondToPendingHoldingHandsLocal(accept, out message);
        }

        message = "No pending request from this player.";
        return false;
    }

    private bool HasPendingFromTarget(out long requesterId)
    {
        requesterId = -1;
        if (this.mod.DatingSystem.TryGetPendingDatingForPlayer(this.mod.LocalPlayerId, out _, out requesterId)
            && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.MarriageSystem.TryGetPendingMarriageForPlayer(this.mod.LocalPlayerId, out requesterId)
            && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.PregnancySystem.TryGetPendingTryForBabyForPlayer(this.mod.LocalPlayerId, out requesterId)
            && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.CarrySystem.TryGetPendingCarryForPlayer(this.mod.LocalPlayerId, out requesterId)
            && requesterId == this.targetPlayerId)
        {
            return true;
        }

        if (this.mod.HoldingHandsSystem.TryGetPendingForPlayer(this.mod.LocalPlayerId, out requesterId)
            && requesterId == this.targetPlayerId)
        {
            return true;
        }

        requesterId = -1;
        return false;
    }
}
