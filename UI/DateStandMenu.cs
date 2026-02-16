using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Systems;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class DateStandMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private readonly DateImmersionSystem dateSystem;
    private readonly DateStandType standType;
    private readonly List<StandOfferDefinition> offers;
    private readonly ClickableTextureComponent closeButton;
    private readonly List<ClickableComponent> buyButtons = new();
    private readonly List<ClickableComponent> offerButtons = new();

    public DateStandMenu(ModEntry mod, DateImmersionSystem dateSystem, DateStandType standType)
        : base(
            Game1.uiViewport.Width / 2 - 360,
            Game1.uiViewport.Height / 2 - 210,
            720,
            420,
            showUpperRightCloseButton: false)
    {
        this.mod = mod;
        this.dateSystem = dateSystem;
        this.standType = standType;
        this.offers = this.dateSystem.GetStandOffers(standType).ToList();
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 54, this.yPositionOnScreen + 12, 40, 40),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.2f);
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

        for (int i = 0; i < this.offers.Count; i++)
        {
            StandOfferDefinition offer = this.offers[i];
            if (this.buyButtons[i].containsPoint(x, y))
            {
                this.RunBuyAction(offer, offerToPartner: false);
                return;
            }

            if (this.offerButtons[i].containsPoint(x, y))
            {
                this.RunBuyAction(offer, offerToPartner: true);
                return;
            }
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        int x = this.xPositionOnScreen + 24;
        int y = this.yPositionOnScreen + 20;
        b.DrawString(Game1.dialogueFont, $"{this.standType} Stand", new Vector2(x, y), Color.Black);
        y += 44;

        b.DrawString(Game1.smallFont, "Buy for yourself or offer to your partner.", new Vector2(x, y), Color.DarkSlateGray);
        y += 36;

        for (int i = 0; i < this.offers.Count; i++)
        {
            StandOfferDefinition offer = this.offers[i];
            Rectangle row = new(this.xPositionOnScreen + 20, y + i * 90, this.width - 40, 80);
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                row.X,
                row.Y,
                row.Width,
                row.Height,
                Color.White,
                4f,
                false);
            b.DrawString(Game1.smallFont, $"{offer.DisplayName} - {offer.Price}g", new Vector2(row.X + 14, row.Y + 12), Color.Black);
            b.DrawString(Game1.smallFont, $"Offer bonus: +{offer.HeartDeltaOnOffer}", new Vector2(row.X + 14, row.Y + 42), Color.DarkSlateBlue);
            this.DrawButton(b, this.buyButtons[i], "Buy");
            this.DrawButton(b, this.offerButtons[i], "Offer");
        }

        this.drawMouse(b);
    }

    private void BuildButtons()
    {
        int y = this.yPositionOnScreen + 100;
        for (int i = 0; i < this.offers.Count; i++)
        {
            int rowY = y + i * 90;
            this.buyButtons.Add(new ClickableComponent(new Rectangle(this.xPositionOnScreen + 440, rowY + 10, 110, 58), $"buy_{i}"));
            this.offerButtons.Add(new ClickableComponent(new Rectangle(this.xPositionOnScreen + 560, rowY + 10, 130, 58), $"offer_{i}"));
        }
    }

    private void DrawButton(SpriteBatch b, ClickableComponent button, string label)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            button.bounds.X,
            button.bounds.Y,
            button.bounds.Width,
            button.bounds.Height,
            Color.White,
            4f,
            false);
        b.DrawString(Game1.smallFont, label, new Vector2(button.bounds.X + 18, button.bounds.Y + 18), Color.Black);
    }

    private void RunBuyAction(StandOfferDefinition offer, bool offerToPartner)
    {
        bool ok = this.dateSystem.RequestStandPurchaseFromLocal(this.standType, offer.ItemId, offerToPartner, out string msg);
        if (ok)
        {
            this.mod.Notifier.NotifyInfo(msg, "[PR.System.DateImmersion]");
        }
        else
        {
            this.mod.Notifier.NotifyWarn(msg, "[PR.System.DateImmersion]");
        }
    }
}
