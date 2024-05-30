using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MapExporter.Tabs.UI;
using Menu.Remix.MixedUI;
using MoreSlugcats;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class EditTab : BaseTab
    {
        private OpComboBox scugSelector;
        private OpComboBox regionSelector;
        private OpScrollBox roomSelector;
        private OpScrollBox roomOptions;
        private OpMapBox mapBox;
        private WeakReference<OpTextButton> activeButton = new(null);

        private RegionInfo activeRegion = null;


        public EditTab(OptionInterface owner) : base(owner, "Editor")
        {
        }

        public override void Initialize()
        {
            const float SIDE_PADDING = 10f;
            const float ITEM_GAP = 20f;
            const float MENU_SIZE = 600f;
            const float COMBOBOX_OFFSET = 4f;
            var scugList = Data.RenderedRegions.Keys.ToList();

            // Top of menu
            const float TOPBAR_UNIT_WIDTH = (MENU_SIZE - SIDE_PADDING * 2f - ITEM_GAP * 2) / 4f;
            scugSelector = new(OIUtil.CosmeticBind(""), new(SIDE_PADDING, MENU_SIZE - SIDE_PADDING - 30f + COMBOBOX_OFFSET), TOPBAR_UNIT_WIDTH, scugList.Select(x => x.value).ToArray());
            scugSelector.OnValueChanged += ScugSelector_OnValueChanged;
            regionSelector = new(OIUtil.CosmeticBind(""), new(scugSelector.pos.x + scugSelector.size.x + ITEM_GAP + COMBOBOX_OFFSET, scugSelector.pos.y), TOPBAR_UNIT_WIDTH * 2, [""]);
            regionSelector.OnValueChanged += RegionSelector_OnValueChanged;

            // Input boxes and such
            const float BODY_LEFT_WIDTH = MENU_SIZE / 3;
            const float BODY_RIGHT_WIDTH = MENU_SIZE - BODY_LEFT_WIDTH;
            const float TOPBAR_HEIGHT = 30f + SIDE_PADDING + ITEM_GAP;
            roomSelector = new(
                new Vector2(SIDE_PADDING, SIDE_PADDING),
                new Vector2(BODY_LEFT_WIDTH - SIDE_PADDING - ITEM_GAP / 2f, MENU_SIZE - TOPBAR_HEIGHT),
                0, false, true, true);
            var mapSize = BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2; // trying to make it a square
            roomOptions = new(
                new Vector2(BODY_LEFT_WIDTH + ITEM_GAP / 2, SIDE_PADDING),
                new Vector2(BODY_RIGHT_WIDTH - SIDE_PADDING - ITEM_GAP / 2, roomSelector.size.y - mapSize - ITEM_GAP),
                0, true, true, true);
            mapBox = new OpMapBox(new(roomOptions.pos.x, roomSelector.pos.y + roomSelector.size.y - mapSize), new(mapSize, mapSize));

            // Add the items
            AddItems(
                // Input boxes and such
                roomSelector,
                roomOptions,
                mapBox,

                // Place the top things last for z-index reasons
                regionSelector,
                scugSelector,
                new OpLabel(new Vector2(MENU_SIZE - SIDE_PADDING - TOPBAR_UNIT_WIDTH, MENU_SIZE - SIDE_PADDING - 30f), new(TOPBAR_UNIT_WIDTH, 30f), "EDIT MAP", FLabelAlignment.Center, true)
            );
            mapBox.Initialize();
        }

        public override void Update()
        {
            // throw new NotImplementedException();
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
            Plugin.Logger.LogDebug("EEEEE");
            if (acronym == oldAcronym) return;

            // Remove items from boxes
            foreach (var el in roomSelector.items)
            {
                el.Deactivate();
                el.tab.items.Remove(el);
            }
            roomSelector.items.Clear();
            roomSelector.SetContentSize(0);

            foreach (var el in roomOptions.items)
            {
                el.Deactivate();
                el.tab.items.Remove(el);
            }
            roomOptions.items.Clear();
            roomOptions.SetContentSize(0);

            // Don't put any new stuff if there is no region
            var scug = new SlugcatStats.Name(scugSelector.value, false);
            Plugin.Logger.LogDebug("oh?");
            if (acronym == null || scug.Index == -1 || !Data.RenderedRegions.ContainsKey(scug) ||
                !Data.RenderedRegions[scug].Contains(acronym, StringComparer.InvariantCultureIgnoreCase))
            {
                mapBox.UnloadRegion();
                return;
            }

            // Find the room list and add its contents
            if (File.Exists(Path.Combine(Data.RenderOutputDir(scug.value, acronym), "metadata.json")))
            {
                Plugin.Logger.LogDebug("2");
                const float LIST_EDGE_PAD = 6f;
                const float LIST_LH = 24f;
                const float SCROLL_WIDTH = OIUtil.SLIDER_WIDTH;

                activeRegion = RegionInfo.FromJSON((Dictionary<string, object>)Json.Deserialize(File.ReadAllText(
                    Path.Combine(Data.RenderOutputDir(scug.value, acronym), "metadata.json"))));
                var roomList = activeRegion.rooms.Keys.OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList();

                float y = roomSelector.size.y - LIST_EDGE_PAD;
                float height = LIST_EDGE_PAD * 2;
                foreach (var room in roomList)
                {
                    Plugin.Logger.LogDebug(room);
                    y -= LIST_LH;
                    height += LIST_LH;
                    var button = new OpTextButton(new Vector2(LIST_EDGE_PAD, y), new Vector2(roomSelector.size.x - LIST_EDGE_PAD * 2 - SCROLL_WIDTH, LIST_LH), room)
                    {
                        alignment = FLabelAlignment.Left
                    };
                    button.OnClick += (_) => {
                        if (activeButton.TryGetTarget(out var oldButton)) oldButton.Active = false;
                        button.Active = true;
                        activeButton.SetTarget(button);
                        SwitchToRoom(room);
                    };
                    roomSelector.AddItems(button);
                }
                roomSelector.SetContentSize(height);
                mapBox.LoadRegion(activeRegion);
            }
            else
            {
                Plugin.Logger.LogDebug("BWAJHSDF:LKJLSDF");
            }
        }

        private void SwitchToRoom(string room)
        {
            Plugin.Logger.LogDebug($"Switch to room {room}");
            mapBox.FocusRoom(room);
        }
    }
}
