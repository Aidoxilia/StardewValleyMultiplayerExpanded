using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using PlayerRomance.Systems;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class YouProfileMenu : IClickableMenu
{
    private static readonly string[] SeasonOrder = { "spring", "summer", "fall", "winter" };

    private readonly ModEntry mod;
    private readonly List<string> workingFavorites = new();
    private ClickableTextureComponent closeButton = null!;
    private ClickableTextureComponent prevSeasonButton = null!;
    private ClickableTextureComponent nextSeasonButton = null!;
    private ClickableTextureComponent prevDayButton = null!;
    private ClickableTextureComponent nextDayButton = null!;
    private ClickableTextureComponent saveBirthdayButton = null!;
    private ClickableTextureComponent addHeldGiftButton = null!;
    private ClickableTextureComponent saveFavoritesButton = null!;
    private ClickableTextureComponent clearFavoritesButton = null!;

    private string selectedSeason = "spring";
    private int selectedDay = 1;
    private string hoverText = string.Empty;

    public YouProfileMenu(ModEntry mod)
    {
        this.mod = mod;
        this.RefreshFromProfile();
        this.UpdateLayout();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.UpdateLayout();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.closeButton.containsPoint(x, y))
        {
            this.exitThisMenu();
            Game1.playSound("bigDeSelect");
            return;
        }

        if (this.prevSeasonButton.containsPoint(x, y))
        {
            this.CycleSeason(-1);
            return;
        }

        if (this.nextSeasonButton.containsPoint(x, y))
        {
            this.CycleSeason(+1);
            return;
        }

        if (this.prevDayButton.containsPoint(x, y))
        {
            this.selectedDay = this.selectedDay <= 1 ? 28 : this.selectedDay - 1;
            Game1.playSound("shwip");
            return;
        }

        if (this.nextDayButton.containsPoint(x, y))
        {
            this.selectedDay = this.selectedDay >= 28 ? 1 : this.selectedDay + 1;
            Game1.playSound("shwip");
            return;
        }

        if (this.saveBirthdayButton.containsPoint(x, y))
        {
            bool ok = this.mod.PlayerProfileSystem.RequestBirthdayUpdateFromLocal(this.selectedSeason, this.selectedDay, out string message);
            this.NotifyResult(ok, message);
            return;
        }

        if (this.addHeldGiftButton.containsPoint(x, y))
        {
            this.TryAddHeldItem();
            return;
        }

        if (this.saveFavoritesButton.containsPoint(x, y))
        {
            bool ok = this.mod.PlayerProfileSystem.RequestFavoritesUpdateFromLocal(this.workingFavorites, out string message);
            this.NotifyResult(ok, message);
            this.RefreshFromProfile();
            return;
        }

        if (this.clearFavoritesButton.containsPoint(x, y))
        {
            this.workingFavorites.Clear();
            Game1.playSound("trashcan");
            return;
        }

        Rectangle favoritesPanel = this.GetFavoritesPanel();
        int shown = Math.Min(10, this.workingFavorites.Count);
        for (int i = 0; i < shown; i++)
        {
            Rectangle slot = new(favoritesPanel.X + 20 + i * 68, favoritesPanel.Y + 56, 56, 56);
            if (!slot.Contains(x, y))
            {
                continue;
            }

            this.workingFavorites.RemoveAt(i);
            Game1.playSound("trashcan");
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void performHoverAction(int x, int y)
    {
        this.hoverText = string.Empty;
        if (this.addHeldGiftButton.containsPoint(x, y))
        {
            this.hoverText = "Add held item to favorites";
        }
        else if (this.saveFavoritesButton.containsPoint(x, y))
        {
            this.hoverText = "Save favorites";
        }
        else if (this.saveBirthdayButton.containsPoint(x, y))
        {
            this.hoverText = "Save birthday";
        }
        else if (this.clearFavoritesButton.containsPoint(x, y))
        {
            this.hoverText = "Clear favorite list";
        }
    }

    public override void draw(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

        Utility.drawTextWithShadow(b, "You", Game1.dialogueFont, new Vector2(this.xPositionOnScreen + 40, this.yPositionOnScreen + 24), Game1.textColor);

        Rectangle birthdayPanel = this.GetBirthdayPanel();
        Rectangle favoritesPanel = this.GetFavoritesPanel();

        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), birthdayPanel.X, birthdayPanel.Y, birthdayPanel.Width, birthdayPanel.Height, Color.White, 4f, false);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), favoritesPanel.X, favoritesPanel.Y, favoritesPanel.Width, favoritesPanel.Height, Color.White, 4f, false);

        Utility.drawTextWithShadow(b, "Birthday", Game1.smallFont, new Vector2(birthdayPanel.X + 16, birthdayPanel.Y + 14), Game1.textColor);
        Utility.drawTextWithShadow(
            b,
            $"{PlayerProfileSystem.ToDisplaySeason(this.selectedSeason)} {this.selectedDay}",
            Game1.dialogueFont,
            new Vector2(birthdayPanel.X + 100, birthdayPanel.Y + 46),
            Game1.textColor);

        this.prevSeasonButton.draw(b);
        this.nextSeasonButton.draw(b);
        this.prevDayButton.draw(b);
        this.nextDayButton.draw(b);
        this.saveBirthdayButton.draw(b);
        Utility.drawTextWithShadow(b, "Save Birthday", Game1.smallFont, new Vector2(this.saveBirthdayButton.bounds.X + 8, this.saveBirthdayButton.bounds.Y + 10), Game1.textColor);

        Utility.drawTextWithShadow(b, "Favorite Gifts", Game1.smallFont, new Vector2(favoritesPanel.X + 16, favoritesPanel.Y + 14), Game1.textColor);
        this.addHeldGiftButton.draw(b);
        this.saveFavoritesButton.draw(b);
        this.clearFavoritesButton.draw(b);
        Utility.drawTextWithShadow(b, "Add held", Game1.tinyFont, new Vector2(this.addHeldGiftButton.bounds.X + 8, this.addHeldGiftButton.bounds.Y + 10), Game1.textColor);
        Utility.drawTextWithShadow(b, "Save", Game1.tinyFont, new Vector2(this.saveFavoritesButton.bounds.X + 14, this.saveFavoritesButton.bounds.Y + 10), Game1.textColor);
        Utility.drawTextWithShadow(b, "Clear", Game1.tinyFont, new Vector2(this.clearFavoritesButton.bounds.X + 12, this.clearFavoritesButton.bounds.Y + 10), Game1.textColor);

        if (this.workingFavorites.Count == 0)
        {
            b.DrawString(Game1.smallFont, "Favorites: Not set", new Vector2(favoritesPanel.X + 20, favoritesPanel.Y + 62), Game1.textColor);
        }
        else
        {
            int shown = Math.Min(10, this.workingFavorites.Count);
            for (int i = 0; i < shown; i++)
            {
                Rectangle slot = new(favoritesPanel.X + 20 + i * 68, favoritesPanel.Y + 56, 56, 56);
                b.Draw(Game1.menuTexture, slot, new Rectangle(128, 128, 64, 64), Color.White);
                DrawItemIcon(b, this.workingFavorites[i], slot);
            }

            string idsText = string.Join(", ", this.workingFavorites.Take(6));
            b.DrawString(Game1.tinyFont, idsText, new Vector2(favoritesPanel.X + 20, favoritesPanel.Y + 126), Color.DimGray);
        }

        this.closeButton.draw(b);
        if (!string.IsNullOrWhiteSpace(this.hoverText))
        {
            IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
        }

        this.drawMouse(b);
    }

    private void UpdateLayout()
    {
        this.width = Math.Min(920, Game1.uiViewport.Width - 64);
        this.height = Math.Min(620, Game1.uiViewport.Height - 64);
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

        this.closeButton = new ClickableTextureComponent(
            new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen - 8, 48, 48),
            Game1.mouseCursors,
            new Rectangle(337, 494, 12, 12),
            4f);

        Rectangle birthday = this.GetBirthdayPanel();
        this.prevSeasonButton = ArrowButton(birthday.X + 24, birthday.Y + 44, left: true);
        this.nextSeasonButton = ArrowButton(birthday.X + 300, birthday.Y + 44, left: false);
        this.prevDayButton = ArrowButton(birthday.X + 360, birthday.Y + 44, left: true);
        this.nextDayButton = ArrowButton(birthday.X + 432, birthday.Y + 44, left: false);
        this.saveBirthdayButton = BoxButton(birthday.Right - 186, birthday.Y + 40, 160, 44);

        Rectangle favorites = this.GetFavoritesPanel();
        this.addHeldGiftButton = BoxButton(favorites.Right - 294, favorites.Y + 12, 90, 34);
        this.saveFavoritesButton = BoxButton(favorites.Right - 198, favorites.Y + 12, 90, 34);
        this.clearFavoritesButton = BoxButton(favorites.Right - 102, favorites.Y + 12, 90, 34);
    }

    private void RefreshFromProfile()
    {
        PlayerProfileRecord? profile = this.mod.PlayerProfileSystem.GetProfile(this.mod.LocalPlayerId);
        if (profile is null)
        {
            this.selectedSeason = "spring";
            this.selectedDay = 1;
            this.workingFavorites.Clear();
            return;
        }

        if (!string.IsNullOrWhiteSpace(profile.BirthdaySeason)
            && SeasonOrder.Contains(profile.BirthdaySeason, StringComparer.OrdinalIgnoreCase))
        {
            this.selectedSeason = profile.BirthdaySeason.ToLowerInvariant();
        }

        this.selectedDay = Math.Clamp(profile.BirthdayDay <= 0 ? 1 : profile.BirthdayDay, 1, 28);
        this.workingFavorites.Clear();
        this.workingFavorites.AddRange(profile.FavoriteGiftItemIds);
    }

    private void CycleSeason(int delta)
    {
        int index = Array.FindIndex(SeasonOrder, season => season.Equals(this.selectedSeason, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            index = 0;
        }

        index = (index + delta + SeasonOrder.Length) % SeasonOrder.Length;
        this.selectedSeason = SeasonOrder[index];
        Game1.playSound("shwip");
    }

    private void TryAddHeldItem()
    {
        Item? held = Game1.player?.CurrentItem;
        if (held is null || string.IsNullOrWhiteSpace(held.QualifiedItemId))
        {
            this.mod.Notifier.NotifyWarn("Hold an item first.", "[PR.System.Profile]");
            return;
        }

        if (this.workingFavorites.Contains(held.QualifiedItemId, StringComparer.OrdinalIgnoreCase))
        {
            this.mod.Notifier.NotifyWarn("Item already in favorites.", "[PR.System.Profile]");
            return;
        }

        this.workingFavorites.Add(held.QualifiedItemId);
        Game1.playSound("coin");
    }

    private void NotifyResult(bool ok, string message)
    {
        if (ok)
        {
            this.mod.Notifier.NotifyInfo(message, "[PR.System.Profile]");
        }
        else
        {
            this.mod.Notifier.NotifyWarn(message, "[PR.System.Profile]");
        }
    }

    private Rectangle GetBirthdayPanel()
    {
        return new Rectangle(this.xPositionOnScreen + 28, this.yPositionOnScreen + 72, this.width - 56, 108);
    }

    private Rectangle GetFavoritesPanel()
    {
        return new Rectangle(this.xPositionOnScreen + 28, this.yPositionOnScreen + 196, this.width - 56, this.height - 228);
    }

    private static ClickableTextureComponent ArrowButton(int x, int y, bool left)
    {
        return new ClickableTextureComponent(
            new Rectangle(x, y, 44, 44),
            Game1.mouseCursors,
            left ? new Rectangle(352, 495, 12, 11) : new Rectangle(365, 495, 12, 11),
            3.5f);
    }

    private static ClickableTextureComponent BoxButton(int x, int y, int w, int h)
    {
        return new ClickableTextureComponent(
            new Rectangle(x, y, w, h),
            Game1.mouseCursors,
            new Rectangle(128, 256, 64, 64),
            1f);
    }

    private static void DrawItemIcon(SpriteBatch b, string itemId, Rectangle slot)
    {
        try
        {
            Item? item = ItemRegistry.Create(itemId, 1, 0, allowNull: true);
            if (item is not null)
            {
                item.drawInMenu(b, new Vector2(slot.X + 14, slot.Y + 14), 1f);
                return;
            }
        }
        catch
        {
        }

        b.DrawString(Game1.tinyFont, "?", new Vector2(slot.Center.X - 4, slot.Center.Y - 10), Color.Gray * 0.7f);
    }
}