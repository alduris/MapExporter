using System;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class MainMenuTab(OptionInterface owner) : BaseTab(owner, "Main Menu")
    {
        const string HOW_TO_STRING = "1. Run screenshotter on region\n2. Edit layout\n3. Finalize in generator tab\n4. Test interactive map\n5. Export";
        const string CREDITS_STRING =
            "Made by Henpemaz, Dual-Iron, Noblecat57, & Alduris\n" +
            "with help from Bro748, Vigaro, BensoneWhite, &\n" +
            "iwantbread\n" +
            "\n" +
            "";
        public override void Initialize()
        {
            const int COLUMN_COUNT = 4;
            const float COLUMN_GAP = 20f;
            static float Column(int c, bool label = false) => ((MENU_SIZE - 10f * 2 - (COLUMN_COUNT - c + 1) * COLUMN_GAP) / COLUMN_COUNT) * c + COLUMN_GAP * c + (label ? 30 : 0);
            static float Row(int r) => 355 - 30 * r;

            OpPOIconManager iconManager = null;
            AddItems(
                // Title
                new OpShinyLabel(new Vector2(0f, 570f), new Vector2(600f, 30f), "MAP EXPORTER", FLabelAlignment.Center, true),
                new OpImage(new Vector2(0f, 559f), "pixel") { scale = new Vector2(600f, 2f), color = MenuColorEffect.rgbMediumGrey },

                // How to
                new OpLabel(10f, 520f, "HOW TO USE", true),
                new OpLabelLong(new Vector2(10f, 445f), new Vector2(280f, 75f), HOW_TO_STRING, false, FLabelAlignment.Left), // line height = 15f

                new OpImage(new Vector2(299f, 435f), "pixel") { scale = new Vector2(2f, 550f - 425f), color = MenuColorEffect.rgbMediumGrey },

                // Statistics idk something in this section
                new OpLabel(310f, 520f, "CREDITS", true),
                new OpLabelLong(new Vector2(310, 435f), new Vector2(280f, 75f), CREDITS_STRING, false, FLabelAlignment.Left),
                // todo: instead of credits, quick links to... github, folders, etc

                // Options
                new OpImage(new Vector2(0f, 424f), "pixel") { scale = new Vector2(600f, 2f), color = MenuColorEffect.rgbMediumGrey },
                new OpLabel(new Vector2(0f, 385f), new Vector2(600f, 30f), "OPTIONS", FLabelAlignment.Center, true),
                // Showing things: insects, echoes, iterators, guardians, creatures in screenshots

                MapToPreference(Preferences.ShowCreatures, 0, 0, "Show creatures in the rooms of screenshots. Makes the screenshotting process slower so they can move out of their dens."),
                new OpLabel(Column(0, true), Row(0), "Show creatures"),
                MapToPreference(Preferences.ShowGhosts, 0, 1, "Show echoes in rooms. Echo effect only appears in the room they spawn in."),
                new OpLabel(Column(0, true), Row(1), "Show echoes"),
                MapToPreference(Preferences.ShowGuardians, 0, 2, "Show guardians where they spawn (e.g. Depths, Rubicon, Far Shore, etc)."),
                new OpLabel(Column(0, true), Row(2), "Show guardians"),
                MapToPreference(Preferences.ShowInsects, 0, 3, "Show bugs spawned from insect groups or room effects in rooms."),
                new OpLabel(Column(0, true), Row(3), "Show insects"),
                MapToPreference(Preferences.ShowOracles, 0, 4, "Show iterators where they spawn."),
                new OpLabel(Column(0, true), Row(4), "Show iterators"),

                new OpLabel(Column(1), Row(0), "SCREENSHOTTER"),
                MapToPreference(Preferences.ScreenshotterAutoFill, 1, 1, "Screenshotter: auto-fill with all slugcats that are marked as being able to access the region."),
                new OpLabel(Column(1, true), Row(1), "Slugcat auto-fill"),
                MapToPreference(Preferences.ScreenshotterSkipExisting, 1, 2, "Screenshotter: don't overwrite existing screenshots"),
                new OpLabel(Column(1, true), Row(2), "Skip existing screenshots"),

                new OpLabel(Column(1), Row(3), "MAP EDITOR"),
                MapToPreference(Preferences.EditorCheckOverlap, 1, 4, "Map editor: show cameras that overlap. Causes lag."),
                new OpLabel(Column(1, true), Row(4), "Show overlapping cams"),
                MapToPreference(Preferences.EditorShowCameras, 1, 5, "Map editor: toggles between showing all cameras in the room (checked) or just an outline containing the cameras (unchecked)"),
                new OpLabel(Column(1, true), Row(5), "Show individual cameras"),

                new OpLabel(Column(1), Row(6), "GENERATOR"),
                MapToPreference(Preferences.GeneratorLessInsense, 1, 7, "Generator: makes some parts do less calculation as a performance saver at the cost of taking longer."),
                new OpLabel(Column(1, true), Row(7), "Less intensive"),

                new OpLabel(Column(2), Row(0), "PLACED OBJECTS"),
                iconManager = new OpPOIconManager(new Vector2(Column(2), 10f), new Vector2(Column(2) - Column(0) - COLUMN_GAP, Row(0) - 10f))
            );
            iconManager.Initialize();

            static UIelement MapToPreference<T>(Preferences.Preference<T> preference, int c, int r, string description = null)
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
                        description = ((description ?? "") + " (default: " + (bDV ? "yes" : "no") + ")").TrimStart(' '),
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
                        description = ((description ?? "") + " (default: " + iDV + (preference.hasRange ? $", [{iMin}, {iMax}]" : "") + ")").TrimStart(' ')
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
