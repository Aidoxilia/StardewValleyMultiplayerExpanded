using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using PlayerRomance.Data;

namespace PlayerRomance.UI
{
    public sealed class ChildrenManagementMenu : IClickableMenu
    {
        private readonly ModEntry mod;
        private readonly List<ChildRecord> children;
        private ClickableTextureComponent closeButton;

        // List of child rows
        private readonly List<ChildRowComponent> childRows = new();

        private string hoverText = "";

        // Design Constants (Increased spacing)
        private const int ROW_HEIGHT = 128; // Increased from 112
        private const int ROW_PADDING = 16; // Padding between rows
        private const int MAX_WIDTH = 950;  // Slightly wider
        private const int MAX_HEIGHT = 750;

        public ChildrenManagementMenu(ModEntry mod)
        {
            this.mod = mod;

            // Load children once
            this.children = this.GetChildrenForLocal()
                .OrderByDescending(c => c.AgeYears)
                .ThenBy(c => c.ChildName)
                .ToList();

            // Initialize layout
            this.UpdateLayout();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.UpdateLayout();
        }

        private void UpdateLayout()
        {
            // 1. Calculate menu size based on screen
            this.width = Math.Min(MAX_WIDTH, Game1.uiViewport.Width - 64);
            this.height = Math.Min(MAX_HEIGHT, Game1.uiViewport.Height - 64);

            // 2. Center the menu
            this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
            this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

            // 3. Recreate close button
            this.closeButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f);

            // 4. Rebuild child rows
            this.childRows.Clear();

            int contentTopY = this.yPositionOnScreen + 110; // Increased top margin for title
            int availableWidth = this.width - 64;
            int rowX = this.xPositionOnScreen + 32;

            int maxRowsVisible = (this.height - 130) / (ROW_HEIGHT + ROW_PADDING);
            int rowsToDraw = Math.Min(this.children.Count, maxRowsVisible);

            for (int i = 0; i < rowsToDraw; i++)
            {
                ChildRecord child = this.children[i];
                int currentY = contentTopY + (i * (ROW_HEIGHT + ROW_PADDING));

                // Assignment Button (Work)
                ClickableTextureComponent? workBtn = null;
                bool canWork = child.AgeYears >= Math.Max(16, this.mod.Config.AdultWorkMinAge);

                if (canWork)
                {
                    int btnSize = 64;
                    int btnX = rowX + availableWidth - btnSize - 24; // More padding from right edge
                    int btnY = currentY + (ROW_HEIGHT - btnSize) / 2;

                    workBtn = new ClickableTextureComponent(
                        new Rectangle(btnX, btnY, btnSize, btnSize),
                        Game1.mouseCursors,
                        new Rectangle(366, 373, 16, 16),
                        4f
                    )
                    {
                        myID = child.ChildId.GetHashCode(),
                        hoverText = "Assign Task"
                    };
                }

                Rectangle rowBounds = new Rectangle(rowX, currentY, availableWidth, ROW_HEIGHT);
                this.childRows.Add(new ChildRowComponent(child, workBtn, rowBounds));
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.closeButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                this.exitThisMenu();
                return;
            }

            foreach (var row in this.childRows)
            {
                if (row.WorkButton != null && row.WorkButton.containsPoint(x, y))
                {
                    Game1.playSound("smallSelect");
                    Game1.activeClickableMenu = new ChildTaskAssignmentMenu(this.mod, row.Data.ChildId, row.Data.ChildName);
                    return;
                }
            }

            base.receiveLeftClick(x, y, playSound);
        }

        public override void performHoverAction(int x, int y)
        {
            this.hoverText = "";
            this.closeButton.tryHover(x, y);

            foreach (var row in this.childRows)
            {
                if (row.WorkButton != null)
                {
                    row.WorkButton.tryHover(x, y);
                    if (row.WorkButton.containsPoint(x, y))
                    {
                        this.hoverText = row.WorkButton.hoverText;
                    }
                }

                // Hover logic for icons
                int iconsStartX = row.Bounds.X + (int)(row.Bounds.Width * 0.45f); // Adjusted start position
                int iconsY = row.Bounds.Y + 32; // Adjusted Y

                // Food Icon
                if (new Rectangle(iconsStartX, iconsY, 32, 32).Contains(x, y))
                    this.hoverText = row.Data.IsFedToday ? "Well Fed" : "Hungry!";

                // Care Icon
                if (new Rectangle(iconsStartX + 64, iconsY, 32, 32).Contains(x, y)) // Increased spacing between icons (48 -> 64)
                    this.hoverText = row.Data.IsCaredToday ? "Felt loved today" : "Needs attention";

                // Play Icon
                if (new Rectangle(iconsStartX + 128, iconsY, 32, 32).Contains(x, y)) // Increased spacing
                    this.hoverText = row.Data.IsPlayedToday ? "Had fun today" : "Bored";
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);

            // Background
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            // Title
            string title = "Children Management";
            SpriteText.drawStringHorizontallyCenteredAt(b, title, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen + 35);

            // Column Headers
            int colY = this.yPositionOnScreen + 85;
            b.DrawString(Game1.smallFont, "Identity", new Vector2(this.xPositionOnScreen + 64, colY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Needs & Education", new Vector2(this.xPositionOnScreen + (this.width * 0.45f), colY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Action", new Vector2(this.xPositionOnScreen + (this.width - 160), colY), Game1.textColor);

            foreach (var row in this.childRows)
            {
                this.DrawChildRow(b, row);
            }

            if (this.children.Count == 0)
            {
                string emptyMsg = "No children to manage yet.";
                Vector2 size = Game1.smallFont.MeasureString(emptyMsg);
                b.DrawString(Game1.smallFont, emptyMsg,
                    new Vector2(this.xPositionOnScreen + (this.width - size.X) / 2, this.yPositionOnScreen + 200),
                    Color.Gray);
            }

            this.closeButton.draw(b);

            if (!string.IsNullOrEmpty(this.hoverText))
            {
                IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
            }

            this.drawMouse(b);
        }

        private void DrawChildRow(SpriteBatch b, ChildRowComponent row)
        {
            // Row Background
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                row.Bounds.X, row.Bounds.Y, row.Bounds.Width, row.Bounds.Height, Color.White, 4f, false);

            // 1. Portrait (Left)
            NPC npcChild = Game1.getCharacterFromName(row.Data.ChildName);
            Vector2 portraitPos = new Vector2(row.Bounds.X + 28, row.Bounds.Y + 28); // Adjusted padding

            if (npcChild != null)
            {
                b.Draw(npcChild.Sprite.Texture,
                    portraitPos,
                    new Rectangle(0, 0, 16, 24),
                    Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
            }
            else
            {
                b.Draw(Game1.mouseCursors, portraitPos, new Rectangle(896, 336, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
            }

            // 2. Info Text (Left + Offset)
            int textStartX = row.Bounds.X + 110; // Increased offset
            b.DrawString(Game1.dialogueFont, row.Data.ChildName, new Vector2(textStartX, row.Bounds.Y + 24), Game1.textColor);
            string ageString = $"{row.Data.AgeYears} year(s) (Stage: {row.Data.Stage})";
            b.DrawString(Game1.smallFont, ageString, new Vector2(textStartX, row.Bounds.Y + 70), Game1.textShadowColor);

            // 3. Status Icons (Middle - Relative 45%)
            int statusStartX = row.Bounds.X + (int)(row.Bounds.Width * 0.45f);
            int iconY = row.Bounds.Y + 32;

            int iconSpacing = 64; // Increased spacing
            DrawStatusIcon(b, 1, row.Data.IsFedToday, statusStartX, iconY);
            DrawStatusIcon(b, 2, row.Data.IsCaredToday, statusStartX + iconSpacing, iconY);
            DrawStatusIcon(b, 3, row.Data.IsPlayedToday, statusStartX + (iconSpacing * 2), iconY);

            // 4. Progress Bar (Below Icons)
            int barWidth = (int)(row.Bounds.Width * 0.30f); // Slightly wider bar
            int barX = statusStartX;
            int barY = row.Bounds.Y + 85; // Lowered Y position
            int barHeight = 24;

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6), barX, barY, barWidth, barHeight, Color.White, 4f, false);

            float progress = Math.Clamp(row.Data.FeedingProgress / 100f, 0f, 1f);
            if (progress > 0)
            {
                Color barColor = progress > 0.8f ? Color.LimeGreen : (progress > 0.4f ? Color.Orange : Color.Red);
                b.Draw(Game1.staminaRect, new Rectangle(barX + 4, barY + 4, (int)((barWidth - 8) * progress), barHeight - 8), barColor);
            }
            Utility.drawTextWithShadow(b, "Education", Game1.tinyFont, new Vector2(barX + barWidth / 2 - 25, barY + 4), Game1.textColor);

            // 5. Action Button
            if (row.WorkButton != null)
            {
                row.WorkButton.draw(b);
            }
            else
            {
                Vector2 textSize = Game1.tinyFont.MeasureString("Too young");
                b.DrawString(Game1.tinyFont, "Too young",
                    new Vector2(row.Bounds.Right - textSize.X - 40, row.Bounds.Center.Y - (textSize.Y / 2)),
                    Color.Gray);
            }
        }

        private void DrawStatusIcon(SpriteBatch b, int type, bool isActive, int x, int y)
        {
            Color c = isActive ? Color.White : Color.Black * 0.3f;

            if (type == 1) // Food
                b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(182, 383, 16, 16), c, 0f, Vector2.Zero, 2.5f, SpriteEffects.None, 0.9f);
            else if (type == 2) // Heart
                b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(211, 428, 7, 6), c, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0.9f);
            else if (type == 3) // Fun
                b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(20, 428, 10, 10), c, 0f, Vector2.Zero, 3.5f, SpriteEffects.None, 0.9f);
        }

        private IEnumerable<ChildRecord> GetChildrenForLocal()
        {
            return this.mod.IsHostPlayer
                ? this.mod.HostSaveData.Children.Values
                : this.mod.ClientSnapshot.Children;
        }

        private class ChildRowComponent
        {
            public ChildRecord Data { get; }
            public ClickableTextureComponent? WorkButton { get; }
            public Rectangle Bounds { get; }

            public ChildRowComponent(ChildRecord data, ClickableTextureComponent? btn, Rectangle bounds)
            {
                this.Data = data;
                this.WorkButton = btn;
                this.Bounds = bounds;
            }
        }
    }
}