using System;
using System.Collections.Generic;
using System.Linq;
using Menu;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class ScreenshotTab : BaseTab
    {
        private readonly struct QueueData(string name, HashSet<SlugcatStats.Name> scugs) : IEquatable<QueueData>, IEquatable<string>
        {
            public readonly string name = name;
            public readonly HashSet<SlugcatStats.Name> scugs = scugs;

            public bool Equals(QueueData other)
            {
                return name.Equals(other.name);
            }

            public bool Equals(string other)
            {
                return name == other;
            }
        }
        private const float SCROLLBAR_WIDTH = 20f;
        private readonly Dictionary<string, HashSet<SlugcatStats.Name>> WaitingRegions = [];
        private readonly Queue<QueueData> QueuedRegions = [];
        private readonly Dictionary<string, string> RegionNames = [];
        private readonly List<SlugcatStats.Name> Slugcats;

        private static readonly Color BlueColor = new(0.5f, 0.65f, 0.95f);
        private static readonly Color RedColor = new(0.85f, 0.5f, 0.55f);
        private static readonly Color YellowColor = new(0.95f, 0.9f, 0.65f);

        private OpComboBox comboRegions;
        private OpScrollBox waitBox;
        private OpScrollBox queueBox;

        private bool WaitDirty = false;
        private bool QueueDirty = false;

        public ScreenshotTab(OptionInterface owner) : base(owner, "Screenshotter")
        {
            List<SlugcatStats.Name> list = [];
            for (int i = 0; i < SlugcatStats.Name.values.Count; i++)
            {
                var scug = new SlugcatStats.Name(SlugcatStats.Name.values.GetEntry(i), false);
                if (!SlugcatStats.HiddenOrUnplayableSlugcat(scug) || (ModManager.MSC && scug == MoreSlugcatsEnums.SlugcatStatsName.Sofanthiel))
                {
                    list.Add(scug);
                }
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
            comboRegions = new OpComboBox(OpUtil.CosmeticBind(""), new Vector2(10f, 530f), 175f, RegionNames.Keys.ToArray()) { listHeight = (ushort)Math.Min(20, RegionNames.Count) };
            var addButton = new OpSimpleButton(new Vector2(195f, 530f), new Vector2(40f, 24f), "ADD");
            var addAllButton = new OpSimpleButton(new Vector2(245f, 530f), new Vector2(40f, 24f), "ALL");
            var startButton = new OpHoldButton(new Vector2(10f, 10f), new Vector2(80f, 24f), "START", 40f) { colorEdge = BlueColor };
            var clearButton = new OpHoldButton(new Vector2(100f, 10f), new Vector2(80f, 24f), "CLEAR", 40f);

            waitBox = new OpScrollBox(new Vector2(10f, 50f), new Vector2(275f, 470f), 0f, false, true, true);
            queueBox = new OpScrollBox(new Vector2(315f, 10f), new Vector2(275f, 510f), 0f, false, true, true);

            var abortButton = new OpHoldButton(new Vector2(315f, 530f), new Vector2(80f, 24f), "ABORT", 80f) { colorEdge = RedColor };
            var skipButton = new OpSimpleButton(new Vector2(405f, 530f), new Vector2(80f, 24f), "SKIP");

            // Event listeners
            addButton.OnClick += AddButton_OnClick;
            addAllButton.OnClick += AddAllButton_OnClick;
            startButton.OnPressDone += StartButton_OnPressDone;
            clearButton.OnPressDone += ClearButton_OnPressDone;

            // Add UI to UI
            AddItems([
                // Left side
                new OpLabel(10f, 560f, "PREPARE", true),
                addButton,
                addAllButton,
                startButton,
                clearButton,
                waitBox,

                // Middle line
                new OpImage(new(299f, 10f), "pixel") { scale = new Vector2(2f, 580f), color = MenuColorEffect.rgbMediumGrey },

                // Right side
                new OpLabel(315f, 560f, "QUEUE", true),
                abortButton,
                skipButton,
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
                    var y = waitBox.size.y + 4f; // 4f padding taken away from first element to make it 12f

                    foreach (var item in WaitingRegions)
                    {
                        // Delete button and text
                        y -= 36f; // 20f line height, 16f top padding
                        var delButton = new OpSimpleButton(new(6f, y), new(20f, 20f), "\xd7") { colorEdge = RedColor };
                        delButton.OnClick += _ =>
                        {
                            WaitingRegions.Remove(item.Key);
                            WaitDirty = true;
                        };
                        var regionLabel = new OpLabel(32f, y, item.Key);

                        waitBox.AddItems(delButton, regionLabel);

                        // Buttons
                        y -= 30f; // 24f button size, 6f top padding
                        var x = 0f;
                        int lines = 1;
                        for (int i = 0; i < Slugcats.Count; i++)
                        {
                            // Don't go out of the area
                            var textWidth = LabelTest.GetWidth(Slugcats[i].value);
                            if (x + 36f > waitBox.size.x - SCROLLBAR_WIDTH - 12f)
                            {
                                x = 0f;
                                y -= 30f;
                                lines++;
                            }

                            // Create checkbox
                            var j = i; // needed for variable reference purposese
                            var has = item.Value.Contains(Slugcats[i]);
                            var checkbox = new OpCheckBox(OpUtil.CosmeticBind(has), new(6f + x, y))
                            {
                                colorEdge = ScugDisplayColor(PlayerGraphics.DefaultSlugcatColor(Slugcats[i]), has),
                                description = Slugcats[i].value
                            };
                            checkbox.OnValueUpdate += (_, _, _) =>
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

                            // Create label
                            x += 36f;
                            var scugLabel = new OpLabel(x, y, Slugcats[i].value, false) { color = checkbox.colorEdge };

                            // Add and adjust for next
                            waitBox.AddItems(checkbox, scugLabel);
                            x += textWidth + 12f; // extra right padding
                        }

                        height += 36f + 30f * lines;
                    }

                    waitBox.SetContentSize(height);
                }
            }

            // Right scrollbox
            if (QueueDirty)
            {
                QueueDirty = false;

                // Remove previous items
                foreach (var item in queueBox.items)
                {
                    item.Deactivate();
                }
                queueBox.items.Clear();
                queueBox.SetContentSize(0f);

                // Calculate new size
                if (QueuedRegions.Count > 0)
                {
                    var height = 24f;
                    var y = queueBox.size.y;

                    foreach (var item in QueuedRegions)
                    {
                        // Delete button and text
                        y -= 32f; // 20f line height, 12f top padding

                        // todo: the rest and add height
                    }

                    queueBox.SetContentSize(height);
                }
            }
        }

        private static Color ScugEnabledColor(Color orig)
        {
            var hsl = Custom.RGB2HSL(orig);
            return Custom.HSL2RGB(hsl.x, hsl.y, Mathf.Lerp(0.4f, 1f, hsl.z));
        }

        private static Color ScugDisabledColor(Color orig) => Color.Lerp(orig, MenuColorEffect.rgbMediumGrey, 0.65f);

        private static Color ScugDisplayColor(Color orig, bool enabled) => enabled ? ScugEnabledColor(orig) : ScugDisabledColor(orig);

        private void AddButton_OnClick(UIfocusable trigger)
        {
            // If option is invalid, don't do anything
            if (comboRegions.value == null || !RegionNames.ContainsKey(comboRegions.value))
            {
                return;
            }

            // Add values
            var region = comboRegions.value;
            WaitingRegions[region] = [];
            foreach (var scug in Slugcats)
            {
                if (SlugcatStats.getSlugcatStoryRegions(scug).Contains(RegionNames[region]) || SlugcatStats.getSlugcatOptionalRegions(scug).Contains(RegionNames[region]))
                {
                    WaitingRegions[region].Add(scug);
                }
            }
            comboRegions.value = null;
            WaitDirty = true;
        }

        private void AddAllButton_OnClick(UIfocusable trigger)
        {
            foreach (var region in RegionNames)
            {
                if (!WaitingRegions.ContainsKey(region.Key))
                {
                    HashSet<SlugcatStats.Name> scugs = [];
                    WaitingRegions.Add(region.Key, scugs);
                    foreach (var scug in Slugcats)
                    {
                        scugs.Add(scug);
                    }
                }
            }
            comboRegions.value = null;
            WaitDirty = true;
        }

        private void StartButton_OnPressDone(UIfocusable trigger)
        {
            foreach (var region in WaitingRegions)
            {
                if (!QueuedRegions.Any(x => x.Equals(region.Key)))
                {
                    QueuedRegions.Enqueue(new QueueData(region.Key, region.Value));
                }
            }
            WaitingRegions.Clear();

            WaitDirty = true;
            QueueDirty = true;
        }

        private void ClearButton_OnPressDone(UIfocusable trigger)
        {
            WaitingRegions.Clear();
            WaitDirty = true;
        }
    }
}
