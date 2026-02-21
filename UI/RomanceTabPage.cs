using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Patches;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class RomanceTabPage : IClickableMenu
{
    // â”€â”€ Layout constants â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Inset from the drawDialogueBox border to the usable content area.
    private const int BorderX = 40;
    private const int BorderTop = 76;
    private const int BorderBottom = 40;
    // Gap between the left category panel and the right content panel.
    private const int PanelGap = 16;
    // Padding inside panels.
    private const int InnerPad = 12;
    // Category button height.
    private const int BtnH = 52;
    private const int BtnSpacing = 8;

    private readonly ModEntry mod;
    private readonly List<CategoryEntry> categories;
    private readonly List<ClickableComponent> categoryButtons = new();
    private ClickableComponent openButton = null!;
    private ClickableTextureComponent closeButton = null!;
    private int selectedIndex;

    // Cached layout rectangles.
    private Rectangle leftPanel;
    private Rectangle rightPanel;

    private sealed record CategoryEntry(string Id, string Label, string Description, Func<IClickableMenu> OpenFactory);

    public RomanceTabPage(ModEntry mod, int x, int y, int width, int height)
        : base(x, y, width, height)
    {
        this.mod = mod;
        this.categories = BuildCategories(mod);
        this.RebuildLayout();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.RebuildLayout();
    }

    // â”€â”€ Input â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            this.exitThisMenu();
            Game1.playSound("bigDeSelect");
            return;
        }

        for (int i = 0; i < this.categoryButtons.Count; i++)
        {
            if (!this.categoryButtons[i].containsPoint(x, y)) continue;
            if (this.selectedIndex != i)
            {
                this.selectedIndex = i;
                Game1.playSound("smallSelect");
            }
            return;
        }

        if (this.openButton.containsPoint(x, y))
        {
            this.OpenSelectedCategory();
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        this.exitThisMenu();
    }

    public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
    {
        if (Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            this.exitThisMenu();
            return;
        }
        base.receiveKeyPress(key);
    }

    // â”€â”€ Draw â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public override void draw(SpriteBatch b)
    {
        // Dim screen behind menu.
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.5f);

        // Outer box.
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

        // Title (inside the box header area).
        Utility.drawTextWithShadow(b, "â™¥ Romance", Game1.dialogueFont,
            new Vector2(this.xPositionOnScreen + BorderX + InnerPad, this.yPositionOnScreen + 28),
            Game1.textColor);

        // Left panel box.
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            this.leftPanel.X, this.leftPanel.Y, this.leftPanel.Width, this.leftPanel.Height, Color.White, 4f, false);

        // Right panel box.
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18),
            this.rightPanel.X, this.rightPanel.Y, this.rightPanel.Width, this.rightPanel.Height, Color.White, 4f, false);

        // Category buttons.
        for (int i = 0; i < this.categoryButtons.Count; i++)
        {
            bool selected = i == this.selectedIndex;
            bool hovered = this.categoryButtons[i].bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color tint = selected ? Color.White : hovered ? Color.Wheat : Color.LightGray;
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                this.categoryButtons[i].bounds.X, this.categoryButtons[i].bounds.Y,
                this.categoryButtons[i].bounds.Width, this.categoryButtons[i].bounds.Height,
                tint, 4f, false);
            // Vertically-centred label.
            Vector2 labelSize = Game1.smallFont.MeasureString(this.categoryButtons[i].name);
            float labelX = this.categoryButtons[i].bounds.X + InnerPad;
            float labelY = this.categoryButtons[i].bounds.Center.Y - labelSize.Y / 2f;
            Utility.drawTextWithShadow(b, this.categoryButtons[i].name, Game1.smallFont,
                new Vector2(labelX, labelY),
                selected ? Game1.textColor : Game1.unselectedOptionColor);
        }

        // Right panel: heading.
        CategoryEntry sel = this.categories[this.selectedIndex];
        float headingY = this.rightPanel.Y + InnerPad + 4;
        Utility.drawTextWithShadow(b, sel.Label, Game1.dialogueFont,
            new Vector2(this.rightPanel.X + InnerPad, headingY), Game1.textColor);

        // Divider line under heading.
        float headingH = Game1.dialogueFont.MeasureString(sel.Label).Y;
        float dividerY = headingY + headingH + 4;
        b.Draw(Game1.staminaRect,
            new Rectangle(this.rightPanel.X + InnerPad, (int)dividerY, this.rightPanel.Width - InnerPad * 2, 2),
            Game1.textColor * 0.3f);

        // Word-wrapped description below divider.
        int descMaxW = this.rightPanel.Width - InnerPad * 2;
        string desc = Game1.parseText(sel.Description, Game1.smallFont, descMaxW);
        b.DrawString(Game1.smallFont, desc,
            new Vector2(this.rightPanel.X + InnerPad, dividerY + 10), Game1.textColor * 0.85f);

        // "Open" button â€” drawn manually so text is centred and no bad sprite.
        bool openHovered = this.openButton.bounds.Contains(Game1.getMouseX(), Game1.getMouseY());
        Color openTint = openHovered ? Color.Wheat : Color.White;
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
            this.openButton.bounds.X, this.openButton.bounds.Y,
            this.openButton.bounds.Width, this.openButton.bounds.Height,
            openTint, 4f, false);
        Vector2 openTextSize = Game1.smallFont.MeasureString("Open " + sel.Label);
        float openTextX = this.openButton.bounds.Center.X - openTextSize.X / 2f;
        float openTextY = this.openButton.bounds.Center.Y - openTextSize.Y / 2f;
        Utility.drawTextWithShadow(b, "Open " + sel.Label, Game1.smallFont,
            new Vector2(openTextX, openTextY), Game1.textColor);

        // Close button (X).
        this.closeButton.draw(b);

        this.drawMouse(b);
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private void OpenSelectedCategory()
    {
        if (this.selectedIndex < 0 || this.selectedIndex >= this.categories.Count) return;
        try
        {
            // Register this menu's position as return point so the back button can reopen it.
            GameMenuPatches.SetReturnPoint(
                this.mod,
                this.xPositionOnScreen,
                this.yPositionOnScreen,
                this.width,
                this.height);

            Game1.playSound("bigSelect");
            Game1.activeClickableMenu = this.categories[this.selectedIndex].OpenFactory();
        }
        catch (Exception ex)
        {
            // If opening failed, don't leave a stale return point.
            GameMenuPatches.ClearReturnPoint();
            this.mod.Monitor.Log(
                $"[PR.UI.RomanceTab] Failed to open '{this.categories[this.selectedIndex].Id}': {ex}",
                StardewModdingAPI.LogLevel.Error);
        }
    }

    private void RebuildLayout()
    {
        int mx = this.xPositionOnScreen;
        int my = this.yPositionOnScreen;
        int mw = this.width;
        int mh = this.height;

        // Usable content area.
        int contentX = mx + BorderX;
        int contentY = my + BorderTop;
        int contentW = mw - BorderX * 2;
        int contentH = mh - BorderTop - BorderBottom;

        // Split content area: left panel ~28%, right panel the rest.
        int leftW = Math.Max(160, Math.Min(240, contentW * 28 / 100));
        int rightW = contentW - leftW - PanelGap;

        this.leftPanel = new Rectangle(contentX, contentY, leftW, contentH);
        this.rightPanel = new Rectangle(contentX + leftW + PanelGap, contentY, rightW, contentH);

        // Category buttons â€” stacked from top of left panel.
        this.categoryButtons.Clear();
        int btnX = this.leftPanel.X + InnerPad;
        int btnW = this.leftPanel.Width - InnerPad * 2;
        for (int i = 0; i < this.categories.Count; i++)
        {
            int btnY = this.leftPanel.Y + InnerPad + i * (BtnH + BtnSpacing);
            this.categoryButtons.Add(new ClickableComponent(
                new Rectangle(btnX, btnY, btnW, BtnH),
                this.categories[i].Label));
        }

        // Open button â€” bottom of the right panel, full-width minus padding.
        int openBtnH = 52;
        int openBtnX = this.rightPanel.X + InnerPad;
        int openBtnY = this.rightPanel.Bottom - openBtnH - InnerPad;
        int openBtnW = this.rightPanel.Width - InnerPad * 2;
        this.openButton = new ClickableComponent(
            new Rectangle(openBtnX, openBtnY, openBtnW, openBtnH),
            "open");

        // Close button (X) top-right corner of the outer box.
        this.closeButton = new ClickableTextureComponent(
            new Rectangle(mx + mw - 48, my - 8, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f);
    }

    private static List<CategoryEntry> BuildCategories(ModEntry mod)
    {
        List<CategoryEntry> entries =
        [
            new CategoryEntry("You", "You", "Set your birthday and favorite gifts.", () => new YouProfileMenu(mod)),
            new CategoryEntry("Romance Hub", "Romance Hub", "Open the romance hub menu.", () => new RomanceHubMenu(mod)),
            new CategoryEntry("Children", "Children", "Manage your children.", () => new ChildrenManagementMenu(mod)),
            new CategoryEntry("Pregnancy", "Pregnancy", "View pregnancy status and controls.", () => new PregnancyMenu(mod))
        ];

        Type? romanceMenuType = Type.GetType("PlayerRomance.UI.RomanceMenu, PlayerRomance", throwOnError: false);
        if (romanceMenuType is not null)
        {
            entries.Add(new CategoryEntry(
                "Overview", "Overview",
                "Open the romance overview menu.",
                () => (IClickableMenu)Activator.CreateInstance(romanceMenuType, mod)!));
        }

        return entries;
    }
}

