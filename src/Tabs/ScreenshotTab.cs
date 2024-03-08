using System.Collections.Generic;
using System.Linq;
using Menu;
using Menu.Remix;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class ScreenshotTab : BaseTab
    {
        private readonly Dictionary<string, HashSet<SlugcatStats.Name>> WaitingRegions = [];
        private readonly Dictionary<string, HashSet<SlugcatStats.Name>> QueuedRegions = [];
        private readonly Dictionary<string, string> RegionNames = [];
        private readonly List<SlugcatStats.Name> Slugcats;

        private static readonly Color BlueColor = new(0.5f, 0.65f, 0.95f);
        private static readonly Color RedColor = new(0.85f, 0.5f, 0.55f);

        private OpComboBox comboRegions;
        private OpScrollBox waitBox;
        private OpScrollBox queueBox;

        private bool WaitDirty = false;
        private bool QueueDirty = false;

        public ScreenshotTab(OptionInterface owner) : base(owner, "Screenshotter")
        {
            List<SlugcatStats.Name> list = [
                SlugcatStats.Name.White,
                SlugcatStats.Name.Yellow,
                SlugcatStats.Name.Red
            ];
            if (ModManager.MSC)
            {
                list.AddRange([
                    MoreSlugcatsEnums.SlugcatStatsName.Gourmand,
                    MoreSlugcatsEnums.SlugcatStatsName.Artificer,
                    MoreSlugcatsEnums.SlugcatStatsName.Rivulet,
                    MoreSlugcatsEnums.SlugcatStatsName.Spear,
                    MoreSlugcatsEnums.SlugcatStatsName.Saint,
                    MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel
                ]);
            }
            Slugcats = list;
        }

        public override void Initialize()
        {
            // Get all regions with their name as survivor
            RegionNames.Clear();
            IEnumerable<(string name, string acronym)> nameValuePairs = Region.GetFullRegionOrder().Select(x => (Region.GetRegionFullName(x, SlugcatStats.Name.White), x));
            foreach (var pair in nameValuePairs) {
                RegionNames.Add(pair.name, pair.acronym);
            }

            // Set up UI
            comboRegions = new OpComboBox(OpUtil.CosmeticBind(""), new(10f, 530f), 205f, RegionNames.Keys.ToArray());
            var addButton = new OpSimpleButton(new(225f, 530f), new(60f, 24f), "ADD");
            var startButton = new OpHoldButton(new Vector2(10f, 10f), new Vector2(60f, 24f), "START", 40f) { colorEdge = BlueColor };

            waitBox = new OpScrollBox(new Vector2(10f, 50f), new Vector2(275f, 470f), 0f, false, true, true);
            queueBox = new OpScrollBox(new Vector2(315f, 10f), new Vector2(275f, 510f), 0f, false, true, true);

            var abortButton = new OpHoldButton(new Vector2(315f, 530f), new Vector2(60f, 24f), "ABORT", 80f) { colorEdge = RedColor };

            // Event listeners
            addButton.OnClick += AddButton_OnClick;

            // Add UI to UI
            AddItems([
                // Left side
                new OpLabel(10f, 560f, "SCREENSHOTS", true),
                addButton,
                startButton,
                waitBox,

                // Middle line
                new OpImage(new(299f, 10f), "pixel") { scale = new Vector2(2f, 580f), color = MenuColorEffect.rgbMediumGrey },

                // Right side
                new OpLabel(315f, 560f, "ENQUEUED", true),
                abortButton,
                queueBox,

                // For z-index ordering reasons
                comboRegions
            ]);
        }

        public override void Update()
        {
            // Left scrollbox update
            if (WaitDirty)
            {
                WaitDirty = false;

                // Remove previous items
                foreach (var item in waitBox.items)
                {
                    item.Deactivate();
                }
                waitBox.items.Clear();
                waitBox.SetContentSize(0f);

                // Calculate new size
                if (WaitingRegions.Count > 0)
                {
                    var height = 24f;
                    var y = waitBox.size.y;

                    foreach (var item in WaitingRegions)
                    {
                        // Delete button and text
                        y -= 32f; // 20f line height, 12f top padding
                        var delButton = new OpSimpleButton(new(6f, y), new(20f, 20f), "\xd7") { colorEdge = RedColor };
                        delButton.OnClick += _ =>
                        {
                            WaitingRegions.Remove(item.Key);
                            WaitDirty = true;
                        };
                        var label = new OpLabel(32f, y, item.Key);

                        waitBox.AddItems(delButton, label);

                        // Buttons
                        y -= 30f; // 24f button size, 6f top padding
                        for (int i = 0; i < Slugcats.Count; i++)
                        {
                            var j = i; // needed for variable reference purposese
                            var has = item.Value.Contains(Slugcats[i]);
                            var button = new OpCheckBox(OpUtil.CosmeticBind(has), new(6f + 30f * i, y))
                            {
                                colorEdge = PlayerGraphics.DefaultSlugcatColor(Slugcats[i]),
                                description = Slugcats[i].value
                            };
                            if (!has)
                            {
                                button.colorEdge = Color.Lerp(button.colorEdge, new(.5f, .5f, .5f), .65f);
                            }
                            button.OnValueUpdate += (_, _, _) =>
                            {
                                var scug = Slugcats[j];
                                if (item.Value.Contains(scug))
                                {
                                    item.Value.Remove(scug);
                                }
                                else
                                {
                                    item.Value.Add(scug);
                                }
                                WaitDirty = true;
                            };
                            waitBox.AddItems(button);
                        }

                        height += 62f;
                    }

                    waitBox.SetContentSize(height);
                }
            }
        }

        private void AddButton_OnClick(UIfocusable trigger)
        {
            // If option is invalid, don't do anything
            if (comboRegions.value == null || !RegionNames.ContainsKey(comboRegions.value))
            {
                return;
            }

            // Add values
            WaitingRegions[comboRegions.value] = [];
            foreach (var scug in Slugcats)
            {
                WaitingRegions[comboRegions.value].Add(scug);
            }
            comboRegions.value = null;
            WaitDirty = true;
        }
    }
}
