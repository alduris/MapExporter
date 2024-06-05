using System;
using System.Collections.Generic;
using System.Linq;
using Menu.Remix.MixedUI;
using UnityEngine;
using static MapExporter.RegionInfo;

namespace MapExporter.Tabs.UI
{
    internal class OpControlBox : OpScrollBox
    {
        private OpMapBox mapBox;
        private string lastRoom = null;

        public OpControlBox(OpTab tab, float contentSize, bool horizontal = false, bool hasSlideBar = true) : base(tab, contentSize, horizontal, hasSlideBar)
        {
            throw new NotImplementedException(); // nope, not for you :3
        }

        public OpControlBox(Vector2 pos, Vector2 size) : base(pos, size, size.x, true, true, true)
        {
        }

        public void Initialize(OpMapBox mapBox)
        {
            this.mapBox = mapBox;
        }

        public override void Update()
        {
            base.Update();

            if (lastRoom != mapBox.activeRoom)
            {
                lastRoom = mapBox.activeRoom;
                if (mapBox.activeRoom == null) return;
                var room = mapBox.activeRegion.rooms[mapBox.activeRoom];

                // Clear out anything previously there
                foreach (var item in items)
                {
                    tab._RemoveItem(item);
                }
                items.Clear();
                SetContentSize(0f);

                // Time to make the options and stuff!
                const float SCROLLBAR_WIDTH = BaseTab.SCROLLBAR_WIDTH;
                const float INNER_PAD = 6f;
                const int CONTENT_ROWS = 4;
                const float CONTENT_GAP = 4f;
                float optionsHeight = size.y - SCROLLBAR_WIDTH - INNER_PAD * 2f;
                float YPosForRow(int row, int maxRows=CONTENT_ROWS) {
                    float rowHeight = (optionsHeight - CONTENT_GAP * (maxRows - 1)) / maxRows;
                    float elementOffset = (rowHeight - 30f) / 2f;
                    return SCROLLBAR_WIDTH + INNER_PAD + rowHeight * (maxRows - row - 1) + elementOffset;
                };

                // Create basic room metadata inputs
                const float METADATA_WIDTH = 120f;
                float nmWidth = LabelTest.GetWidth("NM: ");
                var nameInput = new OpTextBox(OIUtil.CosmeticBind(room.roomName), new Vector2(INNER_PAD + nmWidth, YPosForRow(0, 3)), METADATA_WIDTH - nmWidth)
                {
                    maxLength = 240, // fun fact: 240 is the actual max length the name of a room with a settings file can be because of Windows file system restrictions
                    accept = OpTextBox.Accept.StringASCII,
                    allowSpace = true,
                    description = "Room name"
                };
                nameInput.OnValueChanged += NameInput_OnValueChanged;

                float sbrWidth = LabelTest.GetWidth("SBR: ");
                var sbrListItems = new HashSet<string>(mapBox.activeRegion.rooms.Values.Select(x => x.subregion ?? "")).ToArray();
                var subregionInput = new OpComboBox(OIUtil.CosmeticBind(room.subregion), new Vector2(INNER_PAD + sbrWidth, YPosForRow(1, 3)), METADATA_WIDTH - sbrWidth, sbrListItems)
                {
                    allowEmpty = true,
                    description = "Subregion"
                };

                var echoToggle = new OpTextButton(
                    new Vector2(INNER_PAD, YPosForRow(2, 3)),
                    new Vector2(METADATA_WIDTH, 30f),
                    mapBox.activeRegion.echoRoom == room.roomName ? "Echo room" : "Not echo room");
                echoToggle.OnClick += (_) => EchoToggle_OnClick(echoToggle, room.roomName);

                AddItems(
                    new OpLabel(INNER_PAD, YPosForRow(0, 3), "NM:"),
                    nameInput,
                    new OpLabel(INNER_PAD, YPosForRow(1, 3), "SBR:"),
                    subregionInput,
                    echoToggle,
                    new OpImage(new Vector2(INNER_PAD + METADATA_WIDTH + INNER_PAD, SCROLLBAR_WIDTH + INNER_PAD), "pixel")
                    {
                        scale = new Vector2(1f, optionsHeight),
                        color = colorEdge
                    }
                );

                // Move comboboxes to the front for z-indexing reasons
                foreach (var item in items)
                {
                    if (item is OpComboBox)
                    {
                        item.MoveToFront();
                    }
                }

                SetContentSize(INNER_PAD * 2f + METADATA_WIDTH + INNER_PAD * 2f + 1f);
                //             edge pad -----   left part ----   separator ---------
            }
        }

        private void EchoToggle_OnClick(OpTextButton trigger, string room)
        {
            bool flag = mapBox.activeRegion.echoRoom == room;
            mapBox.activeRegion.echoRoom = flag ? null : room;
            trigger.text = flag ? "Echo room" : "Not echo room";
        }

        private static readonly char[] DisallowedChars = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];
        private void NameInput_OnValueChanged(UIconfig config, string value, string oldValue)
        {
            if (value == oldValue) return;
            var region = mapBox.activeRegion;

            // Figure out if room name exists or contains non-windows file system characters
            bool flag = region.rooms.ContainsKey(value);
            if (!flag)
            {
                foreach (var c in DisallowedChars)
                {
                    if (value.IndexOf(c) > -1)
                    {
                        flag = true;
                        break;
                    }
                }
            }

            if (flag)
            {
                // Special behavior for if so
                var textbox = (OpTextBox)config;
                Color oldColor = textbox.colorEdge;
                int errorPing = 10;

                void grafUpdate(float timeStacker)
                {
                    textbox.colorEdge = Color.Lerp(oldColor, new Color(1f, 0f, 0f), Mathf.Max(0f, (errorPing - timeStacker) / 10f));
                }
                void update()
                {
                    errorPing--;
                    if (errorPing == 0)
                    {
                        textbox.OnUpdate -= update;
                        textbox.OnGrafUpdate -= grafUpdate;
                    }
                }
                textbox.OnUpdate += update;
                textbox.OnGrafUpdate += grafUpdate;

                config.value = oldValue;
                PlaySound(SoundID.MENU_Error_Ping);
            }
            else
            {
                // Update data then
                region.rooms[value] = region.rooms[oldValue];
                region.rooms.Remove(oldValue);
                region.rooms[value].roomName = value;

                foreach (var connection in region.connections)
                {
                    if (connection.roomA == oldValue)
                    {
                        connection.roomA = value;
                    }
                    if (connection.roomB == oldValue)
                    {
                        connection.roomB = value;
                    }
                }

                if (region.echoRoom == oldValue)
                {
                    region.echoRoom = value;
                }

                if (mapBox.activeRoom == oldValue)
                {
                    mapBox.FocusRoom(value);
                }
                (tab as EditTab)._UpdateButtonText();
            }

        }
    }
}
