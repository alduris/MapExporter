using System;
using System.Collections.Generic;
using System.Linq;
using MapExporterNew.Generation;
using MapExporterNew.Server;
using MapExporterNew.Tabs.UI;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporterNew.Tabs
{
    internal class GenerateTab(OptionInterface owner) : BaseTab(owner, "Generator")
    {
        private OpComboBox regionSelector;
        private OpSimpleButton regionAdd;
        private OpSimpleButton regionAddAll;
        private OpScrollBox queueBox;
        private OpScrollBox currentBox;
        private OpLabel progressLabel;
        private OpProgressBar progressBar;

        private bool queueDirty = false;
        private bool currentDirty = false;

        private Generator generator;
        private int dataVersion = 0;
        private bool running = false;

        private readonly LinkedList<string> queue = [];
        private string current;
        private readonly Queue<SlugcatStats.Name> slugQueue = [];

        public override void Initialize()
        {
            const float PADDING = 10f;
            const float MARGIN = 6f;
            const float BOX_HEIGHT = (MENU_SIZE - PADDING * 4 - BIG_LINE_HEIGHT * 3 - MARGIN * 4 - 24f * 2) / 2;

            var allRegions = Data.RenderedRegions.Keys
                .OrderBy(s => s, StringComparer.InvariantCultureIgnoreCase)
                .Select((x, i) => new ListItem(x, $"({x}) {Data.RegionNameFor(x, null)}", i))
                .ToList();
            if (allRegions.Count == 0)
            {
                allRegions.Add(new ListItem("", ""));
            }

            regionSelector = new OpComboBox(OIUtil.CosmeticBind(""), new Vector2(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT - MARGIN - 24f), 200f, allRegions)
            {
                listHeight = 20
            };
            regionAdd = new OpSimpleButton(new Vector2(regionSelector.pos.x + regionSelector.size.x + MARGIN, regionSelector.pos.y), new Vector2(60f, 24f), Translate("ADD"));
            regionAdd.OnClick += RegionAdd_OnClick;
            regionAddAll = new OpSimpleButton(new Vector2(regionSelector.pos.x + regionSelector.size.x + regionAdd.size.x + MARGIN * 2, regionSelector.pos.y), new Vector2(60f, 24f), Translate("ADD ALL"));
            regionAddAll.OnClick += RegionAddAll_OnClick;

            queueBox = new OpScrollBox(
                new Vector2(PADDING, regionSelector.pos.y - PADDING - BIG_LINE_HEIGHT - MARGIN - BOX_HEIGHT),
                new Vector2(MENU_SIZE - 2 * PADDING, BOX_HEIGHT),
                0f, true, true, true);
            currentBox = new OpScrollBox(new Vector2(PADDING, PADDING), new Vector2(MENU_SIZE - 2 * PADDING, BOX_HEIGHT), BOX_HEIGHT, false, true, false);

            var startButton = new OpSimpleButton(new Vector2(PADDING, currentBox.pos.y + BOX_HEIGHT + MARGIN), new Vector2(80f, 24f), Translate("START")) { colorEdge = BlueColor };
            startButton.OnClick += StartButton_OnClick;
            var retryButton = new OpSimpleButton(new Vector2(MARGIN + startButton.pos.x + startButton.size.x, currentBox.pos.y + BOX_HEIGHT + MARGIN), new Vector2(80f, 24f), Translate("RETRY"));
            retryButton.OnClick += RetryButton_OnClick;

            AddItems(
                new OpShinyLabel(PADDING, MENU_SIZE - PADDING - BIG_LINE_HEIGHT, Translate("GENERATE"), true),
                new OpLabel(PADDING, regionSelector.pos.y - PADDING - BIG_LINE_HEIGHT, Translate("QUEUE"), true),
                queueBox,
                new OpLabel(PADDING, queueBox.pos.y - PADDING - BIG_LINE_HEIGHT, Translate("CURRENT"), true),
                currentBox,
                startButton,
                retryButton,

                // for z-index ordering reasons
                regionSelector, regionAdd, regionAddAll
            );
        }

        public override void Update()
        {
            // Update queue
            if (generator == null && (queue.Count > 0 || current != null) && running)
            {
                if (current == null)
                {
                    current = queue.First();
                    queue.RemoveFirst();

                    var scugs = Data.RenderedRegions[current].OrderBy(s => s.Index);
                    slugQueue.Clear(); // there should be nothing in it but just as a safety
                    foreach (var scug in scugs) slugQueue.Enqueue(scug);
                }
                generator = new Generator(slugQueue.Peek(), current);

                queueDirty = true;
                currentDirty = true;
            }
            else if (queue.Count == 0 && current == null)
            {
                running = false;
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
                    string name = Data.RegionNameFor(item, null);
                    float namewidth = LabelTest.GetWidth(name);
                    float boxwidth = namewidth + 30f + ITEM_PAD * 3;
                    float boxtop = queueBox.size.y - Q_SPACING - ITEM_PAD;

                    var delButton = new OpSimpleButton(
                        new Vector2(x + ITEM_PAD + namewidth + ITEM_PAD, boxtop - 24f),
                        new Vector2(24f, 24f), "\xd7")
                    {
                        colorEdge = RedColor, colorFill = RedColor
                    };
                    delButton.OnClick += (_) =>
                    {
                        queue.Remove(item);
                        queueDirty = true;
                    };

                    queueBox.AddItems(
                        new OpRect(new Vector2(x, SCROLLBAR_WIDTH + Q_SPACING), new Vector2(boxwidth, queueBox.size.y - SCROLLBAR_WIDTH - Q_SPACING * 2)),
                        new OpLabel(x + ITEM_PAD, boxtop - 22f, name, false),
                        delButton
                    );

                    x += boxwidth + Q_SPACING;
                }

                queueBox.SetContentSize(x, false);
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
                    string text = generator != null ? "Processing..." : "Generator missing!";
                    if (generator?.Failed ?? false) text = "Error detected!";
                    progressLabel = new OpLabel(C_SPACING, progressBar.pos.y + progressBar.size.y + C_SPACING, Translate(text), false);
                    var displayText = slugQueue.Peek().value + " - " + Translate(Data.RegionNameFor(current, slugQueue.Peek()));

                    currentBox.AddItems(
                        new OpLabel(C_SPACING, currentBox.size.y - C_SPACING - BIG_LINE_HEIGHT, displayText, true),
                        progressLabel,
                        progressBar
                    );
                    progressBar.Initialize();
                }
                else
                {
                    progressBar = null;
                    progressLabel = null;
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
                    progressLabel.text = $"{Translate("Progress")}: {generator.Progress:0.000%} ({generator.CurrentTask})";
                    progressBar.Progress(generator.Progress);
                    if (generator.Done)
                    {
                        currentDirty = true;
                        if (!generator.Failed)
                        {
                            if (Data.FinishedRegions.TryGetValue(current, out var slugcats))
                            {
                                Data.FinishedRegions[current].Add(slugQueue.Peek());
                            }
                            else
                            {
                                Data.FinishedRegions.Add(current, [slugQueue.Peek()]);
                            }
                            Data.SaveData();
                            slugQueue.Dequeue();
                            if (slugQueue.Count == 0)
                            {
                                current = null;
                            }
                            generator = null;
                            Exporter.ResetFileCounter();
                        }
                    }
                }
            }

            // Update region list if necessary
            if (dataVersion != Data.Version)
            {
                dataVersion = Data.Version;

                var itemList = Data.RenderedRegions.Keys
                    .OrderBy(s => s, StringComparer.InvariantCultureIgnoreCase)
                    .Select((x, i) => new ListItem(x, $"({x}) {Translate(Data.RegionNameFor(x, null))}", i))
                    .ToList();
                if (itemList.Count == 0)
                {
                    itemList.Add(new ListItem("", ""));
                }

                regionSelector._itemList = [.. itemList];
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

            queue.AddLast(regionSelector.value);
            regionSelector.value = null;
            queueDirty = true;
        }

        private void RegionAddAll_OnClick(UIfocusable trigger)
        {
            var allRegions = Data.RenderedRegions.Keys.OrderBy(s => s, StringComparer.InvariantCultureIgnoreCase).ToHashSet();
            var alreadyAdded = queue.ToHashSet();
            foreach (var region in allRegions.Except(alreadyAdded))
            {
                queue.AddLast(region);
            }
            regionSelector.value = null;
            queueDirty = true;
        }

        private void StartButton_OnClick(UIfocusable trigger)
        {
            running = true;
        }

        private void RetryButton_OnClick(UIfocusable trigger)
        {
            if (current != null && generator != null && generator.Failed)
            {
                generator = null;
                running = true;
            }
        }
    }
}
