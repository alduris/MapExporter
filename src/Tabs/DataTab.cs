using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;
using ResetSeverity = MapExporter.Resources.ResetSeverity;

namespace MapExporter.Tabs
{
    internal class DataTab(OptionInterface owner) : BaseTab(owner, "Data")
    {
        private int dataVersion;

        private OpScrollBox renderedList;
        private OpScrollBox processedList; // why do I use like 500 different terms for this

        private bool renderedDirty = true;
        private bool processedDirty = true;

        private Stack<FileSizeReader> renderedStack = [];
        private Stack<FileSizeReader> generatedStack = [];

        public override void Initialize()
        {
            const float SIDE_PADDING = 10f;
            const float ITEM_GAP = 20f;
            string tutorialText = Translate("MAPEX:datatutorial");
            dataVersion = Data.Version;

            // Controls
            var openFolderButton = new OpSimpleButton(new Vector2(SIDE_PADDING, SIDE_PADDING), new Vector2(160f, 24f), Translate("OPEN DATA FOLDER"));
            openFolderButton.OnClick += OpenFolderButton_OnClick;
            var deleteEverythingButton = new OpHoldButton(new Vector2(MENU_SIZE - SIDE_PADDING - 120f, SIDE_PADDING), new Vector2(120f, 24f), Translate("DELETE ALL"), 400) // 10 seconds
            { colorEdge = RedColor };
            deleteEverythingButton.OnPressDone += DeleteEverythingButton_OnPressDone;

            // Scrollboxes
            const float TOP_GAP = SIDE_PADDING + 60f + ITEM_GAP;
            const float BOTTOM_GAP = SIDE_PADDING + 24f + ITEM_GAP * 2 + 2f;
            const float SCROLLBOX_HEIGHT = MENU_SIZE - TOP_GAP - BOTTOM_GAP - 30f;
            renderedList = new OpScrollBox(new Vector2(299f - ITEM_GAP - 200f, BOTTOM_GAP), new Vector2(200f, SCROLLBOX_HEIGHT), 0f, false, true, true);
            processedList = new OpScrollBox(new Vector2(301f + ITEM_GAP, BOTTOM_GAP), new Vector2(200f, SCROLLBOX_HEIGHT), 0f, false, true, true);

            AddItems(
                // top
                new OpShinyLabel(new Vector2(0f, MENU_SIZE - SIDE_PADDING - 30f), new Vector2(600f, 30f), Translate("DATA MANAGEMENT"), FLabelAlignment.Center, true),
                new OpLabelLong(new Vector2(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 70), new Vector2(MENU_SIZE - SIDE_PADDING * 2, 40), tutorialText, true, FLabelAlignment.Center)
                { verticalAlignment = OpLabel.LabelVAlignment.Top },

                // middle
                renderedList,
                processedList,
                new OpLabel(new Vector2(renderedList.pos.x, renderedList.pos.y + renderedList.size.y), new Vector2(renderedList.size.x, 30f), Translate("SCREENSHOTTED"), FLabelAlignment.Right, true)
                { verticalAlignment = OpLabel.LabelVAlignment.Center },
                new OpLabel(new Vector2(processedList.pos.x, processedList.pos.y + processedList.size.y), new Vector2(processedList.size.x, 30f), Translate("GENERATED"), FLabelAlignment.Left, true)
                { verticalAlignment = OpLabel.LabelVAlignment.Center },
                new OpImage(new Vector2(299f, BOTTOM_GAP), "pixel") { scale = new Vector2(2f, SCROLLBOX_HEIGHT + 30f), color = MenuColorEffect.rgbMediumGrey },

                // bottom
                new OpImage(new Vector2(SIDE_PADDING, BOTTOM_GAP - ITEM_GAP - 2f), "pixel") { scale = new Vector2(MENU_SIZE - 2 * SIDE_PADDING, 2f), color = MenuColorEffect.rgbMediumGrey },
                openFolderButton,
                deleteEverythingButton
            );
        }

        public override void Update()
        {
            const float INNER_PAD = 6f;

            // Update lists if necessary
            if (dataVersion != Data.Version)
            {
                dataVersion = Data.Version;
                renderedDirty = true;
                processedDirty = true;
            }

            // Update lists if necessary
            if (renderedDirty)
            {
                renderedDirty = false;

                // Remove previous items
                foreach (var item in renderedList.items)
                {
                    _RemoveItem(item);
                }
                renderedList.items.Clear();
                renderedList.SetContentSize(0f);

                renderedStack.Clear();

                // Get file list
                var dir = Directory.CreateDirectory(Data.RenderDir);
                if (dir.GetDirectories().Length == 0)
                {
                    renderedList.AddItems(new OpLabel(INNER_PAD, renderedList.size.y - INNER_PAD - LabelTest.LineHeight(false), Translate("Empty!")));
                }
                else
                {
                    float y = renderedList.size.y - INNER_PAD;
                    GenerateDirectoryBox(renderedList, dir, () => renderedDirty = true, INNER_PAD, ref y, true, Resources.ResetSeverity.InputOnly, renderedStack);
                    renderedList.SetContentSize(renderedList.size.y - y + INNER_PAD);
                }
            }

            if (processedDirty)
            {
                processedDirty = false;

                // Remove previous items
                foreach (var item in processedList.items)
                {
                    _RemoveItem(item);
                }
                processedList.items.Clear();
                processedList.SetContentSize(0f);
                
                generatedStack.Clear();

                // Get file list
                var dir = Directory.CreateDirectory(Data.FinalDir);
                if (dir.GetDirectories().Length == 0)
                {
                    processedList.AddItems(new OpLabel(INNER_PAD, processedList.size.y - INNER_PAD - LabelTest.LineHeight(false), Translate("Empty!")));
                }
                else
                {
                    float y = processedList.size.y - INNER_PAD;
                    GenerateDirectoryBox(processedList, dir, () => processedDirty = true, INNER_PAD, ref y, true, Resources.ResetSeverity.OutputOnly, generatedStack);
                    processedList.SetContentSize(processedList.size.y - y + INNER_PAD);
                }
            }

            // Update file size stacks
            if (renderedStack.Count > 0)
            {
                renderedStack.Pop().Read();
            }
            if (generatedStack.Count > 0)
            {
                generatedStack.Pop().Read();
            }
        }

        private void GenerateDirectoryBox(OpScrollBox box, DirectoryInfo dir, Action markDirty, float x, ref float y, bool isRoot, Resources.ResetSeverity resetSeverity, Stack<FileSizeReader> stack, FileSizeReader parentReader = null)
        {
            float initialY = 0f; // we only use it if !isRoot so 0f is just a placeholder
            var reader = new FileSizeReader(dir, parentReader);
            stack.Push(reader);
            if (!isRoot)
            {
                y -= 30;
                initialY = y;

                // Delete button and directory name
                var button = new OpHoldButton(new Vector2(x, y), new Vector2(24f, 24f), "\xd7", 40) { colorEdge = RedColor, colorFill = RedColor };
                bool pressed = false;
                button.OnPressInit += (_) => pressed = false;
                button.OnPressDone += (_) =>
                {
                    if (!pressed)
                    {
                        pressed = true;
                        try
                        {
                            Directory.Delete(dir.FullName);
                            markDirty();
                        }
                        catch
                        {
                            button.PlaySound(SoundID.MENU_Error_Ping);
                        }
                    }
                };
                box.AddItems(button, new OpLabel(x + 30f, y + (12f - LabelTest.LineHalfHeight(false)), dir.Name));
            }

            // Children and fancy side thingy
            const string ROUND_SPRITE_NAME = "UIroundedCorner"; // 7x7 image
            bool success = true;
            List<UIelement> border = [];
            try
            {
                float lastY = y;
                bool isFirst = true;
                foreach (var item in dir.GetDirectories())
                {
                    // Draw border thingy
                    if (!isRoot)
                    {
                        border.Add(new OpImage(new Vector2(x + 11, y - 9), "pixel") { scale = new Vector2(2f, isFirst ? 7f : (lastY - y)), color = MenuColorEffect.rgbMediumGrey });
                        var corner = new OpImage(new Vector2(x + 18, y - 16), ROUND_SPRITE_NAME) { color = MenuColorEffect.rgbMediumGrey };
                        corner.sprite.rotation = -90f;
                        border.Add(corner);
                        border.Add(new OpImage(new Vector2(x + 18, y - 16), "pixel") { scale = new Vector2(8f, 2f), color = MenuColorEffect.rgbMediumGrey });
                    }

                    // New subchildren
                    lastY = y;
                    GenerateDirectoryBox(box, item, markDirty, x + (isRoot ? 0f : 30f), ref y, false, resetSeverity, stack, reader);
                    isFirst = false;
                }
            }
            catch
            {
                success = false;
            }

            if (success)
            {
                foreach (var item in border)
                {
                    box.AddItems(item);
                }
            }

            // Size text
            if (isRoot)
            {
                y -= LabelTest.LineHeight(false) * 2;
                var label = new OpLabel(x, y, Translate("Total size: <calculating>"), false);
                reader.OnRead += (size) => label.text = $"{Translate("Total size:")} {FileSizeToString(size)}";

                y -= 30f;
                var deleteFolderButton = new OpHoldButton(new Vector2(x, y), new Vector2(120f, 24f), Translate("DELETE ALL"), 200) // 5 sec
                { colorEdge = RedColor };
                deleteFolderButton.OnPressDone += (_) => DeleteFolderButton_OnPressDone(deleteFolderButton, resetSeverity);
                box.AddItems(label, deleteFolderButton);
            }
            else
            {
                const string PLACEHOLDER = "???";
                var placeholderWidth = LabelTest.GetWidth(PLACEHOLDER);
                var label = new OpLabel(box.size.x - SCROLLBAR_WIDTH - placeholderWidth - 2f, initialY, PLACEHOLDER, false);
                reader.OnRead += (size) =>
                {
                    string text = FileSizeToString(size);
                    float textWidth = LabelTest.GetWidth(text);
                    label.PosX = box.size.x - SCROLLBAR_WIDTH - textWidth - 2f;
                    label.text = text;
                };
                box.AddItems(label);
            }
        }

        private string FileSizeToString(long size)
        {
            var (suffix, divisor) = size switch
            {
                >= 1_000_000_000_000L => (Translate("filesize:TB"), 100_000_000_000L),
                >= 1_000_000_000L     => (Translate("filesize:GB"), 100_000_000L),
                >= 1_000_000L         => (Translate("filesize:MB"), 100_000L),
                >= 1_000L             => (Translate("filesize:KB"), 100L),
                _                     => (Translate("filesize:B"),  1L)
            };

            size /= divisor;
            if (size >= 1000) size -= size % 10L; // no decimal on triple digit things
            decimal dec = size / 10.0m;
            return dec + " " + suffix;
        }

        private class FileSizeReader(DirectoryInfo dir, FileSizeReader parent)
        {
            private readonly DirectoryInfo directory = dir;
            private readonly FileSizeReader parent = parent;

            private long size = 0;
            private bool updated = false;

            public event Action<long> OnRead;

            public void Read()
            {
                if (updated) return;
                updated = true;
                foreach (var file in directory.GetFiles())
                {
                    size += file.Length;
                }
                parent?.Update(size);
                OnRead?.Invoke(size);
            }

            protected void Update(long size)
            {
                this.size += size;
            }
        }

        private void DeleteEverythingButton_OnPressDone(UIfocusable trigger)
        {
            Resources.Reset(ResetSeverity.Everything);
            ModdingMenu.instance.cfgContainer._SwitchMode(Menu.Remix.ConfigContainer.Mode.ModView);
            trigger.held = false;
        }

        private void DeleteFolderButton_OnPressDone(OpHoldButton deleteFolderButton, ResetSeverity resetSeverity)
        {
            Resources.Reset(resetSeverity);
            renderedDirty = true;
            processedDirty = true;
        }

        private void OpenFolderButton_OnClick(UIfocusable trigger)
        {
            var path = Data.DataDirectory;
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()) && !path.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
            {
                path += Path.DirectorySeparatorChar.ToString();
            }
            using (Process.Start(path)) { }
        }
    }
}
