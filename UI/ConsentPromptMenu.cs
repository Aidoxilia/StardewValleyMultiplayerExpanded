using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
            Game1.uiViewport.Width / 2 - 290,
            Game1.uiViewport.Height / 2 - 150,
            580,
            300,
            showUpperRightCloseButton: false)
    {
        this.title = title;
        this.body = body;
        this.acceptAction = acceptAction;
        this.rejectAction = rejectAction;
        this.closedAction = closedAction;

        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen + 12, 36, 36),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3f);

        this.acceptButton = new ClickableComponent(
            new Rectangle(this.xPositionOnScreen + 56, this.yPositionOnScreen + 210, 210, 58),
            "accept");
        this.rejectButton = new ClickableComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 266, this.yPositionOnScreen + 210, 210, 58),
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
            this.AcceptAndClose();
            return;
        }

        if (this.rejectButton.containsPoint(x, y))
        {
            this.RejectAndClose();
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key is Keys.Enter or Keys.Y)
        {
            this.AcceptAndClose();
            return;
        }

        if (key is Keys.Escape or Keys.N)
        {
            this.RejectAndClose();
            return;
        }

        base.receiveKeyPress(key);
    }

    public override void draw(SpriteBatch b)
    {
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        Rectangle header = new(this.xPositionOnScreen + 20, this.yPositionOnScreen + 18, this.width - 80, 38);
        b.Draw(Game1.staminaRect, header, new Color(247, 214, 154));

        b.DrawString(
            Game1.dialogueFont,
            this.title,
            new Vector2(this.xPositionOnScreen + 24, this.yPositionOnScreen + 20),
            Color.Black);
        b.DrawString(
            Game1.smallFont,
            this.body,
            new Vector2(this.xPositionOnScreen + 24, this.yPositionOnScreen + 86),
            Color.DarkSlateGray);
        b.DrawString(
            Game1.smallFont,
            "Shortcuts: [Enter/Y] Accept  [Esc/N] Reject",
            new Vector2(this.xPositionOnScreen + 24, this.yPositionOnScreen + 172),
            new Color(88, 88, 88));

        this.DrawActionButton(b, this.acceptButton, "Accept", new Color(206, 244, 203), Game1.getMouseX(), Game1.getMouseY());
        this.DrawActionButton(b, this.rejectButton, "Reject", new Color(244, 208, 208), Game1.getMouseX(), Game1.getMouseY());

        this.drawMouse(b);
    }

    private void DrawActionButton(SpriteBatch b, ClickableComponent button, string text, Color color, int mouseX, int mouseY)
    {
        bool hover = button.containsPoint(mouseX, mouseY);
        Color fill = hover ? Color.Lerp(color, Color.White, 0.35f) : color;
        IClickableMenu.drawTextureBox(
            b,
            Game1.mouseCursors,
            new Rectangle(384, 373, 18, 18),
            button.bounds.X,
            button.bounds.Y,
            button.bounds.Width,
            button.bounds.Height,
            fill,
            4f,
            false);

        Vector2 size = Game1.smallFont.MeasureString(text);
        b.DrawString(
            Game1.smallFont,
            text,
            new Vector2(button.bounds.X + (button.bounds.Width - size.X) / 2f, button.bounds.Y + (button.bounds.Height - size.Y) / 2f),
            Color.Black);
    }

    private void AcceptAndClose()
    {
        Game1.playSound("smallSelect");
        this.acceptAction();
        this.CloseOnly();
    }

    private void RejectAndClose()
    {
        Game1.playSound("cancel");
        this.rejectAction();
        this.CloseOnly();
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
