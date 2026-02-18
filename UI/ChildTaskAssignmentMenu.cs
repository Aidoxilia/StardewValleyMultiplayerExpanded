using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;

namespace PlayerRomance.UI;

public sealed class ChildTaskAssignmentMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private readonly string childId;
    private readonly string childName;
    private readonly ClickableTextureComponent closeButton;
    private readonly List<(string token, string label, string description, ClickableComponent button)> taskButtons = new();

    public ChildTaskAssignmentMenu(ModEntry mod, string childId, string childName)
        : base(
            Game1.uiViewport.Width / 2 - 400,
            Game1.uiViewport.Height / 2 - 300, // Adjusted height
            800,
            600, // Increased height for better spacing
            showUpperRightCloseButton: false)
    {
        this.mod = mod;
        this.childId = childId;
        this.childName = childName;
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 58, this.yPositionOnScreen + 14, 44, 44),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.6f);
        this.BuildTaskButtons();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            Game1.playSound("bigDeSelect");
            Game1.activeClickableMenu = new ChildrenManagementMenu(this.mod);
            return;
        }

        foreach ((string token, string label, string description, ClickableComponent button) task in this.taskButtons)
        {
            if (!task.button.containsPoint(x, y))
            {
                continue;
            }

            bool ok = this.mod.ChildGrowthSystem.SetChildTaskFromLocal(this.childId, task.token, out string msg);
            if (ok)
            {
                this.mod.Notifier.NotifyInfo(msg, "[PR.System.ChildGrowth]");
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = new ChildrenManagementMenu(this.mod);
            }
            else
            {
                this.mod.Notifier.NotifyWarn(msg, "[PR.System.ChildGrowth]");
                Game1.playSound("cancel");
            }

            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);

        // Background
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        // Title
        string title = $"Task Assignment: {this.childName}";
        SpriteText.drawStringHorizontallyCenteredAt(b, title, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen + 35);

        foreach ((string token, string label, string description, ClickableComponent button) task in this.taskButtons)
        {
            // Button Background (Interactive style)
            bool isHovered = task.button.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                task.button.bounds.X,
                task.button.bounds.Y,
                task.button.bounds.Width,
                task.button.bounds.Height,
                isHovered ? Color.Wheat : Color.White,
                4f,
                false);

            // Label
            b.DrawString(Game1.dialogueFont, task.label, new Vector2(task.button.bounds.X + 24, task.button.bounds.Y + 16), Game1.textColor);

            // Description (Smaller, subdued color)
            b.DrawString(Game1.smallFont, task.description, new Vector2(task.button.bounds.X + 24, task.button.bounds.Y + 48), Color.DarkSlateGray);
        }

        this.drawMouse(b);
    }

    private void BuildTaskButtons()
    {
        List<(string token, string label, string description)> defs = new()
        {
            ("auto", "Auto", "Balanced helper routine."),
            ("water", "Water", "Waters crops for the day."),
            ("feed", "Feed Animals", "Feeds farm animals."),
            ("collect", "Collect", "Collects produce and resources."),
            ("harvest", "Harvest", "Harvests mature crops."),
            ("ship", "Ship", "Ships selected goods."),
            ("fish", "Fish", "Goes fishing (if enabled)."),
            ("stop", "Stop", "No work assignment today.")
        };

        int y = this.yPositionOnScreen + 100; // Start lower to account for title
        int buttonHeight = 80; // Taller buttons
        int spacing = 16;      // Space between buttons

        // Calculate columns if too many items fit vertically
        // For simplicity with this list size, we stick to one column but ensure it fits

        foreach ((string token, string label, string description) def in defs)
        {
            if (def.token == "fish" && !this.mod.Config.EnableChildFishingTask)
            {
                continue;
            }

            this.taskButtons.Add((
                def.token,
                def.label,
                def.description,
                new ClickableComponent(new Rectangle(this.xPositionOnScreen + 40, y, this.width - 80, buttonHeight), def.token)));

            y += buttonHeight + spacing;
        }
    }
}