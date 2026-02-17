using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;
using PlayerRomance.Data;

namespace PlayerRomance.UI
{
    public sealed class ChildrenManagementMenu : IClickableMenu
    {
        private readonly ModEntry mod;
        private readonly List<ChildRecord> children;
        private ClickableTextureComponent closeButton;

        // Liste des lignes d'enfants
        private readonly List<ChildRowComponent> childRows = new();

        private string hoverText = "";

        // Constantes de design (Taille idéale)
        private const int ROW_HEIGHT = 112;
        private const int MAX_WIDTH = 900;
        private const int MAX_HEIGHT = 700;

        public ChildrenManagementMenu(ModEntry mod)
        {
            this.mod = mod;

            // On charge les enfants une seule fois
            this.children = this.GetChildrenForLocal()
                .OrderByDescending(c => c.AgeYears)
                .ThenBy(c => c.ChildName)
                .ToList();

            // Initialisation de la mise en page (calcule positions et tailles)
            this.UpdateLayout();
        }

        /// <summary>
        /// Méthode native de Stardew Valley appelée quand la fenêtre change de taille.
        /// </summary>
        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.UpdateLayout();
        }

        /// <summary>
        /// Recalcule toutes les positions en fonction de la taille actuelle de l'écran.
        /// </summary>
        private void UpdateLayout()
        {
            // 1. Calculer la taille du menu en fonction de l'écran (Responsive)
            // On prend 90% de l'écran ou la taille Max, selon le plus petit.
            this.width = Math.Min(MAX_WIDTH, Game1.uiViewport.Width - 64);
            this.height = Math.Min(MAX_HEIGHT, Game1.uiViewport.Height - 64);

            // 2. Centrer le menu
            this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
            this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;

            // 3. Recréer le bouton fermer (ancré en haut à droite)
            this.closeButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen - 8, 48, 48),
                Game1.mouseCursors,
                new Rectangle(337, 494, 12, 12),
                4f);

            // 4. Reconstruire les lignes d'enfants
            this.childRows.Clear();

            int contentTopY = this.yPositionOnScreen + 100; // Marge pour le titre
            int availableWidth = this.width - 64; // Largeur interne (avec marges)
            int rowX = this.xPositionOnScreen + 32;

            // Pour éviter que les lignes sortent du bas de l'écran si l'écran est tout petit
            int maxRowsVisible = (this.height - 120) / (ROW_HEIGHT + 8);
            int rowsToDraw = Math.Min(this.children.Count, maxRowsVisible);

            for (int i = 0; i < rowsToDraw; i++)
            {
                ChildRecord child = this.children[i];
                int currentY = contentTopY + (i * (ROW_HEIGHT + 8));

                // Bouton d'assignation (Travail) - Position relative à droite
                ClickableTextureComponent? workBtn = null;
                bool canWork = child.AgeYears >= Math.Max(16, this.mod.Config.AdultWorkMinAge);

                if (canWork)
                {
                    // Ancré à droite de la ligne
                    int btnSize = 64;
                    int btnX = rowX + availableWidth - btnSize - 16;
                    int btnY = currentY + (ROW_HEIGHT - btnSize) / 2; // Centré verticalement dans la ligne

                    workBtn = new ClickableTextureComponent(
                        new Rectangle(btnX, btnY, btnSize, btnSize),
                        Game1.mouseCursors,
                        new Rectangle(366, 373, 16, 16),
                        4f
                    )
                    {
                        myID = child.ChildId.GetHashCode(),
                        hoverText = "Assigner une tâche"
                    };
                }

                // Création de la ligne avec ses limites dynamiques
                Rectangle rowBounds = new Rectangle(rowX, currentY, availableWidth, ROW_HEIGHT);
                this.childRows.Add(new ChildRowComponent(child, workBtn, rowBounds));
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            if (this.closeButton.containsPoint(x, y))
            {
                Game1.playSound("bigDeSelect");
                this.exitThisMenu();
                return;
            }

            foreach (var row in this.childRows)
            {
                if (row.WorkButton != null && row.WorkButton.containsPoint(x, y))
                {
                    Game1.playSound("smallSelect");
                    Game1.activeClickableMenu = new ChildTaskAssignmentMenu(this.mod, row.Data.ChildId, row.Data.ChildName);
                    return;
                }
            }

            base.receiveLeftClick(x, y, playSound);
        }

        public override void performHoverAction(int x, int y)
        {
            this.hoverText = "";
            this.closeButton.tryHover(x, y);

            foreach (var row in this.childRows)
            {
                if (row.WorkButton != null)
                {
                    row.WorkButton.tryHover(x, y);
                    if (row.WorkButton.containsPoint(x, y))
                    {
                        this.hoverText = row.WorkButton.hoverText;
                    }
                }

                // --- Logique responsive pour le survol des icones ---
                // On utilise les mêmes ratios que dans DrawChildRow pour trouver la position

                // Zone Icons (apx 40% de la largeur)
                int iconsStartX = row.Bounds.X + (int)(row.Bounds.Width * 0.40f);
                int iconsY = row.Bounds.Y + 24;

                // Icone Nourriture
                if (new Rectangle(iconsStartX, iconsY, 32, 32).Contains(x, y))
                    this.hoverText = row.Data.IsFedToday ? "Bien nourri" : "A faim !";

                // Icone Soin
                if (new Rectangle(iconsStartX + 48, iconsY, 32, 32).Contains(x, y))
                    this.hoverText = row.Data.IsCaredToday ? "S'est senti aimé aujourd'hui" : "A besoin d'attention";

                // Icone Jeu
                if (new Rectangle(iconsStartX + 96, iconsY, 32, 32).Contains(x, y))
                    this.hoverText = row.Data.IsPlayedToday ? "S'est amusé aujourd'hui" : "S'ennuie";
            }
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.6f);

            // Fond
            Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, false, true);

            // Titre centré
            string title = "Gestion des Enfants";
            SpriteText.drawStringHorizontallyCenteredAt(b, title, this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen + 30);

            // Entêtes de colonnes dynamiques
            int colY = this.yPositionOnScreen + 80;
            // On place les textes selon des pourcentages de la largeur
            b.DrawString(Game1.smallFont, "Identité", new Vector2(this.xPositionOnScreen + 64, colY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Besoins & Education", new Vector2(this.xPositionOnScreen + (this.width * 0.45f), colY), Game1.textColor);
            b.DrawString(Game1.smallFont, "Action", new Vector2(this.xPositionOnScreen + (this.width - 140), colY), Game1.textColor);

            foreach (var row in this.childRows)
            {
                this.DrawChildRow(b, row);
            }

            if (this.children.Count == 0)
            {
                string emptyMsg = "Aucun enfant à gérer pour le moment.";
                Vector2 size = Game1.smallFont.MeasureString(emptyMsg);
                b.DrawString(Game1.smallFont, emptyMsg,
                    new Vector2(this.xPositionOnScreen + (this.width - size.X) / 2, this.yPositionOnScreen + 200),
                    Color.Gray);
            }

            this.closeButton.draw(b);

            if (!string.IsNullOrEmpty(this.hoverText))
            {
                IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
            }

            this.drawMouse(b);
        }

        private void DrawChildRow(SpriteBatch b, ChildRowComponent row)
        {
            // Fond de la ligne
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 396, 15, 15),
                row.Bounds.X, row.Bounds.Y, row.Bounds.Width, row.Bounds.Height, Color.White, 4f, false);

            // 1. Portrait (Gauche)
            NPC npcChild = Game1.getCharacterFromName(row.Data.ChildName);
            Vector2 portraitPos = new Vector2(row.Bounds.X + 24, row.Bounds.Y + 24);

            if (npcChild != null)
            {
                b.Draw(npcChild.Sprite.Texture,
                    portraitPos,
                    new Rectangle(0, 0, 16, 24),
                    Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
            }
            else
            {
                b.Draw(Game1.mouseCursors, portraitPos, new Rectangle(896, 336, 16, 16), Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.89f);
            }

            // 2. Info Texte (Gauche + décalage)
            int textStartX = row.Bounds.X + 96;
            b.DrawString(Game1.dialogueFont, row.Data.ChildName, new Vector2(textStartX, row.Bounds.Y + 20), Game1.textColor);
            string ageString = $"{row.Data.AgeYears} an(s) (Stade: {row.Data.Stage})";
            b.DrawString(Game1.smallFont, ageString, new Vector2(textStartX, row.Bounds.Y + 60), Game1.textShadowColor);

            // 3. Icones de statut (Milieu - Position relative 40%)
            int statusStartX = row.Bounds.X + (int)(row.Bounds.Width * 0.40f);
            int iconY = row.Bounds.Y + 24;

            DrawStatusIcon(b, 1, row.Data.IsFedToday, statusStartX, iconY);
            DrawStatusIcon(b, 2, row.Data.IsCaredToday, statusStartX + 48, iconY);
            DrawStatusIcon(b, 3, row.Data.IsPlayedToday, statusStartX + 96, iconY);

            // 4. Barre de progression (Sous les icones)
            int barWidth = (int)(row.Bounds.Width * 0.25f); // La barre prend 25% de la ligne
            int barX = statusStartX;
            int barY = row.Bounds.Y + 70;
            int barHeight = 24;

            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(403, 383, 6, 6), barX, barY, barWidth, barHeight, Color.White, 4f, false);

            float progress = Math.Clamp(row.Data.FeedingProgress / 100f, 0f, 1f);
            if (progress > 0)
            {
                Color barColor = progress > 0.8f ? Color.LimeGreen : (progress > 0.4f ? Color.Orange : Color.Red);
                b.Draw(Game1.staminaRect, new Rectangle(barX + 4, barY + 4, (int)((barWidth - 8) * progress), barHeight - 8), barColor);
            }
            Utility.drawTextWithShadow(b, "Education", Game1.tinyFont, new Vector2(barX + barWidth / 2 - 20, barY + 4), Game1.textColor);

            // 5. Bouton Action (Déjà positionné dans UpdateLayout, on le dessine juste)
            if (row.WorkButton != null)
            {
                row.WorkButton.draw(b);
            }
            else
            {
                // Texte aligné à droite si pas de bouton
                Vector2 textSize = Game1.tinyFont.MeasureString("Trop jeune");
                b.DrawString(Game1.tinyFont, "Trop jeune",
                    new Vector2(row.Bounds.Right - textSize.X - 32, row.Bounds.Center.Y - (textSize.Y / 2)),
                    Color.Gray);
            }
        }

        private void DrawStatusIcon(SpriteBatch b, int type, bool isActive, int x, int y)
        {
            Color c = isActive ? Color.White : Color.Black * 0.3f;

            if (type == 1) // Food
                b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(182, 383, 16, 16), c, 0f, Vector2.Zero, 2.5f, SpriteEffects.None, 0.9f);
            else if (type == 2) // Heart
                b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(211, 428, 7, 6), c, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0.9f);
            else if (type == 3) // Fun
                b.Draw(Game1.mouseCursors, new Vector2(x, y), new Rectangle(20, 428, 10, 10), c, 0f, Vector2.Zero, 3.5f, SpriteEffects.None, 0.9f);
        }

        private IEnumerable<ChildRecord> GetChildrenForLocal()
        {
            return this.mod.IsHostPlayer
                ? this.mod.HostSaveData.Children.Values
                : this.mod.ClientSnapshot.Children;
        }

        private class ChildRowComponent
        {
            public ChildRecord Data { get; }
            public ClickableTextureComponent? WorkButton { get; }
            public Rectangle Bounds { get; }

            public ChildRowComponent(ChildRecord data, ClickableTextureComponent? btn, Rectangle bounds)
            {
                this.Data = data;
                this.WorkButton = btn;
                this.Bounds = bounds;
            }
        }
    }
}