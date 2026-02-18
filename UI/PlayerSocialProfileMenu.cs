using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using PlayerRomance.Data;

namespace PlayerRomance.UI
{
    public sealed class PlayerSocialProfileMenu : IClickableMenu
    {
        private static readonly Rectangle EmptyHeartSource = new(211, 428, 7, 6);
        private static readonly Rectangle FullHeartSource = new(218, 428, 7, 6);

        private const int PROFILE_WIDTH = 800;
        private const int PROFILE_HEIGHT = 600;
        private const int PORTRAIT_SIZE = 128;

        private readonly ModEntry mod;
        private readonly long targetPlayerId;
        private ClickableTextureComponent closeButton;

        private string hoverText = "";
        private Farmer? targetFarmer;
        private RelationshipRecord? relationship;

        private Rectangle portraitArea;
        private Rectangle infoArea;
        private Rectangle giftLogArea;

        public PlayerSocialProfileMenu(ModEntry mod, long targetPlayerId)
        {
            this.mod = mod;
            this.targetPlayerId = targetPlayerId;

            this.targetFarmer = this.mod.FindFarmerById(this.targetPlayerId, includeOffline: true);
            this.relationship = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);

            this.UpdateLayout();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.UpdateLayout();
        }

        private void UpdateLayout()
        {
            this.width = Math.Min(PROFILE_WIDTH, Game1.uiViewport.Width - 64);
            this.height = Math.Min(PROFILE_HEIGHT, Game1.uiViewport.Height - 64);
            this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
            this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

            this.closeButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f);

            this.portraitArea = new Rectangle(this.xPositionOnScreen + 48, this.yPositionOnScreen + 48, PORTRAIT_SIZE, PORTRAIT_SIZE + 32);
            this.infoArea = new Rectangle(this.portraitArea.Right + 32, this.yPositionOnScreen + 48, this.width - 250, 200);
            this.giftLogArea = new Rectangle(this.xPositionOnScreen + 32, this.yPositionOnScreen + 250, this.width - 64, this.height - 280);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.closeButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                this.exitThisMenu();
                return;
            }
            base.receiveLeftClick(x, y, playSound);
        }

        public override void performHoverAction(int x, int y)
        {
            this.hoverText = "";
            this.closeButton.tryHover(x, y);
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);

            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            if (this.targetFarmer is null)
            {
                SpriteText.drawStringHorizontallyCenteredAt(b, "Player not found", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen + this.height / 2);
                this.closeButton.draw(b);
                this.drawMouse(b);
                return;
            }

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                this.portraitArea.X - 12, this.portraitArea.Y - 12, this.portraitArea.Width + 24, this.portraitArea.Height + 24, Color.White, 4f, false);

            this.targetFarmer.FarmerRenderer.drawMiniPortrat(b,
                new Vector2(this.portraitArea.X, this.portraitArea.Y),
                0.0001f, 4f, 0, this.targetFarmer);

            int textX = this.infoArea.X;
            int textY = this.infoArea.Y;

            // Fix for Stardew 1.6+ SpriteText signature (passing null for color)
            SpriteText.drawString(b, this.targetFarmer.Name, textX, textY - 10, 999, -1, 999, 1f, 0.88f, false, -1, "", null);

            string status = this.GetRelationshipStatusLabel();
            b.DrawString(Game1.smallFont, status, new Vector2(textX + 4, textY + 48), Game1.textColor);

            string birthday = "Birthday: ???";
            b.DrawString(Game1.smallFont, birthday, new Vector2(textX + 4, textY + 78), Game1.textShadowColor);

            this.DrawHearts(b, textX, textY + 110);
            this.DrawGiftLogSection(b);

            this.closeButton.draw(b);
            if (!string.IsNullOrEmpty(this.hoverText))
            {
                IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
            }
            this.drawMouse(b);
        }

        private void DrawHearts(SpriteBatch b, int x, int y)
        {
            int hearts = this.relationship?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
            int maxHearts = Math.Max(10, this.mod.Config.MaxHearts);

            for (int i = 0; i < maxHearts; i++)
            {
                int col = i % 10;
                int row = i / 10;

                Vector2 pos = new Vector2(x + (col * 32), y + (row * 32));

                bool hover = new Rectangle((int)pos.X, (int)pos.Y, 28, 28).Contains(Game1.getMouseX(), Game1.getMouseY());
                if (hover)
                {
                    pos.X += Game1.random.Next(-1, 2);
                    pos.Y += Game1.random.Next(-1, 2);
                    this.hoverText = $"{hearts} / {maxHearts} Hearts";
                }

                Rectangle src = i < hearts ? FullHeartSource : EmptyHeartSource;
                b.Draw(Game1.mouseCursors, pos, src, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.8f);
            }
        }

        private void DrawGiftLogSection(SpriteBatch b)
        {
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6),
                this.giftLogArea.X, this.giftLogArea.Y, this.giftLogArea.Width, 4, Color.White * 0.5f, 4f, false);

            Vector2 titlePos = new Vector2(this.giftLogArea.X + 16, this.giftLogArea.Y + 16);
            Utility.drawTextWithShadow(b, "Loved Gifts", Game1.dialogueFont, titlePos, Game1.textColor);

            int startX = (int)titlePos.X;
            int startY = (int)titlePos.Y + 50;

            for (int i = 0; i < 5; i++)
            {
                Rectangle slotBounds = new Rectangle(startX + (i * 72), startY, 64, 64);
                b.Draw(Game1.menuTexture, slotBounds, new Rectangle(128, 128, 64, 64), Color.White);
                b.DrawString(Game1.tinyFont, "?", new Vector2(slotBounds.Center.X - 4, slotBounds.Center.Y - 10), Color.Gray * 0.5f);
            }

            b.DrawString(Game1.smallFont, "Gift history will appear here.",
                new Vector2(startX, startY + 80), Color.DarkGray);
        }

        private string GetRelationshipStatusLabel()
        {
            if (this.relationship == null) return "Single";

            return this.relationship.State switch
            {
                RelationshipState.Married => "Married",
                RelationshipState.Engaged => "Engaged",
                RelationshipState.Dating => "Dating",
                _ => "Single"
            };
        }
    }
}