using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using PlayerRomance.Data;
using PlayerRomance.UI;

namespace PlayerRomance.Patches
{
    public static class SocialPagePatches
    {
        private static ModEntry? Mod;
        private static FieldInfo? CachedEntriesField;
        private static bool IsFailed = false;

        // Textures Sources exactes de Stardew Valley
        private static readonly Rectangle HeartFull = new(218, 428, 7, 6);
        private static readonly Rectangle HeartEmpty = new(211, 428, 7, 6);
        private static readonly Rectangle GiftBoxEmpty = new(227, 425, 9, 9);
        private static readonly Rectangle GiftIconSource = new(229, 410, 14, 14);

        public static void Initialize(ModEntry mod)
        {
            Mod = mod;
            var harmony = new Harmony(mod.ModManifest.UniqueID);

            try
            {
                harmony.Patch(
                    original: AccessTools.Constructor(typeof(SocialPage), new[] { typeof(int), typeof(int), typeof(int), typeof(int) }),
                    postfix: new HarmonyMethod(typeof(SocialPagePatches), nameof(SocialPage_Constructor_Postfix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(SocialPage), "drawNPCSlot"),
                    prefix: new HarmonyMethod(typeof(SocialPagePatches), nameof(SocialPage_DrawNPCSlot_Prefix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(SocialPage), nameof(SocialPage.receiveLeftClick)),
                    prefix: new HarmonyMethod(typeof(SocialPagePatches), nameof(SocialPage_ReceiveLeftClick_Prefix))
                );
            }
            catch (Exception ex)
            {
                Mod.Monitor.Log($"[SocialPatches] Failed to apply harmony patches: {ex.Message}", LogLevel.Error);
            }
        }

        private static IList? GetSocialEntries(SocialPage instance)
        {
            if (IsFailed) return null;
            if (CachedEntriesField != null) return CachedEntriesField.GetValue(instance) as IList;

            var field = AccessTools.Field(typeof(SocialPage), "socialEntries")
                        ?? AccessTools.Field(typeof(SocialPage), "names");

            if (field == null)
            {
                var fields = AccessTools.GetDeclaredFields(typeof(SocialPage));
                field = fields.FirstOrDefault(f => f.FieldType.IsGenericType && f.FieldType.GetGenericArguments()[0].Name.Contains("SocialEntry"));
            }

            if (field == null) { IsFailed = true; return null; }
            CachedEntriesField = field;
            return field.GetValue(instance) as IList;
        }

        private static void SocialPage_Constructor_Postfix(SocialPage __instance)
        {
            if (Mod == null || IsFailed) return;
            try
            {
                IList? entries = GetSocialEntries(__instance);
                if (entries == null) return;
                Type entryType = entries.GetType().GetGenericArguments()[0];
                var relationships = Mod.DatingSystem.GetRelationshipsForPlayer(Mod.LocalPlayerId);

                foreach (var relation in relationships)
                {
                    long partnerId = relation.GetOther(Mod.LocalPlayerId);
                    // Le partenaire sera trouvé même s'il est hors-ligne grâce à getAllFarmers
                    Farmer partner = Mod.FindFarmerById(partnerId, true);
                    if (partner != null)
                    {
                        object? newEntry = null;
                        try { newEntry = Activator.CreateInstance(entryType, new object[] { partner, partner.UniqueMultiplayerID }); }
                        catch
                        {
                            try { newEntry = Activator.CreateInstance(entryType, new object[] { partner }); } catch { }
                        }
                        if (newEntry != null) entries.Insert(0, newEntry);
                    }
                }
                __instance.updateSlots();
            }
            catch (Exception ex) { Mod.Monitor.Log($"[SocialPatches] Error: {ex.Message}", LogLevel.Warn); }
        }

        private static bool SocialPage_DrawNPCSlot_Prefix(SocialPage __instance, SpriteBatch b, int i)
        {
            if (Mod == null || IsFailed) return true;
            try
            {
                IList? entries = GetSocialEntries(__instance);
                if (entries == null || i < 0 || i >= entries.Count) return true;

                object entry = entries[i];
                var charField = AccessTools.Field(entry.GetType(), "Character");
                if (charField == null) return true;

                if (charField.GetValue(entry) is Farmer farmer && farmer.UniqueMultiplayerID != Mod.LocalPlayerId)
                {
                    int slotPosition = Mod.Helper.Reflection.GetField<int>(__instance, "slotPosition").GetValue();
                    int y = __instance.yPositionOnScreen + 100 + (i - slotPosition) * 112;
                    int x = __instance.xPositionOnScreen + 32;
                    int width = __instance.width - 64;

                    var relation = Mod.DatingSystem.GetRelationship(Mod.LocalPlayerId, farmer.UniqueMultiplayerID);
                    if (relation != null)
                    {
                        DrawPlayerRow(b, farmer, relation, x, y, width);
                    }
                    return false;
                }
            }
            catch { return true; }
            return true;
        }

        private static bool SocialPage_ReceiveLeftClick_Prefix(SocialPage __instance, int x, int y)
        {
            if (Mod == null || IsFailed) return true;
            try
            {
                int slotPosition = Mod.Helper.Reflection.GetField<int>(__instance, "slotPosition").GetValue();
                int index = (y - __instance.yPositionOnScreen - 100) / 112 + slotPosition;
                IList? entries = GetSocialEntries(__instance);
                if (entries == null) return true;

                if (index >= 0 && index < entries.Count)
                {
                    object entry = entries[index];
                    var charField = AccessTools.Field(entry.GetType(), "Character");
                    if (charField?.GetValue(entry) is Farmer farmer && farmer.UniqueMultiplayerID != Mod.LocalPlayerId)
                    {
                        Game1.playSound("bigSelect");
                        Game1.activeClickableMenu = new PlayerSocialProfileMenu(Mod, farmer.UniqueMultiplayerID);
                        return false;
                    }
                }
            }
            catch { }
            return true;
        }

        private static void DrawPlayerRow(SpriteBatch b, Farmer farmer, RelationshipRecord relation, int x, int y, int width)
        {
            // --- 1. DÉTECTION HOVER ---
            Rectangle rowBounds = new Rectangle(x, y, width, 112);
            bool isHovered = rowBounds.Contains(Game1.getMouseX(), Game1.getMouseY());

            // --- 2. DESSIN DU FOND ---
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), x, y, width, 112, Color.White, 4f, false);

            // --- 3. SURBRILLANCE (Hover) ---
            if (isHovered)
            {
                // Couleur bleutée semi-transparente
                b.Draw(Game1.staminaRect, new Rectangle(x + 4, y + 4, width - 8, 104), Color.SkyBlue * 0.3f);
            }

            // --- 4. PORTRAIT ---
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15), x + 24, y + 12, 88, 88, Color.White, 4f, false);
            farmer.FarmerRenderer.drawMiniPortrat(b, new Vector2(x + 36, y + 24), 0.88f, 4f, 2, farmer);

            // --- 5. TEXTES ---
            Utility.drawTextWithShadow(b, farmer.Name, Game1.dialogueFont, new Vector2(x + 128, y + 16), Game1.textColor);
            b.DrawString(Game1.smallFont, GetStatusText(relation), new Vector2(x + 128, y + 56), Game1.textShadowColor);

            // --- 6. CŒURS ---
            int hearts = relation.GetHeartLevel(Mod.Config.HeartPointsPerHeart, Mod.Config.MaxHearts);
            int maxHearts = Math.Min(12, Mod.Config.MaxHearts);
            int heartsX = x + 350;
            int heartsY = y + 40;

            for (int k = 0; k < maxHearts; k++)
            {
                if (k >= 10) break;
                Rectangle src = (k < hearts) ? HeartFull : HeartEmpty;

                // Effet de vibration si survolé (Juicy effect)
                Vector2 heartPos = new Vector2(heartsX + (k * 32), heartsY);
                if (isHovered) heartPos.Y += (float)(Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 150.0 + k) * 2.0);

                b.Draw(Game1.mouseCursors, heartPos, src, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.88f);
            }

            // --- 7. CADEAUX (Icônes comme les NPCs) ---
            int giftX = x + width - 110;
            int giftY = y + 30;

            // Icône cadeau (petit nœud)
            b.Draw(Game1.mouseCursors, new Vector2(giftX + 10, giftY - 24), GiftIconSource, Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0.88f);
            // Les deux cases cadeaux hebdomadaires
            b.Draw(Game1.mouseCursors, new Vector2(giftX, giftY), GiftBoxEmpty, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0.88f);
            b.Draw(Game1.mouseCursors, new Vector2(giftX + 36, giftY), GiftBoxEmpty, Color.White, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0.88f);
        }

        private static string GetStatusText(RelationshipRecord relation)
        {
            return relation.State switch
            {
                RelationshipState.Dating => "(dating)",
                RelationshipState.Engaged => "(engaged)",
                RelationshipState.Married => "(married)",
                _ => "(single)"
            };
        }
    }
}