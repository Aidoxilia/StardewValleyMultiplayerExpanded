using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class ChildFeedInventoryMenu : IClickableMenu
{
    private readonly string childId;
    private readonly string childName;
    private readonly Action<string, string?> onPickItem;
    private readonly List<(int index, Item item, ClickableComponent button)> rows = new();
    private readonly ClickableComponent cancelButton;

    public ChildFeedInventoryMenu(ModEntry mod, string childId, string childName, Action<string, string?> onPickItem)
        : base(
            Game1.uiViewport.Width / 2 - 340,
            Game1.uiViewport.Height / 2 - 220,
            680,
            440,
            showUpperRightCloseButton: false)
    {
        this.childId = childId;
        this.childName = string.IsNullOrWhiteSpace(childName) ? "Votre enfant" : childName;
        this.onPickItem = onPickItem;

        this.cancelButton = new ClickableComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 170, this.yPositionOnScreen + this.height - 72, 140, 48),
            "cancel");

        int rowY = this.yPositionOnScreen + 112;
        for (int i = 0; i < Game1.player.Items.Count; i++)
        {
            Item? item = Game1.player.Items[i];
            if (item is null || item.Stack <= 0 || !mod.ChildGrowthSystem.IsValidFoodItem(item))
            {
                continue;
            }

            this.rows.Add((
                i,
                item,
                new ClickableComponent(new Rectangle(this.xPositionOnScreen + 28, rowY, this.width - 56, 52), $"item_{i}")));

            rowY += 58;
            if (rowY > this.yPositionOnScreen + this.height - 140)
            {
                break;
            }
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.cancelButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            this.onPickItem(this.childId, null);
            Game1.activeClickableMenu = null;
            return;
        }

        foreach ((int index, Item item, ClickableComponent button) row in this.rows)
        {
            if (!row.button.containsPoint(x, y))
            {
                continue;
            }

            Game1.playSound("smallSelect");
            this.onPickItem(this.childId, row.item.QualifiedItemId);
            Game1.activeClickableMenu = null;
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
    {
        if (Game1.options.menuButton.Contains(new InputButton(key)) || Game1.options.cancelButton.Contains(new InputButton(key)))
        {
            Game1.playSound("bigDeSelect");
            this.onPickItem(this.childId, null);
            Game1.activeClickableMenu = null;
            return;
        }

        base.receiveKeyPress(key);
    }

    public override void draw(SpriteBatch b)
    {
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        b.DrawString(Game1.dialogueFont, $"Donner à manger - {this.childName}", new Vector2(this.xPositionOnScreen + 24, this.yPositionOnScreen + 20), Color.Black);
        b.DrawString(Game1.smallFont, "Cliquez sur une nourriture dans votre inventaire.", new Vector2(this.xPositionOnScreen + 26, this.yPositionOnScreen + 70), Color.DarkSlateGray);

        if (this.rows.Count == 0)
        {
            b.DrawString(Game1.smallFont, "Inventaire nourriture vide.", new Vector2(this.xPositionOnScreen + 30, this.yPositionOnScreen + 130), Color.Maroon);
        }
        else
        {
            foreach ((int index, Item item, ClickableComponent button) row in this.rows)
            {
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.mouseCursors,
                    new Rectangle(384, 373, 18, 18),
                    row.button.bounds.X,
                    row.button.bounds.Y,
                    row.button.bounds.Width,
                    row.button.bounds.Height,
                    Color.White,
                    4f,
                    false);

                row.item.drawInMenu(b, new Vector2(row.button.bounds.X + 12, row.button.bounds.Y + 10), 0.8f);
                b.DrawString(
                    Game1.smallFont,
                    $"{row.item.DisplayName} x{row.item.Stack}",
                    new Vector2(row.button.bounds.X + 58, row.button.bounds.Y + 14),
                    Color.Black);
            }
        }

        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            this.cancelButton.bounds.X,
            this.cancelButton.bounds.Y,
            this.cancelButton.bounds.Width,
            this.cancelButton.bounds.Height,
            Color.White,
            4f,
            false);
        b.DrawString(Game1.smallFont, "Annuler", new Vector2(this.cancelButton.bounds.X + 26, this.cancelButton.bounds.Y + 14), Color.Black);

        this.drawMouse(b);
    }
}
