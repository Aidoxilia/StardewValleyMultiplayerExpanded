using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewValley;
using StardewValley.Menus;

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
            Game1.uiViewport.Height / 2 - 260,
            800,
            520,
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
        this.drawBackground(b);
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        b.DrawString(Game1.dialogueFont, $"Work assignment - {this.childName}", new Vector2(this.xPositionOnScreen + 30, this.yPositionOnScreen + 22), Color.Black);

        foreach ((string token, string label, string description, ClickableComponent button) task in this.taskButtons)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                task.button.bounds.X,
                task.button.bounds.Y,
                task.button.bounds.Width,
                task.button.bounds.Height,
                Color.White,
                4f,
                false);

            b.DrawString(Game1.smallFont, task.label, new Vector2(task.button.bounds.X + 12, task.button.bounds.Y + 10), Color.Black);
            b.DrawString(Game1.smallFont, task.description, new Vector2(task.button.bounds.X + 12, task.button.bounds.Y + 34), Color.DarkSlateGray);
        }

        this.drawMouse(b);
    }

    private void BuildTaskButtons()
    {
        List<(string token, string label, string description)> defs = new()
        {
            ("auto", "Auto", "Balanced helper routine."),
            ("water", "Water", "Waters crops for the day."),
            ("feed", "Feed animals", "Feeds farm animals."),
            ("collect", "Collect", "Collects produce and resources."),
            ("harvest", "Harvest", "Harvests mature crops."),
            ("ship", "Ship", "Ships selected goods."),
            ("fish", "Fish", "Goes fishing (if enabled)."),
            ("stop", "Stop", "No work assignment today.")
        };

        int y = this.yPositionOnScreen + 76;
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
                new ClickableComponent(new Rectangle(this.xPositionOnScreen + 26, y, this.width - 52, 58), def.token)));
            y += 62;
        }
    }
}
