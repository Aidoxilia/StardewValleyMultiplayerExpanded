using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class ConsentPromptMenu : IClickableMenu
{
    private readonly string title;
    private readonly string body;
    private readonly Action acceptAction;
    private readonly Action rejectAction;
    private readonly Action closedAction;
    private readonly ClickableComponent acceptButton;
    private readonly ClickableComponent rejectButton;
    private readonly ClickableTextureComponent closeButton;
    private bool isClosed;

    public ConsentPromptMenu(
        string title,
        string body,
        Action acceptAction,
        Action rejectAction,
        Action closedAction)
        : base(
            Game1.uiViewport.Width / 2 - 260,
            Game1.uiViewport.Height / 2 - 120,
            520,
            240,
            showUpperRightCloseButton: false)
    {
        this.title = title;
        this.body = body;
        this.acceptAction = acceptAction;
        this.rejectAction = rejectAction;
        this.closedAction = closedAction;

        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen + 10, 36, 36),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3f);

        this.acceptButton = new ClickableComponent(
            new Rectangle(this.xPositionOnScreen + 60, this.yPositionOnScreen + 160, 170, 52),
            "accept");
        this.rejectButton = new ClickableComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 230, this.yPositionOnScreen + 160, 170, 52),
            "reject");
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            this.CloseOnly();
            return;
        }

        if (this.acceptButton.containsPoint(x, y))
        {
            Game1.playSound("smallSelect");
            this.acceptAction();
            this.CloseOnly();
            return;
        }

        if (this.rejectButton.containsPoint(x, y))
        {
            Game1.playSound("cancel");
            this.rejectAction();
            this.CloseOnly();
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        b.DrawString(
            Game1.dialogueFont,
            this.title,
            new Vector2(this.xPositionOnScreen + 20, this.yPositionOnScreen + 20),
            Color.Black);
        b.DrawString(
            Game1.smallFont,
            this.body,
            new Vector2(this.xPositionOnScreen + 20, this.yPositionOnScreen + 78),
            Color.DarkSlateGray);

        this.DrawButton(b, this.acceptButton, "Accept", Color.White);
        this.DrawButton(b, this.rejectButton, "Reject", Color.White);

        this.drawMouse(b);
    }

    private void DrawButton(SpriteBatch b, ClickableComponent button, string text, Color color)
    {
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            button.bounds.X,
            button.bounds.Y,
            button.bounds.Width,
            button.bounds.Height,
            color,
            4f,
            false);
        b.DrawString(
            Game1.smallFont,
            text,
            new Vector2(button.bounds.X + 48, button.bounds.Y + 16),
            Color.Black);
    }

    private void CloseOnly()
    {
        if (this.isClosed)
        {
            return;
        }

        this.isClosed = true;
        Game1.activeClickableMenu = null;
        this.closedAction();
    }

}
