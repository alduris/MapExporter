using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapExporter.Tabs.UI;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using UnityEngine;

#pragma warning disable IDE1006 // Naming Styles (beginning with underscore)

namespace MapExporter.Tabs
{
    internal class EditTab(OptionInterface owner) : BaseTab(owner, "Editor")
    {
        private OpComboBox scugSelector;
        private OpComboBox regionSelector;
        private OpScrollBox roomSelector;
        private OpControlBox controlBox;
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
            const float MENU_SIZE = 600f;
            // const float COMBOBOX_OFFSET = 4f;
            var scugList = Data.RenderedRegions.Keys.ToList();
            if (scugList.Count == 0)
                scugList.Add(new SlugcatStats.Name("", false)); // dummy placeholder

            // Top of menu
            const float TOPBAR_UNIT_WIDTH = (MENU_SIZE - SIDE_PADDING * 2f - ITEM_GAP * 2) / 5f;
            scugSelector = new(OIUtil.CosmeticBind(""), new(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 30f), TOPBAR_UNIT_WIDTH, scugList.Select(x => x.value).ToArray());
            scugSelector.OnValueChanged += ScugSelector_OnValueChanged;
            regionSelector = new(OIUtil.CosmeticBind(""), new(SIDE_PADDING + TOPBAR_UNIT_WIDTH + ITEM_GAP, scugSelector.pos.y), TOPBAR_UNIT_WIDTH * 2 + ITEM_GAP, [""]);
            regionSelector.OnValueChanged += RegionSelector_OnValueChanged;
            var saveButton = new OpSimpleButton(new(MENU_SIZE - SIDE_PADDING - TOPBAR_UNIT_WIDTH, MENU_SIZE - SIDE_PADDING - 30f), new(TOPBAR_UNIT_WIDTH, 24f), "SAVE")
            {
                colorEdge = BlueColor
            };
            saveButton.OnClick += SaveButton_OnClick;

            // Body boxes and such
            const float BODY_LEFT_WIDTH = MENU_SIZE / 3;
            const float BODY_RIGHT_WIDTH = MENU_SIZE - BODY_LEFT_WIDTH;
            const float TOPBAR_HEIGHT = 30f + SIDE_PADDING + ITEM_GAP;
            roomSelector = new(
                new Vector2(SIDE_PADDING, SIDE_PADDING),
                new Vector2(BODY_LEFT_WIDTH - SIDE_PADDING - ITEM_GAP / 2f, MENU_SIZE - TOPBAR_HEIGHT),
                0, false, true, true);
            float mapSize = BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2; // supposed to be a square
            controlBox = new(
                new Vector2(BODY_LEFT_WIDTH + ITEM_GAP / 2, SIDE_PADDING),
                new Vector2(BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2, roomSelector.size.y - mapSize - ITEM_GAP));
            mapBox = new OpMapBox(new(controlBox.pos.x, roomSelector.pos.y + roomSelector.size.y - mapSize), new(mapSize, mapSize));

            // Add the items
            AddItems(
                // Input boxes and such
                roomSelector,
                controlBox,
                mapBox,

                // Place the top things last for z-index reasons
                regionSelector,
                scugSelector,
                saveButton,
                new OpLabel(new(SIDE_PADDING + TOPBAR_UNIT_WIDTH * 3 + ITEM_GAP * 2, MENU_SIZE - SIDE_PADDING - 30f), new(TOPBAR_UNIT_WIDTH, 30f), "EDIT MAP", FLabelAlignment.Center, true)
            );
            mapBox.Initialize();
            controlBox.Initialize(mapBox);
        }

        public override void Update()
        {
            if (dataVersion != Data.Version)
            {
                dataVersion = Data.Version;

                // Update slugcat list
                var scugList = Data.RenderedRegions.Keys.ToList();
                if (scugList.Count == 0)
                    scugList.Add(new SlugcatStats.Name("", false)); // dummy placeholder
                scugSelector._itemList = scugList.Select((x, i) => new ListItem(x.value, i)).ToArray();
                scugSelector._ResetIndex();
                scugSelector.Change();
                ScugSelector_OnValueChanged(null, scugSelector.value, null);
            }
        }

        private void ScugSelector_OnValueChanged(UIconfig config, string slugcat, string oldSlugcat)
        {
            const string PLACEHOLDERSTR = ":"; // impossible for region to have as its name because Windows file names can't have colons
            if (oldSlugcat == slugcat) return;

            var scug = new SlugcatStats.Name(slugcat);

            // Clear old list
            var oldList = regionSelector.GetItemList();
            regionSelector.AddItems(false, new ListItem(PLACEHOLDERSTR));
            foreach (var region in oldList)
            {
                regionSelector.RemoveItems(true, region.name);
            }

            // Add new items
            if (Data.RenderedRegions.ContainsKey(scug))
            {
                var regions = Data.RenderedRegions[scug];
                foreach (var region in regions)
                {
                    regionSelector.AddItems(false, new ListItem(region, $"({region}) {Region.GetRegionFullName(region, scug)}"));
                }
            }
            else
            {
                regionSelector.AddItems(false, new ListItem("")); // there has to be at least 1 for some reason
            }

            regionSelector.RemoveItems(true, PLACEHOLDERSTR);
            regionSelector.value = null;
        }

        private void RegionSelector_OnValueChanged(UIconfig config, string acronym, string oldAcronym)
        {
            if (acronym == oldAcronym) return;

            // Remove items from boxes
            foreach (var item in roomSelector.items)
            {
                _RemoveItem(item);
            }
            roomSelector.items.Clear();
            roomSelector.SetContentSize(0);

            // Don't put any new stuff if there is no region
            var scug = new SlugcatStats.Name(scugSelector.value, false);
            if (acronym == null || scug.Index == -1 || !Data.RenderedRegions.ContainsKey(scug) ||
                !Data.RenderedRegions[scug].Contains(acronym, StringComparer.InvariantCultureIgnoreCase))
            {
                mapBox.UnloadRegion();
                return;
            }

            // Find the room list and add its contents
            if (File.Exists(Path.Combine(Data.RenderOutputDir(scug.value, acronym), "metadata.json")))
            {

                activeRegion = RegionInfo.FromJson((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(
                    Path.Combine(Data.RenderOutputDir(scug.value, acronym), "metadata.json"))));
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
            throw new NotImplementedException();
        }
    }
}
