using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using RWCustom;
using UnityEngine;
using static MapExporter.Data;

namespace MapExporter.Tabs
{
    internal class ScreenshotTab : BaseTab
    {
        private const int MAX_RETRY_ATTEMPTS = 1; //3;

        private readonly Dictionary<string, (HashSet<SlugcatStats.Name> scugs, SSUpdateMode updateMode)> WaitingRegions = [];
        private readonly Dictionary<string, string> RegionNames = [];
        private readonly List<SlugcatStats.Name> Slugcats;
        private static readonly Regex PascalRegex = new(@"(?<=[A-Z])(?=[A-Z][a-z])|(?<=[^A-Z])(?=[A-Z])|(?<=[A-Za-z])(?=[^A-Za-z])");

        private OpComboBox comboRegions;
        private OpScrollBox waitBox;
        private OpScrollBox queueBox;

        private Process ScreenshotProcess;
        private int RetryAttempts = 0;

        private bool WaitDirty = true;
        private bool QueueDirty = true;

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
            // Get all regions with their name (not for any particular slugcat)
            var regionOrder = Region.GetFullRegionOrder();
            RegionNames.Clear();
            IEnumerable<(string acronym, string name)> nameValuePairs = regionOrder.Select(x => (x, Region.GetRegionFullName(x, null)));
            foreach (var pair in nameValuePairs) {
                RegionNames.Add(pair.acronym, pair.name);
            }

            // Set up UI
            var regionList = nameValuePairs
                .Select(x => new ListItem(x.acronym, $"({x.acronym}) {x.name}")) // display as "(acronym) full name" but keep the acronym as the value
                .ToList(); // OpComboBox wants it as a list
            comboRegions = new OpComboBox(OIUtil.CosmeticBind(""), new Vector2(10f, 530f), 175f, regionList)
            {
                listHeight = 20
            };
            var addButton = new OpSimpleButton(new Vector2(195f, 530f), new Vector2(40f, 24f), "ADD");
            var addAllButton = new OpSimpleButton(new Vector2(245f, 530f), new Vector2(40f, 24f), "ALL");
            var startButton = new OpSimpleButton(new Vector2(10f, 10f), new Vector2(80f, 24f), "START") { colorEdge = BlueColor };
            var clearButton = new OpSimpleButton(new Vector2(100f, 10f), new Vector2(80f, 24f), "CLEAR");

            waitBox = new OpScrollBox(new Vector2(10f, 50f), new Vector2(275f, 470f), 0f, false, true, true);
            queueBox = new OpScrollBox(new Vector2(315f, 10f), new Vector2(275f, 510f), 0f, false, true, true);

            var abortButton = new OpSimpleButton(new Vector2(315f, 530f), new Vector2(80f, 24f), "ABORT") { colorEdge = RedColor };
            var skipButton = new OpSimpleButton(new Vector2(405f, 530f), new Vector2(80f, 24f), "SKIP");
            var retryButton = new OpSimpleButton(new Vector2(495f, 530f), new Vector2(80f, 24f), "RETRY");

            // Event listeners
            addButton.OnClick += AddButton_OnClick;
            addAllButton.OnClick += AddAllButton_OnClick;
            startButton.OnClick += StartButton_OnClick;
            clearButton.OnClick += ClearButton_OnClick;

            abortButton.OnClick += AbortButton_OnClick;
            skipButton.OnClick += SkipButton_OnClick;
            retryButton.OnClick += RetryButton_OnClick;

            // Add UI to UI
            AddItems([
                // Left side
                new OpShinyLabel(10f, 560f, "PREPARE", true),
                addButton,
                addAllButton,
                startButton,
                clearButton,
                waitBox,

                // Middle line
                new OpImage(new(299f, 10f), "pixel") { scale = new Vector2(2f, 580f), color = MenuColorEffect.rgbMediumGrey },

                // Right side
                new OpShinyLabel(315f, 560f, "SCREENSHOTTING", true),
                abortButton,
                skipButton,
                retryButton,
                queueBox,

                // For z-index ordering reasons
                comboRegions
            ]);
        }

        public override void Update()
        {
            const float BIG_PAD = 12f; // big pad
            const float SM_PAD = 6f;   // small pad
            const float SEP_PAD = 18f; // separation pad
            const float CHECKBOX_LH = CHECKBOX_SIZE + SM_PAD;
            const float EDGE_PAD = SM_PAD;
            const float QUEUE_SLUG_PAD = EDGE_PAD + SEP_PAD;

            if (Data.QueuedRegions.Count > 0)
            {
                if (RetryAttempts < MAX_RETRY_ATTEMPTS)
                {
                    DoThings();
                }
                else if (Data.ScreenshotterStatus != SSStatus.Errored)
                {
                    // Data.QueuedRegions.Dequeue();
                    // RetryAttempts = 0;
                    Data.ScreenshotterStatus = SSStatus.Errored;
                    QueueDirty = true;
                }
            }

            // Left scrollbox update
            if (WaitDirty)
            {
                WaitDirty = false;

                // Remove previous items
                foreach (var item in waitBox.items)
                {
                    _RemoveItem(item);
                }
                waitBox.items.Clear();
                waitBox.SetContentSize(0f);

                // Calculate new size
                if (WaitingRegions.Count > 0)
                {
                    var y = waitBox.size.y + (SEP_PAD - BIG_PAD);

                    foreach (var item in WaitingRegions)
                    {
                        // Delete button and text
                        y -= LINE_HEIGHT + SEP_PAD;
                        var delButton = new OpSimpleButton(new(EDGE_PAD, y), new(LINE_HEIGHT, LINE_HEIGHT), "\xd7") { colorEdge = RedColor };
                        delButton.OnClick += _ =>
                        {
                            WaitingRegions.Remove(item.Key);
                            WaitDirty = true;
                        };
                        var regionLabel = new OpLabel(EDGE_PAD + LINE_HEIGHT + SM_PAD, y, $"({item.Key}) {RegionNames[item.Key]}");

                        waitBox.AddItems(delButton, regionLabel);

                        // Dropdown for selection
                        if (Data.RenderedRegions.ContainsKey(item.Key))
                        {
                            y -= CHECKBOX_LH;
                            var updateCombo = new OpResourceSelector(OIUtil.CosmeticBind(item.Value.updateMode), new Vector2(EDGE_PAD, y), waitBox.size.x - 2 * EDGE_PAD - SCROLLBAR_WIDTH)
                            {
                                description = "Determines how much data the screenshotter captures from the region. Note that the screenshotter will still screenshot rooms for which the files are missing."
                            };
                            updateCombo._itemList = updateCombo._itemList.Select(x => new ListItem(x.name, PascalRegex.Replace(x.name, " "), x.value)).ToArray();
                            updateCombo.OnValueChanged += (_, val, old) =>
                            {
                                if (old == val) return;
                                if (val == null || !Enum.TryParse<SSUpdateMode>(val, out var value))
                                {
                                    updateCombo.value = item.Value.updateMode.ToString();
                                    updateCombo.PlaySound(SoundID.MENU_Error_Ping);
                                    return;
                                }

                                WaitingRegions[item.Key] = (item.Value.scugs, value);
                            };
                            updateCombo.OnListOpen += (_) => { updateCombo.MoveToFront(); };
                            waitBox.AddItems(updateCombo);
                        }

                        // Buttons
                        y -= CHECKBOX_LH;
                        var x = 0f;
                        for (int i = 0; i < Slugcats.Count; i++)
                        {
                            // Don't go out of the area
                            var textWidth = LabelTest.GetWidth(Slugcats[i].value);
                            if (x + CHECKBOX_SIZE + BIG_PAD > waitBox.size.x - SCROLLBAR_WIDTH - 2 * EDGE_PAD)
                            {
                                x = 0f;
                                y -= CHECKBOX_LH;
                            }

                            // Create checkbox
                            var j = i; // needed for variable reference purposese
                            var has = item.Value.scugs.Contains(Slugcats[i]);
                            var checkbox = new OpCheckBox(OIUtil.CosmeticBind(has), new(EDGE_PAD + x, y))
                            {
                                colorEdge = ScugDisplayColor(PlayerGraphics.DefaultSlugcatColor(Slugcats[i]), has),
                                description = Slugcats[i].value
                            };

                            // Create label
                            x += CHECKBOX_SIZE + BIG_PAD;
                            var scugLabel = new OpLabel(x, y, Slugcats[i].value, false) { color = checkbox.colorEdge };

                            // Event listeners
                            checkbox.OnValueUpdate += (_, _, _) =>
                            {
                                var scug = Slugcats[j];
                                var c = item.Value.scugs.Contains(scug);
                                if (c)
                                {
                                    item.Value.scugs.Remove(scug);
                                }
                                else
                                {
                                    item.Value.scugs.Add(scug);
                                }
                                checkbox.colorEdge = ScugDisplayColor(PlayerGraphics.DefaultSlugcatColor(Slugcats[j]), !c);
                                scugLabel.color = checkbox.colorEdge;
                                // WaitDirty = true;
                            };

                            // Add and adjust for next
                            waitBox.AddItems(checkbox, scugLabel);
                            x += textWidth + BIG_PAD; // extra right padding
                        }
                        y -= CHECKBOX_LH;
                    }

                    waitBox.SetContentSize(waitBox.size.y - EDGE_PAD - y);
                }
            }

            // Right scrollbox
            if (QueueDirty)
            {
                QueueDirty = false;

                // Remove previous items
                foreach (var item in queueBox.items)
                {
                    _RemoveItem(item);
                }
                queueBox.items.Clear();
                queueBox.SetContentSize(0f);

                // Calculate new size
                if (Data.QueuedRegions.Count > 0)
                {
                    var height = 24f;
                    var y = queueBox.size.y + (BIG_PAD - EDGE_PAD);
                    int i = 1;

                    foreach (var item in Data.QueuedRegions)
                    {
                        y -= LINE_HEIGHT + BIG_PAD;

                        // Current header
                        if (i == 1)
                        {
                            y -= BIG_LINE_HEIGHT - LINE_HEIGHT;
                            queueBox.AddItems(new OpLabel(EDGE_PAD, y, "Current:", true));
                            y -= LINE_HEIGHT;
                            height += BIG_LINE_HEIGHT;
                        }
                        else
                        {
                            height += BIG_PAD;
                        }

                        // Region name and scuglats
                        var titleLabel = new OpLabel(EDGE_PAD + SM_PAD, y, $"{i}. ({item.acronym}) {RegionNames[item.acronym]}", false);
                        if (Data.ScreenshotterStatus == SSStatus.Errored)
                        {
                            titleLabel.color = RedColor;
                        }
                        queueBox.AddItems(titleLabel);
                        y -= LINE_HEIGHT;

                        int lines = 2;
                        float x = 0f;
                        foreach (var scug in item.scugs)
                        {
                            var width = LabelTest.GetWidth(scug.value);
                            if (x + width > queueBox.size.x - SCROLLBAR_WIDTH - QUEUE_SLUG_PAD - EDGE_PAD * 2)
                            {
                                x = 0f;
                                y -= LINE_HEIGHT;
                                lines++;
                            }
                            queueBox.AddItems(new OpLabel(x + QUEUE_SLUG_PAD, y, scug.value, false) { color = ScugEnabledColor(PlayerGraphics.DefaultSlugcatColor(scug)) });
                            x += width + BIG_PAD;
                        }
                        height += LINE_HEIGHT * lines;

                        // Horizontal line and queued header
                        if (i == 1 && Data.QueuedRegions.Count > 1)
                        {
                            y -= SM_PAD;
                            queueBox.AddItems(new OpImage(new Vector2(EDGE_PAD, y), "pixel") { scale = new Vector2(queueBox.size.x - SCROLLBAR_WIDTH - EDGE_PAD * 2, 2f), color = MenuColorEffect.rgbMediumGrey });
                            y -= 2f + SM_PAD + BIG_LINE_HEIGHT;
                            height += 2 * SM_PAD + BIG_LINE_HEIGHT + 2f;
                            queueBox.AddItems(new OpLabel(EDGE_PAD, y, "Queued:", true));
                        }

                        i++;
                    }

                    queueBox.SetContentSize(height);
                }
            }
        }

        public void DoThings()
        {
            if (ScreenshotProcess == null)
            {
                // Try to create a new process

                // Set up data so we know if it finishes or crashes
                Data.ScreenshotterStatus = SSStatus.Unfinished;
                Data.SaveData();

                // Get the next item to render
                string next = Data.QueuedRegions.Peek().ToString();
                
                // Create the process (thanks Vigaro and Bensone)
                var currProcess = Process.GetCurrentProcess();
                var envVars = Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
                var doorstopKeys = new List<string>();
                foreach (DictionaryEntry entry in envVars)
                {
                    if (entry.Key.ToString().StartsWith("DOORSTOP"))
                    {
                        doorstopKeys.Add(entry.Key.ToString());
                    }
                }

                var args = Environment.GetCommandLineArgs();
                var processInfo = new ProcessStartInfo(
                    currProcess.MainModule.FileName,
                    $"{string.Join("", args.Skip(1).Select(x => (x.Contains(" ") && !x.StartsWith("\"") ? $"\"{x}\"" : x) + " "))}{Plugin.FLAG_TRIGGER} \"{next}\"")
                {
                    WorkingDirectory = Custom.LegacyRootFolderDirectory(),
                    UseShellExecute = false,
                };

                // Remove doorstop things
                processInfo.EnvironmentVariables.Clear();
                foreach (DictionaryEntry item in envVars)
                {
                    if (!doorstopKeys.Contains(item.Key.ToString()))
                    {
                        processInfo.EnvironmentVariables.Add((string)item.Key, (string)item.Value);
                    }
                }

                ScreenshotProcess = new Process
                {
                    StartInfo = processInfo
                };
                ScreenshotProcess.Start();
            }
            else if (ScreenshotProcess.HasExited)
            {
                // Old process closed, presume finish or crashed. Time to figure out which!
                Data.UpdateSSStatus();

                if (Data.ScreenshotterStatus == SSStatus.Finished)
                {
                    // Finished without crashing, yay
                    var data = Data.QueuedRegions.Dequeue();
                    QueueDirty = true;
                    Data.ScreenshotterStatus = SSStatus.Inactive;
                    if (!Data.RenderedRegions.TryGetValue(data.acronym, out var scugs))
                    {
                        Data.RenderedRegions.Add(data.acronym, data.scugs);
                    }
                    else
                    {
                        foreach (var scug in data.scugs)
                        {
                            scugs.Add(scug);
                        }
                    }
                    Data.SaveData();
                    RetryAttempts = 0;
                }
                else if (Data.ScreenshotterStatus == SSStatus.Relaunch)
                {
                    Data.GetData();
                    QueueDirty = true;
                    Data.ScreenshotterStatus = SSStatus.Inactive;
                    var data = Data.QueuedRegions.Peek();
                    Data.SaveData();
                    RetryAttempts = 0;
                }
                else
                {
                    // Uh-oh spaghetti-o's! Retry just in case user accidentally closed it or the problem was fixed idk
                    RetryAttempts++;
                }
                ScreenshotProcess.Dispose();
                ScreenshotProcess = null;
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
            if (comboRegions.value == null || !RegionNames.ContainsKey(comboRegions.value) || WaitingRegions.ContainsKey(comboRegions.value))
            {
                trigger.PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            // Add values
            var acronym = comboRegions.value;
            WaitingRegions[acronym] = ([], SSUpdateMode.Everything);
            if (Preferences.ScreenshotterAutoFill.GetValue())
            {
                foreach (var scug in Slugcats)
                {
                    if (SlugcatStats.SlugcatStoryRegions(scug).Contains(acronym) || SlugcatStats.SlugcatOptionalRegions(scug).Contains(acronym))
                    {
                        WaitingRegions[acronym].scugs.Add(scug);
                    }
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
                    WaitingRegions.Add(region.Key, (scugs, SSUpdateMode.Everything));
                    if (Preferences.ScreenshotterAutoFill.GetValue())
                    {
                        foreach (var scug in Slugcats)
                        {
                            if (SlugcatStats.SlugcatStoryRegions(scug).Contains(region.Value) || SlugcatStats.SlugcatOptionalRegions(scug).Contains(region.Value))
                            {
                                scugs.Add(scug);
                            }
                        }
                    }
                }
            }
            comboRegions.value = null;
            WaitDirty = true;
        }

        private void StartButton_OnClick(UIfocusable trigger)
        {
            foreach (var region in WaitingRegions)
            {
                if (!Data.QueuedRegions.Any(x => x.Equals(region.Key)) && region.Value.scugs.Count > 0)
                {
                    Data.QueuedRegions.Enqueue(new QueueData(region.Key, region.Value.scugs, region.Value.updateMode));
                }
            }
            WaitingRegions.Clear();
            Data.SaveData();

            WaitDirty = true;
            QueueDirty = true;
        }

        private void ClearButton_OnClick(UIfocusable trigger)
        {
            WaitingRegions.Clear();
            WaitDirty = true;
        }

        private void AbortButton_OnClick(UIfocusable trigger)
        {
            if (Data.QueuedRegions.Count == 0)
            {
                trigger.PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            if (ScreenshotProcess != null)
            {
                ScreenshotProcess.Kill();
                ScreenshotProcess.Close();
                ScreenshotProcess.Dispose();
                ScreenshotProcess = null;
            }
            Data.QueuedRegions.Clear();
            QueueDirty = true;
            Data.ScreenshotterStatus = SSStatus.Inactive;
            Data.SaveData();
        }

        private void SkipButton_OnClick(UIfocusable trigger)
        {
            if (Data.QueuedRegions.Count == 0)
            {
                trigger.PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            if (ScreenshotProcess != null)
            {
                ScreenshotProcess.Kill();
                ScreenshotProcess.Close();
                ScreenshotProcess.Dispose();
                ScreenshotProcess = null;
            }
            RetryAttempts = 0;
            Data.QueuedRegions.Dequeue();
            QueueDirty = true;
            Data.ScreenshotterStatus = SSStatus.Inactive;
            Data.SaveData();
        }

        private void RetryButton_OnClick(UIfocusable trigger)
        {
            if (Data.QueuedRegions.Count == 0)
            {
                trigger.PlaySound(SoundID.MENU_Error_Ping);
                return;
            }

            if (ScreenshotProcess != null)
            {
                ScreenshotProcess.Kill();
                ScreenshotProcess.Close();
                ScreenshotProcess.Dispose();
                ScreenshotProcess = null;
            }
            RetryAttempts = 0;

            QueueDirty = true;
            Data.GetData();
            Data.ScreenshotterStatus = SSStatus.Inactive;
            Data.SaveData();
        }
    }
}
