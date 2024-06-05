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
                SetContentSize(size.x * 2f);

                // Time to make the options and stuff!
                const float SCROLLBAR_HEIGHT = BaseTab.SCROLLBAR_WIDTH;
                const float PADDING = 6f;
                const float MARGIN = 4f;
                const int CONTENT_COLS = 4;
                float columnWidth = (contentSize - PADDING * 2 - (PADDING * 2 + 2f) * (CONTENT_COLS - 1)) / CONTENT_COLS;
                const int CONTENT_ROWS = 4;
                float optionsHeight = size.y - PADDING * 2f - SCROLLBAR_HEIGHT;
                float XPosForCol(int col) => PADDING + (columnWidth + PADDING * 2 + 2f) * col;
                float YPosForRow(int row, float height = 30f) {
                    float rowHeight = (optionsHeight - MARGIN * (CONTENT_ROWS - 1)) / CONTENT_ROWS;
                    float elementOffset = (rowHeight - height) / 2f;
                    return SCROLLBAR_HEIGHT + PADDING + rowHeight * (CONTENT_ROWS - row - 1) + elementOffset;
                };

                // Create basic room metadata inputs
                float nmWidth = LabelTest.GetWidth("NM: ");
                var nameInput = new OpTextBox(
                    OIUtil.CosmeticBind(room.roomName),
                    new Vector2(XPosForCol(0) + nmWidth, YPosForRow(1, OIUtil.COMBOBOX_HEIGHT)),
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
                    new Vector2(XPosForCol(0) + sbrWidth, YPosForRow(2, OIUtil.COMBOBOX_HEIGHT)),
                    columnWidth - sbrWidth,
                    sbrListItems)
                {
                    allowEmpty = true,
                    description = "Subregion"
                };

                var echoToggle = new OpTextButton(
                    new Vector2(XPosForCol(0), YPosForRow(3)),
                    new Vector2(columnWidth, 30f),
                    mapBox.activeRegion.echoRoom == room.roomName ? "Echo room" : "Not echo room");
                echoToggle.OnClick += (_) => EchoToggle_OnClick(echoToggle, room.roomName);

                AddItems(
                    new OpLabel(XPosForCol(0), YPosForRow(0), "METADATA", true),
                    new OpLabel(XPosForCol(0), YPosForRow(1, OIUtil.LABEL_HEIGHT), "NM:"),
                    nameInput,
                    new OpLabel(XPosForCol(0), YPosForRow(2, OIUtil.LABEL_HEIGHT), "SBR:"),
                    subregionInput,
                    echoToggle
                );

                // Connections
                AddItems(
                    new OpLabel(XPosForCol(1), YPosForRow(0), "EXITS", true)
                );

                // Creatures
                AddItems(
                    new OpLabel(XPosForCol(2), YPosForRow(0), "SPAWNS", true)
                );

                // Room tags
                var tagBtnWidth = Mathf.Max(LabelTest.GetWidth("ADD"), LabelTest.GetWidth("DEL")) + 8f;
                var tagBox = new OpComboBox(OIUtil.CosmeticBind(""), new(XPosForCol(3), YPosForRow(2)), columnWidth - tagBtnWidth - MARGIN, room.tags.Length == 0 ? [""] : room.tags);
                var tagAddInput = new OpTextBox(OIUtil.CosmeticBind(""), new Vector2(XPosForCol(3), YPosForRow(1)), columnWidth - tagBtnWidth - MARGIN)
                {
                    maxLength = int.MaxValue
                };
                var tagAddButton = new OpSimpleButton(new Vector2(XPosForCol(3) + columnWidth - tagBtnWidth, YPosForRow(1)), new Vector2(tagBtnWidth, OIUtil.TEXTBOX_HEIGHT), "ADD")
                {
                    colorEdge = BaseTab.GreenColor,
                    colorFill = BaseTab.GreenColor
                };
                var tagDelButton = new OpSimpleButton(new Vector2(XPosForCol(3) + columnWidth - tagBtnWidth, YPosForRow(2)), new Vector2(tagBtnWidth, OIUtil.TEXTBOX_HEIGHT), "DEL")
                {
                    colorEdge = BaseTab.RedColor,
                    colorFill = BaseTab.RedColor
                };
                var tagCountLabel = new OpLabel(XPosForCol(3), YPosForRow(3), room.tags.Length + " room tags");
                tagAddButton.OnClick += (_) => TagAddButton_OnClick(tagAddInput, tagBox, tagCountLabel, room);
                tagDelButton.OnClick += (_) => TagDelButton_OnCllick(tagBox, tagCountLabel, room);
                AddItems(
                    new OpLabel(XPosForCol(3), YPosForRow(0), "TAGS", true),
                    tagAddInput, tagAddButton,
                    tagBox, tagDelButton,
                    tagCountLabel
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

        private void TagAddButton_OnClick(OpTextBox textBox, OpComboBox comboBox, OpLabel label, RoomEntry room)
        {
            var tag = textBox.value.ToUpperInvariant().Trim();
            if (tag.Length == 0 || new HashSet<string>(room.tags).Contains(tag))
            {
                PlaySound(SoundID.MENU_Error_Ping);
                return;
            }
            Array.Resize(ref room.tags, room.tags.Length + 1);
            room.tags[room.tags.Length - 1] = tag;

            comboBox._itemList = room.tags.Select(x => new ListItem(x)).ToArray();
            comboBox._ResetIndex();
            comboBox.Change();

            label.text = room.tags.Length + " room tags";
            textBox.value = "";
            textBox.Change();

            PlaySound(SoundID.HUD_Food_Meter_Fill_Plop_A);
        }

        private void TagDelButton_OnCllick(OpComboBox comboBox, OpLabel label, RoomEntry room)
        {
            var tag = comboBox.value.ToUpperInvariant();
            var list = room.tags.ToList();
            if (!list.Remove(tag))
            {
                PlaySound(SoundID.MENU_Error_Ping);
                return;
            }
            room.tags = [.. list];

            comboBox._itemList = room.tags.Length > 0 ? room.tags.Select(x => new ListItem(x)).ToArray() : [new ListItem("")];
            comboBox._ResetIndex();
            comboBox.value = null;
            comboBox.Change();

            label.text = room.tags.Length + " room tags";

            PlaySound(SoundID.HUD_Food_Meter_Deplete_Plop_A);
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
