using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Systems;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayerRomance.UI
{
    public sealed class RomanceHubMenu : IClickableMenu
    {
        private readonly ModEntry mod;
        private readonly ClickableTextureComponent closeButton;

        // --- Player Selection ---
        private readonly List<long> playerIds = new();
        private long selectedPlayerId = -1;
        private ClickableTextureComponent prevPlayerButton;
        private ClickableTextureComponent nextPlayerButton;

        // --- Layout Panels ---
        private Rectangle topPanel;      // Status & Player Info
        private Rectangle leftPanel;     // Categories
        private Rectangle contentPanel;  // Action Grid

        // --- Categories & Actions ---
        private enum ActionCategory
        {
            Romance,
            Dates,
            Intimacy
        }

        private sealed class CategoryComponent
        {
            public string Label;
            public ActionCategory Category;
            public Rectangle Bounds;
        }

        private sealed class ActionButton
        {
            public string Label;
            public ActionCategory Category;
            public Func<(bool enabled, string disabledReason)> State;
            public Action Execute;
            public Rectangle Bounds; // Calculated dynamically
        }

        private readonly List<CategoryComponent> categories = new();
        private readonly List<ActionButton> allActions = new();
        private ActionCategory currentCategory = ActionCategory.Romance;

        private string hoverText = string.Empty;

        public RomanceHubMenu(ModEntry mod)
            : base(0, 0, 0, 0, true)
        {
            this.mod = mod;
            this.UpdateLayout();

            // Init buttons
            this.closeButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f);

            this.prevPlayerButton = new ClickableTextureComponent(
                Rectangle.Empty, Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 4f);
            this.nextPlayerButton = new ClickableTextureComponent(
                Rectangle.Empty, Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 4f);

            // Init Data
            this.InitializeCategories();
            this.BuildActions();
            this.RefreshPlayers();

            if (!this.mod.IsHostPlayer)
            {
                this.mod.NetSync.RequestSnapshotFromHost();
            }
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            this.UpdateLayout();
        }

        private void UpdateLayout()
        {
            // Responsive Size
            this.width = Math.Min(1000, Game1.uiViewport.Width - 64);
            this.height = Math.Min(750, Game1.uiViewport.Height - 64);
            this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
            this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

            // Reposition Close Button
            if (this.closeButton != null)
                this.closeButton.bounds = new Rectangle(this.xPositionOnScreen + this.width - 40, this.yPositionOnScreen - 20, 48, 48);

            int padding = 24;

            // 1. Top Panel (Player Status) - Fixed Height
            int topHeight = 200;
            this.topPanel = new Rectangle(
                this.xPositionOnScreen + padding,
                this.yPositionOnScreen + padding + 20,
                this.width - (padding * 2),
                topHeight);

            // Update Player Selector Arrows relative to Top Panel
            if (this.prevPlayerButton != null && this.nextPlayerButton != null)
            {
                int portraitSize = 128;
                int portraitX = this.topPanel.X + 32;
                int portraitY = this.topPanel.Y + (this.topPanel.Height - portraitSize) / 2;
                int arrowY = portraitY + (portraitSize / 2) - 22;

                this.prevPlayerButton.bounds = new Rectangle(portraitX - 48, arrowY, 48, 44);
                this.nextPlayerButton.bounds = new Rectangle(portraitX + portraitSize, arrowY, 48, 44);
            }

            // 2. Bottom Area (Split Left/Right)
            int bottomY = this.topPanel.Bottom + 16;
            int bottomHeight = (this.yPositionOnScreen + this.height) - bottomY - padding;

            // Left Panel (Categories)
            int leftWidth = 240;
            this.leftPanel = new Rectangle(
                this.topPanel.X,
                bottomY,
                leftWidth,
                bottomHeight);

            // Content Panel (Actions)
            this.contentPanel = new Rectangle(
                this.leftPanel.Right + 16,
                bottomY,
                this.topPanel.Width - leftWidth - 16,
                bottomHeight);

            // Update Category Button Rects
            int catH = 64;
            int catY = this.leftPanel.Y + 16;
            foreach (var cat in this.categories)
            {
                cat.Bounds = new Rectangle(this.leftPanel.X + 12, catY, this.leftPanel.Width - 24, catH);
                catY += catH + 12;
            }
        }

        private void InitializeCategories()
        {
            this.categories.Clear();
            this.categories.Add(new CategoryComponent { Label = "Romance", Category = ActionCategory.Romance });
            this.categories.Add(new CategoryComponent { Label = "Dates", Category = ActionCategory.Dates });
            this.categories.Add(new CategoryComponent { Label = "Intimacy", Category = ActionCategory.Intimacy });
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.closeButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                this.exitThisMenu();
                return;
            }

            // Player Cycle
            if (this.prevPlayerButton.containsPoint(x, y))
            {
                this.CyclePlayer(-1);
                Game1.playSound("shwip");
                return;
            }
            if (this.nextPlayerButton.containsPoint(x, y))
            {
                this.CyclePlayer(1);
                Game1.playSound("shwip");
                return;
            }

            // Actions Click
            var activeActions = this.allActions.Where(a => a.Category == this.currentCategory).ToList();
            foreach (var action in activeActions)
            {
                if (action.Bounds.Contains(x, y))
                {
                    (bool enabled, string reason) = action.State();
                    if (!enabled)
                    {
                        Game1.playSound("cancel");
                        this.mod.Notifier.NotifyWarn(reason, "[PR.UI]");
                        return;
                    }

                    Game1.playSound("smallSelect");
                    action.Execute();
                    return;
                }
            }
        }

        public override void performHoverAction(int x, int y)
        {
            this.hoverText = "";
            this.closeButton.tryHover(x, y);
            this.prevPlayerButton.tryHover(x, y);
            this.nextPlayerButton.tryHover(x, y);

            // Hover Category -> Switch View
            if (this.leftPanel.Contains(x, y))
            {
                foreach (var cat in this.categories)
                {
                    if (cat.Bounds.Contains(x, y))
                    {
                        this.currentCategory = cat.Category;
                        break;
                    }
                }
            }

            // Hover Action -> Tooltip
            var activeActions = this.allActions.Where(a => a.Category == this.currentCategory).ToList();
            foreach (var action in activeActions)
            {
                if (action.Bounds.Contains(x, y))
                {
                    (bool enabled, string reason) = action.State();
                    if (!enabled) this.hoverText = reason;
                    break;
                }
            }
        }

        public override void draw(SpriteBatch b)
        {
            // Dim background
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);

            // Main Background
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            this.closeButton.draw(b);

            // --- Draw Top Panel (Status) ---
            this.DrawTopPanel(b);

            // --- Draw Left Panel (Categories) ---
            IClickableMenu.drawTextureBox(b, this.leftPanel.X, this.leftPanel.Y, this.leftPanel.Width, this.leftPanel.Height, Color.White);
            foreach (var cat in this.categories)
            {
                bool isSelected = cat.Category == this.currentCategory;

                // Draw selection highlight
                if (isSelected)
                    IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(375, 357, 3, 3), cat.Bounds.X, cat.Bounds.Y, cat.Bounds.Width, cat.Bounds.Height, Color.White, 4f, false);

                // Draw Label
                Vector2 textSize = Game1.smallFont.MeasureString(cat.Label);
                Vector2 textPos = new Vector2(
                    cat.Bounds.X + (cat.Bounds.Width - textSize.X) / 2,
                    cat.Bounds.Y + (cat.Bounds.Height - textSize.Y) / 2);

                Color textColor = isSelected ? Game1.textColor : Game1.textShadowColor;
                Utility.drawTextWithShadow(b, cat.Label, Game1.smallFont, textPos, textColor);
            }

            // --- Draw Content Panel (Actions) ---
            this.DrawActionGrid(b);

            // Tooltip
            if (!string.IsNullOrEmpty(this.hoverText))
            {
                IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
            }

            this.drawMouse(b);
        }

        private void DrawTopPanel(SpriteBatch b)
        {
            // Panel Background (slightly darker/grouped)
            IClickableMenu.drawTextureBox(b, this.topPanel.X, this.topPanel.Y, this.topPanel.Width, this.topPanel.Height, Color.White);

            if (this.playerIds.Count == 0)
            {
                Utility.drawTextWithShadow(b, "No other players online.", Game1.dialogueFont, new Vector2(this.topPanel.X + 32, this.topPanel.Y + 32), Game1.textColor);
                return;
            }

            // Player Selection Controls
            this.prevPlayerButton.draw(b);
            this.nextPlayerButton.draw(b);

            // Portrait
            Farmer target = this.mod.FindFarmerById(this.selectedPlayerId, true);
            int portraitSize = 128;
            int portraitX = this.topPanel.X + 32;
            int portraitY = this.topPanel.Y + (this.topPanel.Height - portraitSize) / 2;

            // Draw Portrait background
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), portraitX, portraitY, portraitSize, portraitSize, Color.White, 4f, false);
            if (target != null)
            {
                target.FarmerRenderer.drawMiniPortrat(b, new Vector2(portraitX + 12, portraitY + 12), 0.0001f, 4f, 0, target);
            }

            // Info Text Area
            int infoX = portraitX + portraitSize + 64;
            int infoY = this.topPanel.Y + 24;
            string name = target?.Name ?? "Unknown";

            // Title / Name
            Utility.drawTextWithShadow(b, name, Game1.dialogueFont, new Vector2(infoX, infoY), Game1.textColor);

            // Relationship Stats
            RelationshipRecord relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.selectedPlayerId);
            string state = relation?.State.ToString() ?? "None";
            int hearts = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
            string session = this.GetActiveSessionText(this.selectedPlayerId);

            infoY += 50;
            DrawStatusLine(b, "Relationship:", state, infoX, ref infoY);
            DrawStatusLine(b, "Hearts:", $"{hearts}/{this.mod.Config.MaxHearts}", infoX, ref infoY);
            DrawStatusLine(b, "Status:", session, infoX, ref infoY);
        }

        private void DrawStatusLine(SpriteBatch b, string label, string value, int x, ref int y)
        {
            b.DrawString(Game1.smallFont, label, new Vector2(x, y), Game1.textShadowColor);
            b.DrawString(Game1.smallFont, value, new Vector2(x + 140, y), Game1.textColor);
            y += 30;
        }

        private void DrawActionGrid(SpriteBatch b)
        {
            var actions = this.allActions.Where(a => a.Category == this.currentCategory).ToList();

            // Grid Layout calculation
            int cols = 2;
            int gap = 12;
            int btnW = (this.contentPanel.Width - (gap * (cols + 1))) / cols;
            int btnH = 64;

            int currentX = this.contentPanel.X + gap;
            int currentY = this.contentPanel.Y + gap;

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];

                // Update bounds for hit detection
                action.Bounds = new Rectangle(currentX, currentY, btnW, btnH);

                // Draw Button
                (bool enabled, _) = action.State();
                float alpha = enabled ? 1f : 0.6f;
                Color color = enabled ? Color.White : Color.Gray;

                // Hover effect
                if (enabled && action.Bounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                    color = Color.Wheat;

                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), action.Bounds.X, action.Bounds.Y, action.Bounds.Width, action.Bounds.Height, color, 4f, false);

                // Center Text
                Vector2 textSize = Game1.smallFont.MeasureString(action.Label);
                // Simple wrapping or fitting
                float scale = 1f;
                if (textSize.X > action.Bounds.Width - 20) scale = (action.Bounds.Width - 20) / textSize.X;

                Vector2 textPos = new Vector2(
                    action.Bounds.X + (action.Bounds.Width - (textSize.X * scale)) / 2,
                    action.Bounds.Y + (action.Bounds.Height - (textSize.Y * scale)) / 2);

                Utility.drawTextWithShadow(b, action.Label, Game1.smallFont, textPos, Game1.textColor * alpha, scale);

                // Grid Flow
                if ((i + 1) % cols == 0)
                {
                    currentX = this.contentPanel.X + gap;
                    currentY += btnH + gap;
                }
                else
                {
                    currentX += btnW + gap;
                }
            }
        }

        // --- Logic Helpers ---

        private void CyclePlayer(int direction)
        {
            if (this.playerIds.Count <= 1) return;

            int index = this.playerIds.IndexOf(this.selectedPlayerId);
            if (index == -1) index = 0;

            index += direction;
            if (index >= this.playerIds.Count) index = 0;
            if (index < 0) index = this.playerIds.Count - 1;

            this.selectedPlayerId = this.playerIds[index];
        }

        private void RefreshPlayers()
        {
            this.playerIds.Clear();
            HashSet<long> unique = new();

            // Add Online Farmers
            foreach (Farmer farmer in Game1.getOnlineFarmers().Where(p => p.UniqueMultiplayerID != this.mod.LocalPlayerId))
            {
                if (unique.Add(farmer.UniqueMultiplayerID))
                    this.playerIds.Add(farmer.UniqueMultiplayerID);
            }

            // Add Connected Peers (in case not in onlineFarmers list yet)
            foreach (var peer in this.mod.Helper.Multiplayer.GetConnectedPlayers())
            {
                if (peer.PlayerID != this.mod.LocalPlayerId && unique.Add(peer.PlayerID))
                    this.playerIds.Add(peer.PlayerID);
            }

            // Ensure selection is valid
            if (this.playerIds.Count > 0 && !this.playerIds.Contains(this.selectedPlayerId))
            {
                this.selectedPlayerId = this.playerIds[0];
            }
        }

        private string GetActiveSessionText(long targetPlayerId)
        {
            if (this.mod.DateImmersionSystem.GetActivePublicState() is { IsActive: true } immersive
                && (immersive.PlayerAId == targetPlayerId || immersive.PlayerBId == targetPlayerId)
                && (immersive.PlayerAId == this.mod.LocalPlayerId || immersive.PlayerBId == this.mod.LocalPlayerId))
            {
                return $"Date ({immersive.Location})";
            }

            if (this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, targetPlayerId))
            {
                return "Holding hands";
            }

            return this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, targetPlayerId) ? "Being carried" : "None";
        }

        // --- Logic Actions State Checkers (Copied from original) ---

        private bool TryGetSelectedTarget(out Farmer? target)
        {
            target = null;
            if (this.playerIds.Count == 0) return false;
            target = this.mod.FindFarmerById(this.selectedPlayerId, true);
            return target != null || this.mod.IsPlayerOnline(this.selectedPlayerId);
        }

        private void BuildActions()
        {
            this.allActions.Clear();

            // Romance Category
            void Add(string label, ActionCategory cat, Func<(bool, string)> state, Action exec)
            {
                this.allActions.Add(new ActionButton { Label = label, Category = cat, State = state, Execute = exec });
            }

            Add("Dating Proposal", ActionCategory.Romance, this.GetDatingState, () => this.RunResult(this.mod.DatingSystem.RequestDatingFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));
            Add("Marriage Proposal", ActionCategory.Romance, this.GetMarriageState, () => this.RunResult(this.mod.MarriageSystem.RequestMarriageFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));
            Add("Try For Baby", ActionCategory.Romance, this.GetPregnancyState, () => this.RunResult(this.mod.PregnancySystem.RequestTryForBabyFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));

            // Dates Category
            Add("Start Date (Town)", ActionCategory.Dates, () => this.GetImmersiveDateState(ImmersiveDateLocation.Town), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Town, out string msg), msg, "[PR]"));
            Add("Date: Beach", ActionCategory.Dates, () => this.GetImmersiveDateState(ImmersiveDateLocation.Beach), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Beach, out string msg), msg, "[PR]"));
            Add("Date: Forest", ActionCategory.Dates, () => this.GetImmersiveDateState(ImmersiveDateLocation.Forest), () => this.RunResult(this.mod.DateImmersionSystem.StartImmersiveDateFromLocal(this.selectedPlayerId.ToString(), ImmersiveDateLocation.Forest, out string msg), msg, "[PR]"));
            Add("End Date", ActionCategory.Dates, this.GetEndImmersiveState, () => this.RunResult(this.mod.DateImmersionSystem.EndImmersiveDateFromLocal(out string msg), msg, "[PR]"));

            // Intimacy Category
            Add("Start Carry", ActionCategory.Intimacy, this.GetCarryState, () => this.RunResult(this.mod.CarrySystem.RequestCarryFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));
            Add("Stop Carry", ActionCategory.Intimacy, this.GetCarryStopState, () => this.RunResult(this.mod.CarrySystem.StopCarryFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));
            Add("Hold Hands", ActionCategory.Intimacy, this.GetHandsState, () => this.RunResult(this.mod.HoldingHandsSystem.RequestHoldingHandsFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));
            Add("Stop Hands", ActionCategory.Intimacy, this.GetHandsStopState, () => this.RunResult(this.mod.HoldingHandsSystem.StopHoldingHandsFromLocal(this.selectedPlayerId.ToString(), out string msg), msg, "[PR]"));
        }

        private void RunResult(bool success, string message, string category)
        {
            if (success) this.mod.Notifier.NotifyInfo(message, category);
            else this.mod.Notifier.NotifyWarn(message, category);
        }

        // --- State Checkers (Preserved Logic) ---

        private (bool, string) GetDatingState()
        {
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.selectedPlayerId);
            return relation != null && relation.State != RelationshipState.None ? (false, $"Already {relation.State}.") : (true, string.Empty);
        }

        private (bool, string) GetMarriageState()
        {
            if (!this.mod.Config.EnableMarriage) return (false, "Disabled.");
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.selectedPlayerId);
            if (relation == null) return (false, "Date first.");
            if (relation.State == RelationshipState.Married) return (false, "Married.");
            return relation.State == RelationshipState.Dating ? (true, string.Empty) : (false, "Date first.");
        }

        private (bool, string) GetPregnancyState()
        {
            if (!this.mod.Config.EnablePregnancy) return (false, "Disabled.");
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            return this.mod.MarriageSystem.IsMarried(this.mod.LocalPlayerId, this.selectedPlayerId) ? (true, string.Empty) : (false, "Marriage required.");
        }

        private (bool, string) GetCarryState()
        {
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            return this.mod.CarrySystem.CanRequestCarry(this.mod.LocalPlayerId, this.selectedPlayerId, out string reason) ? (true, string.Empty) : (false, reason);
        }

        private (bool, string) GetCarryStopState()
        {
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            return this.mod.CarrySystem.IsCarryActiveBetween(this.mod.LocalPlayerId, this.selectedPlayerId) ? (true, string.Empty) : (false, "Not carrying.");
        }

        private (bool, string) GetHandsState()
        {
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            return this.mod.HoldingHandsSystem.CanRequestHands(this.mod.LocalPlayerId, this.selectedPlayerId, out string reason) ? (true, string.Empty) : (false, reason);
        }

        private (bool, string) GetHandsStopState()
        {
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            return this.mod.HoldingHandsSystem.IsHandsActiveBetween(this.mod.LocalPlayerId, this.selectedPlayerId) ? (true, string.Empty) : (false, "Not holding hands.");
        }

        private (bool, string) GetImmersiveDateState(ImmersiveDateLocation location)
        {
            if (!this.mod.Config.EnableImmersiveDates) return (false, "Disabled.");
            if (!this.TryGetSelectedTarget(out _)) return (false, "Select player.");
            RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.selectedPlayerId);
            if (relation == null || relation.State == RelationshipState.None) return (false, "Relationship required.");
            if (!relation.CanStartImmersiveDateToday(this.mod.GetCurrentDayNumber())) return (false, "Used today.");

            int req = this.mod.DateImmersionSystem.GetRequiredHeartsForLocation(location);
            if (!this.mod.HeartsSystem.IsAtLeastHearts(this.mod.LocalPlayerId, this.selectedPlayerId, req)) return (false, $"{req}+ hearts required.");

            return this.mod.DateImmersionSystem.IsActive ? (false, "Date active.") : (true, string.Empty);
        }

        private (bool, string) GetEndImmersiveState()
        {
            var state = this.mod.DateImmersionSystem.GetActivePublicState();
            if (state == null || !state.IsActive) return (false, "No date.");
            return state.PlayerAId == this.mod.LocalPlayerId || state.PlayerBId == this.mod.LocalPlayerId ? (true, string.Empty) : (false, "Not your date.");
        }
    }
}