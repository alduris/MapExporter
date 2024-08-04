using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class DataTab(OptionInterface owner) : BaseTab(owner, "Data")
    {
        private int dataVersion;

        private OpScrollBox renderedList;
        private OpScrollBox processedList; // why do I use like 500 different terms for this

        private bool renderedDirty = true;
        private bool processedDirty = true;

        public override void Initialize()
        {
            const float SIDE_PADDING = 10f;
            const float ITEM_GAP = 20f;
            const string TUTORIAL = "Shown here is not what is processed but what is present in the mod's data folder,"
                + "and thus may not accurately represent what regions it recognizes as rendered or processed.";
            dataVersion = Data.Version;

            // Controls

            // Scrollboxes
            const float TOP_GAP = SIDE_PADDING + 70f + ITEM_GAP;
            const float BOTTOM_GAP = SIDE_PADDING + 24f + ITEM_GAP * 2 + 2f;
            const float SCROLLBOX_HEIGHT = MENU_SIZE - TOP_GAP - BOTTOM_GAP - 30f;
            renderedList = new OpScrollBox(new Vector2(299f - ITEM_GAP - 200f, BOTTOM_GAP), new Vector2(200f, SCROLLBOX_HEIGHT), 0f, false, true, true);
            processedList = new OpScrollBox(new Vector2(301f + ITEM_GAP, BOTTOM_GAP), new Vector2(200f, SCROLLBOX_HEIGHT), 0f, false, true, true);

            AddItems(
                new OpShinyLabel(new Vector2(0f, MENU_SIZE - SIDE_PADDING - 30f), new Vector2(600f, 30f), "DATA MANAGEMENT", FLabelAlignment.Center, true),
                new OpLabelLong(new Vector2(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 70), new Vector2(MENU_SIZE - SIDE_PADDING * 2, 40), TUTORIAL, true, FLabelAlignment.Center)
                { verticalAlignment = OpLabel.LabelVAlignment.Top },

                renderedList,
                processedList,
                new OpLabel(new Vector2(renderedList.pos.x, renderedList.pos.y + renderedList.size.y), new Vector2(renderedList.size.x, 30f), "RENDERED", FLabelAlignment.Right, true)
                { verticalAlignment = OpLabel.LabelVAlignment.Center },
                new OpLabel(new Vector2(processedList.pos.x, processedList.pos.y + processedList.size.y), new Vector2(processedList.size.x, 30f), "GENERATED", FLabelAlignment.Left, true)
                { verticalAlignment = OpLabel.LabelVAlignment.Center },
                new OpImage(new Vector2(299f, BOTTOM_GAP), "pixel") { scale = new Vector2(2f, SCROLLBOX_HEIGHT + 30f) }
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

                // Get file list
                var dir = Directory.CreateDirectory(Data.RenderDir);
                if (dir.GetDirectories().Length == 0)
                {
                    renderedList.AddItems(new OpLabel(INNER_PAD, renderedList.size.y - INNER_PAD - LabelTest.LineHeight(false), "Empty!"));
                }
                else
                {
                    float y = renderedList.size.y - INNER_PAD;
                    GenerateDirectoryBox(renderedList, dir, () => renderedDirty = true, INNER_PAD, ref y, true);
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

                // Get file list
                var dir = Directory.CreateDirectory(Data.FinalDir);
                if (dir.GetDirectories().Length == 0)
                {
                    processedList.AddItems(new OpLabel(INNER_PAD, processedList.size.y - INNER_PAD - LabelTest.LineHeight(false), "Empty!"));
                }
                else
                {
                    float y = processedList.size.y - INNER_PAD;
                    GenerateDirectoryBox(processedList, dir, () => processedDirty = true, INNER_PAD, ref y, true);
                    processedList.SetContentSize(processedList.size.y - y + INNER_PAD);
                }
            }
        }

        private void GenerateDirectoryBox(OpScrollBox box, DirectoryInfo dir, Action markDirty, float x, ref float y, bool isRoot)
        {
            if (!isRoot)
            {
                y -= 30;

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
                box.AddItems(button, new OpLabel(x + 30f, y + (24f - LabelTest.LineHeight(false)) / 2f, dir.Name));
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
                    GenerateDirectoryBox(box, item, markDirty, x + (isRoot ? 0f : 30f), ref y, false);
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
        }
    }
}
