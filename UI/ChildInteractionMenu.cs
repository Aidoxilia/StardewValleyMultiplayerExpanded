using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public enum ChildInteractionAction
{
    Care = 0,
    Play = 1,
    Cancel = 2
}

public sealed class ChildInteractionMenu : IClickableMenu
{
    private readonly string childId;
    private readonly string childName;
    private readonly Action<string, ChildInteractionAction> onAction;
    private readonly List<(ChildInteractionAction action, string label, ClickableComponent button)> buttons = new();

    public ChildInteractionMenu(string childId, string childName, Action<string, ChildInteractionAction> onAction)
        : base(
            Game1.uiViewport.Width / 2 - 260,
            Game1.uiViewport.Height / 2 - 190,
            520,
            380,
            showUpperRightCloseButton: false)
    {
        this.childId = childId;
        this.childName = string.IsNullOrWhiteSpace(childName) ? "Votre enfant" : childName;
        this.onAction = onAction;
        this.BuildButtons();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        foreach ((ChildInteractionAction action, string label, ClickableComponent button) entry in this.buttons)
        {
            if (!entry.button.containsPoint(x, y))
            {
                continue;
            }

            Game1.playSound("smallSelect");
            this.onAction(this.childId, entry.action);
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
            this.onAction(this.childId, ChildInteractionAction.Cancel);
            Game1.activeClickableMenu = null;
            return;
        }

        base.receiveKeyPress(key);
    }

    public override void draw(SpriteBatch b)
    {
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        b.DrawString(Game1.dialogueFont, this.childName, new Vector2(this.xPositionOnScreen + 28, this.yPositionOnScreen + 24), Color.Black);
        b.DrawString(Game1.smallFont, "Choisissez une interaction :", new Vector2(this.xPositionOnScreen + 30, this.yPositionOnScreen + 76), Color.DarkSlateGray);

        foreach ((ChildInteractionAction action, string label, ClickableComponent button) entry in this.buttons)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                entry.button.bounds.X,
                entry.button.bounds.Y,
                entry.button.bounds.Width,
                entry.button.bounds.Height,
                Color.White,
                4f,
                false);
            b.DrawString(Game1.smallFont, entry.label, new Vector2(entry.button.bounds.X + 18, entry.button.bounds.Y + 16), Color.Black);
        }

        this.drawMouse(b);
    }

    private void BuildButtons()
    {
        List<(ChildInteractionAction action, string label)> entries = new()
        {
            (ChildInteractionAction.Care, "Prendre soin"),
            (ChildInteractionAction.Play, "Jouer"),
            (ChildInteractionAction.Cancel, "Annuler")
        };

        int y = this.yPositionOnScreen + 118;
        foreach ((ChildInteractionAction action, string label) entry in entries)
        {
            this.buttons.Add((
                entry.action,
                entry.label,
                new ClickableComponent(new Rectangle(this.xPositionOnScreen + 30, y, this.width - 60, 58), entry.action.ToString())));
            y += 68;
        }
    }
}
