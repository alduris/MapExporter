using System;
using System.Collections.Generic;
using System.Linq;
using Menu.Remix.MixedUI;
using UnityEngine;
using RoomEntry = MapExporter.RegionInfo.RoomEntry;

namespace MapExporter.Tabs.UI
{
    partial class OpControlBox
    {
        internal class OpTagBox : OpScrollBox
        {
            private RoomEntry room;
            private bool dirty = true;

            public OpTagBox(OpTab tab, float contentSize, bool horizontal = false, bool hasSlideBar = true) : base(tab, contentSize, horizontal, hasSlideBar)
            {
                throw new NotImplementedException();
            }

            public OpTagBox(Vector2 pos, Vector2 size, RoomEntry room) : base(pos, size, 0f, false, false, true)
            {
                this.room = room;
                if (room.tags == null)
                {
                    room.tags = [];
                }
            }

            public override void Update()
            {
                base.Update();
                const float SCROLLBAR_WIDTH = BaseTab.SCROLLBAR_WIDTH;
                const float PADDING = 4f; // not applied to left side

                if (dirty)
                {
                    dirty = false;

                    foreach (var item in items)
                    {
                        tab._RemoveItem(item);
                    }
                    SetContentSize(0f);

                    // Create the input
                    float y = size.y - PADDING - OIUtil.TEXTBOX_HEIGHT;
                    float height = PADDING * 2 + OIUtil.TEXTBOX_HEIGHT;
                    var buttonWidth = LabelTest.GetWidth("ADD") + 8f;
                    var input = new OpTextBox(OIUtil.CosmeticBind(""), new Vector2(0, y), size.x - buttonWidth - PADDING * 2 - SCROLLBAR_WIDTH);
                    var button = new OpSimpleButton(new Vector2(size.x - buttonWidth - PADDING - SCROLLBAR_WIDTH, y), new Vector2(buttonWidth, OIUtil.TEXTBOX_HEIGHT), "ADD")
                    {
                        colorEdge = BaseTab.GreenColor,
                        colorFill = BaseTab.GreenColor
                    };
                    button.OnClick += (_) =>
                    {
                        AddTagToRoom(input.value);
                    };
                    AddItems(input, button);

                    // Create the labels
                    if (room.tags.Length > 0)
                    {
                        height += PADDING;
                        y -= PADDING;

                        foreach (var tag in room.tags)
                        {
                            y -= 24f;
                            height += 24f;
                            var delButton = new OpSimpleButton(new Vector2(0, y), new Vector2(24f, 24f), "\xd7")
                            {
                                colorEdge = BaseTab.RedColor,
                                colorFill = BaseTab.RedColor
                            };

                            var label = new OpLabel(28f, y + 2f, tag);

                            AddItems(delButton, label);
                        }
                    }

                    SetContentSize(height);
                }
            }

            public void AddTagToRoom(string tag)
            {
                tag = tag.ToUpperInvariant();
                if (tag.Length == 0 || room.tags.IndexOf(tag) > -1)
                {
                    PlaySound(SoundID.MENU_Error_Ping);
                    return;
                }
                Array.Resize(ref room.tags, room.tags.Length + 1);
                room.tags[room.tags.Length - 1] = tag;
                dirty = true;
            }

            public void RemoveTagFromRoom(string tag)
            {
                List<string> tags = [.. room.tags];
                tags.Remove(tag);
                room.tags = [.. tags];
                dirty = true;
            }
        }
    }
}
