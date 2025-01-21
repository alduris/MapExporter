using System;
using System.Linq;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class MainMenuTab(OptionInterface owner) : BaseTab(owner, "Main Menu")
    {
        private static readonly float[] COLUMN_RATIOS = [0.75f, 1, 1.5f];
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

            OpPOIconManager iconManager = null;
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
                new OpLabel(new Vector2(0f, 385f), new Vector2(600f, 30f), Translate("OPTIONS"), FLabelAlignment.Center, true),

                new OpLabel(Column(0), Row(0), Translate("SHOW/HIDE")),
                new OpLabel(Column(1), Row(0), "SCREENSHOTTER"),
                new OpLabel(Column(1), Row(3), "MAP EDITOR"),
                new OpLabel(Column(1), Row(6), "GENERATOR"),

                new OpLabel(Column(2), Row(0), "PLACED OBJECTS"),
                iconManager = new OpPOIconManager(new Vector2(Column(2), 10f), new Vector2(ColumnWidth(2), Row(0) - 10f))
            );

            Preferences.Preference<bool>[] Col0 = [
                Preferences.ShowCreatures,
                Preferences.ShowGhosts,
                Preferences.ShowGuardians,
                Preferences.ShowInsects,
                Preferences.ShowOracles
            ];
            for (int i = 0; i < Col0.Length; i++)
            {
                AddItems(MapToPreference(Col0[i], 0, i + 1), new OpLabel(Column(0, true), Row(i + 1), Translate(Col0[i].key)));
            }

            Preferences.Preference<bool>[] Col1 = [
                Preferences.ScreenshotterAutoFill,
                Preferences.ScreenshotterSkipExisting,
                default,
                Preferences.EditorCheckOverlap,
                Preferences.EditorShowCameras
            ];
            for (int i = 0; i < Col1.Length; i++)
            {
                if (Col1[i].key is null) continue;
                AddItems(MapToPreference(Col1[i], 1, i + 1), new OpLabel(Column(1, true), Row(i + 1), Translate(Col1[i].key)));
            }

            Preferences.Preference<int>[] Col1b = [
                Preferences.GeneratorTargetFPS,
                Preferences.GeneratorCacheSize
            ];
            for (int i = 0; i < Col1b.Length; i++)
            {
                AddItems(MapToPreference(Col1b[i], 1, i + 1 + Col1.Length), new OpLabel(Column(1, true), Row(i + 1 + Col1.Length), Translate(Col1b[i].key)));
            }

            iconManager.Initialize();

            UIelement MapToPreference<T>(Preferences.Preference<T> preference, int c, int r)
            {
                // Create the OpCheckBox
                var val = preference.GetValue();
                if (!Data.UserPreferences.ContainsKey(preference.key))
                {
                    Data.UserPreferences.Add(preference.key, preference.defaultValue);
                }

                if (val is bool bV && preference.defaultValue is bool bDV)
                {
                    var checkbox = new OpCheckBox(OIUtil.CosmeticBind(bV), new Vector2(Column(c), Row(r)))
                    {
                        description = $"{Translate(preference.key + "/desc")} ({Translate("default")}: {Translate(bDV ? "Yes" : "No")})".TrimStart(' '),
                        colorEdge = bV ? MenuColorEffect.rgbWhite : MenuColorEffect.rgbMediumGrey
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

                    return checkbox;
                }
                else if (val is int iV && preference.defaultValue is int iDV && preference.minRange is int iMin && preference.maxRange is int iMax)
                {
                    var dragger = new OpDragger(preference.hasRange ? OIUtil.CosmeticRange(iV, iMin, iMax) : OIUtil.CosmeticBind(iV), new Vector2(Column(c), Row(r)))
                    {
                        description = $"{Translate(preference.key + "/desc")} ({Translate("default")}: {iDV}{(preference.hasRange ? $", [{iMin}, {iMax}]" : "")})".TrimStart(' ').TrimStart(' ')
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

                    return dragger;
                }
                else
                {
                    throw new ArgumentException("Preference is unsupported type!");
                }
            }
        }

        public override void Update() { }
    }
}
