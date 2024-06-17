using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    /// <summary>
    /// Directory picker for remix menu. Requires you to 
    /// </summary>
    internal class OpDirPicker : OpScrollBox
    {
        private bool dirty = true;

        public DirectoryInfo CurrentDir { get; private set; } = null; // Directory.CreateDirectory(Custom.LegacyRootFolderDirectory());

        public bool ValidDir { get; private set; }

        public OpDirPicker(OpTab tab) : base(tab, 0f, false, true) { }

        public OpDirPicker(Vector2 pos, Vector2 size) : base(pos, size, 0f, false, true, true)
        {
            this.size = new Vector2(Mathf.Max(160f, size.x), size.y);
        }

        public override void Update()
        {
            base.Update();

            if (dirty && tab != null)
            {
                dirty = false;

                // Clear out old items
                foreach (var item in items)
                {
                    tab._RemoveItem(item);
                }
                items.Clear();
                SetContentSize(0f);

                // Put head
                const float GAP = 6f;

                float y = size.y - GAP - 24f;
                float height = GAP * 4 + 26f;
                float fullWidth = size.x - 2 * GAP - BaseTab.SCROLLBAR_WIDTH;

                var upButton = new OpSimpleImageButton(new Vector2(GAP, y), new Vector2(24f, 24f), "Menu_Symbol_Arrow");
                var refreshButton = new OpSimpleImageButton(new Vector2(GAP * 2 + 24f, y), new Vector2(24f, 24f), "Menu_Symbol_Repeats");
                var doneButton = new OpSimpleImageButton(new Vector2(size.x - GAP - 25f - BaseTab.SCROLLBAR_WIDTH, y), new Vector2(24f, 24f), "Menu_Symbol_CheckBox");
                var pathInput = new OpTextBox(
                    OIUtil.CosmeticBind<string>(CurrentDir?.FullName ?? "/"),
                    new Vector2(GAP * 3 + 24f * 2, y),
                    size.x - GAP * 5 - 24f * 3 - BaseTab.SCROLLBAR_WIDTH)
                    { maxLength = 65535 };

                upButton.OnClick += UpButton_OnClick;
                refreshButton.OnClick += RefreshButton_OnClick;
                doneButton.OnClick += (_) => DoneButton_OnClick(pathInput);

                y -= GAP + 2f;

                AddItems(
                    upButton,
                    refreshButton,
                    doneButton,
                    pathInput,
                    new OpImage(new(GAP, y), "pixel") { scale = new Vector2(fullWidth, 2f), color = colorEdge }
                    );

                y -= GAP + 24f;

                // Get directory contents
                Dictionary<string, DirectoryInfo> map = [];
                if (CurrentDir == null)
                {
                    foreach (var item in Directory.GetLogicalDrives())
                    {
                        map.Add(item, new DirectoryInfo(item));
                    }
                }
                else
                {
                    foreach (var item in CurrentDir.GetDirectories())
                    {
                        map.Add(item.Name, item);
                    }
                }

                // Sort and put in the thingamabob
                var list = map.ToList();
                list.Sort(new Comparison<KeyValuePair<string, DirectoryInfo>>((a, b) => StringComparer.InvariantCultureIgnoreCase.Compare(a.Key, b.Key)));
                foreach (var kv in list)
                {
                    var dirButton = new OpTextButton(new Vector2(GAP, y), new Vector2(fullWidth, 24f), kv.Key) { alignment = FLabelAlignment.Left };
                    dirButton.OnClick += (_) => DirButton_OnClick(kv.Value);
                    AddItems(dirButton);
                    y -= 24f;
                    height += 24f;
                }

                // Set content size
                SetContentSize(height, true);
            }
        }

        private void DirButton_OnClick(DirectoryInfo dir)
        {
            var oldDir = CurrentDir;
            try
            {
                CurrentDir = dir;
                dirty = true;
            }
            catch (Exception e)
            {
                CurrentDir = oldDir;
                PlaySound(SoundID.MENU_Error_Ping);
                Plugin.Logger.LogError(e);
            }
        }

        private void UpButton_OnClick(UIfocusable trigger)
        {
            if (CurrentDir == null)
            {
                PlaySound(SoundID.MENU_Error_Ping);
            }
            else
            {
                var oldDir = CurrentDir;
                try
                {
                    CurrentDir = CurrentDir.Parent;
                    dirty = true;
                }
                catch (Exception e)
                {
                    CurrentDir = oldDir;
                    PlaySound(SoundID.MENU_Error_Ping);
                    Plugin.Logger.LogError(e);
                }
            }
        }

        private void RefreshButton_OnClick(UIfocusable trigger)
        {
            dirty = true;
        }

        private void DoneButton_OnClick(OpTextBox textbox)
        {
            var oldDir = CurrentDir;
            try
            {
                CurrentDir = Directory.CreateDirectory(textbox.value);
                dirty = true;
            }
            catch (Exception e)
            {
                CurrentDir = oldDir;
                PlaySound(SoundID.MENU_Error_Ping);
                Plugin.Logger.LogError(e);
            }
        }
    }
}
