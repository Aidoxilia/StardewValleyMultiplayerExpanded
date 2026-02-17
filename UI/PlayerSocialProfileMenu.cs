using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class PlayerSocialProfileMenu : IClickableMenu
{
    private static readonly Rectangle EmptyHeartSource = new(211, 428, 7, 6);
    private static readonly Rectangle FullHeartSource = new(218, 428, 7, 6);

    private readonly ModEntry mod;
    private readonly long targetPlayerId;
    private readonly ClickableTextureComponent closeButton;

    public PlayerSocialProfileMenu(ModEntry mod, long targetPlayerId)
        : base(
            Game1.uiViewport.Width / 2 - 440,
            Game1.uiViewport.Height / 2 - 280,
            880,
            560,
            showUpperRightCloseButton: false)
    {
        this.mod = mod;
        this.targetPlayerId = targetPlayerId;
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 58, this.yPositionOnScreen + 14, 44, 44),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.6f);
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = null;
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        this.drawBackground(b);
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        Farmer? target = this.mod.FindFarmerById(this.targetPlayerId, includeOffline: true);
        if (target is null)
        {
            b.DrawString(Game1.dialogueFont, "Player unavailable", new Vector2(this.xPositionOnScreen + 30, this.yPositionOnScreen + 30), Color.Black);
            this.drawMouse(b);
            return;
        }

        RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, this.targetPlayerId);
        string status = relation?.State switch
        {
            RelationshipState.Dating or RelationshipState.Engaged => "In relationship",
            RelationshipState.Married => "Married",
            _ => "Single"
        };
        int hearts = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
        int maxHearts = Math.Max(1, this.mod.Config.MaxHearts);

        int left = this.xPositionOnScreen + 32;
        int top = this.yPositionOnScreen + 24;

        b.DrawString(Game1.dialogueFont, target.Name, new Vector2(left + 132, top + 8), Color.Black);
        b.DrawString(Game1.smallFont, status, new Vector2(left + 132, top + 52), Color.DarkSlateGray);

        target.FarmerRenderer.drawMiniPortrat(
            b,
            new Vector2(left + 24, top + 18),
            0.0001f,
            4f,
            0,
            target);

        for (int i = 0; i < Math.Min(maxHearts, 14); i++)
        {
            Rectangle src = i < hearts ? FullHeartSource : EmptyHeartSource;
            b.Draw(Game1.mouseCursors, new Vector2(left + 132 + i * 18, top + 84), src, Color.White, 0f, Vector2.Zero, 2.1f, SpriteEffects.None, 1f);
        }

        this.DrawSection(b, "Gift Log", "- No gift history yet.", left, top + 150);
        this.DrawSection(b, "Favorites", "- Favorite gifts not configured yet.", left, top + 240);
        this.DrawSection(b, "Birthday", "- Unknown", left, top + 330);

        this.drawMouse(b);
    }

    private void DrawSection(SpriteBatch b, string title, string value, int x, int y)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            x,
            y,
            this.width - 64,
            74,
            Color.White,
            4f,
            false);
        b.DrawString(Game1.smallFont, title, new Vector2(x + 14, y + 12), Color.Black);
        b.DrawString(Game1.smallFont, value, new Vector2(x + 14, y + 38), Color.DarkSlateGray);
    }
}
