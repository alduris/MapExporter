using System;
using System.Collections.Generic;
using System.Linq;
using MapExporterNew.Tabs.UI;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;
using static MapExporterNew.Preferences;

namespace MapExporterNew.Tabs
{
    internal class MainMenuTab(OptionInterface owner) : BaseTab(owner, "Main Menu")
    {
        private static readonly float[] COLUMN_RATIOS = [0.75f, 1];
        private static readonly float COLUMN_RATIO_SUM = COLUMN_RATIOS.Sum();
        private const float COLUMN_GAP = 20f;
        private static float Column(int c, bool label = false)
        {
            float totalWidth = MENU_SIZE - 10f * 2 - COLUMN_GAP * (COLUMN_RATIOS.Length - 1);
            float beforeWidth = 0f;
            for (int i = 0; i < c; i++) beforeWidth += COLUMN_RATIOS[i];
            return totalWidth * (beforeWidth / COLUMN_RATIO_SUM) + 10f + COLUMN_GAP * c + (label ? 30f : 0f);
        }
        private static float ColumnWidth(int c) => (MENU_SIZE - 10f * 2 - COLUMN_GAP * (COLUMN_RATIOS.Length - 1)) * (COLUMN_RATIOS[c] / COLUMN_RATIO_SUM);
        private static float Row(int r) => 355f - 30f * r;
        public override void Initialize()
        {
            string HOW_TO_STRING = Translate("MAPEX:mmtutorial").Replace("<LINE>", "\n");
            string CREDITS_STRING = Translate("MAPEX:mmcredits");

            //OpPOIconManager iconManager = null;
            AddItems(
                // Title
                new OpShinyLabel(new Vector2(0f, 570f), new Vector2(600f, 30f), Translate("MAP EXPORTER"), FLabelAlignment.Center, true),
                new OpImage(new Vector2(0f, 559f), "pixel") { scale = new Vector2(600f, 2f), color = MenuColorEffect.rgbMediumGrey },

                // How to
                new OpLabel(10f, 520f, Translate("HOW TO USE"), true),
                new OpLabelLong(new Vector2(10f, 445f), new Vector2(280f, 75f), HOW_TO_STRING, false, FLabelAlignment.Left), // line height = 15f

                new OpImage(new Vector2(299f, 435f), "pixel") { scale = new Vector2(2f, 550f - 425f), color = MenuColorEffect.rgbMediumGrey },

                // Statistics idk something in this section
                new OpLabel(310f, 520f, Translate("CREDITS"), true),
                new OpLabelLong(new Vector2(310, 435f), new Vector2(280f, 75f), CREDITS_STRING.WrapText(false, 280f, true), false, FLabelAlignment.Left),
                // todo: instead of credits, quick links to... github, folders, etc

                // Options
                new OpImage(new Vector2(0f, 424f), "pixel") { scale = new Vector2(600f, 2f), color = MenuColorEffect.rgbMediumGrey },
                new OpLabel(new Vector2(0f, 385f), new Vector2(600f, 30f), Translate("OPTIONS"), FLabelAlignment.Center, true)
            );

            DisplayCell[][] displayCells = [
                // COLUMN 0
                [
                    new LabelCell("SHOW/HIDE"),
                    new BoolPreferenceCell(ShowCreatures),
                    new BoolPreferenceCell(ShowGhosts),
                    new BoolPreferenceCell(ShowGuardians),
                    new BoolPreferenceCell(ShowInsects),
                    new BoolPreferenceCell(ShowOracles),
                    new BoolPreferenceCell(ShowPrince),
                ],

                // COLUMN 1
                [
                    new LabelCell("SCREENSHOTTER"),
                    new BoolPreferenceCell(ScreenshotterAutoFill),
                    new BoolPreferenceCell(ScreenshotterSkipExisting),

                    new LabelCell("MAP EDITOR"),
                    new BoolPreferenceCell(EditorCheckOverlap),
                    new BoolPreferenceCell(EditorShowCameras),

                    new LabelCell("GENERATOR"),
                    new BoolPreferenceCell(GeneratorSkipTiles),
                    new IntPreferenceCell(GeneratorCacheSize),
                    new IntPreferenceCell(GeneratorTargetFPS),
                ],
            ];

            List<UIelement> elements = [];
            for (int i = 0; i < displayCells.Length; i++)
            {
                for (int j = 0; j < displayCells[i].Length; j++)
                {
                    displayCells[i][j].Render(elements, new Vector2(Column(i, false), Row(j)), new Vector2(Column(i, true), Row(j)));
                }
            }

            AddItems([.. elements]);
        }

        public override void Update() { }

        private abstract class DisplayCell
        {
            public abstract void Render(List<UIelement> elements, Vector2 pos, Vector2 pos2);
            public string Translate(string key) => Custom.rainWorld.inGameTranslator.Translate(key);
        }

        private class LabelCell(string text) : DisplayCell
        {
            public override void Render(List<UIelement> elements, Vector2 pos, Vector2 pos2)
            {
                elements.Add(new OpLabel(pos.x, pos.y, Translate(text), false));
            }
        }

        private class WhitespaceCell() : DisplayCell
        {
            public override void Render(List<UIelement> elements, Vector2 pos, Vector2 pos2) { }
        }

        private class BoolPreferenceCell(Preference<bool> preference) : DisplayCell
        {
            public override void Render(List<UIelement> elements, Vector2 pos, Vector2 pos2)
            {
                if (!Data.UserPreferences.ContainsKey(preference.key))
                {
                    Data.UserPreferences.Add(preference.key, preference.defaultValue);
                }

                // Create element
                var checkbox = new OpCheckBox(OIUtil.CosmeticBind(preference.GetValue()), pos)
                {
                    description = $"{Translate(preference.key + "/desc")} ({Translate("default")}: {Translate(preference.defaultValue ? "Yes" : "No")})".TrimStart(' '),
                    colorEdge = preference.GetValue() ? MenuColorEffect.rgbWhite : MenuColorEffect.rgbMediumGrey
                };

                // Change when element changes
                checkbox.OnValueChanged += (_, v, ov) =>
                {
                    if (v != ov)
                    {
                        bool b = ValueConverter.ConvertToValue<bool>(v);
                        Data.UserPreferences[preference.key] = b;
                        Data.SaveData();
                        checkbox.colorEdge = b ? MenuColorEffect.rgbWhite : MenuColorEffect.rgbMediumGrey;
                    }
                };

                // Add
                elements.Add(checkbox);
                elements.Add(new OpLabel(pos2.x, pos2.y, Translate(preference.key)));
            }
        }

        private class IntPreferenceCell(Preference<int> preference) : DisplayCell
        {
            public override void Render(List<UIelement> elements, Vector2 pos, Vector2 pos2)
            {
                if (!Data.UserPreferences.ContainsKey(preference.key))
                {
                    Data.UserPreferences.Add(preference.key, preference.defaultValue);
                }

                // Create element
                var dragger = new OpDragger(preference.hasRange ? OIUtil.CosmeticRange(preference.GetValue(), preference.minRange, preference.maxRange) : OIUtil.CosmeticBind(preference.GetValue()), pos)
                {
                    description = $"{Translate(preference.key + "/desc")} ({Translate("default")}: {preference.defaultValue}{(preference.hasRange ? $", [{preference.minRange}, {preference.maxRange}]" : "")})".TrimStart(' ').TrimStart(' ')
                };

                // Change when element changes
                dragger.OnValueChanged += (_, v, ov) =>
                {
                    if (v != ov)
                    {
                        int i = ValueConverter.ConvertToValue<int>(v);
                        Data.UserPreferences[preference.key] = i;
                        Data.SaveData();
                    }
                };

                // Add
                elements.Add(dragger);
                elements.Add(new OpLabel(pos2.x, pos2.y, Translate(preference.key)));
            }
        }
    }
}
