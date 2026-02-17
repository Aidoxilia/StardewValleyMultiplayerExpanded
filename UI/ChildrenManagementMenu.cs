using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class ChildrenManagementMenu : IClickableMenu
{
    private readonly ModEntry mod;
    private readonly List<ChildRecord> children;
    private readonly ClickableTextureComponent closeButton;
    private readonly List<(ChildRecord child, ClickableComponent assignButton)> assignButtons = new();

    public ChildrenManagementMenu(ModEntry mod)
        : base(
            Game1.uiViewport.Width / 2 - 480,
            Game1.uiViewport.Height / 2 - 300,
            960,
            600,
            showUpperRightCloseButton: false)
    {
        this.mod = mod;
        this.children = this.GetChildrenForLocal().OrderBy(c => c.ChildName).ToList();
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 58, this.yPositionOnScreen + 14, 44, 44),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            3.6f);
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

        foreach ((ChildRecord child, ClickableComponent assignButton) row in this.assignButtons)
        {
            if (!row.assignButton.containsPoint(x, y))
            {
                continue;
            }

            Game1.playSound("smallSelect");
            Game1.activeClickableMenu = new ChildTaskAssignmentMenu(this.mod, row.child.ChildId, row.child.ChildName);
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void draw(SpriteBatch b)
    {
        this.drawBackground(b);
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);
        this.closeButton.draw(b);

        b.DrawString(Game1.dialogueFont, "Children Management", new Vector2(this.xPositionOnScreen + 28, this.yPositionOnScreen + 22), Color.Black);
        b.DrawString(Game1.smallFont, "Name | Age | Care state | Development | Work", new Vector2(this.xPositionOnScreen + 32, this.yPositionOnScreen + 68), Color.DarkSlateGray);

        int y = this.yPositionOnScreen + 98;
        foreach (ChildRecord child in this.children)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                this.xPositionOnScreen + 24,
                y,
                this.width - 48,
                72,
                Color.White,
                4f,
                false);

            string ageText = $"{child.AgeYears}y / {child.AgeDays}d";
            string stateText = $"fed={child.IsFedToday}, care={child.IsCaredToday}, play={child.IsPlayedToday}";
            string progressText = $"feed progress={child.FeedingProgress}, stage={child.Stage}";

            b.DrawString(Game1.smallFont, child.ChildName, new Vector2(this.xPositionOnScreen + 40, y + 12), Color.Black);
            b.DrawString(Game1.smallFont, ageText, new Vector2(this.xPositionOnScreen + 240, y + 12), Color.Black);
            b.DrawString(Game1.smallFont, stateText, new Vector2(this.xPositionOnScreen + 360, y + 12), Color.Black);
            b.DrawString(Game1.smallFont, progressText, new Vector2(this.xPositionOnScreen + 40, y + 40), Color.DarkSlateGray);
            y += 78;
        }

        foreach ((ChildRecord child, ClickableComponent assignButton) row in this.assignButtons)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 373, 18, 18),
                row.assignButton.bounds.X,
                row.assignButton.bounds.Y,
                row.assignButton.bounds.Width,
                row.assignButton.bounds.Height,
                Color.White,
                4f,
                false);
            b.DrawString(Game1.smallFont, "Envoyer travailler", new Vector2(row.assignButton.bounds.X + 8, row.assignButton.bounds.Y + 12), Color.Black);
        }

        if (this.children.Count == 0)
        {
            b.DrawString(Game1.smallFont, "No children found.", new Vector2(this.xPositionOnScreen + 34, this.yPositionOnScreen + 112), Color.Maroon);
        }

        this.drawMouse(b);
    }

    private void BuildButtons()
    {
        int y = this.yPositionOnScreen + 98;
        foreach (ChildRecord child in this.children)
        {
            if (child.AgeYears >= Math.Max(16, this.mod.Config.AdultWorkMinAge))
            {
                this.assignButtons.Add((
                    child,
                    new ClickableComponent(new Rectangle(this.xPositionOnScreen + this.width - 210, y + 14, 168, 40), $"work_{child.ChildId}")));
            }

            y += 78;
        }
    }

    private IEnumerable<ChildRecord> GetChildrenForLocal()
    {
        return this.mod.IsHostPlayer
            ? this.mod.HostSaveData.Children.Values
            : this.mod.ClientSnapshot.Children;
    }
}
