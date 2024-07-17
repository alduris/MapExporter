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
            "with help from Bro748, Vigaro, BensoneWhite, & iwantbread\n" +
            "\n" +
            "\n" +
            "";
        public override void Initialize()
        {
            OpHoldButton deleteScreenshots, deleteOutput, deleteAll;
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

                MapToPreference(Data.PreferenceKeys.SHOW_CREATURES, false, new Vector2(10f, 355f), "Show creatures in the rooms of screenshots. Makes the screenshotting process slower so they can move out of their dens."),
                new OpLabel(40f, 355f, "Show creatures"),
                MapToPreference(Data.PreferenceKeys.SHOW_GHOSTS, true, new Vector2(10f, 325f), "Show echoes in rooms. Echo effect only appears in the room they spawn in."),
                new OpLabel(40f, 325f, "Show echoes"),
                MapToPreference(Data.PreferenceKeys.SHOW_GUARDIANS, true, new Vector2(10f, 295f), "Show guardians where they spawn (e.g. Depths, Rubicon, Far Shore, etc)."),
                new OpLabel(40f, 295f, "Show guardians"),
                MapToPreference(Data.PreferenceKeys.SHOW_INSECTS, false, new Vector2(10f, 265f), "Show bugs spawned from insect groups or room effects in rooms."),
                new OpLabel(40f, 265f, "Show insects"),
                MapToPreference(Data.PreferenceKeys.SHOW_ORACLES, true, new Vector2(10f, 235f), "Show iterators where they spawn."),
                new OpLabel(40f, 235f, "Show iterators"),

                // Code things: re-screenshotted rooms, skip existing tiles (generator), less resource intensive (generator), auto-fill slugcats styles
                // Data things: reset screenshots, reset generator output (I should really come up with a better name for that tab lol), reset everything
                (deleteScreenshots = new OpHoldButton(new Vector2(), new Vector2(), "Screenshots", 120)),
                (deleteOutput = new OpHoldButton(new Vector2(), new Vector2(), "Output", 120)),
                (deleteAll = new OpHoldButton(new Vector2(), new Vector2(), "Everything", 200))
            );

            deleteScreenshots.OnPressDone += DeleteScreenshots_OnPressDone;
            deleteOutput.OnPressDone += DeleteOutput_OnPressDone;
            deleteAll.OnPressDone += DeleteAll_OnPressDone;

            // todo: get rid of this code, we only need checkboxes
            static OpCheckBox MapToPreference(string preference, bool defaultValue, Vector2 pos, string description = null)
            {
                if (!string.IsNullOrEmpty(preference))
                {
                    // Create the OpCheckBox
                    if (!Data.Preferences.TryGetValue(preference, out bool val))
                    {
                        val = defaultValue;
                        Data.Preferences.Add(preference, defaultValue);
                    }

                    var checkbox = new OpCheckBox(OIUtil.CosmeticBind(val), pos)
                    {
                        description = ((description ?? "") + " (default: " + defaultValue + ")").TrimStart(' '),
                        colorEdge = val ? MenuColorEffect.rgbWhite : MenuColorEffect.rgbMediumGrey
                    };

                    // Change when element changes
                    checkbox.OnValueChanged += (_, v, ov) =>
                    {
                        if (v != ov)
                        {
                            bool b = ValueConverter.ConvertToValue<bool>(v);
                            Data.Preferences[preference] = b;
                            Data.SaveData();
                            checkbox.colorEdge = b ? MenuColorEffect.rgbWhite : MenuColorEffect.rgbMediumGrey;
                        }
                    };

                    return checkbox;
                }
                else
                {
                    throw new ArgumentException("Cannot use empty or null preference name!");
                }
            }
        }

        private void DeleteScreenshots_OnPressDone(UIfocusable trigger)
        {
            throw new NotImplementedException();
        }

        private void DeleteOutput_OnPressDone(UIfocusable trigger)
        {
            throw new NotImplementedException();
        }

        private void DeleteAll_OnPressDone(UIfocusable trigger)
        {
            throw new NotImplementedException();
        }

        public override void Update() { }
    }
}
