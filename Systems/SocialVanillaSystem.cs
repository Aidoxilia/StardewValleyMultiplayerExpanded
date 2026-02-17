using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerRomance.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using System.Reflection;

namespace PlayerRomance.Systems;

public sealed class SocialVanillaSystem
{
    private static readonly Rectangle EmptyHeartSource = new(211, 428, 7, 6);
    private static readonly Rectangle FullHeartSource = new(218, 428, 7, 6);

    private readonly ModEntry mod;
    private readonly Dictionary<long, Rectangle> lastClickZonesByPlayerId = new();

    public SocialVanillaSystem(ModEntry mod)
    {
        this.mod = mod;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (!this.mod.Config.EnableVanillaSocialIntegration)
        {
            return;
        }

        if (!this.TryGetSocialPage(out object? socialPage))
        {
            this.lastClickZonesByPlayerId.Clear();
            return;
        }

        this.lastClickZonesByPlayerId.Clear();
        foreach (ClickableTextureComponent entry in this.GetSocialEntryComponents(socialPage!))
        {
            Farmer? farmer = this.ResolveFarmerForSocialEntry(entry);
            if (farmer is null || farmer.UniqueMultiplayerID == this.mod.LocalPlayerId)
            {
                continue;
            }

            Rectangle rowBounds = entry.bounds;
            RelationshipRecord? relation = this.mod.DatingSystem.GetRelationship(this.mod.LocalPlayerId, farmer.UniqueMultiplayerID);
            string status = relation?.State switch
            {
                RelationshipState.Dating or RelationshipState.Engaged => "(in relationship)",
                RelationshipState.Married => "(married)",
                _ => "(single)"
            };

            int hearts = relation?.GetHeartLevel(this.mod.Config.HeartPointsPerHeart, this.mod.Config.MaxHearts) ?? 0;
            int maxHearts = Math.Max(1, this.mod.Config.MaxHearts);

            Vector2 textPos = new(rowBounds.Right - 212, rowBounds.Y + 12);
            spriteBatch.Draw(Game1.staminaRect, new Rectangle(rowBounds.Right - 218, rowBounds.Y + 10, 214, 22), new Color(0, 0, 0, 120));
            Utility.drawTextWithShadow(spriteBatch, status, Game1.smallFont, textPos, Game1.textColor);

            this.DrawHearts(spriteBatch, rowBounds.Right - 206, rowBounds.Y + 34, hearts, maxHearts);
            this.lastClickZonesByPlayerId[farmer.UniqueMultiplayerID] = rowBounds;
        }
    }

    public bool TryHandleSocialMenuClick(ButtonPressedEventArgs e)
    {
        if (!this.mod.Config.EnableVanillaSocialIntegration
            || e.Button != SButton.MouseLeft
            || Game1.activeClickableMenu is null
            || !this.TryGetSocialPage(out _))
        {
            return false;
        }

        Point click = new((int)e.Cursor.ScreenPixels.X, (int)e.Cursor.ScreenPixels.Y);
        foreach ((long playerId, Rectangle zone) in this.lastClickZonesByPlayerId)
        {
            if (!zone.Contains(click))
            {
                continue;
            }

            Farmer? target = this.mod.FindFarmerById(playerId, includeOffline: true);
            if (target is null)
            {
                continue;
            }

            this.mod.Monitor.Log($"[PR.UI.Social] Opening player social profile for {target.Name} ({playerId}).", LogLevel.Trace);
            Game1.activeClickableMenu = new UI.PlayerSocialProfileMenu(this.mod, playerId);
            return true;
        }

        return false;
    }

    private void DrawHearts(SpriteBatch spriteBatch, int x, int y, int hearts, int maxHearts)
    {
        int safeHearts = Math.Clamp(hearts, 0, maxHearts);
        int drawCount = Math.Min(maxHearts, 10);
        for (int i = 0; i < drawCount; i++)
        {
            Rectangle src = i < safeHearts ? FullHeartSource : EmptyHeartSource;
            spriteBatch.Draw(Game1.mouseCursors, new Vector2(x + i * 16, y), src, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 1f);
        }
    }

    private bool TryGetSocialPage(out object? socialPage)
    {
        socialPage = null;
        if (!Context.IsWorldReady || Game1.activeClickableMenu is not GameMenu menu)
        {
            return false;
        }

        try
        {
            int currentTab = this.mod.Helper.Reflection.GetField<int>(menu, "currentTab").GetValue();
            List<IClickableMenu> pages = this.mod.Helper.Reflection.GetField<List<IClickableMenu>>(menu, "pages").GetValue();
            if (currentTab < 0 || currentTab >= pages.Count || pages[currentTab] is not SocialPage)
            {
                return false;
            }

            socialPage = pages[currentTab];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private IEnumerable<ClickableTextureComponent> GetSocialEntryComponents(object socialPage)
    {
        FieldInfo? field = socialPage.GetType().GetField("sprites", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field?.GetValue(socialPage) is IEnumerable<ClickableTextureComponent> typed)
        {
            return typed;
        }

        foreach (FieldInfo fallback in socialPage.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (!typeof(System.Collections.IEnumerable).IsAssignableFrom(fallback.FieldType))
            {
                continue;
            }

            if (fallback.GetValue(socialPage) is not System.Collections.IEnumerable raw)
            {
                continue;
            }

            List<ClickableTextureComponent> list = new();
            foreach (object? item in raw)
            {
                if (item is ClickableTextureComponent c)
                {
                    list.Add(c);
                }
            }

            if (list.Count > 0)
            {
                return list;
            }
        }

        return Array.Empty<ClickableTextureComponent>();
    }

    private Farmer? ResolveFarmerForSocialEntry(ClickableTextureComponent component)
    {
        string key = component.name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (long.TryParse(key, out long playerId))
        {
            Farmer? byId = this.mod.FindFarmerById(playerId, includeOffline: true);
            if (byId is not null)
            {
                return byId;
            }
        }

        return Game1.getAllFarmers().FirstOrDefault(p =>
            p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)
            || key.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
    }
}
