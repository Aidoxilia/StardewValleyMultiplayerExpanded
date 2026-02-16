using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class RomanceMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private readonly ClickableTextureComponent closeButton;

    public RomanceMenu(ModEntry mod)
        : base(
            Game1.uiViewport.Width / 2 - 420,
            Game1.uiViewport.Height / 2 - 300,
            840,
            600,
            true)
    {
        this.mod = mod;
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 60, this.yPositionOnScreen + 16, 44, 44),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f);
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

        int x = this.xPositionOnScreen + 32;
        int y = this.yPositionOnScreen + 32;
        b.DrawString(Game1.dialogueFont, "Player Romance", new Vector2(x, y), Color.Black);
        y += 64;

        b.DrawString(Game1.smallFont, "Commands:", new Vector2(x, y), Color.DarkSlateGray);
        y += 36;
        b.DrawString(Game1.smallFont, "pr.propose <player>", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.accept / pr.reject", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.marry.propose <player>", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.pregnancy.optin <player> [on/off]", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.carry.request <player>", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.hands.request <player>", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.date.immersive.start <player> <town|beach|forest>", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "Press F7 for Romance Hub menu", new Vector2(x + 24, y), Color.Black);
        y += 28;
        b.DrawString(Game1.smallFont, "pr.worker.runonce", new Vector2(x + 24, y), Color.Black);
        y += 56;

        b.DrawString(Game1.smallFont, "Current relationships:", new Vector2(x, y), Color.DarkSlateGray);
        y += 34;
        foreach (Data.RelationshipRecord relation in this.mod.DatingSystem.GetRelationshipsForPlayer(this.mod.LocalPlayerId))
        {
            string line = $"{relation.GetOtherName(this.mod.LocalPlayerId)} - {relation.State}";
            b.DrawString(Game1.smallFont, line, new Vector2(x + 24, y), Color.Black);
            y += 28;
        }

        this.drawMouse(b);
    }
}
