using System;
using System.Collections.Generic;
using System.Linq;
using Menu.Remix.MixedUI;
using UnityEngine;
using static MapExporter.RegionInfo;

namespace MapExporter.Tabs.UI
{
    internal partial class OpControlBox : OpScrollBox
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
                SetContentSize(size.x * 2f);

                // Time to make the options and stuff!
                const float SCROLLBAR_HEIGHT = BaseTab.SCROLLBAR_WIDTH;
                const float PADDING = 6f;
                const float MARGIN = 4f;
                const int CONTENT_COLS = 4;
                float columnWidth = (contentSize - PADDING * 2 - (PADDING * 2 + 2f) * (CONTENT_COLS - 1)) / CONTENT_COLS;
                const int CONTENT_ROWS = 5;
                float optionsHeight = size.y - PADDING * 2f - SCROLLBAR_HEIGHT;
                float titleY = SCROLLBAR_HEIGHT + PADDING + optionsHeight - 30f;
                float XPosForCol(int col) => PADDING + (columnWidth + PADDING * 2 + 2f) * col;
                float YPosForRow(int row, int maxRows = CONTENT_ROWS, float height = 30f) {
                    float rowHeight = (optionsHeight - MARGIN * (maxRows - 1)) / maxRows;
                    float elementOffset = (rowHeight - height) / 2f;
                    return SCROLLBAR_HEIGHT + PADDING + rowHeight * (maxRows - row - 1) + elementOffset;
                };

                // Create basic room metadata inputs
                const int METADATA_ROWS = 4;
                float nmWidth = LabelTest.GetWidth("NM: ");
                var nameInput = new OpTextBox(
                    OIUtil.CosmeticBind(room.roomName),
                    new Vector2(XPosForCol(0) + nmWidth, YPosForRow(1, METADATA_ROWS, OIUtil.COMBOBOX_HEIGHT)),
                    columnWidth - nmWidth)
                {
                    maxLength = 240, // fun fact: 240 is the actual max length the name of a room with a settings file can be because of Windows file system restrictions
                    accept = OpTextBox.Accept.StringASCII,
                    allowSpace = true,
                    description = "Room name"
                };
                nameInput.OnValueChanged += NameInput_OnValueChanged;

                float sbrWidth = LabelTest.GetWidth("SBR: ");
                var sbrListItems = new HashSet<string>(mapBox.activeRegion.rooms.Values.Select(x => x.subregion ?? "")).ToArray();
                var subregionInput = new OpComboBox(
                    OIUtil.CosmeticBind(room.subregion),
                    new Vector2(XPosForCol(0) + sbrWidth, YPosForRow(2, METADATA_ROWS, OIUtil.COMBOBOX_HEIGHT)),
                    columnWidth - sbrWidth,
                    sbrListItems)
                {
                    allowEmpty = true,
                    description = "Subregion"
                };

                var echoToggle = new OpTextButton(
                    new Vector2(XPosForCol(0), YPosForRow(3, METADATA_ROWS)),
                    new Vector2(columnWidth, 30f),
                    mapBox.activeRegion.echoRoom == room.roomName ? "Echo room" : "Not echo room");
                echoToggle.OnClick += (_) => EchoToggle_OnClick(echoToggle, room.roomName);

                AddItems(
                    new OpLabel(XPosForCol(0), titleY, "METADATA", true),
                    new OpLabel(XPosForCol(0), YPosForRow(1, METADATA_ROWS, OIUtil.LABEL_HEIGHT), "NM:"),
                    nameInput,
                    new OpLabel(XPosForCol(0), YPosForRow(2, METADATA_ROWS, OIUtil.LABEL_HEIGHT), "SBR:"),
                    subregionInput,
                    echoToggle
                );

                // Connections
                AddItems(
                    new OpLabel(XPosForCol(1), titleY, "EXITS", true)
                );

                // Creatures
                AddItems(
                    new OpLabel(XPosForCol(2), titleY, "SPAWNS", true)
                );

                // Room tags
                var tagBox = new OpTagBox(new Vector2(XPosForCol(3), SCROLLBAR_HEIGHT + PADDING), new Vector2(columnWidth, optionsHeight - 30f - MARGIN), room);
                AddItems(
                    new OpLabel(XPosForCol(3), titleY, "TAGS", true),
                    tagBox
                );

                // Lines between columns
                for (int i = 0; i < CONTENT_COLS; i++)
                {
                    AddItems(new OpImage(new Vector2(XPosForCol(i + 1) - PADDING - 2f, SCROLLBAR_HEIGHT + PADDING), "pixel")
                    {
                        scale = new Vector2(2f, optionsHeight),
                        color = colorEdge
                    });
                }

                // Move comboboxes to the front for z-indexing reasons
                foreach (var item in items)
                {
                    if (item is OpComboBox)
                    {
                        item.MoveToFront();
                    }
                }
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
