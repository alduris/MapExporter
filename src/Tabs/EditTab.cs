using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapExporter.Tabs.UI;
using Menu.Remix.MixedUI;
using Menu.Remix.MixedUI.ValueTypes;
using RWCustom;
using UnityEngine;

#pragma warning disable IDE1006 // Naming Styles (beginning with underscore)

namespace MapExporter.Tabs
{
    internal class EditTab(OptionInterface owner) : BaseTab(owner, "Editor")
    {
        private OpComboBox regionSelector;
        private OpComboBox scugSelector;
        private OpComboBox importSelector;
        private OpScrollBox roomSelector;
        private OpCheckBox recenterCheckBox;
        private OpMapBox mapBox;
        private readonly WeakReference<OpTextButton> activeButton = new(null);

        private RegionInfo activeRegion = null;
        private int dataVersion = 0;

        private const float ROOMLIST_EDGE_PAD = 6f;
        private const float ROOMLIST_LH = 24f;

        public override void Initialize()
        {
            dataVersion = Data.Version;
            const float SIDE_PADDING = 10f;
            const float ITEM_GAP = 20f;
            var regionList = Data.RenderedRegions.Keys.OrderBy(s => s, StringComparer.InvariantCultureIgnoreCase).ToList();
            if (regionList.Count == 0)
                regionList.Add(""); // dummy placeholder

            // Top of menu
            const float TOPBAR_UNIT_WIDTH = (MENU_SIZE - SIDE_PADDING * 2f - ITEM_GAP * 2) / 5f;
            regionSelector = new OpComboBox(OIUtil.CosmeticBind(""),
                new(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 30f),
                TOPBAR_UNIT_WIDTH * 2 + ITEM_GAP,
                regionList.Select((x, i) => new ListItem(x, $"({x}) {Translate(Data.RegionNameFor(x, null))}", i)).ToList())
            {
                listHeight = 20
            };
            regionSelector.OnValueChanged += RegionSelector_OnValueChanged;
            scugSelector = new OpComboBox(OIUtil.CosmeticBind(""), new(SIDE_PADDING + TOPBAR_UNIT_WIDTH * 2 + ITEM_GAP * 2, regionSelector.pos.y), TOPBAR_UNIT_WIDTH, [""])
            {
                listHeight = 10
            };
            scugSelector.OnValueChanged += ScugSelector_OnValueChanged;
            var saveButton = new OpSimpleButton(new(MENU_SIZE - SIDE_PADDING - TOPBAR_UNIT_WIDTH, MENU_SIZE - SIDE_PADDING - 30f), new(TOPBAR_UNIT_WIDTH, 24f), "SAVE")
            {
                colorEdge = BlueColor
            };
            saveButton.OnClick += SaveButton_OnClick;

            // Bottom of menu
            string IMPORT_TEXT = Translate("Import from: ");
            var importLabel = new OpLabel(SIDE_PADDING, SIDE_PADDING + (12f - LabelTest.LineHeight(false) / 2f), IMPORT_TEXT, false);
            importSelector = new OpComboBox(OIUtil.CosmeticBind(""), new(importLabel.pos.x + LabelTest.GetWidth(IMPORT_TEXT, false) + 6f, importLabel.pos.y), TOPBAR_UNIT_WIDTH, [""])
            {
                listHeight = 10
            };
            var importButton = new OpSimpleButton(new(importSelector.pos.x + importSelector.size.x + 6f, importSelector.pos.y), new Vector2(80f, 24f), Translate("IMPORT"));
            importButton.OnClick += ImportButton_OnClick;

            string RECENTER_TEXT = Translate("Recenter on save: ");
            recenterCheckBox = new OpCheckBox(OIUtil.CosmeticBind(true), new(MENU_SIZE - SIDE_PADDING - CHECKBOX_SIZE, SIDE_PADDING));
            float recenterWidth = LabelTest.GetWidth(RECENTER_TEXT, false);
            var recenterLabel = new OpLabel(recenterCheckBox.pos.x - 6f - recenterWidth, SIDE_PADDING + (12f - LabelTest.LineHeight(false) / 2f), RECENTER_TEXT, false);

            // Body boxes and such
            const float BODY_LEFT_WIDTH = MENU_SIZE * 0.25f;
            const float BODY_RIGHT_WIDTH = MENU_SIZE - BODY_LEFT_WIDTH;
            const float TOPBAR_HEIGHT = 50f + SIDE_PADDING + ITEM_GAP;
            const float BOTTOMBAR_HEIGHT = 24f + ITEM_GAP;
            roomSelector = new(
                new Vector2(SIDE_PADDING, SIDE_PADDING + BOTTOMBAR_HEIGHT),
                new Vector2(BODY_LEFT_WIDTH - SIDE_PADDING - ITEM_GAP / 2f, MENU_SIZE - TOPBAR_HEIGHT - BOTTOMBAR_HEIGHT),
                0, false, true, true);
            float mapWidth = BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2;
            mapBox = new OpMapBox(new(BODY_LEFT_WIDTH + ITEM_GAP / 2, roomSelector.pos.y), new(mapWidth, roomSelector.size.y));

            // Add the items
            string MOUSE_MODE_TEXT = Translate("MAPEX:mousetutorial");
            string CONTROLLER_TEXT = Translate("MAPEX:controllertut");
            AddItems(
                // Input boxes and such
                roomSelector,
                mapBox,

                // Tutorial
                new OpLabel(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 50f, Custom.rainWorld.processManager.menuesMouseMode ? MOUSE_MODE_TEXT : CONTROLLER_TEXT),

                // Top things
                new OpShinyLabel(new(SIDE_PADDING + TOPBAR_UNIT_WIDTH * 3 + ITEM_GAP * 2, MENU_SIZE - SIDE_PADDING - 30f), new(TOPBAR_UNIT_WIDTH, 30f), Translate("MOVE"), FLabelAlignment.Center, true),
                saveButton,

                // Bottom things
                importLabel,
                importButton,
                recenterCheckBox,
                recenterLabel,

                // Place the combo boxes last for z-index reasons
                scugSelector,
                regionSelector,
                importSelector
            );
            mapBox.Initialize();
        }

        public override void Update()
        {
            if (dataVersion != Data.Version)
            {
                dataVersion = Data.Version;

                // Update region list
                var regionList = Data.RenderedRegions.Keys
                    .OrderBy(s => s, StringComparer.InvariantCultureIgnoreCase)
                    .Select((x, i) => new ListItem(x, $"({x}) {Translate(Data.RegionNameFor(x, null))}", i))
                    .ToList();
                if (regionList.Count == 0)
                    regionList.Add(new ListItem("", "")); // dummy placeholder
                regionSelector._itemList = [.. regionList];
                regionSelector._ResetIndex();
                regionSelector.Change();
                RegionSelector_OnValueChanged(null, regionSelector.value, null);
            }
        }

        private void RegionSelector_OnValueChanged(UIconfig config, string region, string oldRegion)
        {
            const string PLACEHOLDERSTR = ":"; // idk why someone would make their slugcat's id a colon; regardless, windows file names can't have colons
            if (oldRegion == region) return;

            // Clear old list
            var oldList = scugSelector.GetItemList();
            scugSelector.AddItems(false, new ListItem(PLACEHOLDERSTR));
            importSelector.AddItems(false, new ListItem(PLACEHOLDERSTR));
            foreach (var scug in oldList)
            {
                scugSelector.RemoveItems(true, scug.name);
                importSelector.RemoveItems(true, scug.name);
            }

            // Add new items
            if (Data.RenderedRegions.TryGetValue(region, out var scugs) && scugs.Count > 0)
            {
                foreach (var scug in scugs)
                {
                    scugSelector.AddItems(true, new ListItem(scug.value));
                    importSelector.AddItems(true, new ListItem(scug.value));
                }
            }
            else
            {
                // There has to be at least 1 item for some reason
                scugSelector.AddItems(false, new ListItem(""));
                importSelector.AddItems(false, new ListItem(""));
            }

            scugSelector.RemoveItems(true, PLACEHOLDERSTR);
            importSelector.RemoveItems(true, PLACEHOLDERSTR);
            scugSelector.value = null;
            importSelector.value = null;
            mapBox.UnloadRegion();
        }

        private void ScugSelector_OnValueChanged(UIconfig config, string scug, string oldScug)
        {
            if (scug == oldScug) return;

            // Remove items from boxes
            foreach (var item in roomSelector.items)
            {
                _RemoveItem(item);
            }
            roomSelector.items.Clear();
            roomSelector.SetContentSize(0);

            // Don't put any new stuff if there is no region
            var region = regionSelector.value;
            if (region == null || region == "" || scug == "" || new SlugcatStats.Name(scug, false).Index == -1 || !Data.RenderedRegions.TryGetValue(region, out var scugs) || scugs.Count == 0)
            {
                mapBox.UnloadRegion();
                return;
            }

            // Find the room list and add its contents
            if (File.Exists(Path.Combine(Data.RenderOutputDir(scug, region), "metadata.json")))
            {
                activeRegion = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(
                    Path.Combine(Data.RenderOutputDir(scug, region), "metadata.json"))));
                var roomList = activeRegion.rooms.Keys.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();

                float y = roomSelector.size.y - ROOMLIST_EDGE_PAD;
                float height = ROOMLIST_EDGE_PAD * 2;
                foreach (var room in roomList)
                {
                    y -= ROOMLIST_LH;
                    height += ROOMLIST_LH;
                    var button = new OpTextButton(new Vector2(ROOMLIST_EDGE_PAD, y), new Vector2(roomSelector.size.x - ROOMLIST_EDGE_PAD * 2 - SCROLLBAR_WIDTH, ROOMLIST_LH), room)
                    {
                        alignment = FLabelAlignment.Left
                    };
                    button.OnClick += (_) => RoomButton_OnClick(button, room);
                    roomSelector.AddItems(button);
                }
                roomSelector.SetContentSize(height);
                mapBox.LoadRegion(activeRegion);
            }
        }

        private void SwitchToRoom(string room)
        {
            mapBox.FocusRoom(room);
        }

        internal void _SwitchActiveButton(string room)
        {
            if (activeButton.TryGetTarget(out var oldButton))
            {
                oldButton.Active = false;
            }
            activeButton.SetTarget(null);
            foreach (var item in roomSelector.items)
            {
                if (item is OpTextButton button && button.text == room)
                {
                    button.Active = true;
                    activeButton.SetTarget(button);
                    roomSelector.ScrollToRect(new Rect(button.pos, button.size));
                    break;
                }
            }
        }

        internal void _UpdateButtonText()
        {
            // Remove items from boxes
            foreach (var item in roomSelector.items)
            {
                _RemoveItem(item);
            }
            roomSelector.items.Clear();
            roomSelector.SetContentSize(0);

            _SwitchActiveButton(null);

            // New room list
            var roomList = activeRegion.rooms.Keys.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();

            float y = roomSelector.size.y - ROOMLIST_EDGE_PAD;
            float height = ROOMLIST_EDGE_PAD * 2;
            foreach (var room in roomList)
            {
                y -= ROOMLIST_LH;
                height += ROOMLIST_LH;
                var button = new OpTextButton(new Vector2(ROOMLIST_EDGE_PAD, y), new Vector2(roomSelector.size.x - ROOMLIST_EDGE_PAD * 2 - SCROLLBAR_WIDTH, ROOMLIST_LH), room)
                {
                    alignment = FLabelAlignment.Left
                };
                button.OnClick += (_) => RoomButton_OnClick(button, room);
                roomSelector.AddItems(button);
            }
            roomSelector.SetContentSize(height);

            _SwitchActiveButton(mapBox.activeRoom);
        }

        private void RoomButton_OnClick(OpTextButton button, string room)
        {
            if (button.Active)
            {
                button.Active = false;
                activeButton.SetTarget(null);
                SwitchToRoom(null);
            }
            else
            {
                if (activeButton.TryGetTarget(out var oldButton)) oldButton.Active = false;
                button.Active = true;
                activeButton.SetTarget(button);
                SwitchToRoom(room);
            }
        }

        private void SaveButton_OnClick(UIfocusable trigger)
        {
            if (activeRegion == null)
            {
                return;
            }

            // Zero the rooms
            if (recenterCheckBox.GetValueBool())
            {
                Vector2 midpoint = Vector2.zero;
                foreach (var room in activeRegion.rooms.Values)
                {
                    midpoint += room.devPos;
                }
                midpoint /= activeRegion.rooms.Count;
                foreach (var room in activeRegion.rooms.Values)
                {
                    room.devPos -= midpoint;
                }
            }

            // Save
            File.WriteAllText(Path.Combine(Data.RenderOutputDir(scugSelector.value, activeRegion.acronym), "metadata.json"), Json.Serialize(activeRegion));

            // Reset
            SwitchToRoom(null);
            _SwitchActiveButton(null);
            mapBox.viewOffset = Vector2.zero;
            mapBox.UpdateMap();
        }

        private void ImportButton_OnClick(UIfocusable trigger)
        {
            var scug = importSelector.value;

            // Don't do anything cases
            if (scug == null || scug == "" || scug == scugSelector.value || new SlugcatStats.Name(scug, false).Index == -1)
                return;

            var region = activeRegion.acronym;
            if (File.Exists(Path.Combine(Data.RenderOutputDir(scug, region), "metadata.json")))
            {
                importSelector.value = null;
                SwitchToRoom(null);

                // Get the other slugcat's region and load its positions
                var newState = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(Path.Combine(Data.RenderOutputDir(scug, region), "metadata.json"))));
                foreach (var newRoom in newState.rooms)
                {
                    if (activeRegion.rooms.TryGetValue(newRoom.Key, out var room))
                    {
                        room.devPos = newRoom.Value.devPos;
                        room.hidden = newRoom.Value.hidden;
                    }
                }

                // Update map
                mapBox.UpdateMap();
            }
        }
    }
}
