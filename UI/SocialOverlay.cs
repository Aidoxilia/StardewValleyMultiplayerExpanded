using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.UI;

public sealed class SocialOverlay
{
    private readonly ModEntry mod;

    public SocialOverlay(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not GameMenu menu)
        {
            return;
        }

        if (!this.IsSocialPageActive(menu))
        {
            return;
        }

        List<string> lines = this.BuildOverlayLines();
        if (lines.Count == 0)
        {
            return;
        }

        int x = menu.xPositionOnScreen + menu.width - 380;
        int y = menu.yPositionOnScreen + 88;
        int width = 330;
        int height = 72 + lines.Count * 34;

        IClickableMenu.drawTextureBox(spriteBatch, x, y, width, height, Color.White);
        spriteBatch.DrawString(Game1.smallFont, "Player Romance", new Vector2(x + 16, y + 16), Color.SaddleBrown);

        int lineY = y + 48;
        foreach (string line in lines)
        {
            spriteBatch.DrawString(Game1.smallFont, line, new Vector2(x + 20, lineY), Color.Black);
            lineY += 30;
        }
    }

    private List<string> BuildOverlayLines()
    {
        List<string> lines = new();
        foreach (Data.RelationshipRecord relation in this.mod.DatingSystem.GetRelationshipsForPlayer(this.mod.LocalPlayerId))
        {
            string stateText = relation.State.ToString();
            if (relation.PendingDatingFrom.HasValue)
            {
                stateText += " (dating pending)";
            }

            if (relation.PendingMarriageFrom.HasValue)
            {
                stateText += " (marriage pending)";
            }

            int hearts = relation.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts);
            lines.Add($"<3 {relation.GetOtherName(this.mod.LocalPlayerId)}: {stateText} | Hearts {hearts}/{this.mod.Config.MaxHearts}");
        }

        if (lines.Count == 0)
        {
            lines.Add("No player relationship yet.");
        }

        return lines;
    }

    private bool IsSocialPageActive(GameMenu menu)
    {
        try
        {
            int currentTab = this.mod.Helper.Reflection.GetField<int>(menu, "currentTab").GetValue();
            List<IClickableMenu> pages = this.mod.Helper.Reflection.GetField<List<IClickableMenu>>(menu, "pages").GetValue();
            return currentTab >= 0 && currentTab < pages.Count && pages[currentTab] is SocialPage;
        }
        catch
        {
            return Game1.activeClickableMenu?.GetType().Name.Contains("Social", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
