using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace PlayerRomance.Patches
{
    /// <summary>
    /// Harmony patches on <see cref="GameMenu"/> to add a Romance tab between Collections and Options.
    /// We never modify <c>tabs</c> or <c>pages</c> — instead we:
    ///  • Draw the extra tab in a <c>draw</c> postfix (cursor re-drawn at end to fix z-order).
    ///  • Intercept clicks in a <c>receiveLeftClick</c> prefix.
    /// Back-button navigation state is stored here and drawn via RenderedActiveMenu.
    /// </summary>
    public static class GameMenuPatches
    {
        // Heart icon sprite from Game1.mouseCursors (7×6 px source, drawn at 4× = 28×24).
        private static readonly Rectangle HeartIconSource = new(218, 428, 7, 6);

        // Vanilla tab texture box source (same rect used by vanilla tabs inside GameMenu.draw).
        private static readonly Rectangle TabBoxSource = new(16, 368, 16, 16);

        // Left-arrow sprite (vanilla scroll-left arrow used in collection/shop pages).
        private static readonly Rectangle LeftArrowSource = new(352, 495, 12, 11);

        private static ModEntry? Mod;

        // Lazily-cached tab metric fields (read via reflection once).
        private static FieldInfo? TabsField;
        private static bool FieldsCached;

        // ─── Back-button navigation state ────────────────────────────────────
        // Set by RomanceTabPage when opening a sub-menu; cleared on back-click.
        private static bool _showBackButton;
        private static int _returnX, _returnY, _returnW, _returnH;

        /// <summary>Register all Harmony patches.</summary>
        public static void Initialize(ModEntry mod)
        {
            Mod = mod;
            var harmony = new Harmony(mod.ModManifest.UniqueID + ".gamemenu");

            try
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(GameMenu), "draw", new[] { typeof(SpriteBatch) }),
                    postfix: new HarmonyMethod(typeof(GameMenuPatches), nameof(DrawPostfix)));

                harmony.Patch(
                    original: AccessTools.Method(typeof(GameMenu), "receiveLeftClick", new[] { typeof(int), typeof(int), typeof(bool) }),
                    prefix: new HarmonyMethod(typeof(GameMenuPatches), nameof(ReceiveLeftClickPrefix)));

                mod.Monitor.Log("[PR.UI.RomanceTab] GameMenu patches applied.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"[PR.UI.RomanceTab] Failed to apply GameMenu patches: {ex}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Registers the back-button input handler. Must be called after Initialize.
        /// Uses a separate subscription that runs even when a menu is open.
        /// </summary>
        public static void RegisterNavigationEvents(IModEvents events)
        {
            events.Input.ButtonPressed += OnButtonPressedForNavigation;
        }

        // ─── Back-button public API (called by RomanceTabPage) ────────────────

        /// <summary>Called by RomanceTabPage before opening a sub-menu.</summary>
        public static void SetReturnPoint(ModEntry mod, int x, int y, int w, int h)
        {
            Mod = mod;
            _returnX = x; _returnY = y; _returnW = w; _returnH = h;
            _showBackButton = true;
        }

        /// <summary>Clears the back-button state (called after back is pressed).</summary>
        public static void ClearReturnPoint()
        {
            _showBackButton = false;
        }

        /// <summary>
        /// Draws the back-button overlay when a sub-menu is active.
        /// Call this from RenderedActiveMenu so it draws on top of everything.
        /// </summary>
        public static void DrawNavigationOverlay(SpriteBatch b)
        {
            // Auto-clear when the user returned to vanilla GameMenu via ESC.
            if (Game1.activeClickableMenu is GameMenu)
            {
                _showBackButton = false;
                return;
            }

            if (!_showBackButton || Game1.activeClickableMenu is null)
                return;

            IClickableMenu menu = Game1.activeClickableMenu;
            Rectangle backBounds = GetBackButtonBounds(menu);

            // Draw vanilla-style tab/button box.
            bool hovered = backBounds.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color tint = hovered ? Color.Wheat : Color.White;
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(384, 396, 15, 15),
                backBounds.X, backBounds.Y, backBounds.Width, backBounds.Height,
                tint, 4f, false);

            // Draw left-arrow icon centred in the button.
            int arrW = (int)(LeftArrowSource.Width * 4f);
            int arrH = (int)(LeftArrowSource.Height * 4f);
            b.Draw(
                Game1.mouseCursors,
                new Vector2(backBounds.Center.X - arrW / 2f, backBounds.Center.Y - arrH / 2f),
                LeftArrowSource,
                Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

            // Tooltip on hover.
            if (hovered)
            {
                IClickableMenu.drawHoverText(b, "Back", Game1.smallFont);
            }

            // Re-draw mouse cursor on top of our overlay.
            menu.drawMouse(b);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Harmony postfix: draw the Romance tab after the vanilla tabs
        // ─────────────────────────────────────────────────────────────────────

        private static void DrawPostfix(GameMenu __instance, SpriteBatch b)
        {
            if (Mod is null || !Mod.Config.EnableRomanceTab || Mod.Config.DisableRomanceTabOnError)
                return;

            try
            {
                Rectangle tabBounds = GetRomanceTabBounds(__instance);
                if (tabBounds == Rectangle.Empty)
                    return;

                bool hovered = tabBounds.Contains(Game1.getMouseX(), Game1.getMouseY());

                // Draw vanilla-style tab box.
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.mouseCursors,
                    TabBoxSource,
                    tabBounds.X,
                    tabBounds.Y,
                    tabBounds.Width,
                    tabBounds.Height,
                    hovered ? Color.Wheat : Color.White,
                    4f,
                    false);

                // Draw heart icon centred in the tab.
                float ix = tabBounds.Center.X - 14f;
                float iy = tabBounds.Center.Y - 12f;
                b.Draw(Game1.mouseCursors, new Vector2(ix, iy), HeartIconSource, Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 1f);

                // Show hover tooltip.
                if (hovered)
                    IClickableMenu.drawHoverText(b, "Romance", Game1.smallFont);

                // Re-draw mouse cursor ON TOP of our tab box to fix z-ordering:
                // GameMenu.draw() calls drawMouse before this postfix runs, so without
                // this re-draw the cursor appears behind our tab graphic.
                __instance.drawMouse(b);
            }
            catch (Exception ex)
            {
                DisableOnError(ex);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Harmony prefix: intercept left-clicks that land on the Romance tab
        // ─────────────────────────────────────────────────────────────────────

        private static bool ReceiveLeftClickPrefix(GameMenu __instance, int x, int y)
        {
            if (Mod is null || !Mod.Config.EnableRomanceTab || Mod.Config.DisableRomanceTabOnError)
                return true; // run original

            try
            {
                Rectangle tabBounds = GetRomanceTabBounds(__instance);
                if (tabBounds == Rectangle.Empty || !tabBounds.Contains(x, y))
                    return true; // run original

                // Click landed on our tab — open the Romance page.
                Game1.playSound("smallSelect");
                Game1.activeClickableMenu = new UI.RomanceTabPage(
                    Mod,
                    __instance.xPositionOnScreen,
                    __instance.yPositionOnScreen,
                    __instance.width,
                    __instance.height);

                return false; // skip original
            }
            catch (Exception ex)
            {
                DisableOnError(ex);
                return true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes where the Romance tab should be drawn: the slot immediately after
        /// the LAST vanilla tab.  Pure read-only — we never mutate the list.
        /// </summary>
        private static Rectangle GetRomanceTabBounds(GameMenu menu)
        {
            EnsureFields();

            if (TabsField?.GetValue(menu) is not List<ClickableComponent> tabs || tabs.Count == 0)
                return Rectangle.Empty;

            // Place after the last existing tab.
            ClickableComponent lastTab = tabs[tabs.Count - 1];
            int spacing = ComputeTabSpacing(tabs, tabs.Count - 1);

            return new Rectangle(
                lastTab.bounds.X + spacing,
                lastTab.bounds.Y,
                lastTab.bounds.Width,
                lastTab.bounds.Height);
        }

        private static int ComputeTabSpacing(List<ClickableComponent> tabs, int index)
        {
            if (index + 1 < tabs.Count)
            {
                int d = tabs[index + 1].bounds.X - tabs[index].bounds.X;
                if (d > 0) return d;
            }
            if (index > 0)
            {
                int d = tabs[index].bounds.X - tabs[index - 1].bounds.X;
                if (d > 0) return d;
            }
            return tabs[index].bounds.Width;
        }

        private static void EnsureFields()
        {
            if (FieldsCached) return;
            TabsField = AccessTools.Field(typeof(GameMenu), "tabs");
            FieldsCached = true;
        }

        private static void DisableOnError(Exception ex)
        {
            if (Mod is null) return;
            Mod.Config.DisableRomanceTabOnError = true;
            Mod.Helper.WriteConfig(Mod.Config);
            Mod.Monitor.Log($"[PR.UI.RomanceTab] GameMenu patch disabled due to error: {ex}", LogLevel.Error);
        }

        // ─── Back-button input handler ────────────────────────────────────────

        /// <summary>
        /// Handles back-button clicks. Registered as a SMAPI ButtonPressed handler
        /// (not gated by IsPlayerFree so it fires while menus are open).
        /// </summary>
        private static void OnButtonPressedForNavigation(object? sender, ButtonPressedEventArgs e)
        {
            if (!_showBackButton || Mod is null) return;
            if (e.Button != SButton.MouseLeft) return;
            if (Game1.activeClickableMenu is null) return;

            int mx = Game1.getMouseX();
            int my = Game1.getMouseY();
            Rectangle backBounds = GetBackButtonBounds(Game1.activeClickableMenu);
            if (!backBounds.Contains(mx, my)) return;

            // Capture coordinates before clearing (ClearReturnPoint zeroes the fields).
            int rx = _returnX, ry = _returnY, rw = _returnW, rh = _returnH;
            ClearReturnPoint();
            Game1.playSound("smallSelect");
            Game1.activeClickableMenu = new UI.RomanceTabPage(Mod, rx, ry, rw, rh);
        }

        private static Rectangle GetBackButtonBounds(IClickableMenu menu)
        {
            // Mirror the close button position on the left side of the menu frame.
            return new Rectangle(
                menu.xPositionOnScreen - 12,
                menu.yPositionOnScreen - 8,
                48, 48);
        }
    }
}
