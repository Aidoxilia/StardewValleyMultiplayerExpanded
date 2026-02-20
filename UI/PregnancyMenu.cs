using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class PregnancyMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private ClickableTextureComponent closeButton = null!;

    public PregnancyMenu(ModEntry mod)
    {
        this.mod = mod;
        this.UpdateLayout();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.UpdateLayout();
    }

    private void UpdateLayout()
    {
        this.width = Math.Min(760, Game1.uiViewport.Width - 64);
        this.height = Math.Min(540, Game1.uiViewport.Height - 64);
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 52, this.yPositionOnScreen + 12, 44, 44),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.5f);
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
        this.closeButton.tryHover(x, y);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

        SpriteText.drawStringHorizontallyCenteredAt(
            b,
            "Pregnancy Status",
            this.xPositionOnScreen + this.width / 2,
            this.yPositionOnScreen + 28);

        this.DrawContent(b);

        this.closeButton.draw(b);
        this.drawMouse(b);
    }

    private void DrawContent(SpriteBatch b)
    {
        int baseX = this.xPositionOnScreen + 36;
        int lineY = this.yPositionOnScreen + 92;

        if (!this.mod.Config.EnablePregnancy)
        {
            Utility.drawTextWithShadow(b, "Pregnancy system is disabled in config.", Game1.dialogueFont, new Vector2(baseX, lineY), Color.Gray);
            return;
        }

        if (!this.mod.PregnancySystem.TryGetActivePregnancyForPlayer(this.mod.LocalPlayerId, out PregnancyRecord? active) || active is null)
        {
            Utility.drawTextWithShadow(b, "No active pregnancy for your couple.", Game1.dialogueFont, new Vector2(baseX, lineY), Game1.textColor);
            lineY += 56;
            Utility.drawTextWithShadow(b, "Use Try For Baby from the interaction menus.", Game1.smallFont, new Vector2(baseX, lineY), Game1.textShadowColor);
            return;
        }

        long partnerId = active.ParentAId == this.mod.LocalPlayerId ? active.ParentBId : active.ParentAId;
        string partnerName = this.mod.FindFarmerById(partnerId, includeOffline: true)?.Name
            ?? (active.ParentAId == this.mod.LocalPlayerId ? active.ParentBName : active.ParentAName);

        string carrierName = this.mod.FindFarmerById(active.PregnantPlayerId, includeOffline: true)?.Name
            ?? (active.PregnantPlayerId == active.ParentAId ? active.ParentAName : active.ParentBName);

        float progress = this.mod.PregnancySystem.GetPregnancyProgress01(this.mod.LocalPlayerId);
        int barWidth = this.width - 72;
        int barHeight = 28;

        Utility.drawTextWithShadow(b, $"Partner: {partnerName}", Game1.dialogueFont, new Vector2(baseX, lineY), Game1.textColor);
        lineY += 48;
        Utility.drawTextWithShadow(b, $"Pregnant player: {carrierName}", Game1.smallFont, new Vector2(baseX, lineY), Game1.textColor);
        lineY += 36;
        Utility.drawTextWithShadow(
            b,
            $"Day {Math.Max(1, active.CurrentPregnancyDay)}/{Math.Max(1, active.PregnancyDurationDays)} ({active.DaysRemaining} day(s) remaining)",
            Game1.smallFont,
            new Vector2(baseX, lineY),
            Game1.textColor);
        lineY += 34;
        Utility.drawTextWithShadow(b, this.mod.PregnancySystem.GetPregnancyStageText(this.mod.LocalPlayerId), Game1.smallFont, new Vector2(baseX, lineY), Game1.textShadowColor);
        lineY += 44;

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), baseX, lineY, barWidth, barHeight, Color.White, 4f, false);
        if (progress > 0f)
        {
            b.Draw(
                Game1.staminaRect,
                new Rectangle(baseX + 4, lineY + 4, Math.Max(1, (int)((barWidth - 8) * Math.Clamp(progress, 0f, 1f))), barHeight - 8),
                Color.Pink * 0.8f);
        }

        lineY += 42;
        Utility.drawTextWithShadow(b, "Belly overlay is shown automatically during active pregnancy.", Game1.tinyFont, new Vector2(baseX, lineY), Color.Gray);
    }
}
