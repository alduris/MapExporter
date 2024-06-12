using System;
using System.Collections.Generic;
using System.Linq;
using MapExporter.Generation;
using MapExporter.Tabs.UI;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class GenerateTab(OptionInterface owner) : BaseTab(owner, "Generator")
    {
        private OpComboBox regionSelector;
        private OpSimpleButton regionAdd;
        private OpScrollBox queueBox;
        private OpScrollBox currentBox;
        private OpLabel progressLabel;
        private OpProgressBar progressBar;

        private bool queueDirty = false;
        private bool currentDirty = false;

        private Generator generator;
        private int dataVersion = 0;

        private readonly Queue<string> queue = [];
        private string current;
        private readonly Queue<SlugcatStats.Name> slugQueue = [];

        public override void Initialize()
        {
            const float PADDING = 10f;
            const float MARGIN = 6f;
            const float BOX_HEIGHT = (MENU_SIZE - 4 * PADDING - 3 * BIG_LINE_HEIGHT - 3 * MARGIN) / 2;

            var allRegions = new HashSet<string>(Data.RenderedRegions.Values.SelectMany(x => x))
                .Select((x, i) => new ListItem(x, $"({x}) {Region.GetRegionFullName(x, null)}", i))
                .ToList();

            regionSelector = new OpComboBox(OIUtil.CosmeticBind(""), new Vector2(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT - MARGIN - 24f), 200f, allRegions);
            regionAdd = new OpSimpleButton(new Vector2(regionSelector.pos.x + regionSelector.size.x + MARGIN, regionSelector.pos.y), new Vector2(60f, 24f), "ADD");
            regionAdd.OnClick += RegionAdd_OnClick;

            queueBox = new OpScrollBox(
                new Vector2(PADDING, regionSelector.pos.y - PADDING - BIG_LINE_HEIGHT - MARGIN - BOX_HEIGHT),
                new Vector2(MENU_SIZE - 2 * PADDING, BOX_HEIGHT),
                0f, true, true, true);
            currentBox = new OpScrollBox(new Vector2(PADDING, PADDING), new Vector2(MENU_SIZE - 2 * PADDING, BOX_HEIGHT), BOX_HEIGHT, false, true, false);

            AddItems(
                new OpLabel(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT, "GENERATE", true),
                regionSelector, regionAdd,
                new OpLabel(PADDING, regionSelector.pos.y - PADDING - BIG_LINE_HEIGHT, "QUEUE", true),
                queueBox,
                new OpLabel(PADDING, queueBox.pos.y - PADDING - BIG_LINE_HEIGHT, "CURRENT", true),
                currentBox
            );
        }

        public override void Update()
        {
            // Update queue
            if (current == null && generator == null && queue.Count > 0)
            {
                current = queue.Dequeue();

                var scugs = Data.RenderedRegions.Select(x => x.Value.Any(x => x == current) ? x.Key : null).Where(x => x != null);
                slugQueue.Clear(); // there should be nothing in it but just as a safety
                foreach (var scug in scugs) slugQueue.Enqueue(scug);
                generator = new Generator(slugQueue.Peek(), current);

                queueDirty = true;
                currentDirty = true;
            }

            // Update queuebox
            if (queueDirty)
            {
                queueDirty = false;

                foreach (var item in queueBox.items)
                {
                    _RemoveItem(item);
                }
                queueBox.items.Clear();

                // Place the stuff
                const float Q_SPACING = 6f;
                const float ITEM_PAD = 4f;
                float x = Q_SPACING;
                foreach (var item in queue)
                {
                    string name = Region.GetRegionFullName(item, null);
                    float namewidth = LabelTest.GetWidth(name);
                    float boxwidth = namewidth + 24f + ITEM_PAD * 3;
                    float boxtop = queueBox.size.y - Q_SPACING - ITEM_PAD;

                    var delButton = new OpSimpleButton(
                        new Vector2(x + ITEM_PAD + namewidth + ITEM_PAD, boxtop - 24f),
                        new Vector2(24f, 24f), "\xd7")
                    {
                        colorEdge = RedColor, colorFill = RedColor
                    };

                    queueBox.AddItems(
                        new OpRect(new Vector2(x, SCROLLBAR_WIDTH + Q_SPACING), new Vector2(boxwidth, queueBox.size.y - SCROLLBAR_WIDTH - Q_SPACING * 2)),
                        new OpLabel(x, boxtop - 22f, name, false),
                        delButton
                    );

                    x += boxwidth + Q_SPACING;
                }

                queueBox.SetContentSize(x);
            }

            // Update current box
            if (currentDirty)
            {
                currentDirty = false;

                foreach (var item in currentBox.items)
                {
                    _RemoveItem(item);
                }
                currentBox.items.Clear();

                // Place the stuff
                const float C_SPACING = 6f;
                if (current != null)
                {
                    progressBar = new OpProgressBar(new Vector2(C_SPACING, C_SPACING), currentBox.size.x - C_SPACING * 2);
                    progressLabel = new OpLabel(C_SPACING, progressBar.pos.y + progressBar.size.y + C_SPACING, "Processing...", false);
                    var displayText = slugQueue.Peek().value + " - " + Region.GetRegionFullName(current, slugQueue.Peek());

                    currentBox.AddItems(
                        new OpLabel(C_SPACING, currentBox.size.y - C_SPACING - BIG_LINE_HEIGHT, displayText, true),
                        progressLabel,
                        progressBar
                    );
                    progressBar.Initialize();
                }
            }

            // Update generator
            if (generator != null)
            {
                if (generator.Failed)
                {
                    currentBox.colorEdge = RedColor;
                    currentBox.colorFill = RedColor;
                }
                else
                {
                    generator.Update();
                    progressLabel.text = $"Progress: {generator.Progress.ToString("0.000%")} ({generator.CurrentTask})";
                    progressBar.Update(generator.Progress);
                    if (generator.Done)
                    {
                        currentDirty = true;
                        if (!generator.Failed)
                        {
                            slugQueue.Dequeue();
                            if (slugQueue.Count == 0)
                            {
                                current = null;
                            }
                            generator = null;
                        }
                    }
                }
            }

            // Update region list if necessary
            if (dataVersion != Data.Version)
            {
                dataVersion = Data.Version;

                // Update slugcat list
                var regionListList = Data.RenderedRegions.Values.ToList();
                if (regionListList.Count == 0)
                    regionListList.Add([""]); // dummy placeholder

                var regionList = new HashSet<string>();
                foreach (var list in regionListList)
                {
                    foreach (var region in list)
                    {
                        regionList.Add(region);
                    }
                }
                regionSelector._itemList = regionList.Select((x, i) => new ListItem(x, $"({x}) {Region.GetRegionFullName(x, null)}", i)).ToArray();
                regionSelector._ResetIndex();
                regionSelector.value = null;
                regionSelector.Change();
            }
        }

        private void RegionAdd_OnClick(UIfocusable trigger)
        {
            // Don't add it if not a region or if region is already enqueued or being processed
            if (regionSelector.value == null || regionSelector.value == "" || current == regionSelector.value || queue.Contains(regionSelector.value))
            {
                regionAdd.PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            queue.Enqueue(regionSelector.value);
            regionSelector.value = null;
        }
    }
}
